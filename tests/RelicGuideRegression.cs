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
    }
}
