namespace Auditor;

// Marginal deck-fit heuristic, not a predicted combat result. Indices preserve
// duplicate offers and partial selections; offered cards are never deck support
// unless they are actually in the same proposed package.
internal static class BlitzDraft
{
    internal sealed class Memo
    {
        object? owner;
        string? fingerprint;
        string? text;
        internal int Evaluations { get; private set; }
        internal string Get(object? screen, string key, Func<string> evaluate)
        {
            if (ReferenceEquals(owner, screen) && fingerprint == key && text != null) return text;
            owner = screen;
            fingerprint = key;
            Evaluations++;
            return text = evaluate();
        }
    }
    static readonly Memo draftMemo = new(), bundleMemo = new();
    internal static int DraftEvaluations => draftMemo.Evaluations;
    static object Snapshot(Card c) => new
    {
        c.Id,
        c.Name,
        c.Cost,
        c.Up,
        c.Attack,
        c.Known,
        c.Gives,
        c.Needs,
        c.IsCurse,
        c.IsStatus,
        c.Skill,
        c.Power,
        c.VariableCost,
        c.ForeignModel,
        c.CombatKnown,
        c.AuditedCurse,
        c.NativeShoot,
        c.Immediate
    };
    static string Fingerprint(Card[][] offers, Deck deck, object selection) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            Build = Catalog.Profile.Build,
            Offers = offers.Select(b => b.Select(Snapshot).ToArray()).ToArray(),
            Cards = deck.Cards.Select(Snapshot).ToArray(),
            deck.Hp,
            deck.MaxHp,
            deck.Act,
            deck.EliteOnly,
            deck.Relics,
            deck.Character,
            Names = deck.RelicNames?.OrderBy(p => p.Key).ToArray(),
            Selection = selection
        });
    internal sealed record Choice(int[] Indices, double Score)
    {
        internal bool Partial { get; init; }
    }

    // Native Locked In retrieves one Shoot-tag card from the draw pile. This
    // models access, not an extra copy or a guaranteed same-hand combo.
    internal static double Assess(Card card, Deck context)
    {
        if (Catalog.Profile.Build != "26660232" || card.ForeignModel || card.Id != "LOCKED_IN")
            return Coach.Assess(card, context).Score;
        if (card.NativeShoot == null || context.Cards.Any(c => c.NativeShoot == null)) return double.NaN;
        var targets = context.Cards.Where(c => c.NativeShoot == true).ToArray();
        if (targets.Length == 0) return -1.8;
        return (card.Up ? 1.1 : .2) + Math.Min(1.2, targets.Length * .3);
    }

    internal static double Package(Card[] additions, Deck deck)
    {
        double score = 0;
        for (int i = 0; i < additions.Length; i++)
        {
            var context = deck with { Cards = [.. deck.Cards, .. additions.Where((_, j) => j != i)] };
            var value = Assess(additions[i], context);
            if (!double.IsFinite(value)) return double.NaN;
            score += value;
        }
        // Each extra draw has a cost even when the entire package is mandatory.
        return score - additions.Length * .4;
    }

    internal static Choice? Best(Card[] offers, Deck deck, int min, int max, int[] selected)
    {
        if (min < 0 || max < min || max > offers.Length || selected.Distinct().Count() != selected.Length
            || selected.Any(i => i < 0 || i >= offers.Length) || selected.Length > max) return null;
        // Bound combinatorial work on the UI thread. Never substitute a greedy
        // ranking and pretend it compared every legal package.
        if (offers.Length > 16) return null;
        Choice? best = null;
        bool incomplete = false;
        var chosen = new List<int>(selected);
        var available = Enumerable.Range(0, offers.Length).Except(selected).ToArray();
        void Compare(int start)
        {
            if (chosen.Count >= min)
            {
                var indices = chosen.Order().ToArray();
                var score = Package(indices.Select(i => offers[i]).ToArray(), deck);
                if (!double.IsFinite(score)) incomplete = true;
                else if (best == null || score > best.Score) best = new(indices, score);
            }
            if (chosen.Count == max) return;
            for (int i = start; i < available.Length; i++)
            {
                if (chosen.Count + available.Length - i < min) break;
                chosen.Add(available[i]);
                Compare(i + 1);
                chosen.RemoveAt(chosen.Count - 1);
            }
        }
        Compare(0);
        return best == null ? null : best with { Partial = incomplete };
    }

    internal static string Advice(Card[] offers, Deck deck, int min, int max, int[] selected, bool explicitPick, object? screen = null) =>
        draftMemo.Get(screen, Fingerprint([offers], deck, new { min, max, selected, explicitPick }),
            () => AdviceCore(offers, deck, min, max, selected, explicitPick));

    static string AdviceCore(Card[] offers, Deck deck, int min, int max, int[] selected, bool explicitPick)
    {
        string range = min == max ? $"Choose {min}" : $"Choose {min}–{max}";
        string state = $"{range} total; {selected.Length} selected.";
        if (selected.Length > max) return state + " Deselect cards to meet the limit.";
        var best = Best(offers, deck, min, max, selected);
        if (best == null)
        {
            string unknown = string.Join(", ", offers.Where(c => !double.IsFinite(Assess(c, deck)))
                .Select(c => c.Name).Distinct());
            return state + " I can’t fully compare these legal combinations."
                + (unknown.Length > 0 ? $" Unmodeled: {unknown}." : "");
        }
        var remaining = best.Indices.Except(selected).ToArray();
        if (remaining.Length == 0)
            return state + (selected.Length >= min ? " You can confirm this selection." : "");
        string names = string.Join(", ", remaining.Select(i => offers[i].Name));
        var package = best.Indices.Select(i => offers[i]).ToArray();
        var support = package.SelectMany(c => c.Needs.Select(tag => (Card: c, Tag: tag)))
            .Select(pair => (pair.Card, Source: deck.Cards.Concat(package.Where(c => !ReferenceEquals(c, pair.Card)))
                .FirstOrDefault(c => c.Has(pair.Tag))))
            .FirstOrDefault(pair => pair.Source != null);
        string reason = support.Source == null ? $" Compared together against your {deck.Cards.Length}-card deck."
            : $" {support.Source.Name} supports {support.Card.Name}; they still need to line up in play.";
        if (package.Any(c => c.Id == "LOCKED_IN" && !c.ForeignModel))
        {
            var target = deck.Cards.Concat(package).FirstOrDefault(c => c.NativeShoot == true);
            reason += target == null ? " Locked In has no verified Shoot target."
                : $" Locked In can fetch {target.Name} only while it is in the draw pile.";
        }
        string action = explicitPick ? $"Add {names}." : $"Best remaining fit: {names}.";
        foreach (var card in package.Concat(offers.Except(package)))
        {
            var context = deck with { Cards = [.. deck.Cards, .. package.Where(c => !ReferenceEquals(c, card))] };
            if (StatusCardGuide.GeneratorCue(card, context) is not { } cue) continue;
            reason += " " + cue;
            break;
        }
        string caveat = "";
        if (best.Partial)
        {
            var unknown = offers.Where(c => !double.IsFinite(Assess(c, deck))).Select(c => c.Name).Distinct();
            caveat = $" Unmodeled: {string.Join(", ", unknown)}; these could change the choice.";
        }
        return state + (best.Partial ? " Among assessed combinations: " : " ")
            + action + reason + caveat;
    }

    internal static string Bundles(Card[][] bundles, Deck deck, bool canSkip, bool explicitPick, object? screen = null) =>
        bundleMemo.Get(screen, Fingerprint(bundles, deck, new { canSkip, explicitPick }),
            () => BundlesCore(bundles, deck, canSkip, explicitPick));

    static string BundlesCore(Card[][] bundles, Deck deck, bool canSkip, bool explicitPick)
    {
        var scores = bundles.Select(b => Package(b, deck)).ToArray();
        if (scores.Length == 0 || scores.Any(s => !double.IsFinite(s)))
            return "Choose one whole bundle. I can’t fully compare every bundle against your deck.";
        int best = Array.IndexOf(scores, scores.Max());
        if (canSkip && scores[best] <= 0) return "You may skip: none of these whole bundles improves the modeled deck fit.";
        return (explicitPick ? $"Prefer bundle {best + 1}: " : $"Bundle {best + 1} fits best: ")
            + string.Join(", ", bundles[best].Select(c => c.Name))
            + $". Compared together against your {deck.Cards.Length}-card deck, including the extra draws.";
    }
}
