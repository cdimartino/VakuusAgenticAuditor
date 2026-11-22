namespace Auditor;

internal readonly record struct Status(int Weak, int Vulnerable, int Artifact);
internal record Foe(string Name, int Life, int Intent, Status Status = default, int Strength = 0, bool Illusion = false,
    int? DamageCap = null, int Block = 0);
// Damage is a per-enemy, per-hit preview; -1 means this enemy isn't a legal target.
internal record Play(string Name, int Energy, int Stars, int Arrows, int Guard, int[] Damage,
    bool Area = false, bool Pivot = false, bool Setup = false, bool Ex = false, bool Sx = false,
    int Weak = 0, int Vulnerable = 0, int Sadness = 0, bool Converts = false, bool Skill = false,
    string HitsFrom = "", bool Attack = false, int HpLoss = 0, bool BlockDamage = false,
    double[]? BlockMultipliers = null, string Instance = "", int Hits = 1, double FutureFit = 0, string? FutureReason = null, bool DrawBoundary = false,
    int EnemyHpLoss = 0, bool OstyAttack = false, bool FreeAfterOsty = false);
internal record Step(int Card, int Target);
internal record TurnLine(List<Step> Steps, int Incoming, int EnemiesLeft, double Score, int Joy = 0, int[]? Remaining = null,
    int RawIntent = 0, int ExpectedBlock = 0, int RemovedIntent = 0, int WeakReduction = 0, int HpCost = 0, bool Recheck = false, string? SetupReason = null);

internal static class TurnCoach
{
    // HARD_TO_KILL_POWER caps each damage event, after modifiers. Counter
    // stacks change the cap; hits do not consume it. Null means no observed cap.
    internal static int TargetDamage(Foe foe, int perHit, int hits = 1) =>
        Math.Min(Math.Max(0, perHit), foe.DamageCap is int cap ? Math.Max(0, cap) : int.MaxValue) * Math.Max(0, hits);

