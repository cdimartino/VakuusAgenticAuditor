namespace Auditor;

// SelfHelpBook selects one legal deck card per enabled option; no card is changed here.
internal static class BookCoach
{
    internal enum Effect { Sharp, Nimble, Swift }
    internal sealed record Offer(Effect Effect, string Title, Card[] Targets);
    internal static Effect? Parse(string key) => key switch
    {
        "SELF_HELP_BOOK.pages.INITIAL.options.READ_THE_BACK" => Effect.Sharp,
        "SELF_HELP_BOOK.pages.INITIAL.options.READ_PASSAGE" => Effect.Nimble,
        "SELF_HELP_BOOK.pages.INITIAL.options.READ_ENTIRE_BOOK" => Effect.Swift,
        _ => null
    };

    internal static string? Compare(Offer[] offers, int hp, int maxHp, bool fiddle, bool explicitPick)
    {
        var choices = new List<(string Title, string Benefit, int Fit)>();
        foreach (var offer in offers)
        {
            int Fit(Card card)
            {
                bool cheap = !card.VariableCost && card.Cost is 0 or 1;
                return offer.Effect switch
                {
                    Effect.Sharp when card.CombatHas("multiple hits") => 7,
                    Effect.Sharp when card.CombatHas("area damage") => 5,
                    Effect.Sharp when card.CombatHas("hit") => cheap ? 3 : 2,
                    Effect.Nimble => (hp * 2 <= maxHp ? 5 : 2) + (cheap ? 1 : 0),
                    Effect.Swift => fiddle ? -1 : cheap ? 5 : 3,
                    _ => 0
                };
            }
            var card = offer.Targets.OrderByDescending(Fit).FirstOrDefault();
            if (card == null) continue;
            string benefit = offer.Effect switch
            {
                Effect.Sharp when card.CombatHas("multiple hits") => $"{card.Name} gets +2 damage per powered hit",
                Effect.Sharp => $"{card.Name}: +2 to powered attack damage",
                Effect.Nimble => $"{card.Name}: +2 Block when it grants Block",
                _ => fiddle ? $"{card.Name}: Fiddle blocks Swift’s in-turn draw" : $"{card.Name}: draw 2 on its first play each combat"
            };
            choices.Add((offer.Title, benefit, Fit(card)));
        }
        if (choices.Count == 0) return null;
        var sorted = choices.OrderByDescending(x => x.Fit).ToArray();
        bool lean = explicitPick && sorted[0].Fit > 0 && (sorted.Length == 1 || sorted[0].Fit > sorted[1].Fit);
        return string.Join("\n", sorted.Select((x, i) => $"{(i == 0 && lean ? "Consider " : "")}{x.Title} — {x.Benefit}."));
    }
}
