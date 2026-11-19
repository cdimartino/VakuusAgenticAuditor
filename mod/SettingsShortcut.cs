namespace Auditor;

// Event edges, not frame-sampled keyboard state. Blocked presses still latch:
// dismissing a modal or rebinding a held key must not replay that press.
internal sealed class SettingsShortcut<T> where T : notnull
{
    readonly HashSet<T> held = new();
    bool controller;
    public bool Key(T key, bool pressed, bool echo, T binding, bool blocked)
    {
        if (!pressed) { held.Remove(key); return false; }
        if (echo || !held.Add(key)) return false;
        return !blocked && EqualityComparer<T>.Default.Equals(key, binding);
    }
    public bool Controller(bool pressed, bool blocked)
    {
        bool edge = pressed && !controller;
        controller = pressed;
        return edge && !blocked;
    }
    public void Reset() { held.Clear(); controller = false; }
}
