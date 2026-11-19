using HarmonyLib;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace Auditor;

// Audited v0.113.1 picker fields. If a future build changes them, omit the
// count rather than confusing an attractive target count with a legal count.
internal static class UpgradeSelection
{
    internal static (int Required, HashSet<CardModel> Selected)? Read(NDeckUpgradeSelectScreen screen)
    {
        if (AccessTools.Field(typeof(NDeckUpgradeSelectScreen), "_prefs")?.GetValue(screen) is not CardSelectorPrefs prefs
            || AccessTools.Field(typeof(NDeckUpgradeSelectScreen), "_selectedCards")?.GetValue(screen) is not HashSet<CardModel> selected
            || prefs.MinSelect != prefs.MaxSelect || prefs.MaxSelect < 1) return null;
        return (prefs.MaxSelect, selected);
    }
    internal static CardModel? Base(NGridCardHolder holder) =>
        AccessTools.Field(typeof(NGridCardHolder), "_baseCard")?.GetValue(holder) as CardModel ?? holder.CardModel;
}
