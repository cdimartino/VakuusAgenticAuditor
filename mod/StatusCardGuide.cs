namespace Auditor;

internal static class StatusCardGuide
{
    internal sealed record Rule(string Trigger, int? Reward, string Cue);
    static readonly Lazy<Dictionary<string, Rule>> rules = new(() =>
    {
        using var reader = new StreamReader(CatalogProfile.Open("26660232/status-cards.tsv"));
        return reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(line => line.TrimEnd('\r').Split('\t')).ToDictionary(a => a[0],
                a => new Rule(a[2], a[3] == "-" ? null : int.Parse(a[3]), a[4]));
    });
    internal static Rule? Lookup(Card c) => c.IsStatus && !c.ForeignModel && Catalog.Profile is { Build: "26660232", Exact: true }
        ? rules.Value.GetValueOrDefault(c.Id) : null;

    internal static string? Choices(Card[] offered)
    {
        if (offered.Length is < 3 or > 5 || offered.Select(c => c.Id).Distinct().Count() != offered.Length) return null;
        var effects = new List<string>();
        foreach (var card in offered)
        {
            if (Lookup(card) is not { Trigger: "chosen" } rule) return null;
            effects.Add($"{card.Name}: {rule.Cue}");
        }
        return string.Join("\n", effects);
    }

    // Hand observations only. Never treat a generated or undrawn status as damage.
    internal static (string? Cue, bool UncertainRisk) Hand(IEnumerable<(Card Card, bool CanPlay)> observed, int disintegration)
    {
        string? best = null;
        int priority = -1;
        bool risk = false, unknown = false;
        foreach (var (card, canPlay) in observed)
        {
            if (!card.IsStatus) continue;
            var rule = Lookup(card);
            if (rule is null || rule.Trigger == "mock") { unknown = true; continue; }
            if (rule.Trigger == "chosen") continue; // No passive hand trigger.
            int rank = rule.Trigger switch { "end-hp" => 5, "end-damage" => 4, "draw" => 3, "play" => 2, _ => 1 };
            risk |= rank >= 4;
            if (rank <= priority) continue;
            priority = rank;
            best = $"{card.Name}: {(canPlay ? "playable now" : "not playable now")}. {rule.Cue}";
        }
        if (disintegration > 0)
            return ($"Disintegration deals {disintegration} blockable damage at turn end." + (priority >= 4 ? " " + best : ""), true);
        return (unknown && !risk && best != null ? "An unassessed Status is also in hand. " + best : best, risk || unknown);
    }

    internal static string? GeneratorCue(Card c, Deck deck) => GeneratorCue(c, deck.Cards);
    internal static string? GeneratorCue(Card c, Card[] cards)
    {
        if (c.ForeignModel || Catalog.Profile is not { Build: "26660232", Exact: true }) return null;
        string? effect = c.Id switch
        {
            "JEST" => "Jest adds one retained Sadness after blocking.",
            "MOURNING_BLOW" => "Mourning Blow becomes Sadness and normally returns to hand.",
            "SORROWFUL_STRIKE" => "Sorrowful Strike adds two retained Sadness after attacking.",
            _ => null
        };
        if (effect == null) return null;
        var payoff = cards.FirstOrDefault(x => !x.ForeignModel && x.Id is ("PUNCH_OF_JOY" or "ECSTASY" or "GROWTH" or "COMPACT" or "FLAK_CANNON"));
        if (payoff != null) return effect + (payoff.Id is "PUNCH_OF_JOY" or "ECSTASY"
            ? $" {payoff.Name} can turn it into Joy." : $" {payoff.Name} can clear it.");
        if (cards.FirstOrDefault(x => !x.ForeignModel && x.Id == "ALL_IS_WELL") is { } block)
            return effect + $" With {block.Name} active, creation grants Block.";
        if (cards.FirstOrDefault(x => !x.ForeignModel && x.Id == "ANGUISH") is { } hit)
            return effect + $" {hit.Name} benefits from Sadness created this combat.";
        return effect + " Without a way to clear it, Sadness crowds later hands.";
    }
}
