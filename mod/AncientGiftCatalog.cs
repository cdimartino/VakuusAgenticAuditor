namespace Auditor;

internal static class AncientGiftCatalog
{
    internal sealed record Offer(string Id, string Title);
    sealed record Gift(string Fit, string Cue);
    static readonly Lazy<Dictionary<(string Owner, string Id), Gift>> Gifts = new(() =>
    {
        using var reader = new StreamReader(CatalogProfile.Open("26660232/ancient-gifts.tsv"));
        var entries = new Dictionary<(string, string), Gift>();
        while (reader.ReadLine() is { } line)
        {
            var row = line.Split('\t');
            entries.Add((row[0], row[1]), new(row[2], row[3]));
        }
        return entries;
    });

    internal static bool HasPool(string owner) => Gifts.Value.Keys.Any(k => k.Owner == owner);

    // These are sequential mode actions, not alternatives to the normal relic roll.
    internal static string? Opening(string owner, string key) => owner == "NEOW" ? key switch
    {
        "ALL_STAR" => "Check which five colorless cards joined your starters before choosing more damage or block.",
        "DRAFT" => "Your ten draft picks replace the starters. Secure both damage and block early.",
        "INSANITY" => "Inspect the random 30-card deck before committing to a synergy.",
        "SEALED_DECK" => "Choose ten cards that work together; they replace the starter deck.",
        "SPECIALIZED" => "Build around the five copies, then cover what that card cannot do.",
        "BLITZ" => "Choose three additions that complement the deck you have.",
        _ => null
    } : null;

    // Contextual fit is a tie-breaker for presentation, not a win-rate estimate.
    static bool Fits(string fit, Deck deck, int upgrades, int gold, int slots, int empty) => fit switch
    {
        "basic" => deck.Cards.Count(c => c.Id.StartsWith("STRIKE_") || c.Id.StartsWith("DEFEND_")) >= 3,
        "starterpair" => deck.Cards.Any(c => c.Id.StartsWith("STRIKE_") && !c.Up) && deck.Cards.Any(c => c.Id.StartsWith("DEFEND_") && !c.Up),
        "strikes" => deck.Cards.Count(c => c.Id.StartsWith("STRIKE_")) >= 3,
        "defends" => deck.Cards.Count(c => c.Id.StartsWith("DEFEND_")) >= 3,
        "energy" or "expensive" => deck.Cards.Count(c => c.Cost >= 2) >= 4,
        "cheap" => deck.Cards.Count(c => c.Cost == 0) >= 3,
        "elite" => deck.EliteOnly,
        "upgrade" => upgrades >= 3,
        "large" => deck.Cards.Length >= 20,
        "hurt" => deck.Hp * 2 <= deck.MaxHp,
        "poor" => gold < 75,
        "fullbelt" => slots > 0 && empty == 0,
        "emptybelt" => empty > 0,
        "attacks" => deck.Cards.Count(c => c.Attack) >= 6,
        "guard" or "discard" or "exhaust" => deck.Cards.Count(c => c.Has(fit)) >= 2,
        _ => false
    };

    internal static string? Compare(string owner, Offer[] options, Deck deck, int upgrades, int gold, int slots, int empty, bool pick, string? tomeTitle = null)
    {
        if (options.Length == 0 || !HasPool(owner)) return null;
        var known = options.Select(o => (o, g: Gifts.Value.GetValueOrDefault((owner, o.Id))))
            .Where(x => x.g != null).Select(x => (x.o, g: x.g!, fits: Fits(x.g!.Fit, deck, upgrades, gold, slots, empty)))
            .OrderByDescending(x => x.fits).ToArray();
        if (known.Length == 0) return null;
        bool partial = known.Length != options.Length;
        bool lean = pick && !partial && known.Count(x => x.fits) == 1;
        string Cue(Offer o, Gift g) => o.Id == "DUSTY_TOME" && tomeTitle != null
            ? $"Adds {tomeTitle.TrimEnd('+')}+; weigh it against your current deck."
            : g.Cue;
        var lines = known.Select((x, i) => $"{(i == 0 && lean ? "Consider " : "")}{x.o.Title}: {Cue(x.o, x.g)}");
        return string.Join("\n", partial ? lines.Append("One or more other gifts aren’t assessed.") : lines);
    }
}
