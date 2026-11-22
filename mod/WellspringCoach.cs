namespace Auditor;

internal static class WellspringCoach
{
    const string Prefix = "WELLSPRING.pages.INITIAL.options.";
    internal static string? Compare(string[] enabled, int emptySlots, bool sozu, Card[] removable, int curses, bool pick)
    {
        if (enabled.Length == 0 || enabled.Any(id => id != Prefix + "BOTTLE" && id != Prefix + "BATHE")) return null;
        bool bottle = enabled.Contains(Prefix + "BOTTLE"), bathe = enabled.Contains(Prefix + "BATHE");
        var permanentCurse = removable.FirstOrDefault(c => c.IsCurse && c.Id is "INJURY" or "REGRET" or "NORMALITY" or "SHAME" or "DOUBT" or "DECAY");
        string potion = sozu ? "Sozu prevents taking the potion." : emptySlots > 0 ? "A random potion fits your empty belt slot." : "Your belt is full; only swap if the potion is better.";
        string removal = removable.Length == 0 ? "No legal card to remove" : permanentCurse != null ? $"Permanently remove {permanentCurse.Name}" : "Permanently remove one card";
        string cost = curses <= 0 ? "." : curses == 1 ? "; Guilty can clog draws for 5 combats." : $"; {curses} Guilty cards can clog draws for 5 combats.";
        if (!bathe) return "Bottle: " + potion;
        if (!bottle) return "Bathe: " + removal + cost;
        bool favorBathe = permanentCurse != null && curses <= 1 || sozu && removable.Length > 0;
        bool favorBottle = !sozu && (emptySlots > 0 || removable.Length == 0) && !favorBathe;
        string a = (pick && favorBottle ? "Consider Bottle: " : "Bottle: ") + potion;
        string b = (pick && favorBathe ? "Consider Bathe: " : "Bathe: ") + removal + cost;
        return favorBathe ? b + "\n" + a : a + "\n" + b;
    }
}
