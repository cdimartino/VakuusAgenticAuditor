using Auditor;

internal static class AncientGiftCatalogRegression
{
    internal static void Run(Action<bool, string> check)
    {
        using var inventory = new StreamReader(typeof(AncientGiftCatalogRegression).Assembly.GetManifestResourceStream("tests/encounter-coverage.tsv")!);
        var gifts = inventory.ReadToEnd().Split('\n').Select(l => l.Split('\t')).Where(row => row[0] == "ancient_relic")
            .Select(row => row[1].Split(".pages.INITIAL.options.")).ToArray();
        var deck = new Deck([], 50, 70, 1, false, []);
        string? Compare(string owner, string[] ids, bool pick = false) => AncientGiftCatalog.Compare(owner,
            ids.Select(id => new AncientGiftCatalog.Offer(id, id)).ToArray(), deck, 0, 40, 2, 1, pick);
        check(gifts.Length == 102 && gifts.GroupBy(g => g[0]).Count() == 8, "gift corpus spans all eight verified relic pools");
        foreach (var group in gifts.GroupBy(g => g[0]))
        {
            var ids = group.Select(g => g[1]).ToArray();
            check(ids.Select((id, i) => ids.Skip(i).Concat(ids.Take(i)).Take(3).ToArray()).All(offer =>
            {
                var lines = Compare(group.Key, offer)?.Split('\n');
                return lines?.Length == offer.Length && offer.All(id => lines.Any(l => l.StartsWith(id + ": ") && l.Length > id.Length + 8));
            }), group.Key + " covers every ID inside a mixed roll with a short cue per gift");
        }
        check(Compare("NEOW", ["GOLDEN_PEARL", "SMALL_CAPSULE"], true)!.StartsWith("Consider GOLDEN_PEARL:"), "low gold identifies a relevant gift");
        check(!Compare("NEOW", ["GOLDEN_PEARL", "UNKNOWN"], true)!.Contains("Consider") && Compare("NEOW", ["UNKNOWN"]) == null,
            "partial and unknown pools do not claim a best pick");
        check(Compare("THE_ARCHITECT", ["GOLDEN_PEARL"]) == null && Compare("PAEL", ["GOLDEN_PEARL"]) == null, "no cross-Ancient or dialogue-only match");
        check(new[] { "ALL_STAR", "INSANITY", "DRAFT", "SEALED_DECK", "SPECIALIZED", "BLITZ" }.All(k => AncientGiftCatalog.Opening("NEOW", k) != null)
            && AncientGiftCatalog.Opening("NEOW", "PROCEED") == null, "all Neow mode openings recognized; controls omitted");
    }
}
