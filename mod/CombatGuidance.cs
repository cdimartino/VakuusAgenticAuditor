namespace Auditor;

// Pure formatting and precondition helpers; callers supply observed facts only.
internal static class CombatGuidance
{
    internal static bool Boundary(string effect) => effect is "hand-upgrade" or "exhaust-choice" or "uncertain-draw";

    internal static string Risk(TurnLine line, Foe[] foes, int hp, bool detailed)
    {
        bool lethal = line.Incoming + line.HpCost >= hp;
        string text = lethal ? "Danger: the shown line is lethal. Recheck defenses or potions. " : "";
        text += detailed
            ? $"Estimate: {line.RawIntent} current intent − {line.RemovedIntent} from projected kills − {line.WeakReduction} from new Weak − {line.ExpectedBlock} block = {line.Incoming} damage against {hp} HP."
            : $"Estimated shown line: {line.Incoming} damage against {hp} HP.";
        if (line.HpCost > 0) text += $" Cards also cost {line.HpCost} HP.";
        if (foes.Any(f => f.Strength > 0)) text += " Visible Strength: zero damage now does not mean a safe fight.";
        if (foes.Any(f => f.Illusion)) text += " Illusion: a projected kill may only remove the current attack, not the returning threat.";
        if (line.Recheck) text += " Recheck after the hand-changing effect; this is not an end-turn guarantee.";
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
