using Auditor;

internal static class RelicGuideRegression
{
    internal static void Run(Action<bool, string> check)
    {
        Card C(string id, bool attack = false, int cost = 1) => Catalog.Get(id, id, cost, false, attack);
        var poison = C("DEADLY_POISON");
        var deck = new Deck([poison], 60, 80, 1, false, []);
        check(Coach.Relic("SNECKO_SKULL", deck).reason.Contains(poison.Name) && Coach.Assess(poison, deck with { Relics = ["SNECKO_SKULL"] }).Score > Coach.Assess(poison, deck).Score,
            "a real poison source supports Snecko Skull in both directions");
        var delayed = C("RELAX");
        check(RelicGuide.Match("FIDDLE", "Fiddle", delayed, immediate: true) == null, "next-turn draw does not trigger an in-turn draw warning");
        check(RelicGuide.Match("HELICAL_DART", "Helical Dart", C("BLADE_DANCE")) == null && RelicGuide.Match("HELICAL_DART", "Helical Dart", C("SHIV", true)) != null,
            "generating a Shiv differs from playing it");
        var draws = C("SHRUG_IT_OFF");
        check(Coach.Assess(draws, deck with { Relics = ["FIDDLE"] }).Score < Coach.Assess(draws, deck).Score, "mid-turn draw conflict lowers deck-fit score");
        check(RelicGuide.Match("BRONZE_SCALES", "Bronze Scales", draws) == null && RelicGuide.Get("BRONZE_SCALES")?.Cue.Length > 0, "passive Thorns gets a cue without inventing a block trigger");
        check(RelicGuide.Match("SNECKO_SKULL", "Snecko Skull", poison with { ForeignModel = true }) == null && RelicGuide.Get("UNKNOWN") == null, "unverified IDs cannot inherit matching rules");
        check(RelicGuide.Match("INTIMIDATING_HELMET", "Helmet", C("BASH", true, 2) with { VariableCost = true }) == null, "variable energy spend is not a guaranteed threshold");
        var fixedZero = C("NEUTRALIZE", true, 0);
        check(Coach.Rank([C("WELL_LAID_PLANS")], deck, true)[0].Id == "~skip",
            "a known role with no scored marginal benefit no longer beats Skip via a blanket card bonus");
        var fiddleDeck = deck with { Cards = [fixedZero], Relics = ["FIDDLE"] };
        check(Coach.Relic("INK_BOTTLE", fiddleDeck).score == 0 && RelicGuide.ForRelic("INK_BOTTLE", "Ink Bottle", fiddleDeck) == null
            && !RelicGuide.ForCard(fixedZero, ["FIDDLE", "INK_BOTTLE"]).Any(x => x.Id == "INK_BOTTLE"), "blocked in-turn draw gives no Ink Bottle shopping or reward synergy");
        check(Coach.Relic("OLD_COIN", deck with { Relics = ["ECTOPLASM"] }).score == 0, "blocked gold has no marginal purchase value");
        var mixed = Coach.Shop(Coach.Assess(draws, deck), 50, RelicGuide.ForRelic("SNECKO_SKULL", "Skull", deck), 170, 75, true, 190);
        check(new[] { "SHRUG_IT_OFF", "Skull", "170g", "Removal (75g)", "potion", "save your 190 gold" }.All(mixed.Contains),
            "shop can present affordable choices and keeping gold together without assigning a cross-category winner");
        check(Coach.Shop(null, 0, null, 0, null, false, 9) == "You can save your 9 gold.", "empty shop summary invents no legal purchase");
    }
}
