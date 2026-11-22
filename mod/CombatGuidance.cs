namespace Auditor;

// Pure formatting and precondition helpers; callers supply observed facts only.
internal static class CombatGuidance
{
    internal static bool Boundary(string effect) => effect is "hand-upgrade" or "exhaust-choice" or "uncertain-draw";

    // Bounded deck-fit estimates, not simulated future draws or guaranteed damage.
    internal static (double fit, string? reason) Setup(Card card, int amount, Card[] deck, Foe[] foes)
    {
        if (card.ForeignModel || amount <= 0) return (0, null);
        int shots = deck.Count(c => c.CombatHas("shots"));
        double cap = foes.Length > 0 && foes.All(f => f.DamageCap.HasValue) ? .65 : 1;
        return card.Id switch
        {
            "HONING" when shots > 0 => (Math.Min(16, amount / 10d * Math.Min(3, shots)) * cap,
                $"Honing boosts later Arrows by {amount}% before enemy damage caps."),
            "BRACE" when shots > 0 => (Math.Min(4, amount) * cap, "Spend remaining energy on Aim for the next Arrow."),
            "INFLAME" or "DEMON_FORM" when deck.Any(c => c.Attack) =>
                (Math.Min(12, amount * 1.5 * deck.Count(c => c.Attack)) * cap, "Strength improves later attacks; multi-hit attacks benefit on each hit."),
            "FOOTWORK" when deck.Any(c => c.CombatHas("guard")) =>
                (Math.Min(12, amount * 1.5 * deck.Count(c => c.CombatHas("guard"))), "Dexterity improves later block cards."),
            "NOXIOUS_FUMES" => (Math.Min(14, amount * 2 * foes.Length), "Poison starts on later turns and affects every surviving enemy."),
            _ => (0, null)
        };
    }

    internal static string Risk(TurnLine line, Foe[] foes, int hp, bool detailed)
    {
        bool lethal = line.Incoming + line.HpCost >= hp;
        string text = lethal ? $"Danger: estimated lethal ({line.Incoming + line.HpCost} HP loss vs {hp} HP)." :
            line.Incoming == 0 ? "No attack damage expected this turn." : $"Expect {line.Incoming} damage after block.";
        if (detailed && line.RawIntent > 0)
            text += $" Intent {line.RawIntent}" + (line.RemovedIntent > 0 ? $" − {line.RemovedIntent} from kills" : "")
                + (line.WeakReduction > 0 ? $" − {line.WeakReduction} from Weak" : "") + $" − {line.ExpectedBlock} block.";
        if (line.HpCost > 0) text += $" Cards cost {line.HpCost} HP.";
        if (foes.Any(f => f.Illusion)) text += " Watch for a returning Illusion.";
        return text;
    }

    // Use stable per-instance references, not names or current hand indices.
    // Recompute every manual request; use this only to retire a displayed line,
    // never as the automatic once-per-turn delivery key.
    internal static string Revision(string turn, IEnumerable<string> handInstances,
        IEnumerable<string> targetInstances, int energy, int stars, int arrows, int block, int hp, bool picker)
        => System.Text.Json.JsonSerializer.Serialize(new
        {
            turn,
            hand = handInstances.ToArray(),
            targets = targetInstances.ToArray(),
            energy,
            stars,
            arrows,
            block,
            hp,
            picker
        });
}
