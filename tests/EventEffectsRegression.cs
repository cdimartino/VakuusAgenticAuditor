using Auditor;

internal static class EventEffectsRegression
{
    internal static void Run(Action<bool, string> check)
    {
        // Audited PCK SPIRIT_GRAFTER option templates; 25/10 are visible
        // screenshot fixture values, not asserted constructor constants.
        EventPick accept = new("Let It In", "Heal [green]25[/green] HP. Add [gold]Metamorphosis[/gold] to your [gold]Deck[/gold].");
        EventPick reject = new("Rejection", "Lose [red]10[/red] HP. [gold]Upgrade[/gold] a card.");
        (EventPick, bool)[] spirit = [(accept, true), (reject, true)];
        var full = new EventEvaluation(80, 80, 50, UpgradeTargets: 2);
        var a = EventEffects.Assess(accept, full);
        check(a.Known.Any(k => k.Contains("heals 0 HP")), "Spirit Grafter full-health healing capped");
        check(a.Unresolved.Any(k => k.Contains("free to play this combat") && k.Contains("not a curse") && k.Contains("only available when drawn")), "Metamorphosis audited effect without false curse penalty");
        var low = full with { Hp = 8 };
        check(EventEffects.Assess(accept, low).Known.Any(k => k.Contains("heals 25 HP")), "Spirit Grafter low-health healing");
        check(EventEffects.Assess(reject, low).Unsafe, "Spirit Grafter lethal rejection flagged");
        string lowAdvice = EventCoach.Evaluate(spirit, low, true)!;
        check(lowAdvice.StartsWith("Let It In.") && lowAdvice.Contains("lethal") && !lowAdvice.Contains("could reverse"), "lethal rejection cannot reverse uncertain alternative ranking");
        check(EventEffects.Assess(reject, full with { UpgradeTargets = 0 }).Unsafe, "Spirit Grafter unavailable upgrade target");
        check(EventCoach.Evaluate(spirit, full, true)!.Contains("could reverse"), "uncertain Metamorphosis blocks unconditional winner");
        check(EventCoach.Evaluate(spirit, full, true, muted: true) == null, "event mute gate preserved");
        check(EventCoach.Evaluate([(reject, true)], full, false)!.StartsWith("Rejection:"), "non-pick mode describes without command");
        EventPick leave = new("Leave", "Leave.");
        var opaque = new EventPick("Risk", "Gain 100 Gold. Fight a mysterious foe.");
        var r = EventEffects.Assess(opaque, full);
        check(r.Known.Contains("Gain 100 Gold") && r.Unresolved.Contains("Fight a mysterious foe"), "opaque clause retains known reward and specific uncertainty");
        var random = EventEffects.Assess(new("Reward", "Obtain a random Relic."), full);
        check(random.Unresolved.Length == 1 && random.Score == 0, "random relic is not guaranteed value");
        check(EventEffects.Assess(new("Pay", "Pay 100 Gold."), full).Unsafe, "unaffordable gold cost flagged");
        check(!EventCoach.Evaluate([(opaque, false), (leave, true)], full, true)!.Contains("mysterious"), "disabled options excluded");
        check(EventEffects.Assess(new("Remove", "Remove 1 card from your Deck."), full with { RemovalTargets = 0 }).Unsafe, "removal legal-target gate");
        check(EventEffects.Assess(new("Potion", "Procure 1 random Uncommon Potion."), full with { EmptyPotionSlots = 0 }).Unresolved.Any(u => u.Contains("belt is full")), "full potion belt not silently replaced");
        check(EventCoach.Evaluate([(new("Heal", "Heal 10 HP."), true), (leave, true)], low, true)!.StartsWith("Heal."), "fully known event supports explicit pick");
        EventPick sword = new("Grab the Sword", "Obtain the [gold]Sword of Stone.[/gold]");
        EventPick dive = new("Dive", "Gain 101 Gold. Lose 7 HP.");
        check(EventCoach.Recommend([dive, sword], 33, 91, 107, true) is { } swordAdvice && swordAdvice.StartsWith("Grab the Sword.") && swordAdvice.Contains("26/91"), "Sunken Statue sword and HP comparison preserved");
        check(EventCoach.Recommend([sword, dive], 85, 91, 107, true)!.StartsWith("Dive."), "Sunken Statue healthy gold trade preserved");
    }
}
