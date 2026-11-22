using Auditor;

internal static class CardCoverageRegression
{
    internal static void Run(Action<bool, string> check)
    {
        Card Card(string id, bool up = false, bool native = true) => Catalog.Get(id, id, 1, up, false, native);
        var deck = new Deck([Card("STRIKE_FOOL")], 20, 80, 1, false, []);
        foreach (string id in new[] { "SPOILS_MAP", "MAD_SCIENCE", "QUICK_SLASH", "MOCK_TURN_END_IN_HAND_RECORDER_CARD", "NEW_MOD_CARD" })
        {
            var card = Card(id, true);
            check(!card.CombatKnown && double.IsNaN(Coach.Assess(card, deck).Score), $"{id}: no generic upgrade/cost reward or combat line");
        }
        var foreign = Card("ADRENALINE", native: false);
        check(!foreign.Known && !foreign.CombatKnown && !foreign.Has("energy"), "foreign model sharing native stable ID cannot supply a fake synergy");
        check(double.IsNaN(Coach.Assess(Card("REGRET", native: false) with { IsCurse = true }, deck).Score), "foreign curse cannot borrow native liabilities");
        check(Coach.Assess(Card("POOR_SLEEP") with { IsCurse = true }, deck).Score < 0
            && double.IsNaN(Coach.Assess(Card("POOR_SLEEP"), deck).Score), "newly accounted native curse uses dilution only when runtime-typed");
        var arrows = Card("WHISTLING_ARROWS");
        check(arrows.Needs.Contains("shots") && !arrows.Has("shots") && !arrows.CombatHas("shots"),
            "Whistling Arrows rewards shooting without consuming an arrow to play itself");
        var spinner = Card("SPINNER");
        check(spinner.Has("orbs") && !spinner.CombatHas("orbs") && Card("SPINNER", true).CombatHas("orbs"),
            "Spinner has recurring orb synergy in both states; only upgraded play channels immediately");
        check(Card("BUFFER").CombatKnown && double.IsNaN(Coach.Assess(Card("BUFFER"), deck).Score), "supported combat setup does not imply ranked reward");
        check(Card("PILLAGE").CombatKnown && Card("NAIL").CombatKnown && Catalog.Effect("NAIL") == "requires-impale", "bespoke combat effects survive reward evidence filtering");
        check(Card("WELL_LAID_PLANS").Has("retain hand") && !Card("WELL_LAID_PLANS").Has("discard"),
            "preventing discard does not enable a discard payoff");
        check(!Card("RELAX").CombatHas("energy") && Card("RELAX").Has("energy") && !Card("MAD_SCIENCE").Has("draw"),
            "next-turn effects and optional random riders do not create an immediate draw/energy pivot");
        check(Card("STACK").CombatHas("guard") && Card("REAVE").Has("souls") && Card("HIDDEN_DAGGERS", true).Has("shivs"),
            "calculated guard and alternate Soul/Shiv templates retain their verified roles");
        var lute = Card("LOST_LUTE", true);
        check(Coach.Assess(lute, deck with { Cards = [Card("KI_VERSE")] }).Score > Coach.Assess(lute, deck).Score,
            "actual native Melody card supports Lost Lute; basic Fool attack alone does not");
        var necro = new Deck([Card("HAUNT"), Card("CALCIFY"), Card("GRAVE_WARDEN"), Card("AFTERLIFE")], 55, 70, 1, false, [], "NECROBINDER");
        var rare = new[] { Card("UNDEATH") with { Cost = 0 }, Card("TRANSFIGURE"), Card("OBLIVION") with { Cost = 0 } };
        var shown = Coach.Rank(rare, necro, true);
        check(shown.Length == 4 && shown.All(x => double.IsFinite(x.Score)) && Coach.PartialReward(shown) == null,
            "pictured Necrobinder reward covers all three actual cards and legal Skip");
        check(Coach.Assess(rare[0], necro).Reason.Contains("7 Block") && Coach.Assess(Card("UNDEATH", true), necro).Reason.Contains("9 Block")
            && Coach.Assess(rare[0], necro).Reason.Contains("copy to your discard this combat"), "Undeath compares blocking against future combat copies");
        check(Coach.Assess(rare[1], necro).Reason.Contains("Haunt", StringComparison.OrdinalIgnoreCase)
            && Coach.Assess(rare[1], necro).Reason.Contains("1 energy") && Coach.Assess(rare[1], necro).Reason.EndsWith("Exhausts.")
            && Coach.Assess(Card("TRANSFIGURE", true), necro).Reason.Contains("does not Exhaust")
            && Coach.Assess(rare[1], necro).Score > Coach.Assess(rare[1], deck).Score,
            "Transfigure needs a replay target in the same hand, accounts for the increased cost and real upgrade");
        check(Coach.Assess(rare[2], necro).Reason.Contains("3 Doom") && Coach.Assess(Card("OBLIVION", true), necro).Reason.Contains("4 Doom")
            && Coach.Assess(rare[2], necro).Reason.Contains("afterward this turn") && Coach.Assess(rare[2], necro).Reason.Contains("GRAVE_WARDEN")
            && Coach.Assess(rare[2], necro).Score > Coach.Assess(rare[2], deck).Score, "Oblivion values follow-up plays supplied by Souls rather than immediate damage");
        check(rare.All(c => double.IsNaN(Coach.Assess(Card(c.Id, native: false), necro).Score)), "foreign cards cannot borrow the three rare reward rules");
        var partial = Coach.PartialReward(Coach.Rank([rare[0], Card("UNSEEN")], necro, ["skip", "another_id"]))!;
        check(partial.Contains("UNDEATH") && partial.Contains("7 Block") && partial.Contains("UNSEEN") && partial.Contains("alternative reward") && partial.Contains("Skip")
            && !partial.Contains("Take "), "mixed reward keeps useful known effect, unknown card, unknown alternative and legal Skip");
        check(!Coach.PartialReward(Coach.Rank([Card("UNSEEN")], necro, false))!.Contains("Skip"), "no absent Skip invented for a forced reward");
    }
}
