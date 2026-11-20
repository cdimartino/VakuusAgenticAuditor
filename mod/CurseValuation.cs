namespace Auditor;

internal record CurseCost(double Cost, string Reason);

// Bounded fit heuristics, not HP predictions or win probabilities. Call only
// for actual CardType.Curse additions. No Curse Mode dependency.
internal static class CurseValuation
{
    // Evidence: installed build 26660232 English cards table (via
    // tools/catalog.py english_cards). DynamicVar quantities deliberately absent.
    internal static readonly Dictionary<string, string> Names = new()
    {
        ["INJURY"] = "Injury",
        ["DECAY"] = "Decay",
        ["DOUBT"] = "Doubt",
        ["SHAME"] = "Shame",
        ["REGRET"] = "Regret",
        ["NORMALITY"] = "Normality",
        ["BAD_LUCK"] = "Bad Luck",
        ["GUILTY"] = "Guilty",
        ["CLUMSY"] = "Clumsy",
        ["WRITHE"] = "Writhe",
        ["CURSE_OF_THE_BELL"] = "Curse of the Bell",
        ["ASCENDERS_BANE"] = "Ascender's Bane",
        ["GREED"] = "Greed"
    };

    internal static CurseCost Addition(string[] ids, Deck deck)
    {
        if (ids.Length == 0) return new(0, "No curse cards added.");
        int size = Math.Max(1, deck.Cards.Length);
        int existing = deck.Cards.Count(c => c.IsCurse);
        // Marginal fall in non-curse draw share; existing curses aren't charged
        // again. Five is a comparison horizon, not assumed live hand/draw size.
        double dilution = 5d * Math.Max(0, size - existing) * ids.Length / (size * (double)(size + ids.Length));
        // Weights below and the factor 20 are comparison-score tuning only,
        // never extracted damage amounts. Discard/exhaust tags alone do not
        // prove that this particular curse will be neutralized in time.
        double liability = 0;
        var notes = new List<string>();
        foreach (string id in ids)
        {
            var (weight, note) = id switch
            {
                "DECAY" => (2d, "Decay deals damage if held at turn end"),
                "DOUBT" => (2d, "Doubt gives Weak if held at turn end"),
                "SHAME" => (2d, "Shame gives Frail if held at turn end"),
                "BAD_LUCK" => (2.5, "Bad Luck loses HP if held at turn end"),
                "REGRET" => (deck.Cards.Any(c => c.Has("retain hand")) ? 4d : 2.5,
                    "Regret loses 1 HP per card in hand if held at turn end"),
                "NORMALITY" => (deck.Cards.Count(c => c.Cost == 0 || c.Has("draw")) >= 4 ? 5d : 2d,
                    "Normality limits the turn to 3 card plays"),
                "GUILTY" => (0d, "Guilty is removed after its specified number of combats; remaining duration is not modeled"),
                _ => (0d, "Additional liabilities are not modeled for " + Names.GetValueOrDefault(id, id))
            };
            liability += weight;
            notes.Add(note);
        }
        return new(20 * dilution + liability,
            $"Adding {ids.Length} curse card{(ids.Length == 1 ? "" : "s")} dilutes draws in your {deck.Cards.Length}-card deck. "
            + string.Join("; ", notes.Distinct()) + ". This is a marginal fit estimate, not predicted HP loss; no removal or curse synergy is assumed.");
    }
}
