namespace Auditor;

/// <summary>Pure comparison rules for the audited Blitz merchant entry IDs.</summary>
internal static class BlitzMerchant
{
    // Stable IDs mirror BlitzMerchantEntryType via the game's SnakeCase helper.
    internal static readonly string[] NativeEntryIds =
    [
        "card_upgrade", "upgraded_card_reward", "random_upgrades", "card_transform",
        "card_removal", "card_rewards", "colorless_card_reward", "potions",
        "card_enchant", "enchanted_card_reward", "prismatic_card_reward", "rare_card_reward",
        "upgraded_rare_card", "relic", "glam", "two_more_choices",
        "card_reward_of_each_rarity", "upgraded_rare_card_reward", "two_relics",
        "full_heal_and_max_hp"
    ];

    internal sealed record Offer(string Id, string Title, string Description, bool Enabled,
        string[] LegalTargets, int LegalTargetCount, bool Cursed);
    internal sealed record Context(int PicksRemaining, int EnabledOfferCount, int CurrentHp,
        int MaxHp, int PotionCount, int PotionSlots, bool Sozu, int CurseCount,
        int RemovableCurseCount, int DeckSize, double CurseCost, string CurseRisk);

    internal static string? Compare(IEnumerable<Offer> source, Context context)
    {
        var offers = source.Where(x => x.Enabled).ToArray();
        if (offers.Length == 0) return null;
        int picks = Math.Clamp(context.PicksRemaining, 0, offers.Length);
        var lines = new List<string>();
        bool unreviewed = offers.Any(o => !NativeEntryIds.Contains(o.Id, StringComparer.Ordinal));
        var ranking = offers.Select(o => (Offer: o,
                Score: Evidence(o, context) - (o.Cursed ? CursePenalty(context) : 0)))
            .OrderByDescending(x => x.Score).ToArray();
        lines.Add(picks == 0
            ? "The merchant’s picks are used; remaining budget: 0."
            : unreviewed
                ? $"I can compare the displayed effects, but can’t rank an unreviewed offer against the others. Remaining budget: {picks} of {context.EnabledOfferCount} enabled offers."
                : $"First recommendation: {ranking[0].Offer.Title}. {Recommendation(ranking[0].Offer, context)} Remaining budget: {picks} of {context.EnabledOfferCount} enabled offers.");

        foreach (var item in offers)
            lines.Add($"{item.Title}: {Explain(item, context)}");
        return string.Join("\n", lines);
    }

