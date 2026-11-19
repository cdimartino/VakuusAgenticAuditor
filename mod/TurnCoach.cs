namespace Auditor;

internal readonly record struct Status(int Weak, int Vulnerable, int Artifact);
internal record Foe(string Name, int Life, int Intent, Status Status = default, int Strength = 0, bool Illusion = false);
// Damage is a per-enemy preview; -1 means this enemy isn't a legal target.
internal record Play(string Name, int Energy, int Stars, int Arrows, int Guard, int[] Damage,
    bool Area = false, bool Pivot = false, bool Setup = false, bool Ex = false, bool Sx = false,
    int Weak = 0, int Vulnerable = 0, int Sadness = 0, bool Converts = false, bool Skill = false,
    string HitsFrom = "", bool Attack = false, int HpLoss = 0, bool BlockDamage = false,
    double[]? BlockMultipliers = null, string Instance = "");
internal record Step(int Card, int Target);
internal record TurnLine(List<Step> Steps, int Incoming, int EnemiesLeft, double Score, int Joy = 0, int[]? Remaining = null,
    int RawIntent = 0, int ExpectedBlock = 0, int RemovedIntent = 0, int WeakReduction = 0, int HpCost = 0, bool Recheck = false);

internal static class TurnCoach
{
    // Bounded beam search. Stop at draws/setup plays: the hand or costs may change.
    record State(int Energy, int Stars, int Arrows, int Block, int[] Life, ulong Used, TurnLine Line,
        Status[] Status, int Sadness, int Created, int Attacks, int Skills, int PaidHp);
    public static TurnLine Plan(Play[] hand, Foe[] foes, int energy, int stars, int arrows, int block, int hp,
        int sadness = 0, int attacks = 0, int skills = 0, int creationBlock = 0, int handSize = 0)
    {
        int incoming = Math.Max(0, foes.Sum(f => f.Intent) - block);
        var best = new TurnLine([], incoming, foes.Length, 0, RawIntent: foes.Sum(f => f.Intent), ExpectedBlock: block);
        var front = new List<State> {new(energy,stars,arrows,block,foes.Select(f=>f.Life).ToArray(),0,best,
            foes.Select(f=>f.Status).ToArray(),sadness,0,attacks,skills,0)};
        for (int depth = 0; depth < Math.Min(hand.Length, 5); depth++)
        {
            var next = new List<State>();
            foreach (var s in front)
                for (int i = 0; i < Math.Min(hand.Length, 63); i++)
                {
                    var c = hand[i]; ulong bit = 1UL << i;
                    int paid = s.PaidHp + c.HpLoss;
                    if ((s.Used & bit) != 0 || c.Energy > s.Energy || c.Stars > s.Stars || c.Arrows > s.Arrows || paid >= hp) continue;
                    bool targeted = !c.Area && c.Damage.Any(n => n >= 0);
                    for (int t = 0; t < (targeted ? foes.Length : 1); t++)
                    {
                        if (targeted && (s.Life[t] <= 0 || c.Damage[t] < 0)) continue;
                        var life = (int[])s.Life.Clone(); var status = (Status[])s.Status.Clone();
                        int dmg = 0, threat = 0, alive = 0; double future = 0;
                        for (int j = 0; j < life.Length; j++)
                        {
                            var z = status[j]; var initial = foes[j].Status;
                            if (c.Area || (targeted && t == j))
                            {
                                // Damage previews already include the current board. Apply
                                // only modifiers newly introduced by this candidate line.
                                // Body Slam's base is the block at this point in the line,
                                // not the starting-board preview (which can be zero).
                                bool newVulnerable = z.Vulnerable > 0 && initial.Vulnerable == 0;
                                // Combine block multipliers before rounding. In particular,
                                // Weak + newly applied Vulnerable must not round twice.
                                int hit = c.BlockDamage
                                    ? (int)Math.Floor(s.Block * (c.BlockMultipliers?[j] ?? (initial.Vulnerable > 0 ? 1.5 : 1))
                                        * (newVulnerable ? 1.5 : 1))
                                    : Math.Max(0, c.Damage[j]);
                                if (!c.BlockDamage && newVulnerable) hit = hit * 3 / 2;
                                int repeats = c.HitsFrom switch { "attacks" => s.Attacks, "skills" => s.Skills, _ => 1 };
                                life[j] = Math.Max(0, life[j] - hit * repeats);
                                if (c.Weak > 0) z = z.Artifact > 0 ? z with { Artifact = z.Artifact - 1 } : z with { Weak = z.Weak + c.Weak };
                                if (c.Vulnerable > 0) z = z.Artifact > 0 ? z with { Artifact = z.Artifact - 1 } : z with { Vulnerable = z.Vulnerable + c.Vulnerable };
                                status[j] = z;
                            }
                            dmg += foes[j].Life - life[j];
                            if (life[j] > 0)
                            {
                                alive++; threat += z.Weak > 0 && initial.Weak == 0 ? foes[j].Intent * 3 / 4 : foes[j].Intent;
                                future += Math.Min(2, z.Weak) - Math.Min(2, initial.Weak)
                                    + Math.Min(2, z.Vulnerable) - Math.Min(2, initial.Vulnerable);
                            }
                        }
                        int added = Math.Min(c.Sadness, Math.Max(0, 10 - (handSize - depth - 1 + s.Created)));
                        bool convert = c.Converts && s.Sadness > 0;
                        int sad = s.Sadness + added - (convert ? 1 : 0), joy = s.Line.Joy + (convert ? 1 : 0);
                        int b = s.Block + c.Guard + added * creationBlock, gap = Math.Max(0, threat - b);
                        double score = dmg * .75 + (incoming - gap) * (incoming >= hp ? 4 : 1.65) + (foes.Length - alive) * 5
                            + (alive == 0 ? 1000 : 0) + (incoming >= hp && gap < hp ? 100 : 0)
                            + (c.Pivot && alive > 0 ? (c.Setup ? 4 : 2) + Math.Max(0, s.Energy - c.Energy) * .65 : 0)
                            + (alive > 0 ? future + joy * 4 - (sad - sadness) * .7 : 0) - .04 * (depth + 1)
                            - paid * 1.65 - (alive > 0 && gap < hp && gap + paid >= hp ? 110 : 0);
                        // Prefer unlocking a draw earlier when the projected outcome
                        // ties: seeing another card before committing energy has value.
                        if (joy > 0 && alive > 0) score += .01 * (hand.Length - (s.Line.Joy == 0 ? depth :
                            s.Line.Steps.FindIndex(step => hand[step.Card].Converts)));
                        int removed = Enumerable.Range(0, life.Length).Where(j => life[j] == 0).Sum(j => foes[j].Intent);
                        var line = new TurnLine([.. s.Line.Steps, new(i, targeted ? t : -1)], gap, alive, score, joy, life,
                            foes.Sum(f => f.Intent), b, removed, foes.Sum(f => f.Intent) - removed - threat, paid,
                            c.Pivot || convert);
                        if (score > best.Score) best = line;
                        if (alive > 0 && !c.Pivot && !convert) next.Add(new(c.Ex ? 0 : s.Energy - c.Energy, c.Sx ? 0 : s.Stars - c.Stars, s.Arrows - c.Arrows, b, life, s.Used | bit, line,
                            status, sad, s.Created + added, s.Attacks + (c.Attack ? 1 : 0), s.Skills + (c.Skill ? 1 : 0), paid));
                    }
                }
            front = next.OrderByDescending(x => x.Line.Score).DistinctBy(x => (x.Used, x.Block, x.Energy, x.Stars, x.Sadness, x.Line.Joy, string.Join(',', x.Life), string.Join(';', x.Status.Select(s => $"{s.Weak}/{s.Vulnerable}/{s.Artifact}")))).Take(24).ToList();
            if (front.Count == 0) break;
        }
        return best;
    }
}
