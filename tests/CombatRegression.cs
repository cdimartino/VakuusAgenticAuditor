using Auditor;

internal static class CombatRegression
{
    public static void Run(Action<bool, string> check)
    {
        check(Catalog.Get("PILLAGE", "Pillage+", 1, true, false).Has("draw"), "Pillage+ audited draw classification");
        Play guard = new("Guard", 1, 0, 0, 14, [-1]);
        var shots = Enumerable.Repeat(Catalog.Get("QUICK_SHOT", "Quick Shot", 1, false, true), 3).ToArray();
        Foe[] shells = [new("Exoskeleton", 28, 3, DamageCap: 9), new("Exoskeleton", 24, 8, DamageCap: 9), new("Exoskeleton", 27, 0, DamageCap: 9)];
        var honing = Catalog.Get("HONING", "Honing", 3, false, false);
        var scaling = CombatGuidance.Setup(honing, 50, shots, shells);
        Play buff = new("Honing", 3, 0, 0, 0, [-1, -1, -1], Pivot: true, Setup: true, FutureFit: scaling.fit, FutureReason: scaling.reason);
        Play draw = new("Wah Cho Ki+", 1, 0, 0, 11, [8, 8, 8], Pivot: true, DrawBoundary: true);
        Play hit = new("Strike", 1, 0, 0, 0, [6, 6, 6]);
        var opener = TurnCoach.Plan([hit, buff, draw], shells, 4, 0, 0, 0, 49);
        check(opener.Steps.Count == 1 && opener.Steps[0].Card == 2 && opener.SetupReason!.Contains("3 energy remains for Honing")
            && opener.Remaining!.Sum() == 71 && opener.Incoming == 0, "draw and block while preserving Honing; never export a speculative post-draw play");
        check(scaling.fit > 0 && CombatGuidance.Setup(honing, 50, [], shells).fit == 0 && scaling.fit < CombatGuidance.Setup(honing, 50, shots, [new("Other", 60, 0)]).fit,
            "Honing depends on actual Arrow support and discounts per-hit caps");
        check(TurnCoach.Plan([buff, guard], [new("Danger", 80, 14)], 3, 0, 0, 0, 10).Steps[0].Card == 1,
            "future scaling never displaces a surviving line with a lethal one");
        Play aim = buff with { Name = "Brace", Energy = 0, Ex = true, Setup = false, FutureFit = 2, FutureReason = "Aim for next Arrow" };
        check(TurnCoach.Plan([aim], shells, 0, 0, 0, 0, 49).Steps.Count == 0 && TurnCoach.Plan([aim], shells, 2, 0, 0, 0, 49).SetupReason != null,
            "X-cost Aim uses actual remaining energy, not free setup at zero");
        Play slam = new("Body Slam", 0, 0, 0, 0, [0], BlockDamage: true);
        Foe[] foe = [new("Target", 14, 50)];
        var line = TurnCoach.Plan([slam, guard], foe, 1, 0, 0, 0, 30);
        check(line.EnemiesLeft == 0 && line.Steps[0].Card == 1, "gain block before Body Slam from zero");
        check(TurnCoach.Plan([slam], foe, 0, 0, 0, 14, 30).EnemiesLeft == 0, "Body Slam uses existing block");
        check(TurnCoach.Plan([slam], [new("Vulnerable", 21, 0, new(0, 1, 0))], 0, 0, 0, 14, 30).EnemiesLeft == 0, "Body Slam applies existing Vulnerable once");
        var weakSlam = slam with { BlockMultipliers = [.75] };
        check(TurnCoach.Plan([weakSlam], [new("Target", 14, 20)], 0, 0, 0, 14, 30).Remaining![0] == 4,
            "Weak Body Slam must not falsely predict a 14 HP kill");
        Play expose = new("Expose", 0, 0, 0, 0, [0], Vulnerable: 1);
        var weakVulnerable = TurnCoach.Plan([weakSlam, expose], [new("Target", 30, 0)], 0, 0, 0, 7, 30);
        check(weakVulnerable.Remaining![0] == 23 && weakVulnerable.Steps[0].Card == 1,
            "Weak and newly applied Vulnerable combine before rounding Body Slam");
        Play strike = new("Strike", 1, 0, 0, 0, [6]);
        check(TurnCoach.Plan([strike], foe, 1, 0, 0, 50, 30).Remaining![0] == 8, "block does not change Strike");
        var danger = TurnCoach.Plan([guard], [new("Obscura", 200, 28), new("Parafright", 200, 22)], 1, 0, 0, 0, 30);
        var text = CombatGuidance.Risk(danger, foe, 30, true);
        check(danger.Incoming == 36 && danger.ExpectedBlock == 14 && text.Contains("lethal") && !text.Contains("unavoidable"), "50 minus 14 against 30 warns about shown line only");
        foreach (string effect in new[] { "uncertain-draw", "hand-upgrade", "exhaust-choice" })
        {
            Play pivot = new(effect, 0, 0, 0, 5, [-1], Pivot: CombatGuidance.Boundary(effect));
            var boundary = TurnCoach.Plan([pivot, strike], [new("A", 100, 20)], 1, 0, 0, 0, 30);
            check(boundary.Steps[^1].Card == 0 && boundary.Recheck, effect + " ends the line");
        }
        var quiet = TurnCoach.Plan([], [new("Quiet", 100, 0)], 0, 0, 0, 0, 30);
        check(CombatGuidance.Risk(quiet, [new("Strong", 100, 0, Strength: 4)], 30, false)
            == CombatGuidance.Risk(quiet, [new("Quiet", 100, 0)], 30, false), "zero incoming has no generic Strength disclaimer");
        check(!CombatGuidance.Risk(quiet, [new("Quiet", 100, 0)], 30, false).Contains("Strength"), "quiet fight has no invented scaling");
        check(CombatGuidance.Risk(quiet, [new("Obscura", 100, 0, Strength: 4, Illusion: true)], 30, false).Contains("returning"), "visible scaling and illusion separate from zero current threat");
        check(CombatGuidance.Revision("1", ["anger1", "anger2"], ["a"], 1, 0, 0, 0, 30, false)
            != CombatGuidance.Revision("1", ["anger2"], ["a"], 1, 0, 0, 0, 30, false), "same-named card instances invalidate independently");
        var context = new BeltContext(1, 1, 3, 10, 2, 0, 20, 0, 0, 2, 0, 0, false, false, false, false, false);
        CombatBottle regen = new("REGEN_POTION", "Regen Potion", 0, [true]);
        var regenHint = PotionCoach.Use([regen], [new("Normal", 100, 12)], [], new([], 12, 1, 0), 30, false, context);
        check(regenHint?.Contains("not immediate block") == true, "dangerous normal fight suggests early delayed Regen");
        check(PotionCoach.Use([regen], [new("Normal", 100, 50)], [], new([], 36, 1, 0), 30, false, context)?.Contains("not immediate block") == true, "Regen does not claim to solve lethal hit");
        check(PotionCoach.Use([regen], [new("Normal", 6, 50)], [], new([], 0, 0, 1000), 30, false, context) == null, "winning hand saves Regen");
        CombatBottle serum = new("STABLE_SERUM", "Stable Serum", 0, [true]);
        var serumLine = new TurnLine([], 36, 1, 0);
        string? serumHint = PotionCoach.Use([serum], [new("Normal", 100, 50)], [], serumLine, 30, false, context);
        check(serumHint?.Contains("not immediate block") == true && serumLine.Incoming == 36,
            "Stable Serum retains future hands without reducing lethal current damage");
        check(PotionCoach.Use([serum], [new("Normal", 100, 50)], [], serumLine, 30, false,
            context with { Expensive = 0 }) == null, "Stable Serum withheld without useful retention targets");
    }
}
