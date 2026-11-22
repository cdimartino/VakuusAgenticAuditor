using System.Text;
namespace Auditor;

internal record FloorResult(int Act, int Floor, string Room, int Damage, int Healed, int Used, int Spent, int Gained, int Removed, int Upgraded, int Turns, int? StartupHpGrant = null,
    int? EndHp = null, int? MaxHp = null, string[]? EventPicks = null, string[]? CampPicks = null, string[]? BeltUsed = null, string[]? PickedCards = null, string[]? BlitzTaken = null, string[]? BlitzPassed = null);
internal static class RunReview
{
    internal static string Write(Deck deck, FloorResult[] path, bool won, bool abandoned, string? killer, int ascension, int gold, string[] potions, string? journalAssessment = null, bool blitz = false)
    {
        var s = new StringBuilder();
        var lines = Archetypes.Analyze(deck);
        var worst = path.OrderByDescending(f => f.Damage).FirstOrDefault();
        s.AppendLine($"{deck.Character}{(blitz ? " · Blitz" : "")} · Ascension {ascension} · {(abandoned ? "Abandoned" : won ? "Victory" : "Defeat")}");
        s.AppendLine($"{path.Length} recorded floors · {deck.Cards.Length} cards · {deck.Relics.Length} relics · {(blitz ? "merchant picks instead of gold" : $"{gold} gold at finish")}");
        s.AppendLine(abandoned ? "\nWHAT TO PRACTICE" : won ? "\nWHAT TO REPEAT" : "\nLOSS ANALYSIS");
        if (!won && !abandoned) s.AppendLine(killer == null ? "The game did not record a killing encounter or event." : $"Recorded cause: {killer}.");
        if (path.LastOrDefault() is { } end) s.AppendLine($"Last floor: {end.Room}; {end.Damage} damage taken, {end.Used} potions used" + (end.Turns > 0 ? $", {end.Turns} turns." : "."));
        s.AppendLine(potions.Length > 0 ? $"Potions at finish: {string.Join(", ", potions)}." : "No potions remained at the finish.");
        if (!won && !abandoned) LossDecisions(s, path, deck);
        s.AppendLine("\nDECK IDENTITY");
        if (lines.Length == 0) s.AppendLine("No supported archetype had a clear signal. A flexible deck is a valid outcome.");
        foreach (var l in lines.Take(3))
        {
            var names = "sources: " + string.Join(", ", l.Sources.Select(x => x.Name).Distinct().Take(3))
                + "; payoffs: " + string.Join(", ", l.Payoffs.Select(x => x.Name).Distinct().Take(3));
            s.AppendLine($"{l.Line.Name} — {(l.Online ? "supported" : l.Payoffs.Length == 0 ? "possible direction; no tagged payoff" : "payoff without a tagged enabler")}: {names}.");
        }
        s.AppendLine($"{deck.Cards.Count(c => c.CombatHas("guard"))} direct block cards · {deck.Cards.Count(c => c.Has("draw"))} draw cards · {deck.Cards.Count(c => c.Up)} upgraded.");
        s.AppendLine("\nHOW THE RUN UNFOLDED");
        foreach (var act in path.GroupBy(f => f.Act))
            s.AppendLine($"Act {act.Key}: {act.Count()} floors; {act.Sum(f => f.Damage)} damage taken, {act.Sum(f => f.Healed)} healed, {act.Sum(f => f.Used)} potions used.");
        s.AppendLine($"{path.Sum(f => f.Gained)} cards gained, {path.Sum(f => f.Removed)} removed, {path.Sum(f => f.Upgraded)} upgrades recorded{(blitz ? "." : $"; {path.Sum(f => f.Spent)} gold spent.")}");
        s.AppendLine("\nPER-FLOOR HISTORY");
        foreach (var f in path)
        {
            s.AppendLine($"A{f.Act}/F{f.Floor} {RoomLabel(f.Room)}: {f.Damage} damage, {f.Healed} game-recorded healing, {f.Used} potions{(blitz ? "" : $", {f.Spent} gold spent")}; +{f.Gained}/−{f.Removed} cards, {f.Upgraded} upgrades, {f.Turns} turns.");
            if (blitz && f.BlitzTaken is { Length: > 0 } perks)
                s.AppendLine($"  Merchant picks: {string.Join(", ", perks)}." + (f.BlitzPassed is { Length: > 0 } others ? $" Passed: {string.Join(", ", others)}." : ""));
            if (f.StartupHpGrant is { } grant) s.AppendLine($"  Identified startup HP grant: {grant}; separate from tactical recovery (included in history healing only if the game records it there).");
        }
        if (path.Any(f => f.Floor == 1 && f.Healed > 0 && f.StartupHpGrant == null))
            s.AppendLine("Floor-one healing may include initialization; history alone does not identify a startup grant or tactical recovery.");
        if (worst is { Damage: > 0 }) s.AppendLine($"Largest HP cost: floor {worst.Floor}, {worst.Room} ({worst.Damage} damage recorded).");
        s.AppendLine("\nNEXT-RUN COACHING");
        if (!won && !abandoned && potions.Length > 0 && worst is { Damage: > 0 })
            s.AppendLine($"• Before ending a high-damage turn, check your belt. You finished with {potions.Length} potion(s); history cannot establish whether they would have prevented the loss.");
        if (lines.FirstOrDefault(x => x.Online) is { } paired)
            s.AppendLine($"• Keep the {paired.Line.Name} pairing in mind: {paired.Sources[0].Name} plus {paired.Payoffs.FirstOrDefault(p => p.Id != paired.Sources[0].Id)?.Name ?? paired.Payoffs[0].Name}. Add reliability before another payoff.");
        else if (lines.FirstOrDefault(x => x.Payoffs.Length > 0 && x.Sources.Length == 0) is { } unsupported)
            s.AppendLine($"• Before another {unsupported.Line.Name} payoff, look for a reliable source of {unsupported.Line.Role}.");
        if (deck.Cards.Count(c => c.CombatHas("guard")) * 4 < deck.Cards.Length && !won)
            s.AppendLine("• Direct block was a small part of the final deck. Before the next elite, check how an ordinary hand handles its first attack; Weak, summons and orbs can also cover damage.");
        else if (worst is { Damage: > 0, Turns: > 0 })
            s.AppendLine($"• Use {worst.Room} as a practice fight: plan for its first dangerous turn before spending energy on setup.");
        else s.AppendLine("• At each reward, name one immediate problem the card solves before taking it over Skip.");
        if (!string.IsNullOrWhiteSpace(journalAssessment)) s.AppendLine("\n" + journalAssessment);
        return s.ToString();
    }
    static void LossDecisions(StringBuilder s, FloorResult[] path, Deck deck)
    {
        if (path.Length == 0) return;
        var end = path[^1];
        var earlier = path.SkipLast(1).ToArray();
        s.AppendLine("Decisions to revisit (in floor order):");
        var points = new List<(FloorResult floor, string text)>();
        // Take one costly encounter per act, plus the highest-cost event choice.
        // Don't label a fight optional unless history actually records the choice.
        var notable = earlier.Where(f => f.Damage > 0 && f.Turns > 0).GroupBy(f => f.Act)
            .Select(g => g.MaxBy(f => f.Damage)!).Concat(earlier.Where(f => f.Damage > 0 && f.EventPicks is { Length: > 0 })
                .OrderByDescending(f => f.Damage).Take(1)).Distinct();
        foreach (var f in notable)
        {
            string choice = f.EventPicks is { Length: > 0 } picks ? $"You chose {string.Join(" → ", picks)}. " : "";
            string potion = f.BeltUsed is { Length: > 0 } belt ? $" and used {string.Join(", ", belt)}" : f.Used > 0 ? $" and used {f.Used} potion(s)" : "";
            string advice = f.EventPicks?.Contains("Fight") == true ? "Before choosing an event fight, weigh its reward against the HP and belt you need for the boss." : f.Turns == 0 ? "Compare the event reward with the HP it leaves for the next fight." : "Before committing to a similar fight, budget for both damage and potion use.";
            points.Add((f, $"{choice}You took {f.Damage} damage{potion}" + (f.EndHp is int hp ? $", finishing at {hp} HP. " : ". ") + advice));
        }
        for (int i = 0; i < path.Length - 1; i++)
        {
            var camp = path[i];
            bool smith = camp.CampPicks?.Contains("SMITH") == true;
            if ((!smith && camp.CampPicks?.Contains("HEAL") != true) || camp.EndHp is not int hp) continue;
            var after = path.Skip(i + 1).TakeWhile(f => f.Act == camp.Act).ToArray();
            if (after.Length == 0) continue;
            int until = Array.FindIndex(after, f => f.CampPicks is { Length: > 0 } || f.Room.Replace(" ", "").Equals("Restsite", StringComparison.OrdinalIgnoreCase));
            var rooms = until < 0 ? after : after[..until];
            string trail = string.Join(" → ", after.Take(until < 0 ? after.Length : until + 1).Select(f => $"F{f.Floor} {RoomLabel(f.Room)}"));
            var next = until < 0 ? null : after[until];
            string result = next == null ? "" : rooms.LastOrDefault()?.EndHp is int arrival ? $" Last recorded HP before that camp: {arrival}." : "";
            if (next?.CampPicks?.Contains("HEAL") == true) result += $" There you healed {next.Healed} to {next.EndHp} HP.";
            string action = smith ? $"You upgraded at {hp}/{camp.MaxHp}." : $"You healed {camp.Healed} HP and left at {hp}/{camp.MaxHp}.";
            string lesson = smith && next != null && rooms.Length <= 1 && rooms.All(f => f.EndHp > camp.MaxHp / 3)
                ? " With another camp this close, upgrading was defensible." : "";
            points.Add((camp, $"{action} Path taken: {trail}. {rooms.Sum(f => f.Damage)} damage recorded on that stretch.{result}{lesson}"));
        }
        foreach (var p in points.OrderBy(x => x.floor.Floor)) s.AppendLine($"• A{p.floor.Act}/F{p.floor.Floor} {RoomLabel(p.floor.Room)}: {p.text}");
        var picksTaken = path.SelectMany(f => f.PickedCards ?? []).ToArray();
        var draws = deck.Cards.Count(c => c.Has("draw"));
        if (deck.Cards.Length >= 25 && picksTaken.Length >= 5 && draws * 8 < deck.Cards.Length)
        {
            var repeat = picksTaken.GroupBy(x => x).Where(g => g.Count() > 1).OrderByDescending(g => g.Count()).Select(g => g.Key).Take(2).ToArray();
            s.AppendLine($"• Reward decisions: you picked {picksTaken.Length} cards" + (repeat.Length > 0 ? $", including repeat picks of {string.Join(" and ", repeat)}" : "") + $". You finished with {deck.Cards.Length} cards and {draws} tagged for draw. Consider skipping a duplicate to see your key cards more often; history doesn’t show that a particular draw killed you.");
        }
        if (points.Count == 0) s.AppendLine("No earlier costly fight or camp decision stands out in the recorded floors.");
    }
    static string RoomLabel(string room)
    {
        var text = room.Replace('_', ' ').Replace("Normal", "regular fight").Replace("RestSite", "Rest site").Replace("Merchant", "Shop");
        return System.Text.RegularExpressions.Regex.Replace(text, "(?<=[a-z])(?=[A-Z])", " ");
    }
}
