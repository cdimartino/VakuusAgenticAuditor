using Auditor;

internal static class EnemyKnowledgeRegression
{
    public static void Run(Action<bool, string> check)
    {
        string Exo(string current, string type, string next, string? logged = null) =>
            EnemyKnowledge.Describe("26660232", "EXOSKELETON", current, type, next,
                [("STRENGTH_POWER", 2), ("HARD_TO_KILL_POWER", 9)], logged);
        var skitter = Exo("SKITTER_MOVE", "RandomBranchState", "RAND", "SKITTER_MOVE");
        check(skitter.Contains("multi-hit attack") && skitter.Contains("fixed follow-up: mandibles")
            && skitter.Contains("9 per hit"), "logged Skitter cannot repeat; current cap has priority over Strength");
        var enrage = Exo("MANDIBLES_MOVE", "MoveState", "ENRAGE_MOVE");
        check(enrage.Contains("fixed follow-up: enrage (+2 Strength)"), "Mandibles leads to verified self buff");
        check(Exo("ENRAGE_MOVE", "RandomBranchState", "RAND", "ENRAGE_MOVE").Contains("random attack: skitter or mandibles"),
            "Enrage does not determine the next attack");
        check(!Exo("SKITTER_MOVE", "RandomBranchState", "RAND").Contains("fixed"),
            "forced or unlogged move cannot establish the no-repeat filter");
        check(!Exo("SKITTER_MOVE", "RandomBranchState", "OTHER", "SKITTER_MOVE").Contains("mandibles"),
            "custom branch cannot inherit the vanilla RAND arms");
        var conditional = EnemyKnowledge.Describe("26660232", "AEONGLASS", "EBB_MOVE", "ConditionalBranchState", "CHECK", []);
        check(conditional.Contains("attack + block") && conditional.Contains("conditional") && !conditional.Contains("fixed"),
            "independently cataloged compound intent; no predicate evaluated");
        var unknown = EnemyKnowledge.Describe("26660232", "UNMAPPED", "EBB_MOVE", "CustomState", "X",
            [("THORNS_POWER", 0), ("UNKNOWN_POWER", 9)]);
        check(unknown == "ebb; follow-up unknown", "unmapped monster gets neither another monster's tag nor an invented branch");
        check(EnemyKnowledge.Describe(null, "EXOSKELETON", "SKITTER_MOVE", "MoveState", "X", []) == ""
            && EnemyKnowledge.Describe("26660232", "EXOSKELETON", null, "MoveState", "X", []) == "",
            "approximate build or hidden/missing move disables pattern output");
    }
}
