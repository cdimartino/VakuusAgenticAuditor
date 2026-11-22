namespace Auditor;

internal static class WuppyCoach
{
    internal enum Cover { None, SingleHit, FirstHitOnly }
    internal static Cover Protection(int hp, bool loyalty, int hits) => hp <= 0 || !loyalty || hits <= 0 ? Cover.None : hits == 1 ? Cover.SingleHit : Cover.FirstHitOnly;

    internal static string? Offer(string[] enabled, int slots, bool sozu)
    {
        const string prefix = "WUPPY_IN_TROUBLE.pages.INITIAL.options.";
        var lines = new List<string>();
        foreach (var id in enabled)
        {
            var line = id switch
            {
                prefix + "BE_CAUTIOUS" => "Be Cautious: pay the shown HP for a 30-HP Wuppy; keep Injury out of your deck.",
                prefix + "HELP_IMMEDIATELY" => "Help Immediately: a 60-HP Wuppy intercepts attacks, but Injury clogs future draws.",
                prefix + "IGNORE" => "Ignore: " + (sozu ? "Sozu prevents taking the potion." : slots > 0 ? "a random potion fits your belt." : "your full belt may need a swap."),
                _ => null
            };
            if (line == null) return null;
            lines.Add(line);
        }
        return lines.Count == 0 ? null : string.Join("\n", lines);
    }
}
