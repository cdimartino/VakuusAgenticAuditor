using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace Auditor;

internal static class BlitzModeRuntime
{
    internal static bool Active(RunState run, string status) =>
        BlitzMode.Active(status.StartsWith("Audited", StringComparison.Ordinal) ? Catalog.Profile?.Build : null,
            run.GameMode.ToString(), RunManager.Instance.NetService.Type.ToString(), run.Players.Count,
            run.IsCustomGame || run.Modifiers.Count > 0)
        && !RunManager.Instance.IsChangingRooms;

    // Use current links, including restored saves and the actual second boss. A branched
    // or replaced map has no verified forced route. No traversal of unrevealed encounters.
    static string[]? Remaining(RunState run)
    {
        if (run.Map.GetType().FullName != "MegaCrit.Sts2.Core.Map.BlitzActMap"
            || run.Map.GetType().Assembly != typeof(MapPoint).Assembly || run.CurrentMapPoint is not { } node) return null;
        return BlitzMode.Remaining(node, p => p.Children, p => p.PointType.ToString());
    }

    internal static string? Route(RunState run, bool details = false)
    {
        if (Remaining(run) is not { } path || BlitzMode.Route(run.CurrentActIndex, path) is not { } route) return null;
        return details && BlitzMode.Difficulty(run.AscensionLevel) is { } difficulty ? route + "\n" + difficulty : route;
    }

    internal static string? Camp(RunState run, Player p, NRestSiteButton[] choices)
    {
        if (Remaining(run) is not { Length: > 0 } path || path.Any(s => s != "Boss")) return null;
        var heal = choices.Any(b => b.Option is HealRestSiteOption { IsEnabled: true });
        var smith = choices.Any(b => b.Option is SmithRestSiteOption { IsEnabled: true });
        var text = BlitzMode.Camp(heal, smith, p.Creature.CurrentHp, p.Creature.MaxHp,
            heal ? (int)Math.Round(HealRestSiteOption.GetHealAmount(p)) : 0, p.Deck.Cards.Count(c => c.IsUpgradable), path.Length);
        var others = choices.Where(b => b.Option is { IsEnabled: true } and not (HealRestSiteOption or SmithRestSiteOption))
            .Select(b => b.Option.Title.GetFormattedText()).ToArray();
        return text == null || others.Length == 0 ? text : text + " Also available: " + string.Join(", ", others) + ".";
    }

    internal static string? Rewards(RunState run, NRewardButton[] buttons)
    {
        if (run.CurrentRoom is not CombatRoom room || room.Encounter.GetType().Assembly != typeof(CombatRoom).Assembly)
            return null;
        return BlitzMode.Rewards(room.RoomType.ToString(), room.Encounter.IsWeak, run.CurrentActIndex,
            buttons.Count(b => b.Reward is CardReward), buttons.Count(b => b.Reward is PotionReward),
            buttons.Count(b => b.Reward is RelicReward));
    }
}