    static string Explain(Offer o, Context c)
    {
        string targets = o.LegalTargetCount == 0 ? "No legal deck targets are available." :
            $"Legal target examples ({o.LegalTargetCount} eligible): {string.Join(", ", o.LegalTargets)}.";
        var note = o.Id switch
        {
            "random_upgrades" => $"The game upgrades up to two randomly selected eligible cards; {targets}",
            "card_upgrade" or "card_removal" or "card_transform" => targets,
            "two_relics" =>
                $"Gives two randomly generated relics. Current deck: {c.DeckSize} cards, including {c.CurseCount} curse(s); {c.RemovableCurseCount} curse(s) are removable.",
            "two_more_choices" => "Spends one current pick to add two, for a net gain of one pick.",
            "full_heal_and_max_hp" => $"Adds 6 max HP, then fully heals. You are at {c.CurrentHp}/{c.MaxHp} now, so the heal restores up to {Math.Max(0, c.MaxHp - c.CurrentHp + 6)} HP.",
            "potions" => c.Sozu
                ? "+1 potion slot, but Sozu prevents receiving potions."
                : $"+1 potion slot and two random potion choices. Current belt: {c.PotionCount}/{c.PotionSlots}.",
            "card_enchant" or "glam" => targets,
            "relic" => "One random relic, with no curse. Its identity is revealed after you spend the pick.",
            "card_rewards" or "colorless_card_reward" or "prismatic_card_reward" or "rare_card_reward" or
                "upgraded_card_reward" or "upgraded_rare_card" or "card_reward_of_each_rarity" or
                "upgraded_rare_card_reward" or "enchanted_card_reward" =>
                "The card reward is still unknown; compare the actual cards when offered, not against a guessed target.",
            _ => ""
        };
        var consequence = o.Cursed
            ? $"Adds a random run-allowed curse (not known yet). Current curses: {c.CurseCount}, removable: {c.RemovableCurseCount}. {c.CurseRisk}"
            : "";
        return string.Join(" ", new[] { o.Description, note, consequence }.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    // Rank only offers with direct current-state evidence. This deliberately does not
    // predict the identity of random cards, relics, curses, or potions.
    static int Evidence(Offer o, Context c) => o.Id switch
    {
        "card_upgrade" when o.LegalTargetCount > 0 => 10,
        "random_upgrades" when o.LegalTargetCount > 0 => 9,
        "card_removal" when o.LegalTargetCount > 0 && c.RemovableCurseCount > 0 => 8,
        "full_heal_and_max_hp" when c.CurrentHp < c.MaxHp * .6 => 8,
        "two_more_choices" when c.EnabledOfferCount > c.PicksRemaining => 7,
        "card_transform" when o.LegalTargetCount > 0 && c.DeckSize >= 20 => 6,
        "card_enchant" or "glam" when o.LegalTargetCount > 0 => 6,
        "potions" when !c.Sozu && c.PotionCount < c.PotionSlots => 5,
        "two_relics" => 6,
        "relic" => 5,
        "card_rewards" or "colorless_card_reward" or "prismatic_card_reward" or
            "rare_card_reward" or "upgraded_card_reward" or "upgraded_rare_card" or
            "card_reward_of_each_rarity" or "upgraded_rare_card_reward" or
            "enchanted_card_reward" => 4,
        "full_heal_and_max_hp" => 4,
        "card_removal" when o.LegalTargetCount > 0 => 4,
        "card_transform" when o.LegalTargetCount > 0 => 3,
        "potions" when !c.Sozu => 3,
        "two_more_choices" => 2,
        "card_upgrade" or "random_upgrades" or "card_transform" or "card_removal" or
            "card_enchant" or "glam" => 0,
        _ => 0
    };

    static int CursePenalty(Context c) =>
        Math.Clamp((int)Math.Round(c.CurseCost / 3), 1, 5);

    static string Recommendation(Offer offer, Context context) => offer.Id switch
    {
        "card_upgrade" or "random_upgrades" when offer.LegalTargetCount > 0 =>
            $"A legal upgrade is available ({string.Join(", ", offer.LegalTargets.Take(3))}); random upgrades can affect at most two.",
        "card_removal" when offer.LegalTargetCount > 0 =>
            context.RemovableCurseCount > 0 ? $"A curse is among the removable cards; legal examples: {string.Join(", ", offer.LegalTargets.Take(3))}." :
                $"A removal is available; legal cards include {string.Join(", ", offer.LegalTargets.Take(3))}.",
        "full_heal_and_max_hp" => $"The guaranteed +6 max HP and full heal restore up to {Math.Max(0, context.MaxHp - context.CurrentHp + 6)} HP; weigh that against the added random curse.",
        "two_more_choices" => "This costs one pick now but raises the remaining budget by one net pick; it also adds a random curse.",
        "two_relics" => "Two relics are guaranteed, but so is a random curse; neither reward is known yet.",
        "potions" => context.Sozu
            ? "Sozu blocks the potion reward, though the extra potion slot still applies."
            : $"+1 potion slot and two random potion options; your current belt is {context.PotionCount}/{context.PotionSlots}.",
        "card_rewards" or "colorless_card_reward" or "prismatic_card_reward" or "rare_card_reward" or
            "upgraded_card_reward" or "upgraded_rare_card" or "card_reward_of_each_rarity" or
            "upgraded_rare_card_reward" or "enchanted_card_reward" =>
            "The card(s) are not revealed yet; compare the actual choices before selecting a card.",
        "relic" => "A random relic gives lasting value without the curse attached to Two Relics.",
        "card_enchant" or "glam" when offer.LegalTargetCount > 0 =>
            $"A legal enchant target is available: {string.Join(", ", offer.LegalTargets.Take(3))}.",
        _ => $"Compare its localized effect with the other offers."
    };
}
