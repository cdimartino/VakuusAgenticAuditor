using Auditor;

internal static class CurseRegression
{
    internal static void Run(Action<bool, string> check)
    {
        Card C(string id, bool curse = false, int cost = 1) => new(id, id, cost, false, false, false, [], []) { IsCurse = curse };
        Deck D(int size) => new(Enumerable.Range(0, size).Select(i => C(i.ToString())).ToArray(), 50, 80, 1, false, []);
        var small = D(10);
        var large = D(30);
        double Cost(string id, Deck d) => CurseValuation.Addition([id], d).Cost;
        check(Cost("INJURY", small) > Cost("INJURY", large), "curse dilution is marginal to deck size");
        check(CurseValuation.Addition(["INJURY", "INJURY"], small).Cost > Cost("INJURY", small), "two curses cost more than one");
        check(Cost("DECAY", small) > Cost("INJURY", small), "verified end-turn liability adds cost");
        check(Cost("NORMALITY", small with { Cards = small.Cards.Select(c => c with { Cost = 0 }).ToArray() }) > Cost("NORMALITY", small), "Normality costs more for many cheap plays");
        check(double.IsNaN(Coach.Assess(C("INJURY"), small).Score), "untyped card ID is not assumed a curse");
        check(Coach.Assess(C("INJURY", true), small).Score < 0, "actual curse gets a negative marginal verdict");
        EventPick Offer(int gold) => new("Accept", $"Gain {gold} Gold. Add Injury to your Deck.", CurseIds: ["INJURY"]);
        EventPick leave = new("Leave", "Leave.");
        check(EventCoach.Recommend([Offer(1), leave], 50, 80, 0, true, small)?.StartsWith("Leave.") == true, "small reward loses to curse cost");
        check(EventCoach.Recommend([Offer(200), leave], 50, 80, 0, true, small)?.StartsWith("Accept.") == true, "large reward can outweigh curse cost");
        check(EventCoach.Recommend([Offer(200), leave], 50, 80, 0, true)!.Contains("unresolved"), "missing deck explains unresolved curse offers");
        check(EventCoach.Recommend([new("Unknown", "Gain 200 Gold. Add a random Curse to your Deck."), leave], 50, 80, 0, true, small)!.Contains("unresolved"), "random curse not silently treated as free");
        check(EventCoach.Recommend([new("Fight", "Gain 200 Gold. Fight."), leave], 50, 80, 0, true, small)!.Contains("unresolved: Fight"), "unsupported event conditions remain explicitly unresolved");
        check(CurseValuation.Addition(["GUILTY"], small).Reason.Contains("duration is not modeled"), "Guilty duration is not invented");
    }
}
