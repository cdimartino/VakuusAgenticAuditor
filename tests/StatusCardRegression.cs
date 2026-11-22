using Auditor;

internal static class StatusCardRegression
{
    internal static void Run(Action<bool, string> check)
    {
        Card C(string id) => Catalog.Get(id, id, 1, false, false);
        Card S(string id) => C(id) with { IsStatus = true };
        var deck = new Deck([], 4, 80, 1, false, []);
        var end = StatusCardGuide.Hand([(S("DAZED"), false), (S("BECKON"), true)], 0);
        check(end.UncertainRisk && end.Cue!.Contains("through Block") && end.Cue.Contains("playable now"), "end HP outranks Ethereal; suppress unsafe line");
        var drawn = StatusCardGuide.Hand([(S("VOID"), false)], 0);
        check(!drawn.UncertainRisk && drawn.Cue!.Contains("Drawing") && drawn.Cue.Contains("not playable"), "Void's draw trigger does not spend energy again");
        check(StatusCardGuide.Hand([(S("DISINTEGRATION"), false)], 0) == (null, false)
            && StatusCardGuide.Hand([], 6).UncertainRisk, "chosen card is not ongoing damage; active power is");
        var choices = new[] { "AMNESIA", "DISINTEGRATION", "MIND_ROT", "SLOTH", "WASTE_AWAY" }.Select(S).ToArray();
        foreach (int size in new[] { 3, 4, 5 })
            check(StatusCardGuide.Choices(choices[..size])?.Split('\n').Length == size
                && choices.All(c => double.IsNaN(Coach.Assess(c, deck).Score)), "Demon choices describe each observed penalty without a reward winner");
        check(StatusCardGuide.Choices([choices[0], choices[1], S("BURN")]) == null
            && StatusCardGuide.Choices([choices[0], choices[1], choices[2] with { ForeignModel = true }]) == null
            && StatusCardGuide.Choices([choices[0], choices[1], C("SLOTH")]) == null, "mixed, untyped or foreign options never inherit Demon choice advice");
        check(StatusCardGuide.Hand([(S("MOD_STATUS"), false)], 0) == (null, true)
            && StatusCardGuide.Lookup(S("BECKON") with { ForeignModel = true }) == null, "unknown and foreign statuses cannot borrow native forecasts");
        check(Coach.Assess(S("BURN"), deck).Score < 0 && double.IsNaN(Coach.Assess(C("BURN"), deck).Score)
            && double.IsNaN(Coach.Assess(S("MIND_ROT"), deck).Score) && !S("MIND_ROT").Has("draw"), "typed liability; encounter choices are not draw rewards");
        check(StatusCardGuide.Lookup(S("MOCK_STATUS_CARD"))?.Reward == null
            && StatusCardGuide.Lookup(C("JOY")) == null, "mock abstains; Joy is not a status");
        var supported = deck with { Cards = [C("PUNCH_OF_JOY")] };
        check(StatusCardGuide.GeneratorCue(C("MOURNING_BLOW"), new[] { C("ALL_IS_WELL") })!.Contains("creation grants Block"),
            "successful combat transformation invokes the generated-card hook");
        foreach (var id in new[] { "JEST", "MOURNING_BLOW", "SORROWFUL_STRIKE" })
        {
            var card = C(id);
            check(Coach.Assess(card, supported).Reason.Contains("into Joy") && Coach.Assess(card, deck).Reason.Contains("crowds")
                && !StatusCardGuide.Hand([(card, true)], 0).UncertainRisk, $"{id}: retained hand cost or converter, never current HP damage");
        }
        var exact = Catalog.Profile;
        try
        {
            Catalog.Profile = exact with { Exact = false };
            check(StatusCardGuide.Lookup(S("SADNESS")) == null && StatusCardGuide.GeneratorCue(C("JEST"), deck) == null,
                "approximate profile never borrows status classification or hand numbers");
        }
        finally { Catalog.Profile = exact; }
    }
}
