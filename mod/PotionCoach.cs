namespace Auditor;

internal record Bottle(string Id, string Name);
internal record CombatBottle(string Id, string Name, int Amount, bool[] Targets);
internal record BeltContext(int Energy, int Round, int Size, int Draw, int Discard, int EmptySlots, int MissingHp,
    int Clutter, int Upgrades, int Expensive, int StarShort, int Strikes, bool Orbs, bool Summon, bool Forge, bool Shots, bool Converts);
internal static class PotionCoach
{
    // Marginal value over the card-only line. Keep the belt intact when the
    // hand already wins; reconsider finishers after a draw changes the hand.
    internal static string? Use(CombatBottle[] belt, Foe[] foes, Play[] hand, TurnLine line, int hp, bool majorFight, BeltContext? ctx = null)
    {
        if (line.EnemiesLeft == 0 || belt.Length == 0) return null;
        var remaining = foes.Select(f => f.Life).ToArray();
        int attacks = 0;
        foreach (var step in line.Steps)
        {
            var c = hand[step.Card];
            if (c.Damage.Any(n => n > 0)) attacks++;
            for (int j = 0; j < remaining.Length; j++)
                if (c.Area || step.Target == j) remaining[j] = Math.Max(0, remaining[j] - Math.Max(0, c.Damage[j]));
        }
        var pivot = line.Steps.Count > 0 && hand[line.Steps[^1].Card].Pivot;
        remaining = line.Remaining ?? remaining;
        var choices = new List<(int priority, string hint)>();
        foreach (var b in belt)
        {
            int gap = line.Incoming;
            if (b.Id is "BLOCK_POTION" or "SHIP_IN_A_BOTTLE" && b.Amount > 0 && gap > 0 && (gap >= hp || gap >= Math.Max(4, b.Amount * 2 / 3)))
                choices.Add((gap >= hp ? 90 : 60, $"Use {b.Name} before ending the turn: it covers {Math.Min(b.Amount, gap)} of {gap} unblocked damage."));
            if (b.Id == "GHOST_IN_A_JAR" && (gap >= hp || gap >= 20))
                choices.Add((gap >= hp ? 95 : 70, $"Use {b.Name} before ending the turn. Intangible reduces the incoming hits."));
            if (b.Id is "STRENGTH_POTION" or "FLEX_POTION" && attacks > 0 &&
                (b.Id == "STRENGTH_POTION" ? majorFight : attacks >= 3))
                choices.Add((35, $"Drink {b.Name} before your first attack{(b.Id == "FLEX_POTION" ? " while you can attack repeatedly this turn" : " for lasting Strength in this fight")}."));
            if (!pivot && b.Amount > 0 && b.Id is "FIRE_POTION" or "EXPLOSIVE_AMPOULE" or "POTION_SHAPED_ROCK")
            {
                var kills = Enumerable.Range(0, foes.Length).Where(j => b.Targets[j] && remaining[j] > 0 && remaining[j] <= b.Amount)
                    .OrderByDescending(j => foes[j].Intent).ToArray();
                if (kills.Length == 0) continue;
                bool all = b.Id == "EXPLOSIVE_AMPOULE";
                var hit = all ? kills : [kills[0]];
                int saved = Math.Min(gap, hit.Sum(j => foes[j].Intent));
                bool ends = hit.Length == line.EnemiesLeft;
                if (!ends && saved < 5 && hit.Length < 2) continue;
                var targetLabel = !all && foes.Length > 1 ? $" on {foes[hit[0]].Name}" : "";
                var timing = line.Steps.Count == 0 ? "now" : "after your cards";
                choices.Add((ends ? 110 : saved >= hp ? 100 : 65, $"Use {b.Name}{targetLabel} {timing} to {(ends ? "finish the fight" : hit.Length > 1 ? "finish multiple enemies" : "remove an attacker")}."));
            }
            if (ctx is not { } x || !Catalog.Potions.TryGetValue(b.Id, out var profile)) continue;
            // A normal encounter can be a long, dangerous fight too. Reevaluate
            // against the current line rather than relying on room class alone.
            bool extended = majorFight || foes.Any(f => f.Strength > 0 || f.Illusion)
                || (line.EnemiesLeft > 0 && remaining.Sum() >= 40 && (gap >= 8 || x.MissingHp >= 15));
            bool early = extended && (x.Round <= 2 || foes.Any(f => f.Strength > 0) || gap >= hp / 2);
            bool shortTurn = line.Steps.Count < 2 && !pivot;
            var target = Enumerable.Range(0, foes.Length).Where(i => b.Targets[i] && remaining[i] > 0 && foes[i].Status.Artifact == 0)
                .OrderByDescending(i => foes[i].Intent).FirstOrDefault(-1);
            bool useful = profile[0] switch
            {
                "emergency" => gap >= hp || gap >= 22,
                "heal" => x.MissingHp >= 15 && gap >= hp / 2,
                "regen" => early && x.MissingHp >= 10,
                "permanent" => true,
                "refill" => x.EmptySlots >= 1,
                "triple block" => line.Steps.Any(s => hand[s.Card].Guard >= 5) && gap >= 8,
                "splash" => b.Amount > 0 && hp > b.Amount && foes.All(f => f.Life <= b.Amount),
                "defense" => gap >= 6 && line.Steps.Any(s => hand[s.Card].Guard > 0),
                "buff" => early && attacks > 0,
                "early defense" => early && gap > 0,
                "scaling" => early,
                "shots" => early && x.Shots,
                "summon" => early && x.Summon,
                "forge" => early && x.Forge,
                "orbs" => early && x.Orbs,
                "strikes" => early && x.Strikes > 0,
                "stars" => x.StarShort > 0 && majorFight,
                "debuff" => target >= 0 && foes[target].Status.Weak == 0 && gap >= 8,
                "vulnerable" => target >= 0 && foes[target].Status.Vulnerable == 0 && attacks >= 2 && majorFight,
                "group debuff" => target >= 0 && gap >= 10,
                "debuff scaling" => target >= 0 && early,
                "energy" => x.Expensive > 0 && !pivot && majorFight,
                "refuel" => x.Size < 8 && majorFight && !pivot,
                "draw" => shortTurn && x.Energy > 0 && x.Size < 8 && x.Draw + x.Discard > 0 && (majorFight || gap >= 8),
                "redraw" or "desperate draw" => shortTurn && x.Draw + x.Discard > 0 && gap >= 10 && x.Energy > 0,
                "free cards" => shortTurn && x.Size <= 7 && (gap >= 7 || majorFight),
                "recall" => shortTurn && x.Size < 10 && x.Discard > 0 && majorFight,
                "clutter" => x.Clutter >= 2 && majorFight,
                "upgrades" => early && x.Upgrades >= 3,
                "repeat" or "big attack" => majorFight && line.Steps.Any(s => hand[s.Card].Damage.Any(d => d >= 15)),
                "retain" => x.Expensive >= 2 && extended && x.Draw + x.Discard > 0,
                "joy" => x.Converts && x.Size <= 6 && majorFight,
                _ => false
            };
            if (useful)
            {
                bool aimed = target >= 0 && foes.Length > 1 && profile[0] is "debuff" or "debuff scaling" or "vulnerable";
                string timing = profile[0] switch
                {
                    "regen" => " Delayed healing over future turns; not immediate block.",
                    "retain" => " Saves future hands; not immediate block.",
                    _ => ""
                };
                choices.Add((profile[0] == "emergency" ? 85 : 25, $"{b.Name}{(aimed ? $" on {foes[target].Name}" : "")}: {profile[1]}{timing}"));
            }
        }
        return choices.OrderByDescending(c => c.priority).FirstOrDefault().hint;
    }
    internal static string? Recommend(Bottle[] offers, Bottle[] belt, int capacity, bool giantNext, bool namePick)
    {
        if (offers.Length == 0 || capacity <= 0) return null;
        var block = offers.FirstOrDefault(x => x.Id == "BLOCK_POTION");
        var ampoule = belt.FirstOrDefault(x => x.Id == "EXPLOSIVE_AMPOULE");
        if (giantNext && block != null)
        {
            const string why = "Save the block for Waterfall Giant’s Explode.";
            if (belt.Length < capacity)
                return namePick ? $"Take {block.Name} in the open slot. {why}" : why;
            if (ampoule != null && !belt.Any(x => x.Id == "BLOCK_POTION"))
                return namePick ? $"Discard {ampoule.Name} for {block.Name}. {why}" :
                    "Area damage only hits one target here. Save the block for Waterfall Giant’s Explode.";
        }
        if (offers.Length == 1 && belt.Length < capacity)
            return namePick ? $"Take {offers[0].Name}. You have an open potion slot." : "You can carry this without giving up a potion.";
        return null;
    }
}
