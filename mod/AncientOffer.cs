namespace Auditor;

// Pure advice only. Counts and card models must come from the current runtime,
// never from name matching, a hypothetical upgrade, or executing an event callback.
internal static class AncientOffer
{
    internal sealed record Context(
        int? Removable = null, int? Transformable = null, int? Upgradeable = null,
        Card? TomeCard = null, Card[]? RemovalTargets = null, Card[]? TransformTargets = null);

    internal sealed record Evaluation(string Id, double Score, string Reason, bool Limited);

    internal static Evaluation? Assess(string id, Deck deck, Context? context = null)
    {
        context ??= new();
        var cards = deck.Cards;
        int basics = cards.Count(Basic);
        switch (id)
        {
            case "EMPTY_CAGE":
                int cuts = Math.Min(2, Math.Max(0, context.Removable ?? 2));
                var targets = context.RemovalTargets;
                int weak = targets == null ? basics : targets.Count(c => Basic(c) || c.IsCurse);
                return new(id, cuts == 0 ? -3 : Math.Min(cuts, weak) * 2.1 + (cards.Length >= 18 ? .5 : 0),
                    cuts == 0 ? "No legal removals remain."
                    : cuts < 2 ? $"Can only remove {cuts}; trim a weak draw so useful cards recur sooner."
                    : weak > 0 ? "Remove 2 weak draws so your useful cards recur sooner."
                    : "Remove 2 cards, but a lean deck without obvious weak draws gains less.",
                    context.Removable == null || targets == null);
            case "ASTROLABE":
                int transforms = Math.Min(3, Math.Max(0, context.Transformable ?? 3));
                int expendable = context.TransformTargets?.Count(Basic) ?? basics;
                return new(id, transforms == 0 ? -3 : Math.Min(transforms, expendable) * 1.25,
                    transforms == 0 ? "No legal transforms remain."
                    : $"{(transforms < 3 ? $"Can only transform {transforms}" : "Transform 3")} and upgrade the results; random replacements can miss your plan.",
                    true);
            case "DUSTY_TOME":
                if (context.TomeCard is not { } tome)
                    return new(id, double.NaN, "Dusty Tome adds an upgraded Ancient card; its offered card must be read before comparing deck fit.", true);
                if (tome.Id == "LOST_LUTE")
                    return new(id, double.NaN, "Lost Lute+: Melody triggers play the top draw-pile card; Melody frequency and autoplay value are not modeled.", true);
                var fit = Coach.Assess(tome, deck);
                return new(id, fit.Score, $"{tome.Name}: {fit.Reason}",
                    !tome.Known || !tome.Up);
            case "BLACK_STAR":
                return new(id, deck.EliteOnly ? 3 : 1, "One extra relic per elite; no immediate combat help, so only pays off if you can safely fight elites.", true);
            case "CALLING_BELL":
                return new(id, double.NaN, "Gain 3 random relics and Curse of the Bell; permanent deck clutter makes the unknown relic rolls a tradeoff.", true);
            case "PANDORAS_BOX":
                int replace = context.RemovalTargets?.Count(Basic) ?? basics;
                return new(id, replace * .65, $"Randomly replace removable basic Strikes and Defends ({replace} apparent targets); more basics give more upside, but replacements can leave defense gaps.", true);
            case "RUNIC_PYRAMID":
                return new(id, cards.Any(c => c.Has("discard")) ? 4 : 2, "Keep your hand between turns to line up key plays; expensive cards and junk can clog it without discard.", true);
            case "SNECKO_EYE":
                int expensive = cards.Count(c => c.Cost >= 2);
                int cheap = cards.Count(c => c.Cost is 0 or 1);
                return new(id, 2 + expensive * .4 - cheap * .15, $"Draw 2 extra each turn with randomized costs; {expensive} expensive versus {cheap} cheap cards makes cost variance a real tradeoff.", true);
            case "ECTOPLASM":
                return new(id, EnergyFit(cards), "+1 energy each turn, but no more gold; immediate tempo costs future merchant flexibility.", true);
            case "SOZU":
                return new(id, EnergyFit(cards), "+1 energy each turn, but no new potions; lose an important emergency resource.", true);
            case "PHILOSOPHERS_STONE":
                return new(id, EnergyFit(cards) - (deck.Hp * 2 < deck.MaxHp ? 2 : 0), "+1 energy each turn, but enemies gain 1 Strength; multi-hit attacks make the downside sharper.", true);
            case "VELVET_CHOKER":
                bool chains = cards.Count(c => c.Cost == 0 || c.Has("draw") || c.Has("shivs")) >= 4;
                return new(id, EnergyFit(cards) - (chains ? 4 : 0), "+1 energy each turn, capped at 6 card plays; cheap cards, draw chains and autoplay can hit that ceiling.", true);
            default:
                return null;
        }
    }

    static double EnergyFit(Card[] cards) => cards.Count(c => c.Cost >= 2) >= 3 ? 4 : 2;

    static bool Basic(Card c) => c.Id.StartsWith("STRIKE_", StringComparison.Ordinal)
        || c.Id.StartsWith("DEFEND_", StringComparison.Ordinal);

    internal static string? Compare(IEnumerable<string> ids, Deck deck, bool explicitPick, Context? context = null)
    {
        var offered = ids.Distinct(StringComparer.Ordinal).ToArray();
        var known = offered.Select(id => Assess(id, deck, context)).Where(x => x != null).Cast<Evaluation>().ToArray();
        if (known.Length == 0) return null;
        var best = known.Where(x => double.IsFinite(x.Score)).OrderByDescending(x => x.Score).FirstOrDefault();
        if (offered.Length == 3 && offered.Order(StringComparer.Ordinal).SequenceEqual(new[] { "ASTROLABE", "DUSTY_TOME", "EMPTY_CAGE" }))
        {
            int? cuts = context?.Removable;
            int weak = (context?.RemovalTargets ?? deck.Cards).Count(c => Basic(c) || c.IsCurse);
            string cage = cuts == 0 ? "Cage has no legal targets"
                : cuts == 1 ? "Cage can only remove 1"
                : weak >= 2
                    ? (explicitPick ? "Lean Cage to cut 2 weak draws" : "Cage cuts 2 weak draws")
                : weak == 1 ? "Cage needs a second expendable card"
                    : "Cage needs 2 expendable cards";
            string astro = context?.Transformable is int n && n < 3 ? $"Astrolabe can only transform {Math.Max(0, n)}" : "Astrolabe rolls 3 upgraded replacements";
            var melody = deck.Cards.FirstOrDefault(c => !c.ForeignModel && c.Has("melody"));
            string tome = context?.TomeCard switch
            {
                { Id: "LOST_LUTE" } when melody != null => $"{melody.Name} helps build Melody for Lost Lute+ to play your top card",
                { Id: "LOST_LUTE" } => "Lost Lute+ needs Melody; no known source in your deck",
                { } card => $"Tome adds {card.Name.TrimEnd('+')}+",
                _ => "Tome’s card is unknown"
            };
            return $"{cage}; {astro}.\n{tome}.";
        }
        // Partial offers get a useful observation, never a fabricated overall winner.
        if (known.Length != offered.Length || best == null)
            return $"{Name(known[0].Id)}: {known[0].Reason}";
        return $"{Name(best.Id)}: {best.Reason}";
    }

    static string Name(string id) => System.Globalization.CultureInfo.InvariantCulture.TextInfo
        .ToTitleCase(id.Replace('_', ' ').ToLowerInvariant());
}
