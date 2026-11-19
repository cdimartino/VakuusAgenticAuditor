using Auditor;

internal static class PresetRegression
{
    internal static void Run(Action<bool, string> check)
    {
        var before = new Dictionary<string, string>();
        var applied = new Dictionary<string, string>();
        var state = Defaults();
        var preset = new PlaytestPreset(before, applied);
        preset.Apply(state);
        preset.Apply(state);
        check(before["Mode"] == "Balanced", "reapplying preserves original snapshot");
        check(state["Mode"] == "Detailed", "preset applies Detailed mode");

        // Simulate deserialization/restart: ownership dictionaries survive, helper doesn't.
        var afterRestart = new PlaytestPreset(before, applied);
        var conflicts = afterRestart.Restore(state);
        check(conflicts.Count == 0, "restart restore has no conflict");
        check(state["Mode"] == "Balanced", "restart restores original mode");

        before = new(); applied = new(); state = Defaults();
        new PlaytestPreset(before, applied).Apply(state);
        state["Mode"] = "On demand"; // user edit while preset active
        conflicts = new PlaytestPreset(before, applied).Restore(state);
        check(conflicts.Contains("Mode"), "external edit is reported");
        check(state["Mode"] == "On demand", "restore preserves external edit");
        check(state["Muted"] == "True", "unmodified owned field restores");
        check(applied.Count == 0 && before.Count == 0, "restore clears stale ownership");
    }

    static Dictionary<string, string> Defaults() => new()
    {
        ["Mode"] = "Balanced",
        ["Muted"] = "True",
        ["CurseMode"] = "True",
        ["AllowClosestCatalog"] = "True",
    };
}
