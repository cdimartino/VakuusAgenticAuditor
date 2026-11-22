namespace Auditor;

internal static class NonupeipeBlessing
{
    // This comparison is complete only for this particular three-relic offer.
    internal static string? Compare(IEnumerable<string> ids, bool explicitPick, bool fiddle = false)
    {
        if (!ids.Order(StringComparer.Ordinal).SequenceEqual(new[] { "BEAUTIFUL_BRACELET", "BRILLIANT_SCARF", "GLITTER" })) return null;
        if (fiddle) return "Fiddle blocks Beautiful Bracelet’s in-turn draw. Brilliant Scarf favors a costly sixth hand play; Glitter gives future card rewards Replay once per combat.";
        return (explicitPick ? "Take Beautiful Bracelet for immediate draw: " : "Beautiful Bracelet helps the deck you already have: ")
            + "four random cards each draw two on first play. Brilliant Scarf pays off if you regularly reach a sixth hand play and save a costly card for it. Glitter favors future picks: each card reward gets Replay once per combat.";
    }
}

// Keep the manual-only evaluator active while its bubble is being read. Match the actual
// enabled option objects, since the same room can present a second page of choices.
internal sealed class EventRequest
{
    object? owner;
    object[] options = [];
    internal bool ForOffer(object screen, object[] enabled, bool pressed, bool visible)
    {
        if (enabled.Length > 0 && (pressed || visible && ReferenceEquals(owner, screen)
            && enabled.SequenceEqual(options, ReferenceEqualityComparer.Instance)))
        {
            if (pressed) { owner = screen; options = enabled; }
            return true;
        }
        Clear();
        return false;
    }
    internal void Clear() { owner = null; options = []; }
}
