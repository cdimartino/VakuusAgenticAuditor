namespace Auditor;

internal static class RouteRegression
{
    internal static void Run(Action<bool, string> check)
    {
        RouteStop Chain(int count, RouteStop end) => count == 0 ? end : new("Unknown", [Chain(count - 1, end)], count, 0);
        var state = new RouteState(30, 80, 20, 0, false);
        var camp = new RouteStop("RestSite", [], 12, 1);
        var elite = new RouteStop("Elite", [], 12, 0);
        var hint = RouteCoach.Choose([Chain(10, elite), Chain(10, camp)], state)!;
        check(hint.Index == 1 && hint.Path.Length == 11, "route sees camp beyond old lookahead");
        var a = new RouteStop("Unknown", [], 1, 0);
        var b = new RouteStop("Unknown", [], 1, 2);
        check(RouteCoach.Choose([b, a], state)!.Path[0] == a, "route ties use stable coordinates");
        check(RouteCoach.Choose([a, b], state)!.Identity == RouteCoach.Choose([b, a], state)!.Identity, "route identity survives input order");
        var announcements = new RouteAnnouncements();
        check(announcements.Update([a], state) != null, "first full route announced even forced");
        check(announcements.Update([a], state) == null, "unchanged route stays quiet");
        check(announcements.Update([a], state with { Hp = 29 }) != null, "health change reassesses route");
        check(announcements.Update([b], state) != null, "branch change reassesses route");
        check(announcements.Update([], state) == null, "no route during transition");
        check(RouteCoach.Choose([new("Unknown", [])], state)!.Text.Contains("outcomes remain unknown"), "unknown is not advertised as safe");
    }
}
