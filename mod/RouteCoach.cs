namespace Auditor;

internal record RouteStop(string Kind, RouteStop[] Next);
internal record RouteState(int Hp, int MaxHp, int Gold, int Potions, bool Ready);
internal record RouteHint(int Index, string Text);
internal static class RouteCoach
{
    record Leg(string Room, string Benefit, string Cost, double Value);
    record Path(double Value, List<Leg> Legs);
    static Path Best(RouteStop node, RouteState s)
    {
        double health = (double)s.Hp / s.MaxHp;
        var leg = node.Kind switch
        {
            "RestSite" => new Leg("camp", health < .65 ? "heal at camp" : "upgrade at camp", "fewer fight rewards", health < .5 ? 10 : health < .75 ? 7 : 4),
            "Shop" => new("shop", $"spend {s.Gold}g at the shop", "spends your gold", s.Gold >= 150 ? 9 : s.Gold >= 75 ? 5 : 0),
            "Elite" => new("elite", "earn an elite relic", "risk elite damage", health > .7 && (s.Ready || s.Potions > 0) ? 7 : health < .5 ? -10 : -3),
            "Monster" => new("fight", "earn a card reward", "take a regular fight", health < .4 ? -4 : s.Ready ? 1 : 3),
            "Treasure" => new("chest", "reach a chest", "", 8),
            "Unknown" => new("?", "avoid a guaranteed fight", "unknown outcome", health < .55 ? 3 : 1.5),
            "Boss" => new("boss", "reach the boss", "boss fight", 0),
            _ => new(node.Kind.ToLowerInvariant(), "", "", 0)
        };
        // Conservative planning assumptions, never presented as known outcomes.
        var after = node.Kind switch
        {
            "RestSite" when health < .65 => s with { Hp = Math.Min(s.MaxHp, s.Hp + s.MaxHp / 3) },
            "Elite" => s with { Hp = Math.Max(1, s.Hp - (s.Ready ? 10 : 16)), Potions = Math.Max(0, s.Potions - 1) },
            "Monster" => s with { Hp = Math.Max(1, s.Hp - 5) },
            "Shop" => s with { Gold = Math.Max(0, s.Gold - 100) },
            _ => s
        };
        var next = node.Next.Select(n => Best(n, after)).OrderByDescending(n => n.Value).FirstOrDefault();
        return new(leg.Value + .78 * (next?.Value ?? 0), [leg, .. next?.Legs ?? []]);
    }
    internal static RouteHint? Choose(RouteStop[] starts, RouteState state)
    {
        if (starts.Length < 2) return null;
        var ranked = starts.Select((n, i) => (path: Best(n, state), i)).OrderByDescending(x => x.path.Value).ToArray();
        var (best, index) = ranked[0]; var (alt, other) = ranked[1];
        string Label(int i) => i == 0 ? "Left" : i == starts.Length - 1 ? "Right" : starts.Length == 3 ? "Middle" : $"#{i + 1} from left";
        string Trail(Path p) => string.Join(" → ", p.Legs.Take(4).Select(x => x.Room));
        var plus = best.Legs.Where(l => l.Benefit != "").OrderByDescending(l => l.Value).FirstOrDefault()?.Benefit;
        var minus = best.Legs.FirstOrDefault(l => l.Room == "elite")?.Cost ??
            best.Legs.FirstOrDefault(l => l.Room is "fight" or "?")?.Cost;
        var altGain = alt.Legs.Where(l => l.Benefit != "" && !best.Legs.Any(b => b.Room == l.Room)).OrderByDescending(l => l.Value).FirstOrDefault();
        var trade = altGain is { Value: > 0 } ? $"give up the chance to {altGain.Benefit}" : minus ?? best.Legs.FirstOrDefault(l => l.Cost != "")?.Cost;
        if (Math.Abs(best.Value - alt.Value) < .01)
            return new(index, $"{Label(index)} or {Label(other)}: tied over the next {best.Legs.Count} rooms. {Label(index)}: {Trail(best)}.");
        return new(index, $"{Label(index)}: {Trail(best)}. {plus}{(trade == null ? "" : $"; tradeoff: {trade}")}.");
    }
}
