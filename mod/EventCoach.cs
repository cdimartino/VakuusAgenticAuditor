using System.Text.RegularExpressions;

namespace Auditor;

internal record EventPick(string Title, string Description, string? Relic = null, string[]? CurseIds = null);
internal record UpgradeTarget(string Key, string Name, double Value, bool Eligible = true, bool Selected = false);
internal static class EventCoach
{
    internal static string? Ancient((string id, string name)[] relics, int upgrades, int basics, int expensive, bool pick, int? legalUpgrades = null)
    {
        int legal = Math.Min(4, Math.Max(0, legalUpgrades ?? upgrades));
        var ranked = relics.Select(r => r.id switch
        {
            "YUMMY_COOKIE" when legal > 0 => (3 * Math.Min(legal, upgrades) + 1, r.name, $"Choose {legal} cards to upgrade permanently; {Math.Min(legal, Math.Max(0, upgrades))} are priority targets. Include eligible basics if needed. No Soot added."),
            "BIIIG_HUG" when basics >= 4 => (basics >= 8 ? 14 : 7, r.name, "Remove four starter cards. The tradeoff is a Soot every shuffle."),
            "PUMPKIN_CANDLE" => (expensive >= 4 ? 15 : 6, r.name, "Extra energy helps your expensive turns. It lasts five fights before it needs kindling."),
            _ => (int.MinValue, r.name, "")
        }).Where(x => x.Item1 > int.MinValue).OrderByDescending(x => x.Item1).ToArray();
        if (ranked.Length == 0) return null;
        return (pick ? $"Take {ranked[0].name}. " : "") + ranked[0].Item3
            + (relics.Any(r => r.id is not ("YUMMY_COOKIE" or "BIIIG_HUG" or "PUMPKIN_CANDLE"))
                ? " Another relic's effect is unmodeled; this comparison is incomplete." : "");
    }
    // Keys identify card instances, not names. Caller supplies the live picker's
    // legal target count (not the count of attractive non-basic cards).
    internal static string? RemainingUpgrades(UpgradeTarget[] cards, int required, int selected)
    {
        int remaining = Math.Max(0, required - selected);
        if (remaining == 0) return "Upgrade selection complete; confirm when ready.";
        var ranked = cards.Where(c => c.Eligible && !c.Selected).DistinctBy(c => c.Key)
            .OrderByDescending(c => c.Value).Take(remaining).ToArray();
        if (ranked.Length == 0) return null;
        return $"Choose {remaining} more upgrade{(remaining == 1 ? "" : "s")}. Priorities: {string.Join(", ", ranked.Select(c => c.Name))}."
            + (ranked.Length < remaining ? " Waiting for the remaining legal targets." : "");
    }

    internal static string? Unsupported(bool manual) => manual
        ? "I can't safely compare these event options: an effect or condition is unrecognized. Check the visible costs and rewards." : null;

    // Exact installed English clauses only; never infer an effect from a title.
    // Pass only visible options, including enabled state, and live empty slots.
    internal static string? Courier((EventPick Option, bool Enabled)[] options, int emptySlots, bool manual)
    {
        var enabled = options.Where(o => o.Enabled).Select(o => o.Option).ToArray();
        if (enabled.Length == 0) return Unsupported(manual);
        var offers = new List<(EventPick option, int count, bool foul)>();
        foreach (var option in enabled)
        {
            string effect = Plain(option.Description);
            var foul = Regex.Match(effect, @"^Procure (\d+) Foul Potions?\.$");
            if (foul.Success && int.TryParse(foul.Groups[1].Value, out int count) && count > 0)
                offers.Add((option, count, true));
            else if (effect == "Procure 1 random Uncommon Potion.") offers.Add((option, 1, false));
            else return Unsupported(manual);
        }
        if (emptySlots <= 0)
            return "Your potion belt is full. Make room before taking potions; I won't assume an existing potion should be discarded."
                + " Foul Potions also damage all players, or can be thrown at the Merchant for gold; the uncommon potion is random.";
        var random = offers.FirstOrDefault(o => !o.foul);
        if (random.option != null)
            return $"{Plain(random.option.Title)} offers one random uncommon potion, not a guaranteed specific effect."
                + (offers.Any(o => o.foul) ? " The alternative's Foul Potions damage ALL players and enemies, or can be thrown at the Merchant for gold; their value depends on that plan." : "")
                + $" You have {emptySlots} empty potion slot{(emptySlots == 1 ? "" : "s")}.";
        var only = offers[0];
        return $"{Plain(only.option.Title)} offers {only.count} Foul Potion{(only.count == 1 ? "" : "s")}: damage to ALL players and enemies, or gold when thrown at the Merchant."
            + $" You have {emptySlots} empty slots; don't assume more will fit.";
    }
    static string Plain(string s) => Regex.Replace(s, @"\[[^\]]+\]", "").Trim();
    // Legacy caller: all supplied options are assumed enabled. New callers should
    // pass enabled state and target eligibility to Evaluate.
    internal static string? Recommend(EventPick[] options, int hp, int maxHp, int gold, bool explicitPick, Deck? deck = null)
        => Evaluate(options.Select(o => (o, true)).ToArray(),
            new(hp, maxHp, gold, deck), explicitPick);

    internal static string? Evaluate((EventPick Option, bool Enabled)[] options,
        EventEvaluation context, bool explicitPick, bool muted = false)
        => muted ? null : EventEffects.Compare(options, context, explicitPick);
}
