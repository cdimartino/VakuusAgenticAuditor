using Auditor;

internal static class EnemyDefenseRegression
{
    public static void Run(Action<bool, string> check)
    {
        Foe shell = new("Exoskeleton", 27, 12, DamageCap: 9);
        Play hit = new("Hit", 1, 0, 0, 0, [40], Attack: true);
        TurnLine Plan(Play[] cards, Foe[] foes, int energy = 3) => TurnCoach.Plan(cards, foes, energy, 0, 0, 0, 30);
        var one = Plan([hit], [shell]);
        check(one.Remaining![0] == 18 && one.Incoming == 12 && one.RemovedIntent == 0,
            "single overkill preview cannot remove capped enemy intent");
        check(Plan([hit with { Hits = 2 }], [shell]).Remaining![0] == 9, "cap applies separately to two hits");
        check(Plan([hit with { Hits = 3 }], [shell]).EnemiesLeft == 0, "three hits can kill 27 HP shell");
        check(Plan([hit, hit, hit], [shell]).EnemiesLeft == 0, "cap does not deplete or reset between cards");
        var group = Plan([hit with { Area = true, Damage = [40, 40], Hits = 2 }],
            [shell, new("Other shell", 26, 8, DamageCap: 9)]);
        check(group.Remaining!.SequenceEqual(new[] { 9, 8 }) && group.Incoming == 20,
            "area multihit preserves each surviving shell's intent");
        var choice = Plan([hit with { Damage = [40, 40] }], [shell, new("Attacker", 26, 15)]);
        check(choice.Steps[0].Target == 1 && choice.Remaining![0] == 27 && choice.Remaining[1] == 0,
            "target selection prefers real kill over capped overkill");
        check(Plan([hit], [shell with { DamageCap = null }]).EnemiesLeft == 0, "absent cap preserves ordinary damage");
        check(Plan([hit], [shell with { DamageCap = 4 }]).Remaining![0] == 23, "observed amount is not hardcoded nine");
        check(Plan([hit], [shell with { DamageCap = 12 }]).Remaining![0] == 15,
            "counter-stack amount changes cap rather than adding a separate reduction");
        check(Plan([hit], [shell with { DamageCap = 0 }]).EnemiesLeft == 1, "zero observed cap is distinct from absent power");
        var counted = TurnCoach.Plan([hit with { HitsFrom = "attacks" }], [shell], 1, 0, 0, 0, 30, attacks: 2);
        check(counted.Remaining![0] == 9, "dynamic repeats are separate capped damage events");
        Play expose = new("Expose", 0, 0, 0, 0, [0], Vulnerable: 1);
        check(Plan([expose, hit with { Damage = [8] }], [shell]).Remaining![0] == 18,
            "new Vulnerable is applied before cap");
        check(Plan([hit with { Damage = [9] }], [shell]).Remaining![0] == 18, "already capped preview is not reduced twice");
        check(Plan([hit with { Damage = [6], Hits = 2 }], [shell]).Remaining![0] == 15,
            "sub-cap hit damage is not promoted to cap");
        check(Plan([hit with { BlockDamage = true }], [shell]).EnemiesLeft == 1,
            "zero-block Body Slam does not invent damage");
        var slam = TurnCoach.Plan([hit with { BlockDamage = true, Hits = 2 }], [shell], 1, 0, 0, 40, 30);
        check(slam.Remaining![0] == 9, "dynamic block damage capped per hit");
        CombatBottle fire = new("FIRE_POTION", "Fire Potion", 40, [true]);
        check(PotionCoach.Use([fire], [shell], [], new([], 12, 1, 0), 30, false) == null,
            "potion cannot falsely finish capped enemy");
        check(PotionCoach.Use([fire], [shell with { Life = 9 }], [], new([], 12, 1, 0), 30, false)?.Contains("finish the fight") == true,
            "potion establishes kill at cap threshold");
        check(PotionCoach.Use([fire], [shell], [hit], one, 30, false) == null,
            "potion after capped cards cannot falsely finish remaining 18 HP");
        var first = Plan([hit], [shell with { Life = 18 }]);
        check(PotionCoach.Use([fire], [shell with { Life = 18 }], [hit], first, 30, false)?.Contains("finish the fight") == true,
            "potion after cards uses projected remaining HP");
        Foe[] mixed = [shell with { Life = 10, Intent = 30 }, new("Uncapped", 10, 12)];
        var aimed = PotionCoach.Use([fire with { Targets = [true, true] }], mixed, [], new([], 42, 2, 0), 30, false);
        check(aimed?.Contains("on Uncapped") == true, "potion reprioritizes actual kill over capped higher-intent target");
        var area = PotionCoach.Use([new("EXPLOSIVE_AMPOULE", "Ampoule", 40, [true, true])],
            mixed, [], new([], 42, 2, 0), 30, false);
        check(area?.Contains("remove an attacker") == true && !area.Contains("finish the fight"),
            "area potion evaluates each target's own cap");
    }
}
