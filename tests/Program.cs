using Auditor;
Catalog.Profile = CatalogProfile.Find("v0.113.1", "d09d48e5", -1414792094) ?? throw new Exception("Missing audited beta catalog");
int count = 0;
void Check(bool v, string name) { if (!v) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
var shortcut = new SettingsShortcut<int>();
Check(shortcut.Key(8, true, false, 8, false) && !shortcut.Key(8, false, false, 8, false), "settings press and release between process frames delivers once");
Check(shortcut.Key(8, true, false, 8, false) && !shortcut.Key(8, true, false, 8, false) && !shortcut.Key(8, true, true, 8, false), "held settings key ignores duplicate delivery and echo");
shortcut.Key(8, false, false, 8, false);
Check(!shortcut.Key(9, true, false, 8, true) && !shortcut.Key(9, true, false, 9, false), "modal key capture and rebinding cannot replay held press");
shortcut.Key(9, false, false, 9, false);
Check(!shortcut.Key(8, true, false, 9, false) && shortcut.Key(9, true, false, 9, false), "rebound settings uses new key only");
shortcut.Reset();
Check(shortcut.Key(9, true, false, 9, false), "focus loss rearms settings after missing release");
Check(!shortcut.Controller(true, true) && !shortcut.Controller(true, false) && !shortcut.Controller(false, false) && shortcut.Controller(true, false) && !shortcut.Controller(true, false), "controller settings respects modal and requires fresh chord edge");
shortcut.Reset();
Check(shortcut.Controller(true, false), "focus loss rearms controller settings chord");
Card C(string id, int cost = 1) => Catalog.Get(id, id, cost, false, id.StartsWith("STRIKE_"));
Check(CatalogProfile.Find("v0.113.1", "different", -1414792094) == null && CatalogProfile.Find("v0.113.1", "d09d48e5", 0) == null && CatalogProfile.Find(null, null, null) == null, "unverified rebuilds never inherit a matching version's card rules");
var profiles = new[] { Catalog.Profile with { Version = "v0.113.9", Build = "9" }, Catalog.Profile with { Version = "v0.113.11", Build = "11" }, Catalog.Profile with { Version = "v0.113.10", Rules = "unsupported" } };
Check(CatalogProfile.Select(profiles, "v0.113.10", "new", null, true)?.Build == "9" && CatalogProfile.Select(profiles, "v0.113.12", "new", null, true)?.Build == "11", "override selects numeric nearest supported rules, older patch on ties");
Check(CatalogProfile.Select(profiles, "v0.113.10", "new", null, false) == null && CatalogProfile.Select(profiles, "v1.0.0", null, null, true) == null && CatalogProfile.Select(profiles, "unknown", null, null, true) == null, "fallback requires opt-in and a comparable major version");
Check(CatalogProfile.Find("v0.113.1", "d09d48e5", -1414792094, true) == Catalog.Profile, "exact build wins with override enabled");
var deck = new Deck(Enumerable.Repeat(C("STRIKE_IRONCLAD"), 6).Concat(Enumerable.Repeat(C("DEFEND_IRONCLAD"), 4)).ToArray(), 70, 80, 1, false, []);
foreach (var (who, enabler, payoff) in new[]{
    ("Ironclad","TRUE_GRIT","FEEL_NO_PAIN"),("Silent","BLADE_DANCE","ACCURACY"),
    ("Defect","COLD_SNAP","DEFRAGMENT"),("Regent","BULWARK","CONQUEROR"),
    ("Necrobinder","AFTERLIFE","PROTECTOR"),("Fool","JEST","ALL_IS_WELL")})
{
    var card = C(payoff);
    var alone = Coach.Assess(card, deck);
    var supported = Coach.Assess(card, deck with { Cards = [.. deck.Cards, C(enabler)] });
    Check(supported.Score > alone.Score + 2 && supported.Reason.Contains(enabler), who + " recognizes enabler");
}
var low = deck with { Hp = 18 };
Check(Coach.Assess(C("SHRUG_IT_OFF"), low).Score > Coach.Assess(C("SHRUG_IT_OFF"), deck).Score, "HP changes defense priority");
var heavy = deck with { Cards = [.. deck.Cards, C("BASH", 2), C("BASH", 2), C("BASH", 2)] };
Check(Coach.Assess(C("BULWARK", 2), heavy).Score < Coach.Assess(C("BULWARK", 2), heavy with { Cards = [.. heavy.Cards, C("ADRENALINE")] }).Score, "energy supply changes expensive pick");
var choice = Coach.Rank([C("ACCURACY")], deck, true);
Check(choice[0].Id == "~skip" && Coach.After(choice[1], choice[0]).Contains("enabler"), "unsupported payoff loses to skip; feedback explains why");
Check(Coach.Rank([C("ACCURACY")], deck, false).Length == 1, "no skip when forced");
Check(double.IsNaN(Coach.Assess(C("UNKNOWN_MOD_CARD"), deck).Score), "unknowns unranked");
Check(Coach.Assess(C("BLADE_DANCE"), deck with { Relics = ["KUNAI"] }).Reason.Contains("Kunai"), "relic synergy");
Check(Coach.Rank([C("TRUE_GRIT"), C("TRUE_GRIT")], deck, false).Select(x => x.Score).Distinct().Count() == 1, "equal choices stay equal");
Play attack = new("Strike", 1, 0, 0, 0, [6]);
Play defend = new("Defend", 1, 0, 0, 5, [-1]);
TurnLine Turn(Play[] h, Foe[] f, int e, int hp = 20, int stars = 0, int arrows = 0) => TurnCoach.Plan(h, f, e, stars, arrows, 0, hp);
Check(Turn([defend, attack, attack], [new("Slug", 12, 12)], 2).EnemiesLeft == 0, "two-hit lethal beats block");
Check(Turn([attack, defend], [new("Slug", 24, 8)], 1, 5).Steps.Single().Card == 1, "surviving beats chip damage");
Check(Turn([attack with { Damage = [6, 6] }], [new("A", 6, 0), new("B", 6, 12)], 1).Steps.Single().Target == 1, "focus the dangerous attacker");
Check(Turn([attack with { Damage = [4, 4], Area = true }], [new("A", 4, 3), new("B", 4, 5)], 1).EnemiesLeft == 0, "area damage removes both intents");
Check(Turn([attack with { Stars = 2 }, attack with { Arrows = 1 }], [new("A", 10, 3)], 3).Steps.Count == 0, "no overspending stars or arrows");
Check(Turn([attack], [new("A", 10, 3)], 3).Steps.Count == 1, "each hand card usable once");
Check(Turn([new("Draw", 0, 0, 0, 0, [-1], Pivot: true)], [new("A", 12, 0)], 1).Steps.Count == 1, "draw is a replan point");
EventPick sword = new("Grab the Sword", "Obtain the [gold]Sword of Stone.[/gold]");
EventPick dive = new("Dive into the Water", "Gain [blue]101[/blue] Gold. Lose [red]7[/red] HP.");
Check(EventCoach.Recommend([dive, sword], 33, 91, 107, true) is { } advice && advice.StartsWith(sword.Title) && advice.Contains("26/91"), "Sunken Statue: recommend sword and quantify the cost of diving");
Check(EventCoach.Recommend([sword, dive], 85, 91, 107, true)!.StartsWith(dive.Title), "healthy player can trade HP for gold");
Check(EventCoach.Recommend([sword, new("Risk", "Gain 101 Gold. Fight an elite.")], 33, 91, 107, true) == null, "unknown event consequence is not treated as free");
Bottle ampoule = new("EXPLOSIVE_AMPOULE", "Explosive Ampoule"), blockPot = new("BLOCK_POTION", "Block Potion"), strength = new("STRENGTH_POTION", "Strength Potion"), other = new("OTHER", "Other");
Check(PotionCoach.Recommend([strength, blockPot], [other, ampoule], 2, true, true)?.StartsWith("Discard Explosive Ampoule for Block Potion.") == true, "full belt before Waterfall Giant favors block over area damage");
Check(PotionCoach.Recommend([strength, blockPot], [ampoule], 2, true, true)?.StartsWith("Take Block Potion in the open slot.") == true, "open slot never suggests discarding");
Check(PotionCoach.Recommend([strength, blockPot], [other, ampoule], 2, false, true) == null, "no Giant swap at a different encounter");
CombatBottle shield = new("BLOCK_POTION", "Block Potion", 12, [true]), fire = new("FIRE_POTION", "Fire Potion", 20, [true]);
string? Use(CombatBottle[] belt, int life, int incoming, int energy = 1)
{
    Foe[] foes = [new("Giant", life, incoming)];
    var plan = Turn([attack], foes, energy);
    return PotionCoach.Use(belt, foes, [attack], plan, 20, true);
}
Check(Use([shield], 240, 24)!.Contains("before ending the turn"), "block potion on a heavy unblocked hit");
Check(Use([shield], 240, 2) == null, "save block potion for meaningful damage");
Check(Use([shield, fire], 6, 24) == null, "save potions when hand alone wins");
Check(Use([shield, fire], 24, 24)!.Contains("Fire Potion after your cards"), "prefer ending fight over blocking it");
Check(Use([fire with { Targets = [false] }], 24, 24) == null, "no fire potion suggestion for illegal target");
Check(Use([new("STRENGTH_POTION", "Strength Potion", 0, [true])], 240, 0)!.Contains("before your first attack"), "time strength before boss attacks");
var vitriol = new Play("Vitriol", 0, 0, 0, 0, [0], Weak: 1, Sadness: 1, Skill: true);
var punch = new Play("Punch of Joy", 2, 0, 0, 0, [14], Converts: true, Attack: true);
Play[] fool = [punch, attack, new("Jest", 2, 0, 0, 12, [-1], Sadness: 1), attack, vitriol];
var combo = Turn(fool, [new("Giant", 240, 0)], 3);
Check(combo.Joy == 1 && combo.Steps.FindIndex(s => s.Card == 4) < combo.Steps.FindIndex(s => s.Card == 0), "Fool turn: use Vitriol before Punch of Joy regardless of hand order");
Check(Turn([vitriol], [new("A", 50, 20)], 0).Incoming == 15, "new Weak reduces the target's intent");
Check(Turn([vitriol], [new("A", 50, 20, new(0, 0, 1))], 0).Incoming == 20, "Artifact prevents the predicted Weak");
Check(Turn([vitriol], [new("A", 50, 15, new(1, 0, 0))], 0).Incoming == 15, "already weakened attack not reduced twice");
Check(TurnCoach.Plan([punch], [new("A", 50, 8)], 2, 0, 0, 0, 20, sadness: 1).Joy == 1, "Sadness already in hand converts without a generator");
Check(Turn([punch], [new("A", 50, 8)], 2).Joy == 0, "no conversion without Sadness");
var beam = attack with { Name = "Beam Cell", Energy = 0, Damage = [3], Vulnerable = 1, Attack = true };
Check(Turn([attack, beam], [new("A", 12, 20)], 1).Steps[0].Card == 1, "Vulnerable opener enables lethal with the next attack");
var finish = attack with { HitsFrom = "attacks", Attack = true };
Check(TurnCoach.Plan([finish], [new("A", 12, 10)], 1, 0, 0, 0, 20, attacks: 2).EnemiesLeft == 0 &&
      Turn([finish], [new("A", 12, 10)], 1).EnemiesLeft == 1, "Finisher counts completed attacks, not future ones");
RouteStop Road(string room, params RouteStop[] next) => new(room, next);
var fork = new[] { Road("Monster", Road("RestSite")), Road("Monster", Road("Shop")) };
var traveler = new RouteState(80, 90, 182, 1, true);
Check(RouteCoach.Choose(fork, traveler)?.Index == 1, "same opening fight: prefer merchant with gold");
Check(RouteCoach.Choose(fork, traveler with { Hp = 24, Gold = 32 })?.Index == 0, "same fork: prefer camp when injured and poor");
Check(RouteCoach.Choose([fork[0]], traveler) == null, "no generic advice for forced step");
var gifts = new[] { ("YUMMY_COOKIE", "Yummy Cookie"), ("BIIIG_HUG", "Biiig Hug"), ("PUMPKIN_CANDLE", "Pumpkin Candle") };
Check(EventCoach.Ancient(gifts, 5, 4, 1, true)!.StartsWith("Take Yummy Cookie"), "Tezcatara upgrades a deck with good upgrade targets");
Check(EventCoach.Ancient(gifts, 1, 5, 5, true)!.StartsWith("Take Pumpkin Candle"), "expensive deck favors energy");
Check(EventCoach.Ancient(gifts, 1, 9, 1, true)!.StartsWith("Take Biiig Hug"), "starter-heavy deck favors removals");
var layout = new RewardLayout(new(0, 0, 1920, 1080), new(350, 650), [new(580, 170, 760, 700), new(0, 0, 1920, 160)]);
Check(layout.Place(315, 214) is { } box && box.X + 315 < 580 && box.Y > 160, "bubble near player clears rewards and HUD");
Check((layout with { Blocked = [layout.View] }).Place(315, 214) == null, "no overlay when no clear space exists");
Check(C("UPPERCUT").Has("weak") && C("UPPERCUT").Has("vulnerable") && !C("GO_FOR_THE_EYES").Has("weak"), "unconditional debuffs distinct from conditional previews");
Check(C("DAGGER_SPRAY").Has("multiple hits") && !C("DISMANTLE").Has("multiple hits"), "conditional second hit not counted as guaranteed");
Check(Coach.Assess(C("DEADLY_POISON"), deck with { Relics = ["SNECKO_SKULL"] }).Reason.Contains("Snecko Skull"), "poison application recognized by owned relic");
Check(Turn([attack with { HpLoss = 2 }], [new("Target", 6, 0)], 1, 2).Steps.Count == 0, "planner cannot spend last HP for a projected kill");
Check(Catalog.Bosses.Count == 12 && Catalog.Bosses["TEST_SUBJECT_BOSS"].Contains("revivals"), "boss notes distinguish phase break from victory");
var drawCtx = new BeltContext(2, 1, 1, 9, 2, 0, 0, 0, 0, 0, 0, 0, false, false, false, false, false);
Check(PotionCoach.Use([new("SWIFT_POTION", "Swift Potion", 0, [true])], [new("Boss", 200, 15)], [], new([], 15, 1, 0), 40, true, drawCtx)?.Contains("Swift Potion") == true, "draw potion suggested with hand space and remaining energy");
Check(PotionCoach.Use([new("SWIFT_POTION", "Swift Potion", 0, [true])], [new("Boss", 200, 15)], [], new([], 15, 1, 0), 40, true, drawCtx with { Size = 10 }) == null, "draw potion withheld with full hand");
Check(Catalog.Potions.Count == 67 && Catalog.Potions["FAIRY_IN_A_BOTTLE"][0] == "automatic", "wiki potion policies include passive revival");
var gate = new DecisionGate(); object offerA = new(), rewardsPage = new();
gate.Finish(offerA);
Check(!gate.Ready(offerA, "take A", 100, true), "no repeat on outgoing offer, even on manual request");
Check(!gate.Ready(null, "choice made", 210, false) && !gate.Ready(rewardsPage, "choice made", 320, false) &&
    gate.Ready(rewardsPage, "choice made", 640, false), "intentional feedback waits through overlay gap for stable rewards");
gate.Transition();
Check(!gate.Ready(rewardsPage, "choice made", 700, false), "screen transition resets the dwell timer");
gate.Reopen(offerA);
Check(gate.Ready(offerA, "new options", 750, true), "fresh offer remains immediately available on demand");
gate.NewReaction(); gate.Delivered(rewardsPage);
Check(!gate.MayDeliver(rewardsPage) && gate.LeftReactionScreen(null) && gate.LeftReactionScreen(new object()), "praise cannot replay after rewards close or combat starts");
Check(gate.FreshTurnHint(offerA, "1", "Sky Shot → Strike"), "first turn hint available");
gate.MarkTurnHint("Sky Shot → Strike"); gate.Transition();
Check(!gate.FreshTurnHint(offerA, "1", "Sky Shot → Strike") && gate.FreshTurnHint(offerA, "1", "Defend"), "transient UI cannot repeat a spoken combat line; changed line remains eligible");
var savedSpots = new Dictionary<string, BubbleSpot> { { "map", BubbleSpot.From(797, 399, 200, 100, 1000, 600) }, { "fight", BubbleSpot.From(-300, -10, 200, 100, 1000, 600) } };
var roundtrip = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, BubbleSpot>>(System.Text.Json.JsonSerializer.Serialize(savedSpots))!;
Check(roundtrip["map"].Restore(200, 100, 800, 500).Left == 594 && roundtrip["fight"].Restore(200, 100, 800, 500).Left == 6, "drag positions persist independently and stay on screen after resize");
Check(!BubbleSpot.Moved(2, 2) && BubbleSpot.Moved(6, 0), "click jitter dismisses; deliberate movement drags");
foreach (var (who, source, payoff, name) in new[]{
    ("IRONCLAD","TRUE_GRIT","FEEL_NO_PAIN","Exhaust engine"),("SILENT","BLADE_DANCE","ACCURACY","Shiv engine"),
    ("DEFECT","COLD_SNAP","BARRAGE","Orb engine"),("REGENT","BULWARK","CONQUEROR","Sovereign Blade"),
    ("NECROBINDER","AFTERLIFE","PROTECTOR","Osty scaling"),("FOOL","VITRIOL","PUNCH_OF_JOY","Sadness conversion")})
{
    var baseDeck = deck with { Character = who, Cards = [C(source)] };
    Check(Coach.Assess(C(payoff), baseDeck).Reason.Contains(name) && Archetypes.Analyze(baseDeck with { Cards = [C(source), C(payoff)] }).Any(x => x.Online), who + " sees the offered path and recognizes the completed engine");
}
FloorResult[] history = [new(1, 1, "Small enemy", 2, 0, 0, 0, 1, 0, 0, 2), new(1, 2, "Elite", 21, 0, 0, 0, 0, 0, 0, 5)];
var loss = RunReview.Write(deck with { Character = "FOOL", Cards = [C("VITRIOL"), C("PUNCH_OF_JOY")] }, history, false, false, "Elite", 2, 42, ["Block Potion"]);
Check(loss.Contains("floor 2, Elite (21") && loss.Contains("Recorded cause: Elite") && loss.Contains("cannot establish whether") && loss.Contains("Sadness conversion"), "loss review ties coaching to history without claiming potion would save the run");
Check(RunReview.Write(deck, history, true, false, null, 0, 0, []).Contains("WHAT TO REPEAT"), "victory review uses winning analysis");
Check(!RunReview.Write(deck, [], false, true, null, 0, 0, []).Contains("LOSS ANALYSIS"), "abandoned run is not misclassified as a death");
var curseOffer = new[] { new Verdict("best", "Best", 5, "", null), new Verdict("middle", "Middle", 2, "", null), new Verdict("~skip", "Skip", 0, "", null) };
string Cursed(int a, int i, bool on = true) => Curse.Advice("Honest", on, a, i.ToString(), curseOffer);
var decisionsToTest = Enumerable.Range(0, 1000).ToArray();
var bitten = decisionsToTest.First(i => Cursed(0, i) != "Honest");
Check(decisionsToTest.All(i => Cursed(10, i, false) == "Honest"), "curse opt-out preserves every hint");
Check(Curse.Chance(-1) == 10 && Curse.Chance(12) == 40 && decisionsToTest.Count(i => Cursed(10, i) != "Honest") > decisionsToTest.Count(i => Cursed(0, i) != "Honest"), "higher ascension recruits more decisions without exceeding the cap");
Check(Cursed(0, bitten).Contains("Take Middle.") && Cursed(5, bitten).Contains("Skip this reward."), "a triggered curse recommends a real weaker choice, then the weakest at A5");
Check(Curse.Advice("Detailed", true, 0, bitten.ToString(), curseOffer) == Cursed(0, bitten), "reopening or asking for detail cannot reroll crooked counsel");
Check(Curse.Advice("Truth", true, 0, bitten.ToString(), null).StartsWith("CURSE · Hint withheld") && Curse.Advice("Truth", true, 0, bitten.ToString(), [curseOffer[0], curseOffer[0]]).Contains("Hint withheld"), "without a clearly weaker alternative Vakuu withholds advice rather than inventing a choice");
CombatRegression.Run(Check);
RoomRegression.Run(Check);
JournalRegression.Run(Check);
PresetRegression.Run(Check);
ExportRegression.Run(Check);
Console.WriteLine($"{count} scenarios passed.");
