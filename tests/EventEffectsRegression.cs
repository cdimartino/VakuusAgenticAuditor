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
        check(lowAdvice.StartsWith("Let It In.") && lowAdvice.Contains("lethal") && lowAdvice.EndsWith("I can't assess every effect."), "lethal rejection cannot reverse uncertain alternative ranking");
        check(!EventEffects.Assess(reject, full with { UpgradeTargets = 0 }).Unsafe, "Spirit Grafter missing upgrade benefit is not lethal");
        var mixedAdvice = EventCoach.Evaluate(spirit, full, true)!;
        check(mixedAdvice.StartsWith("Let It In:") && mixedAdvice.Contains("heals 0 HP") && mixedAdvice.Contains("leaves 70/80 HP")
            && mixedAdvice.EndsWith("I can't assess every effect.") && !mixedAdvice.Contains("Metamorphosis"),
            "mixed event keeps known effects, with one short caveat and no unconditional pick");
        (EventPick, bool)[] gifts = [
            (new("Empty Cage", "Remove 2 cards from your Deck."), true),
            (new("Astrolabe", "Transform 3 cards, then Upgrade them."), true),
            (new("Dusty Tome", "Obtain Lost Lute+."), true),
        ];
        check(EventCoach.Evaluate(gifts, full, true) == "I can't compare these choices.",
            "unmodeled Ancient gifts show one short note without repeating the options");
        check(EventCoach.Evaluate(spirit, full, true, muted: true) == null, "event mute gate preserved");
        check(EventCoach.Evaluate([(reject, true)], full, false)!.StartsWith("Rejection:"), "non-pick mode describes without command");
        EventPick leave = new("Leave", "Leave.");
        var opaque = new EventPick("Risk", "Gain 100 Gold. Fight a mysterious foe.");
        var r = EventEffects.Assess(opaque, full);
        check(r.Known.Contains("Gain 100 Gold") && r.Unresolved.Contains("Fight a mysterious foe"), "opaque clause retains known reward and specific uncertainty");
        var random = EventEffects.Assess(new("Reward", "Obtain a random Relic."), full);
        check(random.Unresolved.Length == 1 && random.Score == 0, "random relic is not guaranteed value");
        check(EventEffects.Assess(new("Pay", "Pay 100 Gold."), full).Unsafe, "unaffordable gold cost flagged");
        check(EventCoach.Evaluate([(opaque, false), (leave, true)], full, true) == "Leave. Leave: no change.", "disabled options excluded");
        check(!EventEffects.Assess(new("Remove", "Remove 1 card from your Deck."), full with { RemovalTargets = 0 }).Unsafe, "no removal target is not an unsafe cost");
        check(EventEffects.Assess(new("Potion", "Procure 1 random Uncommon Potion."), full with { EmptyPotionSlots = 0 }).Unresolved.Any(u => u.Contains("belt is full")), "full potion belt not silently replaced");
        check(EventCoach.Evaluate([(new("Heal", "Heal 10 HP."), true), (leave, true)], low, true)!.StartsWith("Heal."), "fully known event supports explicit pick");
        EventPick sword = new("Grab the Sword", "Obtain the [gold]Sword of Stone.[/gold]");
        EventPick dive = new("Dive", "Gain 101 Gold. Lose 7 HP.");
        check(EventCoach.Recommend([dive, sword], 33, 91, 107, true) is { } swordAdvice && swordAdvice.StartsWith("Grab the Sword.") && swordAdvice.Contains("26/91"), "Sunken Statue sword and HP comparison preserved");
        check(EventCoach.Recommend([sword, dive], 85, 91, 107, true)!.StartsWith("Dive."), "Sunken Statue healthy gold trade preserved");
        // Installed English templates: Zen Weaver, Morphic Grove, Trial,
        // Round Tea Party, Tablet of Truth, Drowning Beacon.
        check(EventEffects.Assess(new("Remove", "Pay 75 Gold. Remove 2 cards from your Deck."),
            full with { RemovalTargets = 1 }).Unsafe, "compound removal checks affordability and quantity");
        var removeTwo = EventEffects.Assess(new("Remove", "Remove 2 cards from your Deck."),
            full with { RemovalTargets = 2 });
        check(removeTwo.Score == 0 && removeTwo.Unresolved.Length == 1, "removal does not invent selected-card value");
        var morph = EventEffects.Assess(new("Transform", "Lose ALL of your Gold. Transform 2 cards."),
            full with { TransformTargets = 2 });
        check(morph.Score < 0 && morph.Unresolved.Length == 1, "all-gold cost retained across random transformation");
        check(EventEffects.Assess(new("Transform", "Transform 3 cards, then Upgrade them."),
            full with { TransformTargets = 2 }) is { Unsafe: false, Score: 0, Unresolved.Length: 1 }, "short transformation selection is uncertain rather than unsafe");
        check(EventEffects.Assess(new("Upgrade", "Upgrade 2 random cards."), full).Score == 0,
            "random upgrades not valued like selected upgrades");
        check(EventEffects.Assess(new("Upgrade", "Upgrade 2 cards."),
            full with { UpgradeTargets = 1 }) is { Unsafe: false, Score: 0, Unresolved.Length: 1 }, "short upgrade selection cannot force a supposedly safe alternative");
        var maxCost = EventEffects.Assess(new("Cost", "Lose 75 Max HP. Lose 6 HP."), full);
        check(maxCost.Unsafe, "max HP cost clamps current HP before mixed lethal cost");
        check(EventEffects.Assess(new("Tea", "Heal to full HP."), low).Known.Contains("heals 72 HP (80/80)"),
            "full heal uses live missing HP");
        check(EventEffects.Assess(new("Gold", "Obtain 300 Gold."), full).Score > 0, "Trial obtain gold wording");
        var mixed = EventEffects.Assess(new("Heal", "Heal 10 HP. Upgrade a card."),
            low with { UpgradeTargets = 0 });
        check(!mixed.Unsafe && mixed.Score == 20 && mixed.Known.Contains("heals 10 HP (18/80)"),
            "no upgrade targets preserves mixed healing benefit");
        var shortAdvice = EventEffects.Compare([(new("Upgrade", "Upgrade 2 cards."), true), (leave, true)],
            full with { UpgradeTargets = 1 }, true)!;
        check(!shortAdvice.StartsWith("Leave.") && shortAdvice.EndsWith("I can't assess every effect."),
            "short upgrade target count does not endorse Leave");
        var allGold = EventEffects.Assess(new("Gold", "Lose ALL your Gold."), full with { Gold = 0 });
        check(!allGold.Unsafe && allGold.Score == 0 && allGold.Known.Contains("lose 0 Gold"), "all-gold loss at zero is affordable");
        check(EventEffects.Assess(new("Gold", "Lose ALL your Gold. Pay 1 Gold."), full).Unsafe,
            "all-gold loss updates balance before later payment");
        bool requestedUpgrade = false;
        var noDeck = full with { ResolveNamedCard = (name, up) => { requestedUpgrade = name == "Lost Lute" && up; return null; } };
        check(EventEffects.Assess(new("Tome", "Obtain Lost Lute+."), noDeck).Unresolved.Length == 1 && requestedUpgrade,
            "named plus reward requests trusted upgrade resolution without synthetic upgrade");
        requestedUpgrade = false;
        EventEffects.Assess(new("Relic", "Obtain Lost Lute+.", Relic: "Lost Lute+"), noDeck);
        check(!requestedUpgrade, "relic metadata prevents named-card resolution");
        check(EventEffects.Assess(new("Remove", "Pay 10 Gold. Remove 2 cards from your Deck."),
            full with { RemovalTargets = 1 }) is { Unsafe: false, Unresolved.Length: 1 },
            "affordable partial removal is uncertain, not unsafe");
        var noRemoval = EventEffects.Assess(new("Gift", "Gain 100 Gold. Remove 2 cards from your Deck."),
            full with { RemovalTargets = 0 });
        check(!noRemoval.Unsafe && noRemoval.Score > 0 && noRemoval.Known.Contains("Gain 100 Gold"),
            "zero removal targets preserves mixed gold benefit");
        var clampedHeal = EventEffects.Assess(new("Heal", "Lose 10 Max HP. Heal to full HP."), low);
        check(!clampedHeal.Unsafe && clampedHeal.Known.Contains("heals 62 HP (70/70)"),
            "full healing after maximum HP loss respects reduced ceiling");
        bool calledResolver = false;
        EventEffects.Assess(accept, full with
        {
            ResolveNamedCard = (_, _) => { calledResolver = true; return null; }
        });
        check(!calledResolver, "audited Metamorphosis uncertainty precedes generic named-card resolution");
    }
}
