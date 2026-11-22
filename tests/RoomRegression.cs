using Auditor;

internal static class RoomRegression
{
    internal static void Run(Action<bool, string> check)
    {
        RouteStop[] camp = [new("RestSite", [])];
        var state = new RouteState(40, 80, 50, 1, false);
        check(new RouteAnnouncements().Update(camp, state) != null, "announce full route at forced opening");
        check(RouteCoach.Choose(camp, state, true)?.Text.Contains("camp") == true, "forced camp manual response");
        check(RouteCoach.Choose([], state, true) == null, "transition has no forced hint");
        check(RouteCoach.Choose([camp[0], new("Shop", [])], state) != null, "branches retain comparison");
        var gifts = new[] { ("YUMMY_COOKIE", "Yummy Cookie"), ("UNKNOWN", "Unknown") };
        var ancient = EventCoach.Ancient(gifts, 2, 0, 0, true, 4)!;
        check(ancient.Contains("Choose 4") && ancient.Contains("2 are priority"), "legal upgrades separate from priority count");
        check(ancient.Contains("incomplete") && !ancient.StartsWith("Take"), "unmodeled ancient qualified without an overall winner");
        var shortHug = EventCoach.Ancient([("BIIIG_HUG", "Hug"), ("PUMPKIN_CANDLE", "Candle")], 0, 0, 2, true, 0, 2)!;
        check(shortHug.Contains("Hug: Remove 2 cards") && shortHug.Contains("Soot") && !shortHug.Contains("starter"),
            "Hug is available with two legal nonstarters and its shuffle cost remains visible");
        UpgradeTarget[] targets = [new("a", "Priority", 9, Selected: true), new("b", "Strike", 1), new("c", "Defend", 2), new("d", "Illegal", 99, false)];
        var remaining = EventCoach.RemainingUpgrades(targets, 4, 2)!;
        check(remaining.Contains("2 more") && remaining.Contains("Defend, Strike") && !remaining.Contains("Illegal"), "remaining picks include eligible basics, exclude selected");
        check(EventCoach.RemainingUpgrades(targets, 4, 4)!.Contains("confirm"), "completed picker confirms");
        (EventPick Option, bool Enabled)[] courier = [(new("Grab Potions", "Procure [blue]3[/blue] [gold]Foul Potions[/gold]."), true), (new("Ransack", "Procure 1 random Uncommon Potion."), true)];
        check(EventCoach.Courier(courier, 1, true)!.Contains("random uncommon"), "actual courier clauses supported");
        check(EventCoach.Courier(courier, 0, true)!.Contains("belt is full"), "full belt cautions");
        courier[1] = (courier[1].Option, false);
        check(!EventCoach.Courier(courier, 0, true)!.Contains("uncommon"), "full belt does not invent the disabled uncommon offer");
        check(EventCoach.Courier(courier, 1, true, sozu: true)!.Contains("prevents"), "Sozu overrides open potion slots");
        check(!EventCoach.Courier(courier, 2, true)!.Contains("Ransack"), "disabled offer excluded");
        courier[0] = (new("Grab Potions", "Procure 3 Foul Potions. Fight an enemy."), true);
        check(EventCoach.Courier(courier, 2, false) == null, "unknown courier auto quiet");
        check(EventCoach.Courier(courier, 2, true) == "I can't compare these choices.", "unknown courier gets a short manual fallback");
    }
}
