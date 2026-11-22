namespace Auditor;

// Selector matches are evidence of deck fit, not a prediction that a counter is ready.
internal static class RelicGuide
{
    internal sealed record Rule(string Kind, string[] Selectors, string Cue);
    internal sealed record Interaction(string Id, string Title, Card Card, Rule Rule, int Specificity)
    {
        internal bool Tradeoff => Rule.Kind == "tradeoff";
        internal string Text => $"{Title} + {Card.Name}: {Rule.Cue}";
    }
    static readonly Lazy<Dictionary<string, Rule>> rules = new(() =>
    {
        var result = new Dictionary<string, Rule>(StringComparer.Ordinal);
        foreach (var part in new[] { "a-g", "h-p", "q-z" })
        {
            using var r = new StreamReader(CatalogProfile.Open($"26660232/relic-guide-{part}.tsv"));
            while (r.ReadLine() is { } line)
            {
                var f = line.Split('\t');
                result.Add(f[0], new(f[1], f[2] == "-" ? [] : f[2].Split(','), f[3]));
            }
        }
        return result;
    });
    internal static Rule? Get(string id) => Catalog.Profile.Build == "26660232" ? rules.Value.GetValueOrDefault(id) : null;
    internal static string Name(string id) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ').ToLowerInvariant());
    static int Evidence(Card c, string s, bool immediate)
    {
        if (c.ForeignModel || c.IsCurse || !c.NativePlayableType && !c.Known) return 0;
        if (s.StartsWith("ID:", StringComparison.Ordinal)) return s.AsSpan(3).SequenceEqual(c.Id) ? 5 : 0;
        return s switch
        {
            "attack" => c.Attack ? 1 : 0,
            "skill" => c.Skill ? 1 : 0,
            "power" => c.Power ? 1 : 0,
            "cheap-attack" => c.Attack && !c.VariableCost && c.Cost is 0 or 1 ? 2 : 0,
            "expensive" => !c.VariableCost && c.Cost >= 2 ? 1 : 0,
            "zero-cost" => !c.VariableCost && c.Cost == 0 ? 2 : 0,
            "multi-hit" => c.Attack && c.CombatHas("multiple hits") ? 4 : 0,
            "basic-strike" => c.Id.StartsWith("STRIKE_", StringComparison.Ordinal) ? 3 : 0,
            "basic-defend" => c.Id.StartsWith("DEFEND_", StringComparison.Ordinal) ? 3 : 0,
            _ => (immediate ? c.CombatHas(s) : c.Has(s)) ? 3 : 0
        };
    }
    internal static Interaction? Match(string id, string title, Card c, bool immediate = false)
    {
        var r = Get(id);
        if (r == null || r.Selectors.Length == 0) return null;
        int evidence = r.Selectors.Max(s => Evidence(c, s, immediate || id == "FIDDLE"));
        return evidence == 0 ? null : new(id, title, c, r, evidence);
    }
    internal static Interaction? Best(IEnumerable<Interaction?> candidates) => candidates.OfType<Interaction>()
        .OrderByDescending(x => x.Specificity).ThenByDescending(x => x.Tradeoff).FirstOrDefault();
    // Only selector payoffs whose advertised draw is on card play.
    internal static bool BlockedDraw(string id, string[] owned) =>
        (id is "GAME_PIECE" or "INK_BOTTLE" or "IRON_CLUB") && owned.Contains("FIDDLE");
    internal static Interaction? ForRelic(string id, string name, Deck deck) =>
        BlockedDraw(id, deck.Relics) ? null : Best(deck.Cards.Select(c => Match(id, name, c)));
    internal static IEnumerable<Interaction> ForCard(Card c, string[] ids, IReadOnlyDictionary<string, string>? names = null, bool immediate = false) => ids.Distinct().Where(id => !BlockedDraw(id, ids))
        .Select(id => Match(id, names?.GetValueOrDefault(id) ?? Name(id), c, immediate)).OfType<Interaction>();
}
