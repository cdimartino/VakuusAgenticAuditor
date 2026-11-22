namespace Auditor;

internal static class BlitzMode
{
    internal static bool Active(string? exactBuild, string mode, string network, int players, bool custom = false) =>
        exactBuild == "26660232" && mode == "Blitz" && network == "Singleplayer" && players == 1 && !custom;

    internal static string? Difficulty(int level)
    {
        string[] levels = ["Weary Traveler", "Tight Belt", "Tough Enemies", "Deadly Enemies", "Ascender’s Bane"];
        return level is < 0 or > 5 ? null : level == 0 ? "Blitz 0: no Ascension effects."
            : $"Blitz {level}: {string.Join(", ", levels.Take(level))}.";
    }

    internal static string? Route(int actIndex, IReadOnlyList<string> remaining)
    {
        if (actIndex < 0 || actIndex > 2) return null;
        if (remaining.Count == 0)
            return actIndex < 2 ? $"End of Act {actIndex + 1}'s route; the next act follows." : "End of the final act’s route.";
        string? Label(string kind) => kind switch
        {
            "Ancient" => "Ancient",
            "Monster" => "fight",
            "Shop" => "merchant",
            "Elite" => "elite",
            "Treasure" => "chest",
            "RestSite" => "camp",
            "Boss" => "boss",
            _ => null
        };
        var labels = remaining.Select(Label).ToArray();
        return labels.Any(x => x == null) ? null : $"Act {actIndex + 1} · fixed route ahead: {string.Join(" → ", labels)}.";
    }

    internal static string[]? Remaining<T>(T node, Func<T, IEnumerable<T>> children, Func<T, string> kind) where T : class
    {
        var seen = new HashSet<T>(ReferenceEqualityComparer.Instance) { node };
        var path = new List<string>();
        while (true)
        {
            using var it = children(node).GetEnumerator();
            if (!it.MoveNext()) return path.ToArray();
            node = it.Current;
            if (it.MoveNext() || !seen.Add(node)) return null;
            path.Add(kind(node));
        }
    }

    // Count remaining buttons rather than reconstructing the reward set: relics, modifiers,
    // claimed rows and external mods may all change what the player can take.
    internal static string? Rewards(string room, bool weak, int act, int cards, int potions, int relics)
    {
        if (act is < 0 or > 2 || room is not ("Monster" or "Elite" or "Boss") || cards + potions + relics == 0)
            return null;
        var parts = new List<string>();
        if (cards > 0)
        {
            string? rarity = room switch { "Elite" => "uncommon", "Monster" => "common", "Boss" when act < 2 => "rare", _ => null };
            if (rarity != null)
            {
                parts.Add($"Base {room.ToLowerInvariant()} card offer: {rarity}.");
                if (room != "Boss" && act > 0)
                    parts.Add($"Blitz attempts to upgrade up to {act} option{(act == 1 ? "" : "s")}; compare the cards as shown.");
            }
        }
        if (room == "Elite" && relics > 0)
            parts.Add(relics == 1 ? "One relic reward remains." : $"{relics} relic rewards remain; they are separate claims.");
        if (room == "Monster" && !weak && potions > 0) parts.Add("Check the potion against your belt before leaving.");
        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    internal static string? Treasure(int enabled) => enabled switch
    {
        2 => "This solo Blitz chest offers two relic picks, one at a time. Taking the first needn’t cost you the second.",
        1 => "One relic is available now. Inspect its effect before leaving the chest.",
        _ => null
    };

    internal static string? Encounter(string id, string move) =>
        id == "GREMLIN_MERC" && move is "GIMME_MOVE" or "DOUBLE_SMASH_MOVE" or "HEHE_MOVE"
            ? "The attack still hits, but this gremlin does not steal gold in Blitz." : null;

    internal static string? Camp(bool heal, bool smith, int hp, int max, int amount, int legalUpgrades, int bossesAhead)
    {
        if (!heal && !smith || bossesAhead is < 1 or > 2) return null;
        int gain = Math.Clamp(amount, 0, Math.Max(0, max - hp));
        var boss = bossesAhead == 2 ? "Two bosses ahead without another camp." : "Boss next.";
        var choices = new List<string>();
        if (heal) choices.Add(gain == 0 ? "Rest adds no HP." : $"Rest: about {hp} → {hp + gain} HP.");
        if (smith && legalUpgrades > 0)
            choices.Add($"Smith: {legalUpgrades} eligible card{(legalUpgrades == 1 ? "" : "s")}.");
        if (choices.Count == 0) return null;
        if (heal && gain > 0 && hp * 2 < max && smith) choices.Add("Favor the heal at this health.");
        return $"{boss} {string.Join(" ", choices)}";
    }
}
