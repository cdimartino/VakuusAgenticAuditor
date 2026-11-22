namespace Auditor;

internal record RouteStop(string Kind, RouteStop[] Next, int Row = -1, int Col = -1)
{
    internal string Coordinate => $"{Row}:{Col}";
}
internal record RouteState(int Hp, int MaxHp, int Gold, int Potions, bool Ready);
internal record RouteHint(int Index, string Text)
{
    internal RouteStop[] Path { get; init; } = [];
    internal string Identity => string.Join("/", Path.Select(n => n.Coordinate));
}

// Scores are preferences, not predictions of damage, event contents, loot or purchases.
// Only visible room types and current player resources are inputs. A camp makes
// later risk less unattractive, but does not manufacture HP or assume a rest choice.
internal static class RouteCoach
{
    record Context(int Fights = 0, int Elites = 0, int Camps = 0, int Shops = 0);
    record Plan(double Value, RouteStop[] Stops);

    internal static RouteHint? Choose(RouteStop[] starts, RouteState state, bool manual = false)
    {
        if (starts.Length == 0) return null;
        var memo = new Dictionary<(RouteStop, Context), Plan>();
        var visiting = new HashSet<RouteStop>(ReferenceEqualityComparer.Instance);
        double health = Math.Clamp((double)state.Hp / Math.Max(1, state.MaxHp), 0, 1);
        double Value(RouteStop n, Context c) => n.Kind switch
        {
            "RestSite" => (health < .5 ? 14 : health < .75 ? 9 : 4) / (1 + c.Camps * .7)
                + Math.Min(5, c.Fights + c.Elites * 2),
            "Shop" => (state.Gold >= 150 ? 12 : state.Gold >= 75 ? 6 : 0) / (1 + c.Shops * 2),
            "Elite" => (health > .7 && (state.Ready || state.Potions > 0) ? 8 : health < .5 ? -14 : -4)
                - c.Elites * (state.Ready ? 3 : 6) - Math.Max(0, c.Fights - c.Camps * 2) * 1.5,
            "Monster" => (health < .4 ? -5 : state.Ready ? 1 : 3)
                - Math.Max(0, c.Fights - c.Camps * 2) * (health < .65 ? 1.5 : .4),
            "Treasure" => 7,
            "Unknown" => 0, // Neither a safe room nor a known reward.
            _ => 0
        };
        Context After(RouteStop n, Context c) => n.Kind switch
        {
            "Monster" => c with { Fights = c.Fights + 1 },
            "Elite" => c with { Elites = c.Elites + 1 },
            "RestSite" => c with { Camps = c.Camps + 1 },
            "Shop" => c with { Shops = c.Shops + 1 },
            _ => c
        };
        Plan Best(RouteStop n, Context c)
        {
            if (memo.TryGetValue((n, c), out var cached)) return cached;
            if (!visiting.Add(n)) throw new ArgumentException("Route graph must be acyclic.", nameof(starts));
            var next = n.Next.OrderBy(x => x.Col).ThenBy(x => x.Row)
                .Select(x => Best(x, After(n, c))).OrderByDescending(x => x.Value).FirstOrDefault();
            visiting.Remove(n);
            return memo[(n, c)] = new(Value(n, c) + (next?.Value ?? 0), [n, .. next?.Stops ?? []]);
        }
        var ranked = starts.Select((n, i) => (Plan: Best(n, new()), Index: i))
            .OrderByDescending(x => x.Plan.Value).ThenBy(x => starts[x.Index].Col)
            .ThenBy(x => starts[x.Index].Row).ThenBy(x => x.Index).ToArray();
        var winner = ranked[0];
        string Label(int i) => starts.Length == 1 ? "Only route" : i == 0 ? "Left" : i == starts.Length - 1 ? "Right" : starts.Length == 3 ? "Middle" : $"#{i + 1} from left";
        string Room(RouteStop n) => n.Kind switch { "RestSite" => "camp", "Shop" => "shop", "Monster" => "fight", "Unknown" => "?", _ => n.Kind.ToLowerInvariant() };
        var path = winner.Plan.Stops;
        var reasons = new List<string>();
        if (path.Any(n => n.Kind == "RestSite")) reasons.Add(health < .65 ? "camp access for healing or upgrades" : "camp access for upgrades or healing");
        if (path.Any(n => n.Kind == "Shop")) reasons.Add($"shop access with {state.Gold}g now; purchases not assumed");
        if (path.Any(n => n.Kind == "Elite")) reasons.Add("elite reward potential at combat risk");
        if (path.Any(n => n.Kind == "Monster")) reasons.Add("fight rewards at combat risk");
        if (path.Any(n => n.Kind == "Unknown")) reasons.Add("? outcomes remain unknown");
        var tied = ranked.Length > 1 && Math.Abs(winner.Plan.Value - ranked[1].Plan.Value) < .000001;
        int position = starts.Select((n, i) => (n, i)).OrderBy(x => x.n.Col).ThenBy(x => x.n.Row)
            .Select(x => x.i).ToList().IndexOf(winner.Index);
        var text = $"{Label(position)}: {string.Join(" → ", path.Select(Room))}. "
            + (tied ? "Visible routes tie on preference; coordinate order breaks the tie. " : "")
            + string.Join("; ", reasons) + (reasons.Count > 0 ? ". " : "")
            + "Whole visible route; reassess as health, gold and readiness change.";
        return new(winner.Index, text) { Path = path };
    }
}

// Own one instance per run/act. Update even on forced steps so the remaining
// overlay path stays current. Reset at a new act; coordinates are act-local.
internal sealed class RouteAnnouncements
{
    RouteState? previousState;
    string? previousGraph;
    string? previousPath;
    internal void Reset() { previousState = null; previousGraph = null; previousPath = null; }
    internal RouteHint? Update(RouteStop[] starts, RouteState state, bool manual = false)
    {
        var hint = RouteCoach.Choose(starts, state, manual);
        if (hint == null) return null;
        var seen = new HashSet<RouteStop>(ReferenceEqualityComparer.Instance);
        var edges = new List<string>();
        void Visit(RouteStop n)
        {
            if (!seen.Add(n)) return;
            edges.Add($"{n.Coordinate}:{n.Kind}>{string.Join(",", n.Next.Select(x => x.Coordinate).Order())}");
            foreach (var child in n.Next) Visit(child);
        }
        foreach (var start in starts) Visit(start);
        var graph = string.Join("|", edges.Order());
        bool changed = previousState != state || previousGraph != graph || previousPath != hint.Identity;
        bool first = previousPath == null;
        previousState = state; previousGraph = graph; previousPath = hint.Identity;
        return manual || changed ? hint with { Text = (first ? "Act route: " : "Route reassessed: ") + hint.Text } : null;
    }
}
