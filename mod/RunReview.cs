using System.Text;
namespace Auditor;

internal record FloorResult(int Act, int Floor, string Room, int Damage, int Healed, int Used, int Spent, int Gained, int Removed, int Upgraded, int Turns, int? StartupHpGrant = null);
internal static class RunReview
{
    internal static string Write(Deck deck, FloorResult[] path, bool won, bool abandoned, string? killer, int ascension, int gold, string[] potions, string? journalAssessment = null)
    {
        var s = new StringBuilder();
        var lines = Archetypes.Analyze(deck);
        var worst = path.OrderByDescending(f => f.Damage).FirstOrDefault();
        s.AppendLine($"{deck.Character} · Ascension {ascension} · {(abandoned ? "Abandoned" : won ? "Victory" : "Defeat")}");
        s.AppendLine($"{path.Length} recorded floors · {deck.Cards.Length} cards · {deck.Relics.Length} relics · {gold} gold at finish");
        s.AppendLine("\nDECK IDENTITY");
        if (lines.Length == 0) s.AppendLine("No supported archetype had a clear signal. A flexible deck is a valid outcome.");
        foreach (var l in lines.Take(3))
        {
            var names = "sources: " + string.Join(", ", l.Sources.Select(x => x.Name).Distinct().Take(3))
                + "; payoffs: " + string.Join(", ", l.Payoffs.Select(x => x.Name).Distinct().Take(3));
            s.AppendLine($"{l.Line.Name} — {(l.Online ? "supported" : l.Payoffs.Length == 0 ? "possible direction; no tagged payoff" : "payoff without a tagged enabler")}: {names}.");
        }
        s.AppendLine($"{deck.Cards.Count(c => c.Has("guard"))} direct block cards · {deck.Cards.Count(c => c.Has("draw"))} draw cards · {deck.Cards.Count(c => c.Up)} upgraded.");
        s.AppendLine("\nHOW THE RUN UNFOLDED");
        foreach (var act in path.GroupBy(f => f.Act))
            s.AppendLine($"Act {act.Key}: {act.Count()} floors; {act.Sum(f => f.Damage)} damage taken, {act.Sum(f => f.Healed)} healed, {act.Sum(f => f.Used)} potions used.");
        s.AppendLine($"{path.Sum(f => f.Gained)} cards gained, {path.Sum(f => f.Removed)} removed, {path.Sum(f => f.Upgraded)} upgrades recorded; {path.Sum(f => f.Spent)} gold spent.");
        s.AppendLine("\nPER-FLOOR HISTORY");
        foreach (var f in path)
        {
            s.AppendLine($"A{f.Act}/F{f.Floor} {RoomLabel(f.Room)}: {f.Damage} damage, {f.Healed} game-recorded healing, {f.Used} potions, {f.Spent} gold spent; +{f.Gained}/−{f.Removed} cards, {f.Upgraded} upgrades, {f.Turns} turns.");
            if (f.StartupHpGrant is { } grant) s.AppendLine($"  Identified startup HP grant: {grant}; separate from tactical recovery (included in history healing only if the game records it there).");
        }
        if (path.Any(f => f.Floor == 1 && f.Healed > 0 && f.StartupHpGrant == null))
            s.AppendLine("Floor-one healing may include initialization; history alone does not identify a startup grant or tactical recovery.");
        if (worst is { Damage: > 0 }) s.AppendLine($"Largest HP cost: floor {worst.Floor}, {worst.Room} ({worst.Damage} damage recorded).");
        s.AppendLine(abandoned ? "\nWHAT TO PRACTICE" : won ? "\nWHAT TO REPEAT" : "\nLOSS ANALYSIS");
        if (!won && !abandoned) s.AppendLine(killer == null ? "The game did not record a killing encounter or event." : $"Recorded cause: {killer}.");
        if (path.LastOrDefault() is { } end) s.AppendLine($"Last floor: {end.Room}; {end.Damage} damage taken, {end.Used} potions used" + (end.Turns > 0 ? $", {end.Turns} turns." : "."));
        s.AppendLine(potions.Length > 0 ? $"Potions at finish: {string.Join(", ", potions)}." : "No potions remained at the finish.");
        s.AppendLine("\nNEXT-RUN COACHING");
        if (!won && !abandoned && potions.Length > 0 && worst is { Damage: > 0 })
            s.AppendLine($"• Before ending a high-damage turn, check your belt. You finished with {potions.Length} potion(s); history cannot establish whether they would have prevented the loss.");
        if (lines.FirstOrDefault(x => x.Online) is { } paired)
            s.AppendLine($"• Keep the {paired.Line.Name} pairing in mind: {paired.Sources[0].Name} plus {paired.Payoffs.FirstOrDefault(p => p.Id != paired.Sources[0].Id)?.Name ?? paired.Payoffs[0].Name}. Add reliability before another payoff.");
        else if (lines.FirstOrDefault(x => x.Payoffs.Length > 0 && x.Sources.Length == 0) is { } unsupported)
            s.AppendLine($"• Before another {unsupported.Line.Name} payoff, look for a reliable source of {unsupported.Line.Role}.");
        if (deck.Cards.Count(c => c.Has("guard")) * 4 < deck.Cards.Length && !won)
            s.AppendLine("• Direct block was a small part of the final deck. Before the next elite, check how an ordinary hand handles its first attack; Weak, summons and orbs can also cover damage.");
        else if (worst is { Damage: > 0 })
            s.AppendLine($"• Use {worst.Room} as a practice fight: plan for its first dangerous turn before spending energy on setup.");
        else s.AppendLine("• At each reward, name one immediate problem the card solves before taking it over Skip.");
        if (!string.IsNullOrWhiteSpace(journalAssessment)) s.AppendLine("\n" + journalAssessment);
        return s.ToString();
    }
    static string RoomLabel(string room)
    {
        var text = room.Replace('_', ' ').Replace("Normal", "regular fight").Replace("RestSite", "Rest site").Replace("Merchant", "Shop");
        return System.Text.RegularExpressions.Regex.Replace(text, "(?<=[a-z])(?=[A-Z])", " ");
    }
}
