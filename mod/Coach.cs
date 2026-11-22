namespace Auditor;

internal record Card(string Id, string Name, int Cost, bool Up, bool Attack, bool Known, string[] Gives, string[] Needs)
{
    // Supplied from runtime CardType.Curse, never inferred from a name/tag.
    internal bool IsCurse { get; init; }
    internal bool IsStatus { get; init; }
    internal bool Skill { get; init; }
    internal bool Power { get; init; }
    internal bool VariableCost { get; init; }
    internal bool NativePlayableType => Attack || Skill || Power;
    internal bool ForeignModel { get; init; }
    internal bool CombatKnown { get; init; }
    internal bool AuditedCurse { get; init; }
    internal bool? NativeShoot { get; init; }
    internal string[]? Immediate { get; init; }
    internal bool Has(string role) => Gives.Contains(role);
    internal bool CombatHas(string role) => (Immediate ?? Gives).Contains(role);
}
internal static class Catalog
{
    internal static CatalogProfile Profile { get; set; } = null!;
    static class Tables
    {
        // Evidence columns: native source, indexed roles, effective base/upgrade roles,
        // payoff roles, combat hook and reward policies. Use effective roles only.
        internal sealed record Rules(string[] Base, string[] Up, string[] Needs, string[] Deferred, bool Hook, bool Curse);
        static string[] Tags(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries);
        internal static readonly Dictionary<string, Rules> Roles = Table("card-coverage.tsv").Skip(1)
            .Where(a => a[1] == "model+text").ToDictionary(a => a[0], a =>
                new Rules(Tags(a[4]), Tags(a[5]), Tags(a[6]), Tags(a[11]), a[7] != "" || a[12] == "yes", a[8] == "typed-curse"));
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
        var roles = vanilla ? Tables.Roles.GetValueOrDefault(id) : null;
        var immediate = up ? roles?.Up ?? [] : roles?.Base ?? [];
        var gives = immediate.Union(roles?.Deferred ?? []).ToArray();
        var needs = roles?.Needs ?? [];
        var known = gives.Length + needs.Length > 0;
        return new(id, name, cost, up, attack, known, gives, needs)
        {
            ForeignModel = !vanilla,
            AuditedCurse = roles?.Curse == true,
            Immediate = immediate,
            CombatKnown = known || roles?.Hook == true
        };
    }
}
internal record Deck(Card[] Cards, int Hp, int MaxHp, int Act, bool EliteOnly, string[] Relics, string Character = "", IReadOnlyDictionary<string, string>? RelicNames = null);
internal record Verdict(string Id, string Name, double Score, string Reason, string? Caution);
internal static class Coach
{
    internal static (int score, string reason) Relic(string id, Deck deck)
    {
        if (RelicGuide.BlockedDraw(id, deck.Relics)) return (0, "Fiddle blocks the in-turn draw.");
        var match = RelicGuide.ForRelic(id, deck.RelicNames?.GetValueOrDefault(id) ?? RelicGuide.Name(id), deck);
        if (match != null) return (match.Tradeoff ? -2 : 5 + match.Specificity, match.Text);
        bool injured = deck.Hp * 2 < deck.MaxHp;
        return Catalog.RelicFit.GetValueOrDefault(id) switch
        {
            "gold" when deck.Relics.Contains("ECTOPLASM") => (0, "Ectoplasm prevents the gold gain."),
            "slots" when deck.Relics.Contains("SOZU") => (0, "Sozu prevents acquiring potions for the extra space."),
            "max hp" => (injured ? 7 : 4, $"More maximum HP gives you room for mistakes at {deck.Hp}/{deck.MaxHp}."),
            "gold" => (4, "Bank the gold for an upgrade to your deck at the next merchant."),
            "sustain" or "rest heal" => (injured ? 8 : 4, $"Repeatable healing matters at {deck.Hp}/{deck.MaxHp} HP."),
            "opening block" => (6, "Opening block buys time to set up your first turn."),
            "energy" or "opening energy" => (deck.Cards.Count(c => c.Cost >= 2) >= 3 ? 8 : 5, "Extra energy makes expensive hands easier to play."),
            "opening draw" => (deck.Cards.Length >= 18 ? 7 : 5, "An extra opening draw helps you find your key cards earlier."),
            "slots" => (4, "An extra potion slot gives you more options at the next elite."),
            "strength" => (deck.Cards.Count(c => c.Attack) >= 6 ? 7 : 4, "Strength adds damage to your attacks."),
            "dexterity" => (deck.Cards.Count(c => c.Has("guard")) >= 4 ? 7 : 4, "Dexterity improves your block cards."),
            _ => (0, RelicGuide.Get(id)?.Cue ?? "")
        };
    }
    // A score is marginal fit for this deck, not predicted win rate.
    internal static Verdict Assess(Card c, Deck d)
    {
        if (c.IsStatus)
        {
            var rule = StatusCardGuide.Lookup(c);
            return new(c.Id, c.Name, rule?.Reward is int value ? value : double.NaN,
                rule?.Cue ?? "I don’t know this status well enough to rank it.", null);
        }
        if (c.IsCurse && !c.ForeignModel && (c.AuditedCurse || CurseValuation.Names.ContainsKey(c.Id)))
        {
            var cost = CurseValuation.Addition([c.Id], d);
            return new(c.Id, c.Name, -cost.Cost, cost.Reason, cost.Reason);
        }
        bool auditedRare = Catalog.Profile.Build == "26660232" && !c.ForeignModel && c.Id is ("UNDEATH" or "TRANSFIGURE" or "OBLIVION");
        if (!c.Known && !auditedRare) return new(c.Id, c.Name, double.NaN, "I don’t know this card well enough to rank it.", null);
        var pros = new List<(double w, string text)>();
        var cons = new List<(double w, string text)>();
        var cards = d.Cards;
        var opportunity = Archetypes.Opportunity(d, c);
        var guards = cards.Count(x => x.Has("guard"));
        var nonBasicHits = cards.Count(x => x.Attack && !x.Id.StartsWith("STRIKE_"));
        string? specific = StatusCardGuide.GeneratorCue(c, d);
        // Existing role and archetype weights already reward Sadness payoffs.
        if (specific != null && !d.Cards.Any(x => !x.ForeignModel && (x.Needs.Contains("sadness")
            || x.Id is "PUNCH_OF_JOY" or "COMPACT" or "FLAK_CANNON")))
            cons.Add((c.Id == "SORROWFUL_STRIKE" ? 1.2 : .6, "Retained Sadness crowds later hands."));
        if (auditedRare)
        {
            if (c.Id == "UNDEATH")
            {
                specific = $"Gain {(c.Up ? 9 : 7)} Block; each play adds a copy to your discard this combat, crowding later draws.";
                pros.Add((1, "Block without spending base energy."));
                cons.Add((guards * 3 >= cards.Length ? 1.25 : .5, "Repeated copies dilute later hands."));
            }
            else if (c.Id == "TRANSFIGURE")
            {
                var candidate = cards.FirstOrDefault(x => !x.ForeignModel && !x.VariableCost && x.Cost is 0 or 1
                    && (x.Id == "HAUNT" && cards.Any(s => s.Has("souls")) || x.Has("area damage") || x.Has("summon") && cards.Any(s => s.Needs.Contains("summon"))));
                specific = candidate == null ? "Choose another card in hand for Replay this combat; fixed energy costs rise by 1."
                    : $"If drawn together, choose {candidate.Name} in hand for Replay this combat; its fixed cost rises by 1 energy.";
                specific += c.Up ? " Transfigure+ does not Exhaust." : " Transfigure Exhausts.";
                if (candidate != null) pros.Add((2, specific));
                cons.Add((.7, "Needs a worthwhile target in hand and energy to play it."));
            }
            else
            {
                var souls = cards.FirstOrDefault(x => !x.ForeignModel && x.Has("souls"));
                var cheap = cards.Count(x => x.Known && !x.VariableCost && x.Cost == 0 && !x.IsCurse);
                var doomPayoff = cards.FirstOrDefault(x => !x.ForeignModel && x.Needs.Contains("doom"));
                specific = $"Each card you play afterward this turn applies {(c.Up ? 4 : 3)} Doom to the target, not immediate damage.";
                if (souls != null) specific += $" Souls from {souls.Name} can add plays.";
                else if (doomPayoff != null) specific += $" {doomPayoff.Name} rewards Doom.";
                else if (cheap == 0) specific += " Needs plays left in hand.";
                if (souls != null || cheap > 0) pros.Add((1.5 + Math.Min(2, cheap) * .3, specific));
                if (doomPayoff != null) pros.Add((1, specific));
                if (c.Up) pros.Add((.4, "More Doom per follow-up."));
            }
        }
        bool drawBenefit = c.Has("draw") && !(c.CombatHas("draw") && d.Relics.Contains("FIDDLE"));
        if (c.Cost == 1 && c.CombatHas("hit") && c.CombatHas("guard") && c.CombatHas("draw") && drawBenefit)
            pros.Add((2.2, "One energy buys damage, block, and a replacement draw."));
        if (c.CombatHas("guard") && (guards * 3 <= cards.Length || d.Hp * 2 <= d.MaxHp))
            pros.Add((d.Hp * 2 <= d.MaxHp ? 2.6 : 1.5, $"At {d.Hp}/{d.MaxHp} HP, another block draw gives you breathing room."));
        if (c.Has("hit") && nonBasicHits < (d.EliteOnly ? 5 : 3))
            pros.Add((d.EliteOnly ? 2.8 : 1.8, $"You have {nonBasicHits} non-Strike attack{(nonBasicHits == 1 ? "" : "s")}{(d.EliteOnly ? " before a forced elite" : "")}. This adds damage now."));
        if (drawBenefit && cards.Length >= 15)
            pros.Add((1.4, $"Draw helps you reach key cards in a {cards.Length}-card deck."));
        if (c.Has("area damage") && !cards.Any(x => x.Has("area damage")))
            pros.Add((1.45, "Your deck has no tagged area attack. This can damage several enemies in one play."));
        if (c.Has("weak") && !cards.Any(x => x.Has("weak")))
            pros.Add((1.15, "Weak gives you a way to reduce incoming attacks."));
        if (c.Has("hp loss") && d.Hp * 3 <= d.MaxHp)
            cons.Add((2.5, $"At {d.Hp} HP, the health cost is a real drawback."));
        if (c.CombatHas("energy") && cards.Count(x => x.Cost >= 2) >= 3)
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
        var matches = RelicGuide.ForCard(c, d.Relics, d.RelicNames).ToArray();
        var benefit = RelicGuide.Best(matches.Where(m => !m.Tradeoff));
        var drawback = RelicGuide.Best(matches.Where(m => m.Tradeoff));
        if (benefit != null) pros.Add((1 + benefit.Specificity * .4, benefit.Text));
        if (drawback != null) cons.Add((1 + drawback.Specificity * .4, drawback.Text));
        if (c.Cost >= 2 && cards.Count(x => x.Cost >= 2) >= 3 && !cards.Any(x => x.Has("energy")))
            cons.Add((1.6, "Several expensive cards already compete for energy."));
        int copies = cards.Count(x => x.Id == c.Id);
        if (copies > 0) cons.Add((Math.Min(2.4, .85 * copies), $"You already have {copies} {(copies == 1 ? "copy" : "copies")}."));
        if (c.Up) pros.Add((.6, "It comes upgraded without spending a campfire."));
        if (cards.Length >= 20 && !drawBenefit) cons.Add((.7, $"Adding it dilutes your {cards.Length}-card deck."));
        var best = pros.OrderByDescending(x => x.w).FirstOrDefault().text;
        var risk = cons.OrderByDescending(x => x.w).FirstOrDefault().text;
        return new(c.Id, c.Name, pros.Sum(x => x.w) - cons.Sum(x => x.w) + (opportunity != null && !opportunity.StartsWith("Possible ") ? 1 : 0),
            specific ?? drawback?.Text ?? benefit?.Text ?? opportunity ?? best ?? risk ?? "No strong interaction stands out in this deck.", risk);
    }
    internal static Verdict[] Rank(IEnumerable<Card> choices, Deck deck, bool skip)
        => Rank(choices, deck, skip ? ["skip"] : []);
    internal static Verdict[] Rank(IEnumerable<Card> choices, Deck deck, IEnumerable<string> alternatives)
    {
        var options = choices.Select(c => Assess(c, deck)).ToList();
        foreach (var id in alternatives.Distinct(StringComparer.OrdinalIgnoreCase))
            options.Add(string.Equals(id, "skip", StringComparison.OrdinalIgnoreCase)
                ? new("~skip", "Skip", deck.Cards.Length >= 16 ? 1 : .2, "Skipping keeps your current cards coming around as often.", null)
                : new("~alternative:" + id, "Other reward", double.NaN, "The alternative reward's value is unassessed.", null));
        return options.OrderByDescending(x => x.Score).ToArray();
    }
    internal static string Shop(Verdict? card, int cardCost, RelicGuide.Interaction? relic, int relicCost,
        int? removalCost, bool affordablePotion, int gold)
    {
        var lines = new List<string>();
        if (card != null) lines.Add($"{card.Name} ({cardCost}g): {card.Reason}");
        if (relic != null) lines.Add($"{relic.Text} ({relicCost}g)");
        if (removalCost != null) lines.Add($"Removal ({removalCost}g): check which draw you can spare.");
        if (affordablePotion) lines.Add("A potion also fits your budget and belt.");
        lines.Add($"You can save your {gold} gold.");
        return string.Join("\n", lines);
    }
    internal static string? PartialReward(Verdict[] ranking)
    {
        var unknown = ranking.Where(x => !double.IsFinite(x.Score)).ToArray();
        if (unknown.Length == 0) return null;
        var known = ranking.Where(x => double.IsFinite(x.Score) && x.Id != "~skip").OrderByDescending(x => x.Score).FirstOrDefault();
        bool skip = ranking.Any(x => x.Id == "~skip");
        var names = unknown.Select(x => x.Id.StartsWith("~alternative:") ? "an alternative reward" : x.Name).Distinct();
        return (known == null ? "" : $"{known.Name}: {known.Reason} ")
            + $"I can’t rank {string.Join(", ", names)}." + (skip ? " Skip keeps the deck unchanged." : "");
    }
    internal static string After(Verdict pick, Verdict top)
    {
        if (double.IsNaN(pick.Score)) return pick.Reason;
        if (top.Score - pick.Score <= .55) return $"Sound choice. {pick.Reason}";
        return pick.Caution ?? (pick.Id == "~skip" ? $"A leaner deck, though {top.Name} had a case: {top.Reason}" :
            $"{pick.Name} is a tradeoff. {top.Name}: {top.Reason}");
    }
}
