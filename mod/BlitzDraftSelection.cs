using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace Auditor;

// Read-only adapter audited against installed build 26660232.
internal static class BlitzDraftSelection
{
    internal static (CardModel[] Cards, int Min, int Max, int[] Selected)? Read(NSimpleCardSelectScreen screen)
    {
        // Only reward additions, not generic discard/exhaust/remove selectors.
        if (AccessTools.Field(typeof(NSimpleCardSelectScreen), "_cardResults")?.GetValue(screen) == null
            || AccessTools.Field(typeof(NSimpleCardSelectScreen), "_prefs")?.GetValue(screen) is not CardSelectorPrefs prefs
            || AccessTools.Field(typeof(NSimpleCardSelectScreen), "_selectedCards")?.GetValue(screen) is not HashSet<CardModel> selected
            || AccessTools.Field(typeof(NCardGridSelectionScreen), "_cards")?.GetValue(screen) is not IEnumerable<CardModel> source)
            return null;
        var cards = source.ToArray();
        if (selected.Any(c => !cards.Contains(c))) return null;
        return (cards, prefs.MinSelect, prefs.MaxSelect,
            Enumerable.Range(0, cards.Length).Where(i => selected.Contains(cards[i])).ToArray());
    }

    internal static string? DeckTarget(NCardGridSelectionScreen screen, Deck deck, Func<CardModel, Card> read)
    {
        if (!Catalog.Profile.Exact) return null;
        var type = screen.GetType();
        if (type.Assembly != typeof(NCardGridSelectionScreen).Assembly
            || AccessTools.Field(type, "_prefs")?.GetValue(screen) is not CardSelectorPrefs prefs
            || AccessTools.Field(type, "_selectedCards")?.GetValue(screen) is not HashSet<CardModel> chosen
            || AccessTools.Field(typeof(NCardGridSelectionScreen), "_cards")?.GetValue(screen) is not IEnumerable<CardModel> pool)
            return null;
        bool remove = screen is NDeckCardSelectScreen && prefs.Prompt?.LocTable == "card_selection" && prefs.Prompt.LocEntryKey == "TO_REMOVE";
        if (!remove && screen is not (NDeckTransformSelectScreen or NDeckEnchantSelectScreen)) return null;
        if (new[] { "%PreviewContainer", "%EnchantSinglePreviewContainer", "%EnchantMultiPreviewContainer" }
            .Any(path => screen.GetNodeOrNull<Godot.Control>(path)?.IsVisibleInTree() == true)) return null;
        if (chosen.Count >= prefs.MaxSelect) return "Selection complete; confirm if these are the targets you want.";
        var legal = pool.Where(c => !chosen.Contains(c)).ToArray();
        if (legal.Length == 0) return null;
        if (screen is NDeckEnchantSelectScreen)
            return $"Choose an eligible enchantment target: {string.Join(", ", legal.Take(3).Select(c => c.Title))}. Check the enchantment in the preview.";
        var cut = legal.FirstOrDefault(c => read(c).IsCurse && c.Id.Entry is "NORMALITY" or "REGRET" or "SHAME" or "DOUBT" or "DECAY")
            ?? legal.FirstOrDefault(c => c.IsBasicStrikeOrDefend && !c.IsUpgraded && c.Id.Entry.StartsWith("STRIKE_") && deck.Cards.Count(x => x.Attack && !x.Id.StartsWith("STRIKE_")) >= 3);
        if (cut == null) return null;
        return remove ? $"Consider removing {cut.Title} to free a draw." : $"Consider transforming {cut.Title}; the replacement is random.";
    }

    internal static (CardModel[][] Bundles, bool CanSkip)? Read(NChooseABundleSelectionScreen screen)
    {
        if (AccessTools.Field(typeof(NChooseABundleSelectionScreen), "_bundles")?.GetValue(screen)
                is not IReadOnlyList<IReadOnlyList<CardModel>> bundles
            || AccessTools.Field(typeof(NChooseABundleSelectionScreen), "_canSkip")?.GetValue(screen) is not bool canSkip)
            return null;
        return (bundles.Select(b => b.ToArray()).ToArray(), canSkip);
    }
}
