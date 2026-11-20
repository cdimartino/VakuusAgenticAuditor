namespace Auditor;

/// <summary>Reversible field ownership, persisted by Preferences as strings.</summary>
internal sealed class PlaytestPreset(Dictionary<string, string> before, Dictionary<string, string> applied)
{
    internal bool Active => applied.Count != 0;

    internal List<string> Apply(Dictionary<string, string> state)
    {
        if (Active) return new(); // Applying twice never replaces the original snapshot.
        ApplyField(state, "Mode", "Detailed");
        ApplyField(state, "Muted", "False");
        ApplyField(state, "CurseMode", "False");
        ApplyField(state, "AllowClosestCatalog", "False");
        return new();
    }

    internal List<string> Restore(Dictionary<string, string> state)
    {
        var conflicts = new List<string>();
        foreach (var (name, ownedValue) in applied)
        {
            if (!state.TryGetValue(name, out var current) || current != ownedValue)
            {
                conflicts.Add(name);
                continue;
            }
            if (before.TryGetValue(name, out var original)) state[name] = original;
        }
        // Clear ownership, including conflicts: a later restore must not clobber
        // the user's newer value.
        applied.Clear(); before.Clear();
        return conflicts;
    }

    void ApplyField(Dictionary<string, string> state, string name, string value)
    {
        if (!state.TryGetValue(name, out var current)) return;
        before[name] = current;
        applied[name] = value;
        state[name] = value;
    }
}
