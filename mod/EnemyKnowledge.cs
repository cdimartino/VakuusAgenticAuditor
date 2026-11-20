namespace Auditor;

// Inputs are a snapshot of one visible intent. Never resolve a branch to populate these fields.
internal static class EnemyKnowledge
{
    const string Build = "26660232";
    static readonly Lazy<Dictionary<(string, string), string>> Moves = new(() =>
    {
        using var reader = new StreamReader(CatalogProfile.Open(Build + "/enemy-patterns.tsv"));
        var moves = new Dictionary<(string, string), string>();
        while (reader.ReadLine() is { } line)
        {
            var parts = line.Split('\t');
            moves.Add((parts[0], parts[1]), parts[2]);
        }
        return moves;
    });

    internal static string Describe(string? exactBuild, string monsterId, string? currentMoveId,
        string? followUpType, string? followUpId, IEnumerable<(string Id, int Amount)> powers, string? lastLoggedMoveId = null)
    {
        if (exactBuild != Build || string.IsNullOrWhiteSpace(currentMoveId)) return "";
        string Label(string id)
        {
            string name = id.EndsWith("_MOVE", StringComparison.Ordinal) ? id[..^5] : id;
            return name.Replace('_', ' ').ToLowerInvariant();
        }
        string Move(string id) => Moves.Value.TryGetValue((monsterId, id), out var kind)
            ? $"{Label(id)} ({kind})" : Label(id);
        string next = followUpType switch
        {
            "MoveState" when !string.IsNullOrWhiteSpace(followUpId) => "fixed follow-up: " + Move(followUpId),
            "ConditionalBranchState" => "conditional follow-up; recheck intent",
            "RandomBranchState" when monsterId == "EXOSKELETON" && followUpId == "RAND"
                && currentMoveId == "SKITTER_MOVE" && lastLoggedMoveId == currentMoveId
                => "fixed follow-up: mandibles (skitter cannot repeat)",
            "RandomBranchState" when monsterId == "EXOSKELETON" && followUpId == "RAND"
                && currentMoveId is "SKITTER_MOVE" or "ENRAGE_MOVE" => "random attack: skitter or mandibles",
            "RandomBranchState" => "random follow-up; recheck intent",
            _ => "follow-up unknown"
        };
        var cue = powers.Where(p => p.Amount > 0).Select(p => PowerCue(p.Id, p.Amount))
            .Where(c => c.Text != null).OrderBy(c => c.Priority).FirstOrDefault().Text;
        return $"{Move(currentMoveId)}; {next}" + (cue == null ? "" : $". {cue}");
    }

    // One actionable visible power; prioritize effects that can invalidate the apparent kill.
    static (int Priority, string? Text) PowerCue(string id, int n) => id switch
    {
        "ILLUSION_POWER" => (0, "Illusion revives next turn"),
        "HARD_TO_KILL_POWER" => (1, $"Damage capped at {n} per hit"),
        "INTANGIBLE_POWER" => (2, "Damage capped at 1 this turn"),
        "SLIPPERY_POWER" => (3, "Next HP loss capped at 1"),
        "BUFFER_POWER" => (4, "Buffer prevents HP loss"),
        "THORNS_POWER" => (5, $"Thorns returns {n} per attack hit"),
        "ENRAGE_POWER" => (6, "Skills feed enemy Strength"),
        "CURL_UP_POWER" => (7, "First damage triggers block"),
        "PAINFUL_STABS_POWER" => (8, "Unblocked hits add Wounds"),
        "RITUAL_POWER" => (9, "Ritual adds Strength each turn"),
        "ARTIFACT_POWER" => (10, $"Artifact can negate {n} debuff(s)"),
        "STRENGTH_POWER" => (11, $"+{n} Strength on attacks"),
        _ => (int.MaxValue, null)
    };
}
