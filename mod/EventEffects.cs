using System.Text.RegularExpressions;

namespace Auditor;

// Counts are live legal targets, not attractive targets. Null means unknown.
// Named cards must be runtime/catalog-resolved; no inference from their titles.
internal record EventEvaluation(int Hp, int MaxHp, int Gold, Deck? Deck = null,
    int? UpgradeTargets = null, int? RemovalTargets = null, int? EmptyPotionSlots = null,
    IReadOnlyDictionary<string, Card>? NamedCards = null);
internal record EventEffectResult(string Title, double Score, string[] Known, string[] Unresolved, bool Unsafe);

internal static class EventEffects
{
    static string Plain(string s) => Regex.Replace(s, @"\[[^\]]+\]", "").Trim();
    internal static EventEffectResult Assess(EventPick option, EventEvaluation c)
    {
        double score = 0;
        int hp = c.Hp, gold = c.Gold;
        bool unsafeChoice = false;
        var known = new List<string>();
        var unknown = new List<string>();
        int matchedCurses = 0;
        foreach (var clause in Plain(option.Description).Split(['.', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var m = Regex.Match(clause, @"^(Gain|Heal|Lose|Pay|Take) (\d+) (Gold|Max HP|HP|damage)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[2].Value, out int n))
            {
                switch ((m.Groups[1].Value.ToLowerInvariant(), m.Groups[3].Value.ToLowerInvariant()))
                {
                    case ("heal", "hp"):
                        int healed = Math.Min(Math.Max(0, c.MaxHp - hp), n);
                        score += healed * (hp * 2 < c.MaxHp ? 2 : 1);
                        hp += healed; known.Add($"heals {healed} HP ({hp}/{c.MaxHp})"); break;
                    case ("lose", "hp"):
                    case ("take", "damage"):
                        hp -= n; score -= n * (hp < c.MaxHp * .4 ? 2.3 : .55);
                        unsafeChoice |= hp <= 0;
                        known.Add(hp <= 0 ? $"{n} HP cost is lethal" : $"leaves {hp}/{c.MaxHp} HP"); break;
                    case ("gain", "gold"): gold += n; score += n / (c.Gold < 150 ? 7d : 10d); known.Add(clause); break;
                    case ("pay" or "lose", "gold"):
                        gold -= n; unsafeChoice |= gold < 0; score -= n / 8d;
                        known.Add(gold < 0 ? $"cannot afford {n} Gold" : clause); break;
                    case ("gain", "max hp"): score += n * 1.65; known.Add(clause); break;
                    default: unknown.Add(clause); break;
                }
                continue;
            }
            if (clause.Equals("Leave", StringComparison.OrdinalIgnoreCase)) { known.Add("no change"); continue; }
            // Preserve the established Sunken Statue comparison; this named
            // reward was already modeled before the general clause evaluator.
            if (clause.Equals("Obtain the Sword of Stone", StringComparison.OrdinalIgnoreCase))
            {
                score += 9; known.Add("obtain the Sword of Stone"); continue;
            }
            if (Regex.IsMatch(clause, @"^Upgrade (a|1) card( in your Deck)?$", RegexOptions.IgnoreCase))
            {
                if (c.UpgradeTargets == 0) { known.Add("no legal upgrade target"); unsafeChoice = true; }
                else if (c.UpgradeTargets == null) unknown.Add("upgrade target eligibility is not known");
                else { score += 8; known.Add("upgrade one eligible card"); }
                continue;
            }
            if (Regex.IsMatch(clause, @"^Remove (a|1) card from your Deck$", RegexOptions.IgnoreCase))
            {
                if (c.RemovalTargets == 0) { known.Add("no legal removal target"); unsafeChoice = true; }
                else unknown.Add(c.RemovalTargets == null ? "removal target eligibility is not known" : "removal value depends on the selected card");
                continue;
            }
            var add = Regex.Match(clause, @"^Add (.+) to your Deck$");
            var gain = Regex.Match(clause, @"^Gain a (.+)$");
            if (add.Success || gain.Success)
            {
                string name = (add.Success ? add : gain).Groups[1].Value;
                if (name == "Metamorphosis") { unknown.Add(CardUnknown(name)); continue; }
                var ids = option.CurseIds;
                if (ids is { Length: > 0 } && c.Deck != null && ids.Distinct().Count() == 1
                    && CurseValuation.Names.TryGetValue(ids[0], out var curseName)
                    && (ids.Length == 1 && name == curseName || name == $"{ids.Length} {curseName}") && matchedCurses == 0)
                {
                    var cost = CurseValuation.Addition(ids, c.Deck);
                    score -= cost.Cost; known.Add(cost.Reason); matchedCurses += ids.Length;
                }
                else if (c.NamedCards != null && c.NamedCards.TryGetValue(name, out var card) && c.Deck != null)
                {
                    var fit = Coach.Assess(card, c.Deck);
                    if (double.IsNaN(fit.Score)) unknown.Add(CardUnknown(name));
                    else { score += fit.Score; known.Add($"add {name}: {fit.Reason}"); }
                }
                else unknown.Add(CardUnknown(name));
                continue;
            }
            if (Regex.IsMatch(clause, @"^(Obtain|Gain|Procure) .*(Relic|Potion|card)s?$", RegexOptions.IgnoreCase))
            {
                if (clause.Contains("Potion", StringComparison.OrdinalIgnoreCase) && c.EmptyPotionSlots == 0)
                    unknown.Add("potion belt is full; replacement/discard is not assumed");
                else unknown.Add(clause + ": reward identity/outcome and value are not guaranteed");
                continue;
            }
            if (option.Relic != null && clause == "Obtain " + Plain(option.Relic))
            {
                unknown.Add($"{option.Relic}: relic value is not resolved"); continue;
            }
            unknown.Add(clause);
        }
        if (known.Count == 0 && unknown.Count == 0) unknown.Add("no visible effect text");
        if ((option.CurseIds?.Length ?? 0) != matchedCurses) unknown.Add("runtime curse additions do not match a recognized visible clause");
        return new(Plain(option.Title), score, known.ToArray(), unknown.Distinct().ToArray(), unsafeChoice);
    }

    // Source: build 26660232 English cards METAMORPHOSIS.description.
    // Assembly Metamorphosis constructor: 2 energy Skill/Event, Exhaust;
    // CardsVar(3), upgrade +2; OnPlay sets generated Attacks free this combat.
    // No random Attack strength is invented. Runtime
    // CardModel remains authoritative for type; this is NOT a curse rule.
    static string CardUnknown(string name) => name == "Metamorphosis"
        ? "Metamorphosis is a 2-energy Skill that Exhausts, not a curse; it adds 3 random Attacks (5 upgraded) to your Draw Pile, free to play this combat but only available when drawn. Their random effects make its deck fit uncertain"
        : $"{name}: added card identity/effect or deck fit is unresolved";

    internal static string? Compare((EventPick Option, bool Enabled)[] options, EventEvaluation c, bool pick)
    {
        var results = options.Where(o => o.Enabled).Select(o => Assess(o.Option, c)).ToArray();
        if (results.Length == 0) return "No enabled event options are visible.";
        string Describe(EventEffectResult r) => $"{r.Title}: "
            + string.Join("; ", r.Known.Concat(r.Unresolved.Select(u => "unresolved: " + u))) + ".";
        var safe = results.Where(r => !r.Unsafe).OrderByDescending(r => r.Score).ToArray();
        if (safe.Length == 0) return string.Join(" ", results.Select(Describe)) + " No safe supported choice identified.";
        // Unknown conditions can change any score; don't silently discard them.
        if (safe.Length == 1 && results.Length > 1)
            return (pick ? safe[0].Title + ". " : "") + Describe(safe[0]) + " "
                + string.Join(" ", results.Where(r => r.Unsafe).Select(r =>
                    r.Title + ": " + string.Join("; ", r.Known) + "."));
        if (safe.Any(r => r.Unresolved.Length > 0))
            return string.Join(" ", results.Select(Describe))
                + " These unresolved effects could reverse the comparison; weigh them before choosing.";
        var top = safe[0];
        return (pick ? top.Title + ". " : "") + string.Join(" ", results.Select(Describe));
    }
}
