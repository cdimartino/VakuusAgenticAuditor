using System.Text.Json;
using Auditor;

internal static class ExportRegression
{
    internal static void Run(Action<bool, string> check)
    {
        var dir = Path.Combine(Path.GetTempPath(), "auditor-export-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(dir, "state.json");
        try
        {
            var export = new PlaytestExport(path);
            export.Observe(new { hand = "one", energy = 3 });
            var revision = export.Revision;
            export.Delivered("Actual delivered advice", [new { action = "play_card", cardRef = "one" }]);
            export.Write(true, 2, "run", "room", 1, true, false, new { guidance = "Detailed" }, []);
            check(JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("delivered").GetProperty("postCurse").GetBoolean(), "export identifies final delivered advice");
            export.Observe(new { hand = "one", energy = 3 });
            check(export.Revision == revision && !export.Changed, "same state doesn't retire a valid decision");
            export.Observe(new { hand = "two", energy = 2 });
            export.Write(true, 2, "run", "room", 1, true, false, new { }, []);
            check(export.Revision != revision && JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty("delivered").ValueKind == JsonValueKind.Null, "changed hand retires the old decision before fresh delivery");
            export.Write(false, 2, "run", "room", 1, true, false, new { }, []);
            check(!File.Exists(path), "disabling local bridge removes stale actionable snapshot");
        }
        finally { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
    }
}
