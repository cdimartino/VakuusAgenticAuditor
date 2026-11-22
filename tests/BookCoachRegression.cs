using Auditor;

internal static class BookCoachRegression
{
    internal static void Run(Action<bool, string> check)
    {
        Card C(string id, string name, int cost) => Catalog.Get(id, name, cost, false, id == "DAGGER_SPRAY");
        var attack = new BookCoach.Offer(BookCoach.Effect.Sharp, "Read the Back", [C("BASH", "Bash", 2), C("DAGGER_SPRAY", "Dagger Spray", 1)]);
        var skill = new BookCoach.Offer(BookCoach.Effect.Nimble, "Read a Random Passage", [C("DEFEND_SILENT", "Defend", 1)]);
        var power = new BookCoach.Offer(BookCoach.Effect.Swift, "Read the Entire Book", [C("FOOTWORK", "Footwork", 1)]);
        var advice = BookCoach.Compare([attack, skill, power], 47, 77, false, true)!;
        check(advice.StartsWith("Consider Read the Back") && advice.Contains("Dagger Spray gets +2 damage per powered hit") && advice.Contains("Defend: +2 Block") && advice.Contains("Footwork: draw 2 on its first play"), "pictured Book compares three benefits with legal named targets");
        check(BookCoach.Compare([skill, power], 20, 77, false, true)!.StartsWith("Consider Read a Random Passage"), "low HP favors repeatable defense over a one-time draw");
        var unindexed = C("UNINDEXED", "Unindexed", 0);
        check(BookCoach.Compare([skill with { Targets = [unindexed] }], 10, 77, false, true)!.Contains("Unindexed: +2 Block"),
            "Nimble's runtime GainsBlock predicate covers legal targets missing catalog tags");
        check(BookCoach.Compare([attack with { Targets = [unindexed] }], 10, 77, false, true) is { } sharp
            && sharp.Contains("no verified powered hit") && !sharp.StartsWith("Consider"), "Sharp on an unverified attack does not promise damage");
        check(BookCoach.Compare([skill, power], 60, 77, true, true)!.Contains("Fiddle blocks Swift") && !BookCoach.Compare([power], 60, 77, true, true)!.StartsWith("Consider"), "no promised Swift draw through Fiddle");
        check(BookCoach.Compare([attack with { Targets = [] }, power], 47, 77, false, false) == "Read the Entire Book — Footwork: draw 2 on its first play each combat.", "empty legal pool excluded; non-pick preference preserved");
        check(BookCoach.Parse("SELF_HELP_BOOK.pages.INITIAL.options.READ_PASSAGE_LOCKED") == null && BookCoach.Parse("OTHER.pages.INITIAL.options.READ_PASSAGE") == null, "never compare locked or unrelated options");
    }
}
