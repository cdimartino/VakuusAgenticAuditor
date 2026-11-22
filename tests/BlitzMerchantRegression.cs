using Auditor;

internal static class BlitzMerchantRegression
{
    internal static void Run(Action<bool, string> check)
    {
        check(BlitzMerchant.NativeEntryIds.Length == 20 &&
            BlitzMerchant.NativeEntryIds.Distinct(StringComparer.Ordinal).Count() == 20,
            "pure coach covers every distinct beta BlitzMerchantEntryType ID (20; not 21)");

        BlitzMerchant.Offer[] fixture =
        [
            new("relic", "Relic", "Gain one random relic.", true, [], 0, false),
            new("random_upgrades", "Random Upgrades", "Upgrade two cards.", true,
                ["Strike (Fool)", "Deep Stretch", "Third card"], 8, false),
            new("two_relics", "Two Relics", "Gain two relics.", true, [], 0, true),
            new("card_transform", "Card Transform", "Transform one card.", true,
                ["Strike (Fool)"], 1, false),
            new("colorless_card_reward", "Colorless Card Reward", "Choose a colorless card.", true, [], 0, false),
            new("upgraded_card_reward", "Upgraded Card Reward", "Choose an upgraded card.", true, [], 0, false),
        ];
        var context = new BlitzMerchant.Context(3, 6, 28, 80, 2, 3, false, 0, 0, 12,
            0, "Possible curses include Normality; actual curse is randomly selected.");
        var advice = BlitzMerchant.Compare(fixture, context)!;
        check(advice.StartsWith("First recommendation: Random Upgrades.")
            && advice.Contains("Remaining budget: 3 of 6 enabled offers")
            && advice.Contains("up to two randomly selected eligible cards")
            && advice.Contains("Legal target examples (8 eligible): Strike (Fool), Deep Stretch, Third card")
            && advice.Contains("Gives two randomly generated relics")
            && advice.Contains("random run-allowed curse")
            && advice.Contains("actual curse is randomly selected")
            && !advice.Contains("Cloak Clasp") && !advice.Contains("gold")
            && !advice.Contains("when they appear"),
            "Blitz compares native effects, two random upgrades, and random curse/relic outcomes without treating outcomes as promises");

        BlitzMerchant.Offer[] alternatives =
        [
            new("full_heal_and_max_hp", "Full Heal and Max HP", "Fully heal and gain Max HP.", true, [], 0, true),
            new("two_more_choices", "Two More Choices", "Gain two more choices.", true, [], 0, true),
            new("potions", "Potions", "Choose a potion.", true, [], 0, false),
        ];
        var changed = BlitzMerchant.Compare(alternatives,
            new(1, 3, 20, 80, 1, 1, false, 2, 1, 24, 1, "Possible curse liabilities are bounded; result is unknown."))!;
        check(changed.StartsWith("First recommendation: Full Heal and Max HP."),
            "missing HP and max HP make healing the first supported pick");
        check(changed.Contains("+6 max HP and full heal") && changed.Contains("Adds a random run-allowed curse"),
            "full heal includes six max HP and its random curse cost");
        check(changed.Contains("net gain of one pick"), "Two More Choices reflects its net pick gain after paying one");
        check(changed.Contains("+1 potion slot and two random potion choices") && changed.Contains("1/1"),
            "potion choice includes the extra slot and live belt");
        check(!changed.Contains("gold"), "Blitz choice text has no gold price");

        var finished = BlitzMerchant.Compare([fixture[0] with { Enabled = false }],
            new(0, 1, 80, 80, 0, 3, false, 0, 0, 12, 0, ""));
        check(finished == null, "disabled or exhausted Blitz offers are not suggested");

        var rewards = BlitzMerchant.Compare(
            [fixture[4], fixture[5]],
            new(1, 2, 50, 80, 0, 3, false, 0, 0, 12, 0, ""))!;
        check(rewards.Contains("card reward is still unknown")
            && !rewards.Contains("Legal target")
            && rewards.StartsWith("First recommendation: Colorless Card Reward."),
            "future card rewards are compared without inventing a current deck target");
    }
}
