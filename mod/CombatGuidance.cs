namespace Auditor;

// Pure formatting and precondition helpers; callers supply observed facts only.
internal static class CombatGuidance
{
    internal static bool Boundary(string effect) => effect is "hand-upgrade" or "exhaust-choice" or "uncertain-draw";

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
