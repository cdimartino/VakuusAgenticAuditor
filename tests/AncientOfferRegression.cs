using Auditor;

internal static class AncientOfferRegression
{
    internal static void Run(Action<bool, string> check)
    {
        Card Basic(string id) => new(id, id, 1, false, true, true, ["hit"], []);
        var deck = new Deck([Basic("STRIKE_IRONCLAD"), Basic("DEFEND_IRONCLAD"), Basic("STRIKE_IRONCLAD")],
            40, 80, 2, false, []);
        string[] trio = ["EMPTY_CAGE", "ASTROLABE", "DUSTY_TOME"];
        var lute = new Card("LOST_LUTE", "Lost Lute+", 2, true, false, true, [], []);
        var context = new AncientOffer.Context(3, 3, 0, lute, deck.Cards, deck.Cards);
        var text = AncientOffer.Compare(trio, deck, true, context)!;
        check(text.Length <= 200 && text.Contains("Lean Cage") && text.Contains("3 upgraded") && text.Contains("Melody"), "Darv trio is concise and actionable");
        check(!text.Contains("++") && !text.Contains("unresolved") && !text.Contains("guaranteed"), "trio avoids duplicate upgrade marker and boilerplate");
        var verse = Catalog.Get("KI_VERSE", "Ki Verse", 1, false, false);
        check(text.Contains("no known source") && AncientOffer.Compare(trio, deck with { Cards = [.. deck.Cards, verse] }, true, context)!.Contains("Ki Verse helps build Melody for Lost Lute+"),
            "Darv distinguishes an actual Melody source from the offered Lute itself");
        check(AncientOffer.Compare(trio.Reverse(), deck, true, context) == text, "trio order invariant");
        check(!AncientOffer.Compare(trio, deck, false, context)!.Contains("Lean "), "indirect preference respected");
        var shortCounts = context with { Removable = 1, Transformable = 2 };
        var limited = AncientOffer.Compare(trio, deck, true, shortCounts)!;
        check(limited.Contains("only remove 1") && limited.Contains("only transform 2"), "short legal pools are capped not impossible");
        check(AncientOffer.Assess("EMPTY_CAGE", deck, context with { Removable = 0 })!.Score < 0, "zero legal removal has no invented value");
        check(AncientOffer.Assess("ASTROLABE", deck, context)!.Limited, "random transforms never deterministic");
        check(double.IsNaN(AncientOffer.Assess("DUSTY_TOME", deck, context)!.Score), "known Lost Lute identity does not imply modeled value");
        check(double.IsNaN(AncientOffer.Assess("DUSTY_TOME", deck)!.Score), "missing actual Tome card is not guessed");
        check(double.IsNaN(AncientOffer.Assess("EMPTY_CAGE", deck)!.Score)
            && double.IsNaN(AncientOffer.Assess("ASTROLABE", deck)!.Score)
            && !AncientOffer.Compare(trio, deck, true)!.Contains("Lean Cage"), "unknown targets or Tome cannot support an overall Cage pick");
        check(!AncientOffer.Compare(trio, deck, true, context with { TomeCard = null })!.Contains("Lean Cage"), "known cuts do not outrank an unread Tome");
        check(AncientOffer.Compare(["UNKNOWN"], deck, true) == null, "unknown offer falls through");
        var partial = AncientOffer.Compare(["UNKNOWN", "EMPTY_CAGE"], deck, true, context)!;
        check(!partial.Contains("Prefer") && !partial.Contains("Take"), "partial coverage never claims a complete winner");
        var lean = deck with { Cards = [Basic("BASH")] };
        check(AncientOffer.Assess("EMPTY_CAGE", deck, context)!.Score >
            AncientOffer.Assess("EMPTY_CAGE", lean, context with { RemovalTargets = lean.Cards })!.Score, "Cage responds to actual expendable draws");
        string[] darv = ["ASTROLABE", "BLACK_STAR", "CALLING_BELL", "EMPTY_CAGE", "PANDORAS_BOX",
            "RUNIC_PYRAMID", "SNECKO_EYE", "ECTOPLASM", "SOZU", "PHILOSOPHERS_STONE", "VELVET_CHOKER", "DUSTY_TOME"];
        check(darv.Count(id => AncientOffer.Assess(id, deck, context) != null) == 12, "all 12 verified Darv relic IDs have bounded advice");
        var costly = deck with { Cards = Enumerable.Range(0, 5).Select(i => Basic("COSTLY") with { Cost = 3 }).ToArray() };
        check(AncientOffer.Assess("SNECKO_EYE", costly)!.Score > AncientOffer.Assess("SNECKO_EYE", deck)!.Score, "Snecko distinguishes expensive from cheap deck");
    }
}
