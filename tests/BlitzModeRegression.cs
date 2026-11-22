using Auditor;

internal static class BlitzModeRegression
{
    sealed class Point(string kind)
    {
        internal readonly string Kind = kind;
        internal readonly List<Point> Children = [];
    }

    internal static void Run(Action<bool, string> check)
    {
        check(BlitzMode.Active("26660232", "Blitz", "Singleplayer", 1)
            && !BlitzMode.Active(null, "Blitz", "Singleplayer", 1)
            && !BlitzMode.Active("26660232", "Standard", "Singleplayer", 1)
            && !BlitzMode.Active("26660232", "Blitz", "Host", 1)
            && !BlitzMode.Active("26660232", "Blitz", "Singleplayer", 2)
            && !BlitzMode.Active("26660232", "Blitz", "Singleplayer", 1, true), "exact solo Blitz gate");
        check(BlitzMode.Difficulty(0)!.Contains("no Ascension")
            && BlitzMode.Difficulty(2) == "Blitz 2: Weary Traveler, Tight Belt."
            && BlitzMode.Difficulty(5)!.Contains("Deadly Enemies, Ascender’s Bane")
            && BlitzMode.Difficulty(6) == null, "Blitz uses its own cumulative five-level progression, not standard ascension");
        check(BlitzMode.Route(1, ["Shop", "RestSite", "Boss", "Boss"]) ==
            "Act 2 · fixed route ahead: merchant → camp → boss → boss."
            && BlitzMode.Route(1, ["Shop", "ForeignRoom"]) == null
            && BlitzMode.Route(2, [])!.Contains("final") && BlitzMode.Route(1, [])!.Contains("next act"),
            "use actual saved route and distinguish end of act from final boss");
        var current = new Point("RestSite");
        var boss = new Point("Boss");
        current.Children.Add(boss);
        string[]? Remaining() => BlitzMode.Remaining(current, p => p.Children, p => p.Kind);
        check(Remaining() is ["Boss"], "fixed preview excludes the player's current room");
        boss.Children.Add(current);
        check(Remaining() == null, "cyclic modded route has no fixed preview");
        boss.Children.Clear();
        current.Children.Add(new Point("Shop"));
        check(Remaining() == null, "branched modded route has no forced-path claim");
        check(BlitzMode.Camp(true, true, 20, 70, 21, 3, 2) is { } camp &&
            camp.Contains("Two bosses") && camp.Contains("20 → 41") && camp.Contains("3 eligible") && camp.Contains("Favor the heal")
            && !BlitzMode.Camp(false, true, 20, 70, 21, 3, 1)!.Contains("Rest")
            && BlitzMode.Camp(true, false, 70, 70, 21, 3, 1)!.Contains("no HP")
            && BlitzMode.Camp(false, false, 70, 70, 21, 3, 1) == null, "pre-boss rest respects enabled actions, missing health, second boss");
        check(BlitzMode.Treasure(2)!.Contains("one at a time") && !BlitzMode.Treasure(1)!.Contains("two") &&
            BlitzMode.Treasure(0) == null && BlitzMode.Treasure(3) == null, "refresh chest advice after each claim; unknown layout abstains");
        check(new[] { "GIMME_MOVE", "DOUBLE_SMASH_MOVE", "HEHE_MOVE" }.All(m => BlitzMode.Encounter("GREMLIN_MERC", m) != null)
            && BlitzMode.Encounter("GREMLIN_MERC", "FUTURE_MOVE") == null && BlitzMode.Encounter("FOREIGN", "GIMME_MOVE") == null,
            "native Gremlin Merc's three visible attacks bypass gold theft");
        check(BlitzMode.Rewards("Monster", true, 0, 1, 0, 0) == "Base monster card offer: common."
            && BlitzMode.Rewards("Monster", false, 1, 1, 1, 0) is { } normal && normal.Contains("1 option;") && normal.Contains("belt")
            && BlitzMode.Rewards("Elite", false, 2, 1, 0, 2) is { } elite && elite.Contains("uncommon") && elite.Contains("2 options") && elite.Contains("separate claims")
            && BlitzMode.Rewards("Elite", false, 1, 0, 0, 1) == "One relic reward remains."
            && BlitzMode.Rewards("Boss", false, 1, 1, 1, 0) == "Base boss card offer: rare."
            && BlitzMode.Rewards("Boss", false, 2, 1, 0, 0) == null
            && BlitzMode.Rewards("Monster", false, 1, 0, 0, 0) == null, "reward context changes with actual remaining rows, room and act");
    }
}
