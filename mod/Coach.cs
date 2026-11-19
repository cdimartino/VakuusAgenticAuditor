namespace Auditor;

internal record Card(string Id, string Name, int Cost, bool Up, bool Attack, bool Known, string[] Gives, string[] Needs)
{
    internal bool Has(string role) => Gives.Contains(role);
}
internal static class Catalog
{
    internal static CatalogProfile Profile { get; set; } = null!;
    static class Tables
    {
        internal static readonly Dictionary<string, string[][]> Roles = Table("roles.tsv").ToDictionary(a => a[0], a => a.Skip(1)
            .Select(s => s.Split(',', StringSplitOptions.RemoveEmptyEntries)).ToArray());
        internal static readonly Dictionary<string, string[]> Relics = Table("relics.tsv").ToDictionary(a => a[0], a => a[1..]);
        internal static readonly Dictionary<string, string> Bosses = Table("bosses.tsv").ToDictionary(a => a[0], a => a[1]);
        internal static readonly Dictionary<string, string[]> Potions = Table("potions.tsv").ToDictionary(a => a[0], a => a[1..]);
        internal static readonly Dictionary<string, string> RelicFit = Table("relic-fit.tsv").ToDictionary(a => a[0], a => a[1]);
        internal static readonly Dictionary<string, string> Effects = Table("effects.tsv").ToDictionary(a => a[0], a => a[1]);
    }
    internal static Dictionary<string, string[]> Relics => Tables.Relics;
    internal static Dictionary<string, string> Bosses => Tables.Bosses;
    internal static Dictionary<string, string[]> Potions => Tables.Potions;
    internal static Dictionary<string, string> RelicFit => Tables.RelicFit;
    internal static string? Effect(string id) => Tables.Effects.GetValueOrDefault(id);
    static string[][] Table(string name)
    {
        using var reader = new StreamReader(CatalogProfile.Open($"{Profile.Build}/{name}"));
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.TrimEnd('\r').Split('\t')).ToArray();
    }
    internal static Card Get(string id, string name, int cost, bool up, bool attack, bool vanilla = true)
    {
        var roles = Tables.Roles.GetValueOrDefault(id);
        return new(id, name, cost, up, attack, vanilla && roles != null, roles?[0] ?? [], roles?[1] ?? []);
    }
}
internal record Deck(Card[] Cards, int Hp, int MaxHp, int Act, bool EliteOnly, string[] Relics, string Character = "");
internal record Verdict(string Id, string Name, double Score, string Reason, string? Caution);
internal static class Coach
{
    internal static (int score, string reason) Relic(string id, Deck deck)
    {
        if (Catalog.Relics.TryGetValue(id, out var rule))
        {
            var support = deck.Cards.Where(c => c.Has(rule[0])).ToArray();
            if (support.Length > 0) return (5 + Math.Min(4, support.Length), $"{rule[1]} {support[0].Name} fits.");
        }
        bool injured = deck.Hp * 2 < deck.MaxHp;
        return Catalog.RelicFit.GetValueOrDefault(id) switch
        {
            "max hp" => (injured ? 7 : 4, $"More maximum HP gives you room for mistakes at {deck.Hp}/{deck.MaxHp}."),
            "gold" => (4, "Bank the gold for an upgrade to your deck at the next merchant."),
            "sustain" or "rest heal" => (injured ? 8 : 4, $"Repeatable healing matters at {deck.Hp}/{deck.MaxHp} HP."),
            "opening block" => (6, "Opening block buys time to set up your first turn."),
            "energy" or "opening energy" => (deck.Cards.Count(c => c.Cost >= 2) >= 3 ? 8 : 5, "Extra energy makes expensive hands easier to play."),
            "opening draw" => (deck.Cards.Length >= 18 ? 7 : 5, "An extra opening draw helps you find your key cards earlier."),
            "slots" => (4, "An extra potion slot gives you more options at the next elite."),
            "strength" => (deck.Cards.Count(c => c.Attack) >= 6 ? 7 : 4, "Strength adds damage to your attacks."),
            "dexterity" => (deck.Cards.Count(c => c.Has("guard")) >= 4 ? 7 : 4, "Dexterity improves your block cards."),
            _ => (0, "")
        };
    }
    // A score is marginal fit for this deck, not predicted win rate.
    internal static Verdict Assess(Card c, Deck d)
    {
        if (!c.Known) return new(c.Id, c.Name, double.NaN, "I don’t know this card well enough to rank it.", null);
        var pros = new List<(double w, string text)>();
        var cons = new List<(double w, string text)>();
        var cards = d.Cards;
        var opportunity = Archetypes.Opportunity(d, c);
        var guards = cards.Count(x => x.Has("guard"));
        var nonBasicHits = cards.Count(x => x.Attack && !x.Id.StartsWith("STRIKE_"));
        if (c.Cost == 1 && c.Has("hit") && c.Has("guard") && c.Has("draw"))
            pros.Add((2.2, "One energy buys damage, block, and a replacement draw."));
        if (c.Has("guard") && (guards * 3 <= cards.Length || d.Hp * 2 <= d.MaxHp))
            pros.Add((d.Hp * 2 <= d.MaxHp ? 2.6 : 1.5, $"At {d.Hp}/{d.MaxHp} HP, another block draw gives you breathing room."));
        if (c.Has("hit") && nonBasicHits < (d.EliteOnly ? 5 : 3))
            pros.Add((d.EliteOnly ? 2.8 : 1.8, $"You have {nonBasicHits} non-Strike attack{(nonBasicHits == 1 ? "" : "s")}{(d.EliteOnly ? " before a forced elite" : "")}. This adds damage now."));
        if (c.Has("draw") && cards.Length >= 15)
            pros.Add((1.4, $"Draw helps you reach key cards in a {cards.Length}-card deck."));
        if (c.Has("area damage") && !cards.Any(x => x.Has("area damage")))
            pros.Add((1.45, "Your deck has no tagged area attack. This can damage several enemies in one play."));
        if (c.Has("weak") && !cards.Any(x => x.Has("weak")))
            pros.Add((1.15, "Weak gives you a way to reduce incoming attacks."));
        if (c.Has("hp loss") && d.Hp * 3 <= d.MaxHp)
            cons.Add((2.5, $"At {d.Hp} HP, the health cost is a real drawback."));
        if (c.Has("energy") && cards.Count(x => x.Cost >= 2) >= 3)
            pros.Add((1.6, "You have several expensive plays competing for the same turn."));
        foreach (var tag in c.Needs)
        {
            var support = cards.Where(x => x.Has(tag)).ToArray();
            if (support.Length > 0) pros.Add((2 + Math.Min(1.2, support.Length * .35), $"{support[0].Name} already supports its {tag} payoff."));
            else cons.Add((1.8, $"No tagged {tag} enabler in your deck yet."));
        }
        foreach (var tag in c.Gives)
            if (cards.FirstOrDefault(x => x.Needs.Contains(tag)) is { } payoff)
                pros.Add((2.4, $"Its {tag} effect helps {payoff.Name} pay off more often."));
        foreach (var id in d.Relics)
            if (Catalog.Relics.TryGetValue(id, out var rule) && c.Has(rule[0]))
                pros.Add((2.3, $"{System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ').ToLowerInvariant())}: {rule[1]}"));
        if (c.Cost >= 2 && cards.Count(x => x.Cost >= 2) >= 3 && !cards.Any(x => x.Has("energy")))
            cons.Add((1.6, "Several expensive cards already compete for energy."));
        int copies = cards.Count(x => x.Id == c.Id);
        if (copies > 0) cons.Add((Math.Min(2.4, .85 * copies), $"You already have {copies} {(copies == 1 ? "copy" : "copies")}."));
        if (c.Up) pros.Add((.6, "It comes upgraded without spending a campfire."));
        if (cards.Length >= 20 && !c.Has("draw")) cons.Add((.7, $"Adding it dilutes your {cards.Length}-card deck."));
        var best = pros.OrderByDescending(x => x.w).FirstOrDefault().text;
        var risk = cons.OrderByDescending(x => x.w).FirstOrDefault().text;
        return new(c.Id, c.Name, .35 + pros.Sum(x => x.w) - cons.Sum(x => x.w) + (opportunity != null && !opportunity.StartsWith("Possible ") ? 1 : 0),
            opportunity ?? best ?? risk ?? "No strong interaction stands out in this deck.", risk);
    }
    internal static Verdict[] Rank(IEnumerable<Card> choices, Deck deck, bool skip)
    {
        var options = choices.Select(c => Assess(c, deck)).ToList();
        if (skip) options.Add(new("~skip", "Skip", deck.Cards.Length >= 16 ? 1 : .2,
            "Skipping keeps your current cards coming around as often.", null));
        return options.OrderByDescending(x => x.Score).ToArray();
    }
    internal static string After(Verdict pick, Verdict top)
    {
        if (double.IsNaN(pick.Score)) return pick.Reason;
        if (top.Score - pick.Score <= .55) return $"Sound choice. {pick.Reason}";
        return pick.Caution ?? (pick.Id == "~skip" ? $"A leaner deck, though {top.Name} had a case: {top.Reason}" :
            $"{pick.Name} is a tradeoff. {top.Name}: {top.Reason}");
    }
}
