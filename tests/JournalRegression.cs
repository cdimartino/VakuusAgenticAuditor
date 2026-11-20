namespace Auditor;

internal static class JournalRegression
{
    internal static void Run(Action<bool, string> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "auditor-journal-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var journal = new RunJournal(directory);
            var state = new JournalState(1, 2, "Monster", "Test", "combat-1", 1, "player", 30, 80, 0, 3, 99,
                [new("ANGER", "Anger", "card-1", 0, false)], [], [], [], [],
                [new("TEST", "Test", "enemy-1", 20, 0, [new("Strength", 2)], ["Attack 10"])]);
            var start = journal.State(state)!.Value;
            check(journal.State(state) == null, "identical state deduplicated");
            journal.Advice(new("decision-1", start, "actual cursed text", true, ["card-1"], "draw"));
            var end = journal.State(state with { Hp = 20, Turn = 2 })!.Value;
            journal.Transition(new(start, end, "Observed enemy-phase endpoints"));
            journal.Forecast(new("decision-1", start, end, 12, true, null));
            journal.Forecast(new("historical-kin", start, end, 21, false, "potion changed the line"));
            var records = RunJournal.Read(journal.FilePath);
            check(records.Any(e => e.Kind == "state" && e.Data.GetProperty("Enemies")[0].GetProperty("Powers")[0].GetProperty("Amount").GetInt32() == 2), "raw enemy powers retained");
            check(records.Any(e => e.Kind == "advice" && e.Data.GetProperty("Cursed").GetBoolean()), "delivered cursed advice retained");
            var assessment = JournalAnalysis.Write(records);
            check(assessment.Contains("prediction mismatch") && assessment.Contains("historical-kin: forecast unresolved"), "only comparable predictions assessed");
            check(!records.Any(e => e.Kind == "observed-action"), "transition never invents actions");
            var summaryJournal = new RunJournal(directory);
            for (var i = 0; i < 1000; i++)
            {
                var before = summaryJournal.State(state with { Turn = i, Hp = 80, Phase = "enemy" })!.Value;
                summaryJournal.Advice(new("summary-" + i, before, "associated advice " + i + new string('x', 1000), false, [], null));
                var after = summaryJournal.State(state with { Turn = i, Hp = 79 - i % 10, Phase = "player", Combat = i == 999 ? "new-combat" : "combat-1" })!.Value;
                summaryJournal.Transition(new(before, after, "observed"));
            }
            var boundedSummary = JournalAnalysis.Write(RunJournal.Read(summaryJournal.FilePath));
            check(boundedSummary.Split('\n').Count(l => l.StartsWith("• A")) == 8 && boundedSummary.Length < 8000, "thousand-transition review bounded to eight drops and short advice");
            check(boundedSummary.Contains("1000 linked transitions") && boundedSummary.Contains("associated advice 999") && boundedSummary.Contains("LAST RECORDED STATE"), "summary totals and latest dangerous endpoint retain associated advice");
            check(boundedSummary.Contains("Boundary/gap: not forecast-comparable") && boundedSummary.Contains("not proof of which action caused them"), "boundary and causality limitations explicit");
            File.AppendAllText(journal.FilePath, "{partial");
            var resumed = new RunJournal(directory, journal.RunId);
            resumed.State(state);
            var resumedRecords = RunJournal.Read(journal.FilePath);
            check(resumedRecords.Last().Sequence > records.Last().Sequence && resumed.SessionId != journal.SessionId, "resume preserves run with new session and increasing sequence");
            check(new RunJournal(directory).RunId != journal.RunId, "new run has unique identity");
            var bounded = new RunJournal(directory, maxBytes: 1024);
            for (var i = 0; i < 30; i++) bounded.State(state with { Turn = i });
            check(bounded.Truncated && new FileInfo(bounded.FilePath).Length <= 1024, "journal byte cap bounds recording");
            var blocked = Path.Combine(directory, "not-a-directory");
            File.WriteAllText(blocked, "test");
            var failed = new RunJournal(blocked);
            check(failed.Failure != null && failed.State(state) == null, "disk failure isolated from gameplay");
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
