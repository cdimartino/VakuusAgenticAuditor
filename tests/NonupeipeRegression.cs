using Auditor;

internal static class NonupeipeRegression
{
    internal static void Run(Action<bool, string> check)
    {
        string[] ids = ["GLITTER", "BEAUTIFUL_BRACELET", "BRILLIANT_SCARF"];
        string? advice = NonupeipeBlessing.Compare(ids, true);
        check(advice != null && advice.Contains("sixth hand play") && advice.Contains("random") && advice.Contains("Replay once per combat"), "Nonupeipe pictured trio compares all three triggers");
        check(NonupeipeBlessing.Compare(ids.Reverse(), false) is string indirect && !indirect.StartsWith("Take "), "blessing order and explicit pick preference");
        check(NonupeipeBlessing.Compare(ids.Take(2), true) == null && NonupeipeBlessing.Compare([.. ids, "OTHER"], true) == null, "different blessing offer is not ranked as the pictured trio");
        var request = new EventRequest();
        object room = new(), a = new(), b = new();
        check(!request.ForOffer(room, [a, b], false, true), "ordinary event bubble does not enable manual fallback");
        check(request.ForOffer(room, [a, b], true, false) && request.ForOffer(room, [a, b], false, true), "requested fallback survives next ordinary tick");
        check(!request.ForOffer(room, [a, new object()], false, true) && !request.ForOffer(room, [a, b], false, true), "changed option in same room invalidates even if original offer returns");
        request.ForOffer(room, [a, b], true, false);
        check(!request.ForOffer(room, [], false, true), "committed choice invalidates request");
        request.ForOffer(room, [a, b], true, false);
        check(!request.ForOffer(new object(), [a, b], false, true), "new room invalidates request");
        request.ForOffer(room, [a, b], true, false);
        check(!request.ForOffer(room, [a, b], false, false), "hidden or expired bubble cannot resurrect the request");
    }
}
