using Auditor;

internal static class BlitzDraftRegression
{
    internal static void Run(Action<bool, string> check)
    {
        Card Hit(string id) => new(id, id, 1, false, true, true, ["hit"], []);
        var deck = new Deck([Hit("STRIKE_TEST")], 40, 80, 1, false, []);
        Card[] cards = [Hit("A"), Hit("B"), Hit("C"), Hit("D")];
        var exact = BlitzDraft.Best(cards, deck, 3, 3, [])!;
        check(exact.Indices.Length == 3, "draft chooses exactly the legal count");
        var partial = BlitzDraft.Best(cards, deck, 3, 3, [3])!;
        check(partial.Indices.Length == 3 && partial.Indices.Contains(3), "draft preserves partial selection");
        check(BlitzDraft.Advice(cards, deck, 3, 3, [0, 1, 2], true).Contains("confirm"),
            "completed draft does not recommend a fourth card");
        var ranged = BlitzDraft.Best(cards, deck, 1, 2, [])!;
        check(ranged.Indices.Length is >= 1 and <= 2
            && BlitzDraft.Advice(cards, deck, 1, 2, [], true).Contains("1–2"),
            "draft displays and respects legal range");
        check(BlitzDraft.Best(cards, deck, 3, 2, []) == null
            && BlitzDraft.Best(cards, deck, 3, 3, [0, 0]) == null,
            "invalid bounds or duplicate selection abstains");
        check(BlitzDraft.Best([Hit("SAME"), Hit("SAME")], deck, 2, 2, [])!.Indices.Length == 2,
            "duplicate card offers retain separate indices");
        var enabler = new Card("ENABLE", "Enable", 1, false, false, true, ["test-support"], []);
        var payoff = new Card("PAYOFF", "Payoff", 1, false, false, true, [], ["test-support"]);
        check(BlitzDraft.Package([payoff, enabler], deck) >
            BlitzDraft.Package([payoff, Hit("OTHER")], deck), "package evaluates internal synergy");
        check(BlitzDraft.Package([payoff], deck with { Cards = [.. deck.Cards, enabler] }) >
            BlitzDraft.Package([payoff], deck), "package evaluates actual existing support");
        var unknown = new Card("UNKNOWN", "Unknown", 1, false, false, false, [], []);
        check(BlitzDraft.Best([.. cards, unknown], deck, 3, 3, []) is { Partial: true }
            && BlitzDraft.Best([.. cards, unknown], deck, 3, 3, [4]) == null,
            "unknown offers qualify assessed winner; selected unknown prevents unsupported comparison");
        check(!BlitzDraft.Advice(cards, deck, 3, 3, [], false).Contains(" Add "),
            "draft respects indirect pick preference");
        check(BlitzDraft.Bundles([[unknown], [cards[0]]], deck, false, true).Contains("can’t fully compare"),
            "unknown bundle prevents overall winner");
        string[] ids = ["JEST", "MOURNING_BLOW", "RECALL", "SORROWFUL_STRIKE", "WAH_HAMMER",
            "CHO_KI", "CHO_LIMBS", "LOCKED_IN", "WAH_BLAST", "KI_VERSE"];
        var pictured = ids.Select(id => Catalog.Get(id, id.Replace('_', ' '), 1, false,
            id is "MOURNING_BLOW" or "SORROWFUL_STRIKE" or "WAH_HAMMER" or "WAH_BLAST")).ToArray();
        var text = BlitzDraft.Advice(pictured, deck, 3, 3, [0], true);
        check(text.Contains("Choose 3 total; 1 selected.") && !text.Contains("Take "),
            "pictured 3-of-10 keeps correct count without forcing a single-card reward pick");
        string[] names = ["Jest", "Mourning Blow", "Recall", "Sorrowful Strike", "Wah Hammer",
            "Cho Ki", "Cho Limbs", "Locked In", "Wah Blast", "Ki Verse"];
        int[] costs = [2, 0, 1, 1, 1, 1, 2, 1, 2, 1];
        pictured = pictured.Select((c, i) => c with { Name = names[i], Cost = costs[i], NativeShoot = false }).ToArray();
        Card Starter(string id, string name, bool attack, bool shoot = false) =>
            Catalog.Get(id, name, 1, false, attack) with { NativeShoot = shoot };
        var fool = new Deck([
            .. Enumerable.Range(0, 5).Select(_ => Starter("STRIKE_FOOL", "Strike", true)),
            .. Enumerable.Range(0, 4).Select(_ => Starter("DEFEND_FOOL", "Defend", false)),
            Starter("FOOLISH_SHOT", "Foolish Shot", true, true)
        ], 77, 77, 1, false, ["SONG_BOW"], "FOOL");
        var fixture = BlitzDraft.Advice(pictured, fool, 3, 3, [], true);
        Console.WriteLine("PICTURED DRAFT: " + fixture);
        check(BlitzDraft.Best(pictured, fool, 3, 3, []) is { Indices.Length: 3, Partial: false }
            && fixture.Contains("Add "), "pictured native Fool starter gets assessed trio");
        var fixturePartial = BlitzDraft.Advice(pictured, fool, 3, 3, [0], true);
        Console.WriteLine("PICTURED PARTIAL: " + fixturePartial);
        check(BlitzDraft.Best(pictured, fool, 3, 3, [0])!.Indices.Contains(0),
            "pictured Fool partial selection is preserved");
        check(BlitzDraft.Assess(pictured[7], fool) >
            BlitzDraft.Assess(pictured[7], fool with { Cards = fool.Cards.Where(c => c.NativeShoot != true).ToArray() }),
            "Locked In values actual native Shoot eligibility, not English card name");
        check(double.IsNaN(BlitzDraft.Assess(pictured[7], fool with { Cards = [unknown] }))
            && double.IsNaN(BlitzDraft.Assess(pictured[7] with { ForeignModel = true }, fool)),
            "Locked In does not infer unverified or foreign tutor support");
        var withUnknown = BlitzDraft.Advice([.. pictured, unknown], fool, 3, 3, [], true);
        check(withUnknown.Contains("Among assessed combinations") && withUnknown.Contains("Add ")
            && withUnknown.Contains("Unmodeled: Unknown"), "unknown offer keeps useful assessed trio with one caveat");
        var memo = new BlitzDraft.Memo();
        object screen = new();
        int evaluations = 0;
        string Evaluate() { evaluations++; return "result"; }
        memo.Get(screen, "unchanged", Evaluate);
        memo.Get(screen, "unchanged", Evaluate);
        check(evaluations == 1, "unchanged screen fingerprint reuses bounded draft memo");
        memo.Get(screen, "changed-selection", Evaluate);
        memo.Get(new object(), "changed-selection", Evaluate);
        check(evaluations == 3, "changed selection or new screen invalidates draft memo");
        object draftScreen = new();
        BlitzDraft.Advice(pictured, fool, 3, 3, [], true, draftScreen);
        int first = BlitzDraft.DraftEvaluations;
        BlitzDraft.Advice(pictured.Select(c => c with { Gives = [.. c.Gives], Needs = [.. c.Needs] }).ToArray(),
            fool with { Cards = [.. fool.Cards] }, 3, 3, [], true, draftScreen);
        check(BlitzDraft.DraftEvaluations == first, "equivalent rebuilt runtime snapshot reuses actual draft advice");
        BlitzDraft.Advice(pictured, fool, 3, 3, [0], true, draftScreen);
        check(BlitzDraft.DraftEvaluations == first + 1, "actual draft selection change recomputes advice");
        BlitzDraft.Advice(pictured, fool with { Hp = 20 }, 3, 3, [0], true, draftScreen);
        check(BlitzDraft.DraftEvaluations == first + 2, "actual draft resource change recomputes advice");
    }
}
