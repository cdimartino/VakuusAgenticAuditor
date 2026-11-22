using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace Auditor;

/// <summary>Adapts verified native Blitz merchant models to the pure coach.</summary>
internal static class BlitzMerchantRuntime
{
    internal static string? Shop(NBlitzMerchantRoom room, Player player, Deck deckSnapshot)
    {
        var screen = room.Inventory;
        if (screen?.IsOpen != true) return null;
        var inventory = screen.Inventory;
        if (inventory is not { IsBlitzShop: true }) return null;
        var entries = VisibleSlots(screen)
            .Where(slot => slot.IsVisibleInTree() && slot.Hitbox.IsEnabled &&
                slot.Entry is BlitzMerchantEntry entry && entry.IsStocked)
            .Select(slot => (BlitzMerchantEntry)slot.Entry)
            .ToArray();
        if (entries.Length == 0) return null;
        var deck = player.Deck.Cards;
        var offers = entries.Select(entry =>
        {
            string id = System.Text.RegularExpressions.Regex.Replace(entry.MerchantEntryType.ToString(),
                "(?<=[a-z0-9])(?=[A-Z])", "_").ToLowerInvariant();
            IEnumerable<CardModel> legal = entry.MerchantEntryType switch
            {
                BlitzMerchantEntryType.RandomUpgrades or BlitzMerchantEntryType.CardUpgrade =>
                    deck.Where(c => c.IsUpgradable),
                BlitzMerchantEntryType.CardTransform =>
                    deck.Where(c => c.Type != CardType.Quest && c.IsTransformable),
                BlitzMerchantEntryType.CardRemoval =>
                    deck.Where(c => c.IsRemovable).OrderByDescending(c => c.Type == CardType.Curse),
                BlitzMerchantEntryType.CardEnchant or BlitzMerchantEntryType.Glam =>
                    EnchantTargets(entry, deck),
                _ => Enumerable.Empty<CardModel>()
            };
            var candidates = legal.ToArray();
            // Tooltip text is the game's resolved, localized entry description.
            return new BlitzMerchant.Offer(id, entry.Title.GetFormattedText(),
                entry.Description.GetFormattedText(), entry.IsStocked,
                candidates.Take(3).Select(c => c.Title).ToArray(), candidates.Length,
                entry.IsCursed);
        }).ToArray();

        var possibleCurses = ModelDb.CardPool<CurseCardPool>()
            .GetUnlockedCards(player.UnlockState, player.RunState.CardMultiplayerConstraint)
            .Where(c => c.CanBeGeneratedByModifiers && c.IsAllowedInRun(player.RunState))
            .Select(c => c.Id.Entry).Distinct().ToArray();
        var curseCosts = possibleCurses.Select(id => (Id: id, Cost: CurseValuation.Addition([id], deckSnapshot)))
            .OrderByDescending(x => x.Cost.Cost).ToArray();
        double meanCurseCost = curseCosts.Length == 0 ? 0 : curseCosts.Average(x => x.Cost.Cost);
        string curseRisk = curseCosts.Length == 0 ? "Curse pool unavailable for valuation." :
            "Higher-liability possibilities include " + string.Join(", ", curseCosts.Take(2)
                .Select(x => CurseValuation.Names.GetValueOrDefault(x.Id, x.Id)))
                + "; the actual curse is randomly selected.";

        return BlitzMerchant.Compare(offers, new(
            inventory.BlitzChoicesRemaining,
            entries.Length,
            player.Creature.CurrentHp,
            player.Creature.MaxHp,
            player.Potions.Count(),
            player.PotionSlots.Count,
            player.Relics.Any(r => r.GetType().Assembly == typeof(RelicModel).Assembly && r.Id.Entry == "SOZU"),
            deck.Count(c => c.Type == CardType.Curse),
            deck.Count(c => c.Type == CardType.Curse && c.IsRemovable),
            deck.Count,
            meanCurseCost,
            curseRisk));
    }

    static IEnumerable<NBlitzMerchantSlot> VisibleSlots(Godot.Node root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Godot.CanvasItem item && !item.IsVisibleInTree()) continue;
            if (child is NBlitzMerchantSlot slot) yield return slot;
            foreach (var nested in VisibleSlots(child)) yield return nested;
        }
    }

    static IEnumerable<CardModel> EnchantTargets(BlitzMerchantEntry entry, IEnumerable<CardModel> deck)
    {
        var field = entry.GetType().GetField("_enchantment",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var value = field?.GetValue(entry);
        var enchant = value switch
        {
            ValueTuple<EnchantmentModel, int> pair => pair.Item1,
            EnchantmentModel model => model,
            _ => null
        };
        return enchant == null ? [] : deck.Where(enchant.CanEnchant);
    }
}
