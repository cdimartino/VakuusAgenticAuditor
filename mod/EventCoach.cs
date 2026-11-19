using System.Text.RegularExpressions;

namespace Auditor;

internal record EventPick(string Title, string Description, string? Relic = null);
internal static class EventCoach
{
    internal static string? Ancient((string id, string name)[] relics, int upgrades, int basics, int expensive, bool pick)
    {
        var ranked = relics.Select(r => r.id switch
        {
            "YUMMY_COOKIE" when upgrades > 0 => (3 * Math.Min(4, upgrades) + 1, r.name, $"Upgrade up to {Math.Min(4, upgrades)} cards permanently without adding Soot."),
            "BIIIG_HUG" when basics >= 4 => (basics >= 8 ? 14 : 7, r.name, "Remove four starter cards. The tradeoff is a Soot every shuffle."),
            "PUMPKIN_CANDLE" => (expensive >= 4 ? 15 : 6, r.name, "Extra energy helps your expensive turns. It lasts five fights before it needs kindling."),
            _ => (int.MinValue, r.name, "")
        }).Where(x => x.Item1 > int.MinValue).OrderByDescending(x => x.Item1).ToArray();
        if (ranked.Length == 0) return null;
        return (pick ? $"Take {ranked[0].name}. " : "") + ranked[0].Item3;
    }
    static string Plain(string s) => Regex.Replace(s, @"\[[^\]]+\]", "").Trim();
    // Evaluate only fully understood offers. A reward followed by an unknown
    // condition (fight, curse, chance, etc.) must not be scored as free.
    internal static string? Recommend(EventPick[] options, int hp, int maxHp, int gold, bool explicitPick)
    {
        if (options.Length < 2) return null;
        var ranked = new List<(string title, string effect, double score, int loss)>();
        foreach (var o in options)
        {
            var effect = Plain(o.Description);
            double score = 0;
            int loss = 0;
            var clauses = effect.Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (clauses.Length == 0) return null;
            foreach (var clause in clauses)
            {
                var m = Regex.Match(clause, @"^(Gain|Heal|Lose|Pay|Take) (\d+) (Gold|Max HP|HP|damage)$", RegexOptions.IgnoreCase);
                if (m.Success && int.TryParse(m.Groups[2].Value, out int n))
                {
                    switch ((m.Groups[1].Value.ToLowerInvariant(), m.Groups[3].Value.ToLowerInvariant()))
                    {
                        case ("gain", "gold"): score += n / (gold < 150 ? 7d : 10d); break;
                        case ("lose" or "pay", "gold"): score -= n > gold ? 10000 : n / 8d; break;
                        case ("gain", "max hp"): score += n * 1.65; break;
                        case ("heal", "hp"): score += Math.Min(maxHp - hp, n) * (hp * 2 < maxHp ? 2 : 1); break;
                        case ("take", "damage"): case ("lose", "hp"): loss += n; break;
                        default: return null;
                    }
                }
                else if (clause.Equals("Obtain the Sword of Stone", StringComparison.OrdinalIgnoreCase)
                    || (o.Relic != null && clause.Equals("Obtain " + Plain(o.Relic), StringComparison.OrdinalIgnoreCase))
                    || clause.Equals("Obtain a random Relic", StringComparison.OrdinalIgnoreCase)) score += 9;
                else if (clause.Equals("Upgrade a card in your Deck", StringComparison.OrdinalIgnoreCase)) score += 8;
                else if (clause.Equals("Transform a card in your Deck", StringComparison.OrdinalIgnoreCase)) score += 6;
                else if (!clause.Equals("Leave", StringComparison.OrdinalIgnoreCase)) return null;
            }
            score -= loss >= hp ? 10000 : loss * (hp - loss < maxHp * .4 ? 2.3 : .55);
            ranked.Add((Plain(o.Title), effect, score, loss));
        }
        var order = ranked.OrderByDescending(x => x.score).ToArray();
        var pick = order[0]; var next = order[1];
        string reason = next.loss > 0 && pick.loss == 0
            ? $"{pick.effect.TrimEnd('.')}. {next.title} would leave you at {Math.Max(0, hp - next.loss)}/{maxHp} HP."
            : pick.loss > 0 ? $"{pick.effect.TrimEnd('.')}. You’d still have {Math.Max(0, hp - pick.loss)}/{maxHp} HP."
            : $"{pick.effect.TrimEnd('.')}. The alternative: {next.effect}";
        return explicitPick ? $"{pick.title}. {reason}" : reason;
    }
}
