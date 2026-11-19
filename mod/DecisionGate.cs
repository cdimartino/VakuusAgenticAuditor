namespace Auditor;

// Use the identity of the actual screen, not the room type: two consecutive
// card offers in one room are still separate decisions.
internal sealed class DecisionGate
{
    object? owner, finished;
    string? content;
    ulong since;
    object? reactionScreen, combat;
    bool reactionShown;
    string? round;
    readonly HashSet<string> spoken = new();
    internal void NewReaction() { reactionScreen = null; reactionShown = false; }
    internal bool MayDeliver(object screen) => !reactionShown;
    internal void Delivered(object screen) { reactionScreen = screen; reactionShown = true; }
    internal bool LeftReactionScreen(object? screen) => reactionShown && !ReferenceEquals(reactionScreen, screen);
    internal bool FreshTurnHint(object? encounter, string turn, string message)
    {
        if (!ReferenceEquals(combat, encounter) || round != turn) { combat = encounter; round = turn; spoken.Clear(); }
        return !spoken.Contains(message);
    }
    internal void MarkTurnHint(string message) => spoken.Add(message);
    internal void Finish(object screen) { finished = screen; owner = null; content = null; }
    internal void Reopen(object screen) { if (ReferenceEquals(finished, screen)) finished = null; }
    internal void Transition() { owner = null; content = null; }
    internal bool Ready(object? screen, string? text, ulong now, bool manual)
    {
        if (screen == null || text == null || ReferenceEquals(finished, screen)) { Transition(); return false; }
        if (!ReferenceEquals(screen, owner) || text != content) { owner = screen; content = text; since = now; }
        return manual || now - since >= 300;
    }
}
