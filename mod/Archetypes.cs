namespace Auditor;

internal sealed record BuildLine(string Role, string Name);
internal sealed record LineEvidence(BuildLine Line, Card[] Sources, Card[] Payoffs)
{
    public bool Online => Sources.Any(s => Payoffs.Any(p => p.Id != s.Id));
}
internal static class Archetypes
{
    static readonly Dictionary<string, BuildLine[]> Profiles = new()
    {
        ["IRONCLAD"] = [new("exhaust", "Exhaust engine"), new("strength", "Strength scaling"), new("guard", "Block payoff"), new("vulnerable", "Vulnerable attacks"), new("hp loss", "Self-damage")],
        ["SILENT"] = [new("poison", "Poison"), new("shivs", "Shiv engine"), new("discard", "Discard engine"), new("weak", "Weak payoff")],
        ["DEFECT"] = [new("orbs", "Orb engine"), new("frost", "Frost defense"), new("lightning", "Lightning scaling")],
        ["REGENT"] = [new("forge", "Sovereign Blade"), new("stars", "Star engine")],
        ["NECROBINDER"] = [new("summon", "Osty scaling"), new("doom", "Doom"), new("souls", "Soul engine")],
        ["FOOL"] = [new("sadness", "Sadness conversion"), new("shots", "Arrow engine"), new("impale", "Impale payoff"), new("aim", "Aim scaling")],
    };
    static bool Payoff(Card c, string role) => c.Needs.Contains(role) || (role == "sadness" && Catalog.Effect(c.Id) == "convert-sadness");
    internal static LineEvidence[] Analyze(Deck d) => Profiles.GetValueOrDefault(d.Character, []).Select(line =>
        new LineEvidence(line, d.Cards.Where(c => c.Known && c.Has(line.Role)).ToArray(), d.Cards.Where(c => c.Known && Payoff(c, line.Role)).ToArray()))
        .Where(x => x.Sources.Length + x.Payoffs.Length > 0)
        .OrderByDescending(x => x.Online).ThenByDescending(x => x.Sources.Length + x.Payoffs.Length).ToArray();
    internal static string? Opportunity(Deck d, Card offered)
    {
        if (!offered.Known) return null;
        foreach (var x in Analyze(d))
        {
            var source = x.Sources.FirstOrDefault(c => c.Id != offered.Id);
            var payoff = x.Payoffs.FirstOrDefault(c => c.Id != offered.Id);
            if (Payoff(offered, x.Line.Role) && source != null)
                return $"{x.Line.Name}: {source.Name} supplies the {x.Line.Role} that {offered.Name} rewards.";
            if (offered.Has(x.Line.Role) && payoff != null)
                return $"{x.Line.Name}: {offered.Name} gives {payoff.Name} a{(x.Online ? "nother" : "n")} enabler.";
            if (source != null && offered.Has(x.Line.Role) && x.Payoffs.Length == 0 && x.Line.Role != "guard")
                return $"Possible {x.Line.Name} line: {source.Name} and {offered.Name} both supply {x.Line.Role}. Look for a payoff before adding more.";
        }
        return null;
    }
}