    // Bounded beam search. Stop at draws/setup plays: the hand or costs may change.
    record State(int Energy, int Stars, int Arrows, int Block, int[] Life, ulong Used, TurnLine Line,
        Status[] Status, int Sadness, int Created, int Attacks, int Skills, int PaidHp, double Economy, int[] EnemyBlock, bool OstyAttacked = false);
    public static TurnLine Plan(Play[] hand, Foe[] foes, int energy, int stars, int arrows, int block, int hp,
        int sadness = 0, int attacks = 0, int skills = 0, int creationBlock = 0, int handSize = 0)
    {
        int incoming = Math.Max(0, foes.Sum(f => f.Intent) - block);
        var best = new TurnLine([], incoming, foes.Length, 0, RawIntent: foes.Sum(f => f.Intent), ExpectedBlock: block);
        var front = new List<State> {new(energy,stars,arrows,block,foes.Select(f=>f.Life).ToArray(),0,best,
             foes.Select(f=>f.Status).ToArray(),sadness,0,attacks,skills,0,0,foes.Select(f=>f.Block).ToArray())};
        var setups = hand.Take(63).Select((play, index) => (play, index))
            .Where(x => x.play.Setup && !x.play.DrawBoundary && x.play.FutureFit > 0 && !x.play.Ex && !x.play.Sx)
            .OrderByDescending(x => x.play.FutureFit).ToArray();
        for (int depth = 0; depth < Math.Min(hand.Length, 5); depth++)
        {
            var next = new List<State>();
            foreach (var s in front)
                for (int i = 0; i < Math.Min(hand.Length, 63); i++)
                {
                    var c = hand[i]; ulong bit = 1UL << i;
                    int cost = c.FreeAfterOsty && s.OstyAttacked ? 0 : c.Energy;
                    int paid = s.PaidHp + c.HpLoss;
                    if ((s.Used & bit) != 0 || cost > s.Energy || c.Stars > s.Stars || c.Arrows > s.Arrows || (c.Ex && c.FutureFit > 0 && s.Energy == 0) || paid >= hp) continue;
                    bool targeted = !c.Area && c.Damage.Any(n => n >= 0);
                    for (int t = 0; t < (targeted ? foes.Length : 1); t++)
                    {
                        if (targeted && (s.Life[t] <= 0 || c.Damage[t] < 0)) continue;
                        var life = (int[])s.Life.Clone(); var status = (Status[])s.Status.Clone();
                        var enemyBlock = (int[])s.EnemyBlock.Clone();
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
                                int damage = TargetDamage(foes[j], hit, c.Hits * repeats);
                                life[j] = Math.Max(0, life[j] - damage);
                                enemyBlock[j] = Math.Max(0, enemyBlock[j] - damage);
                                // Life includes starting Block. Direct HP loss bypasses it,
                                // and is not attack damage (Weak/Vulnerable do not multiply it).
                                if (c.EnemyHpLoss > 0)
                                {
                                    int loss = TargetDamage(foes[j], c.EnemyHpLoss);
                                    life[j] = loss >= Math.Max(0, life[j] - enemyBlock[j])
                                        ? 0 : life[j] - loss;
                                }
                                if (c.Weak > 0) z = z.Artifact > 0 ? z with { Artifact = z.Artifact - 1 } : z with { Weak = z.Weak + c.Weak };
                                if (c.Vulnerable > 0) z = z.Artifact > 0 ? z with { Artifact = z.Artifact - 1 } : z with { Vulnerable = z.Vulnerable + c.Vulnerable };
                                status[j] = z;
                            }
                            dmg += foes[j].Life - life[j] - (life[j] == 0 ? enemyBlock[j] : 0);
                            if (life[j] > 0)
                            {
                                alive++; threat += z.Weak > 0 && initial.Weak == 0 ? foes[j].Intent * 3 / 4 : foes[j].Intent;
                                // One-turn Weak on a non-attacking enemy expires
                                // before its next attack: it has no future mitigation.
                                future += Math.Min(2, Math.Max(0, z.Weak - 1)) - Math.Min(2, Math.Max(0, initial.Weak - 1))
                                   + Math.Min(2, z.Vulnerable) - Math.Min(2, initial.Vulnerable);
                            }
                        }
                        int added = Math.Min(c.Sadness, Math.Max(0, 10 - (handSize - depth - 1 + s.Created)));
                        bool convert = c.Converts && s.Sadness > 0;
                        int sad = s.Sadness + added - (convert ? 1 : 0), joy = s.Line.Joy + (convert ? 1 : 0);
                        int b = s.Block + c.Guard + added * creationBlock, gap = Math.Max(0, threat - b);
                        double score = dmg * .75 + (incoming - gap) * (incoming >= hp ? 4 : 1.65) + (foes.Length - alive) * 5
                            + (alive == 0 ? 1000 : 0) + (incoming >= hp && gap < hp ? 100 : 0)
                            + (c.Pivot && alive > 0 ? (c.Setup ? 4 : 2) + Math.Max(0, s.Energy - cost) * .65 : 0)
                            + (alive > 0 ? future + joy * 4 - (sad - sadness) * .7 : 0) - .04 * (depth + 1)
                            - paid * 1.65 - (alive > 0 && gap < hp && gap + paid >= hp ? 110 : 0);
                        string? setupReason = s.Line.SetupReason;
                        double economy = s.Economy;
                        if (alive > 0 && gap + paid < hp)
                        {
                            // Option value only: never add future buffs to current block, kills or damage.
                            double horizon = Math.Clamp(life.Sum() / 32d, .15, 1);
                            double futureWeight = horizon / (1 + gap / 10d);
                            if (c.FutureReason != null)
                            {
                                double units = c.Ex ? s.Energy : 1;
                                economy += c.FutureFit * units;
                                setupReason = c.FutureReason;
                            }
                            if (c.DrawBoundary && !c.Ex && !c.Sx)
                            {
                                // Keep a known setup available, but stop before actually prescribing it after a draw.
                                int left = s.Energy - cost;
                                var reserve = setups.FirstOrDefault(x => x.index != i && (s.Used & (1UL << x.index)) == 0
                                    && x.play.Energy <= left && x.play.Stars <= s.Stars - c.Stars && x.play.Arrows <= s.Arrows - c.Arrows
                                    && x.play.HpLoss + gap + paid < hp).play;
                                if (reserve != null)
                                {
                                    score += reserve.FutureFit * .8 * futureWeight;
                                    setupReason = $"Recheck the draw; {left} energy remains for {reserve.Name}.";
                                }
                            }
                            score += economy * futureWeight;
                        }
                        // Prefer unlocking a draw earlier when the projected outcome
                        // ties: seeing another card before committing energy has value.
                        if (joy > 0 && alive > 0) score += .01 * (hand.Length - (s.Line.Joy == 0 ? depth :
                            s.Line.Steps.FindIndex(step => hand[step.Card].Converts)));
                        int removed = Enumerable.Range(0, life.Length).Where(j => life[j] == 0).Sum(j => foes[j].Intent);
                        var line = new TurnLine([.. s.Line.Steps, new(i, targeted ? t : -1)], gap, alive, score, joy, life,
                            foes.Sum(f => f.Intent), b, removed, foes.Sum(f => f.Intent) - removed - threat, paid,
                            c.Pivot || convert, setupReason);
                        bool survives = alive == 0 || gap + paid < hp;
                        bool bestSurvives = best.EnemiesLeft == 0 || best.Incoming + best.HpCost < hp;
                        if (survives && !bestSurvives || survives == bestSurvives && score > best.Score) best = line;
                        if (alive > 0 && !c.Pivot && !convert) next.Add(new(c.Ex ? 0 : s.Energy - cost, c.Sx ? 0 : s.Stars - c.Stars, s.Arrows - c.Arrows, b, life, s.Used | bit, line,
                             status, sad, s.Created + added, s.Attacks + (c.Attack ? 1 : 0), s.Skills + (c.Skill ? 1 : 0), paid, economy, enemyBlock, s.OstyAttacked || c.OstyAttack));
                    }
                }
            front = next.OrderByDescending(x => x.Line.Score).DistinctBy(x => (x.Used, x.Block, x.Energy, x.Stars, x.Sadness, x.Line.Joy, string.Join(',', x.Life), string.Join(',', x.EnemyBlock), string.Join(';', x.Status.Select(s => $"{s.Weak}/{s.Vulnerable}/{s.Artifact}")))).Take(24).ToList();
            if (front.Count == 0) break;
        }
        return best;
    }
}
