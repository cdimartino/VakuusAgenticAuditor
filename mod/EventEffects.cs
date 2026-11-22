using System.Text.RegularExpressions;

namespace Auditor;

// Counts are live legal targets, not attractive targets. Null means unknown.
// Named cards must be runtime/catalog-resolved; no inference from their titles.
internal record EventEvaluation(int Hp, int MaxHp, int Gold, Deck? Deck = null,
    int? UpgradeTargets = null, int? RemovalTargets = null, int? EmptyPotionSlots = null,
    IReadOnlyDictionary<string, Card>? NamedCards = null,
    int? TransformTargets = null,
    Func<string, bool, Card?>? ResolveNamedCard = null);
internal record EventEffectResult(string Title, double Score, string[] Known, string[] Unresolved, bool Unsafe, bool OnlyDeckFitUncertain);

internal static class EventEffects
{
    static string Plain(string s) => Regex.Replace(s, @"\[[^\]]+\]", "").Trim();
    internal static EventEffectResult Assess(EventPick option, EventEvaluation c)
    {
        double score = 0;
        int hp = c.Hp, maxHp = c.MaxHp, gold = c.Gold;
        bool unsafeChoice = false;
        var known = new List<string>();
        var unknown = new List<string>();
        int matchedCurses = 0;
        int uncertainDeckFits = 0;
        foreach (var clause in Plain(option.Description).Split(['.', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (clause.Equals("Heal to full HP", StringComparison.OrdinalIgnoreCase))
            {
                int healed = Math.Max(0, maxHp - hp);
                score += healed * (hp * 2 < maxHp ? 2 : 1);
                hp += healed; known.Add($"heals {healed} HP ({hp}/{maxHp})"); continue;
            }
            if (Regex.IsMatch(clause, @"^Lose ALL (of )?your Gold$", RegexOptions.IgnoreCase))
            {
                score -= gold / 8d; known.Add($"lose {gold} Gold"); gold = 0; continue;
            }
            var m = Regex.Match(clause, @"^(Gain|Obtain|Heal|Lose|Pay|Take) (\d+) (Gold|Max HP|HP|damage)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[2].Value, out int n))
            {
                switch ((m.Groups[1].Value.ToLowerInvariant(), m.Groups[3].Value.ToLowerInvariant()))
                {
                    case ("heal", "hp"):
                        int healed = Math.Min(Math.Max(0, maxHp - hp), n);
                        score += healed * (hp * 2 < maxHp ? 2 : 1);
                        hp += healed; known.Add($"heals {healed} HP ({hp}/{maxHp})"); break;
                    case ("lose", "hp"):
                    case ("take", "damage"):
                        hp -= n; score -= n * (hp < maxHp * .4 ? 2.3 : .55);
                        unsafeChoice |= hp <= 0;
                        known.Add(hp <= 0 ? $"{n} HP cost is lethal" : $"leaves {hp}/{maxHp} HP"); break;
                    case ("gain" or "obtain", "gold") when c.Deck?.Relics.Contains("ECTOPLASM") == true:
                        known.Add("Ectoplasm prevents the gold gain"); break;
                    case ("gain" or "obtain", "gold"): gold += n; score += n / (c.Gold < 150 ? 7d : 10d); known.Add(clause); break;
                    case ("pay" or "lose", "gold"):
                        gold -= n; unsafeChoice |= gold < 0; score -= n / 8d;
                        known.Add(gold < 0 ? $"cannot afford {n} Gold" : clause); break;
                    case ("gain", "max hp"): maxHp += n; score += n * 1.65; known.Add(clause); break;
                    case ("lose", "max hp"):
                        maxHp -= n; hp = Math.Min(hp, maxHp);
                        score -= n * 1.65; unsafeChoice |= maxHp <= 0;
                        known.Add(maxHp <= 0 ? $"{n} Max HP cost is lethal" : $"leaves {hp}/{maxHp} HP"); break;
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
            var upgrade = Regex.Match(clause, @"^Upgrade (a|\d+) cards?( in your Deck)?$", RegexOptions.IgnoreCase);
            if (upgrade.Success)
            {
                int count = upgrade.Groups[1].Value == "a" ? 1 : int.Parse(upgrade.Groups[1].Value);
                if (c.UpgradeTargets == 0) known.Add("no legal upgrade target; no upgrade benefit");
                else if (c.UpgradeTargets < count) unknown.Add($"only {c.UpgradeTargets} legal upgrade targets for {count} requested; selection limit is not verified");
                else if (c.UpgradeTargets == null) unknown.Add("upgrade target eligibility is not known");
                else { score += 8 * count; known.Add($"upgrade {count} eligible {(count == 1 ? "card" : "cards")}"); }
                continue;
            }
            var remove = Regex.Match(clause, @"^Remove (a|\d+) cards? from your Deck$", RegexOptions.IgnoreCase);
            if (remove.Success)
            {
                int count = remove.Groups[1].Value == "a" ? 1 : int.Parse(remove.Groups[1].Value);
                if (c.RemovalTargets == 0) known.Add("no legal removal target; no removal benefit");
                else if (c.RemovalTargets < count) unknown.Add($"only {c.RemovalTargets} legal removal targets for {count} requested; selection limit is not verified");
                else unknown.Add(c.RemovalTargets == null ? "removal target eligibility is not known" : $"remove {count} card(s): value depends on the selected cards");
                continue;
            }
            var transform = Regex.Match(clause, @"^(?:Transform (a|\d+) cards?(?: in your Deck)?(?:, then Upgrade them)?|Choose a card to Transform)$", RegexOptions.IgnoreCase);
            if (transform.Success)
            {
                int count = int.TryParse(transform.Groups[1].Value, out int amount) ? amount : 1;
                if (c.TransformTargets == 0) known.Add("no legal transform target; no transformation benefit");
                else if (c.TransformTargets < count) unknown.Add($"only {c.TransformTargets} legal transform targets for {count} requested; selection limit is not verified");
                else unknown.Add("transformation outcome is random; resulting card value is not assumed");
                continue;
            }
            var add = Regex.Match(clause, @"^Add (.+) to your Deck$");
            var gain = Regex.Match(clause, @"^(?:Gain a|Obtain) (.+)$");
            if (add.Success || gain.Success && !Regex.IsMatch(gain.Groups[1].Value,
                @"\b(random|Relics?|Potions?|cards?)\b", RegexOptions.IgnoreCase))
            {
                string name = (add.Success ? add : gain).Groups[1].Value;
                if (option.Relic != null && (name == Plain(option.Relic) || name == "the " + Plain(option.Relic)))
                {
                    unknown.Add($"{option.Relic}: relic value is not resolved"); continue;
                }
                if (name == "Metamorphosis") { unknown.Add(CardUnknown(name)); uncertainDeckFits++; continue; }
                bool upgraded = name.EndsWith('+');
                var ids = option.CurseIds;
                if (ids is { Length: > 0 } && c.Deck != null && ids.Distinct().Count() == 1
                    && CurseValuation.Names.TryGetValue(ids[0], out var curseName)
                    && (ids.Length == 1 && name == curseName || name == $"{ids.Length} {curseName}") && matchedCurses == 0)
                {
                    var cost = CurseValuation.Addition(ids, c.Deck);
                    score -= cost.Cost; known.Add(cost.Reason); matchedCurses += ids.Length;
                }
                else if (c.ResolveNamedCard?.Invoke(upgraded ? name[..^1] : name, upgraded) is Card resolved
                    && (!upgraded || resolved.Up) && c.Deck != null)
                {
                    var fit = Coach.Assess(resolved, c.Deck);
                    if (double.IsFinite(fit.Score)) { score += fit.Score; known.Add($"add {name}: {fit.Reason}"); }
                    else unknown.Add(CardUnknown(name));
                }
                else if (!upgraded && c.NamedCards != null && c.NamedCards.TryGetValue(name, out var card) && c.Deck != null)
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
                if (c.Deck?.Relics.Contains("SOZU") == true && Regex.IsMatch(clause,
                    @"^(?:Procure|Obtain|Gain) (?:a|an|\d+) (?:random )?(?:(?:Common|Uncommon|Rare) )?Potions?$", RegexOptions.IgnoreCase))
                    known.Add("Sozu prevents acquiring the potion");
                else if (clause.Contains("Potion", StringComparison.OrdinalIgnoreCase) && c.EmptyPotionSlots == 0)
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
        return new(Plain(option.Title), score, known.ToArray(), unknown.Distinct().ToArray(), unsafeChoice,
            uncertainDeckFits > 0 && uncertainDeckFits == unknown.Count);
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
        string DescribeKnown(EventEffectResult r) => r.Title + ": " + string.Join("; ", r.Known) + ".";
        var safe = results.Where(r => !r.Unsafe).OrderByDescending(r => r.Score).ToArray();
        var details = results.Where(r => r.Known.Length > 0).Select(DescribeKnown).ToList();
        if (safe.Length == 0)
        {
            details.Add("No safe supported choice identified.");
            return string.Join(" ", details);
        }

        // Uncertainty still limits ranking, but don't echo the option buttons.
        bool uncertain = safe.Any(r => r.Unresolved.Length > 0);
        if (details.Count == 0) return "I can't compare these choices.";
        if (uncertain) details.Add("I can't assess every effect.");
        // Metamorphosis has verified addition semantics; only the card's future deck fit
        // is uncertain. An opaque event clause, by contrast, may itself be dangerous.
        bool onlyKnownSafeAddition = safe.Length == 1 && results.Length > 1 && safe[0].OnlyDeckFitUncertain;
        bool canPick = (!uncertain || onlyKnownSafeAddition)
            && (safe.Length == 1 || safe[0].Score > safe[1].Score);
        return (pick && canPick ? safe[0].Title + ". " : "") + string.Join(" ", details);
    }
}
