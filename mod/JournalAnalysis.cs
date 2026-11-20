using System.Text;
using System.Text.Json;
namespace Auditor;

internal static class JournalAnalysis
{
    internal static string Write(IEnumerable<JournalEntry> records, bool incomplete = false)
    {
        var entries = records.Where(e => e.Schema == RunJournal.SchemaVersion).ToArray();
        var s = new StringBuilder("RECORDED STATE EVIDENCE\n");
        var states = new Dictionary<(string, long), JournalState>();
        foreach (var e in entries.Where(e => e.Kind == "state"))
            try { if (e.Data.Deserialize<JournalState>() is { } state) states.TryAdd((e.RunId, e.Sequence), state); } catch (JsonException) { }
        s.AppendLine($"{states.Count} state snapshots; {entries.Count(e => e.Kind == "advice")} delivered recommendations; {entries.Count(e => e.Kind == "observed-action")} explicitly observed actions.");
        s.AppendLine("State transitions show changed facts, not proof of which action caused them.");
        s.AppendLine("HP changes across combat boundaries cannot validate a combat forecast; net HP deltas are not damage attribution.");
        var advice = new Dictionary<(string, long), JournalAdvice>();
        foreach (var e in entries.Where(e => e.Kind == "advice"))
            try { if (e.Data.Deserialize<JournalAdvice>() is { } a) advice[(e.RunId, a.StateSequence)] = a; } catch (JsonException) { }
        var drops = new List<(JournalEntry Entry, JournalTransition Transition, JournalState From, JournalState To)>();
        var seen = new HashSet<(string, long, long)>();
        var transitions = 0;
        long lost = 0, gained = 0;
        foreach (var e in entries.Where(e => e.Kind == "transition"))
        {
            try
            {
                if (e.Data.Deserialize<JournalTransition>() is { } t && t.FromSequence < t.ToSequence
                    && seen.Add((e.RunId, t.FromSequence, t.ToSequence))
                    && states.TryGetValue((e.RunId, t.FromSequence), out var a) && states.TryGetValue((e.RunId, t.ToSequence), out var b))
                {
                    transitions++;
                    lost += Math.Max(0, a.Hp - b.Hp);
                    gained += Math.Max(0, b.Hp - a.Hp);
                    if (a.Hp > b.Hp) drops.Add((e, t, a, b));
                }
            }
            catch (JsonException) { }
        }
        s.AppendLine($"{transitions} linked transitions; summed observed net HP drops {lost}, gains {gained} (not history damage/healing totals; overlapping endpoints may double-count).");
        s.AppendLine("LARGEST OBSERVED HP DROPS (up to 8; recent first on ties)");
        foreach (var d in drops.OrderByDescending(d => d.From.Hp - d.To.Hp).ThenByDescending(d => d.Entry.Utc).ThenByDescending(d => d.Entry.Sequence).Take(8))
        {
            var a = d.From; var b = d.To;
            var boundary = a.Combat == null || a.Combat != b.Combat || a.Floor != b.Floor || a.Act != b.Act;
            s.AppendLine($"• A{a.Act}/F{a.Floor}/T{a.Turn} {Clip(a.Phase)} → A{b.Act}/F{b.Floor}/T{b.Turn} {Clip(b.Phase)}: HP {a.Hp}→{b.Hp} (drop {a.Hp - b.Hp}); block {a.Block}→{b.Block}." + (boundary ? " Boundary/gap: not forecast-comparable." : ""));
            if (advice.TryGetValue((d.Entry.RunId, d.Transition.FromSequence), out var delivered))
                s.AppendLine($"  Advice at source snapshot: {Clip(delivered.DeliveredText)}");
        }
        var last = entries.LastOrDefault(e => e.Kind == "state" && states.ContainsKey((e.RunId, e.Sequence)));
        if (last != null)
        {
            var state = states[(last.RunId, last.Sequence)];
            s.AppendLine($"LAST RECORDED STATE: A{state.Act}/F{state.Floor}/T{state.Turn} {Clip(state.Phase)}, HP {state.Hp}/{state.MaxHp}, block {state.Block}, energy {state.Energy}; {Clip(state.Encounter ?? state.Room)}.");
            var lastAdvice = entries.LastOrDefault(e => e.RunId == last.RunId && e.Kind == "advice");
            if (lastAdvice != null)
                try
                {
                    if (lastAdvice.Data.Deserialize<JournalAdvice>() is { } a && states.TryGetValue((last.RunId, a.StateSequence), out var source))
                        s.AppendLine($"Last delivered advice (A{source.Act}/F{source.Floor}/T{source.Turn} {Clip(source.Phase)}, HP {source.Hp}; may be stale): {Clip(a.DeliveredText)}");
                }
                catch (JsonException) { }
        }
        var forecastLines = new List<string>();
        foreach (var e in entries.Where(e => e.Kind == "forecast"))
        {
            try
            {
                var f = e.Data.Deserialize<JournalForecast>();
                if (f == null) continue;
                if (!f.Comparable || f.StartSequence >= f.EndSequence || !states.TryGetValue((e.RunId, f.StartSequence), out var a) || !states.TryGetValue((e.RunId, f.EndSequence), out var b)
                    || a.Combat == null || a.Combat != b.Combat || a.Floor != b.Floor || a.Act != b.Act)
                { forecastLines.Add($"{Clip(f.DecisionId)}: forecast unresolved; endpoints/preconditions not comparable. {Clip(f.Limitation ?? "")}"); continue; }
                var loss = a.Hp - b.Hp;
                forecastLines.Add($"{Clip(f.DecisionId)}: predicted HP loss {f.ExpectedHpLoss}; observed net HP change {loss}" + (loss != f.ExpectedHpLoss ? " — prediction mismatch (not a causal diagnosis)." : " — matched."));
            }
            catch (JsonException) { }
        }
        s.AppendLine($"{forecastLines.Count} recorded forecast assessments (showing last up to 8):");
        foreach (var line in forecastLines.TakeLast(8)) s.AppendLine(line);
        s.AppendLine(incomplete ? "Journal incomplete: retention, storage failure or recording gaps limit conclusions." : "Only recorded endpoints are assessed; unrecorded turns and counterfactual survival are unknown.");
        return s.ToString();
    }
    static string Clip(string text)
    {
        var flat = text.Replace('\r', ' ').Replace('\n', ' ');
        return flat.Length > 200 ? flat[..200] + "…" : flat;
    }
}
