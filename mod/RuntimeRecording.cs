using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace Auditor;

internal sealed class RuntimeRecording
{
    sealed record Reference(string Value);
    static readonly ConditionalWeakTable<object, Reference> references = new();
    static long referenceCount;
    internal static string Ref(object instance) => references.GetValue(instance, _ => new Reference((++referenceCount).ToString())).Value;
    internal static int? Profile => SaveManager.Instance.IsProfileInitialized
        ? SaveManager.Instance.CurrentProfileId : null;
    internal static string RunIdentity(RunState r)
    {
        // A repeated seed is not a repeated run. The game's history start time
        // and local profile distinguish sessions and survive a normal resume.
        long start = RunManager.Instance.History?.StartTime ?? 0;
        var identity = $"{Profile}:{start}:{r.Rng.StringSeed}:{(start == 0 ? Ref(r) : "")}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..32].ToLowerInvariant();
    }
    internal RunJournal? Journal { get; private set; }
    internal long LastStateSequence { get; private set; }
    internal JournalState? Latest { get; private set; }
    readonly HashSet<string> observed = [];
    string run = "";
    bool gap;
    internal bool Incomplete => gap || Journal?.Truncated == true || Journal?.Failure != null;

    internal static JournalState Capture(RunState r, Player p)
    {
        var combat = CombatManager.Instance.DebugOnlyGetState();
        var pc = p.PlayerCombatState;
        bool inCombat = CombatManager.Instance.IsInProgress;
        JournalCard Card(CardModel c) => new(c.Id.Entry, c.Title, Ref(c),
            c.EnergyCost.GetWithModifiers(CostModifiers.All), c.IsUpgraded);
        var enemies = inCombat && combat != null ? combat.Enemies.Select(e => new JournalEnemy(
            e.Monster?.Id.Entry ?? "unknown", e.Monster?.Title.GetFormattedText() ?? "unknown", e.CombatId.ToString(),
            e.CurrentHp, e.Block, e.Powers.Select(x => new JournalPower(x.Id.Entry, x.Amount)).ToArray(),
            e.Monster?.NextMove?.Intents.Select(x => x is AttackIntent a
                ? $"attack:{a.GetTotalDamage(combat.PlayerCreatures, e)}" : x.GetType().Name).ToArray() ?? [])).ToArray() : [];
        var overlay = MegaCrit.Sts2.Core.Nodes.Screens.Overlays.NOverlayStack.Instance?.Peek();
        string? selectionState = null;
        if (overlay is MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen upgrade
            && UpgradeSelection.Read(upgrade) is { } selection)
            selectionState = System.Text.Json.JsonSerializer.Serialize(new
            {
                required = selection.Required,
                selected = selection.Selected.Select(Ref).Order().ToArray()
            });
        return new(r.CurrentActIndex + 1, r.ActFloor, r.CurrentRoom?.GetType().Name ?? "none",
            (r.CurrentRoom as MegaCrit.Sts2.Core.Rooms.CombatRoom)?.Encounter.Id.Entry,
            inCombat ? $"{r.CurrentActIndex}:{r.ActFloor}:{Ref(r.CurrentRoom!)}" : null,
            inCombat ? combat?.RoundNumber ?? 0 : 0,
            inCombat ? $"{combat?.CurrentSide}:{pc?.Phase}" : "outside-combat",
            p.Creature.CurrentHp, p.Creature.MaxHp, p.Creature.Block, pc?.Energy ?? 0, p.Gold,
            pc?.Hand.Cards.Select(Card).ToArray() ?? [], p.Deck.Cards.Select(Card).ToArray(),
            p.Relics.Select(x => x.Id.Entry).ToArray(), p.Potions.Select(x => x.Id.Entry).ToArray(),
            p.Creature.Powers.Select(x => new JournalPower(x.Id.Entry, x.Amount)).ToArray(), enemies,
            pc?.Stars ?? 0, pc?.AvailableArrowCount ?? 0, pc?.DrawPile.Cards.Count ?? 0,
            pc?.DiscardPile.Cards.Count ?? 0, pc?.ExhaustPile.Cards.Count ?? 0,
            MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance?.IsOpen == true ? "map"
                : overlay?.GetType().Name, selectionState, p.Character.Id.Entry, r.AscensionLevel, Catalog.Profile?.Build, Choices());
    }

    static JournalChoice[] Choices()
    {
        Node? root = MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance?.IsOpen == true
            ? MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapScreen.Instance
            : MegaCrit.Sts2.Core.Nodes.Screens.Overlays.NOverlayStack.Instance?.Peek() as Node
                ?? (Node?)MegaCrit.Sts2.Core.Nodes.Rooms.NEventRoom.Instance
                ?? MegaCrit.Sts2.Core.Nodes.Combat.NPlayerHand.Instance;
        if (root == null || !GodotObject.IsInstanceValid(root) || root is CanvasItem visible && !visible.IsVisibleInTree()) return [];
        static IEnumerable<Node> Visible(Node parent)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is CanvasItem item && !item.IsVisibleInTree()) continue;
                yield return child;
                foreach (var descendant in Visible(child)) yield return descendant;
            }
        }
        return Visible(root).Select(node => node switch
        {
            MegaCrit.Sts2.Core.Nodes.Events.NEventOptionButton b when b.Option != null =>
                new JournalChoice(Ref(b), b.Option.Title.GetFormattedText(), b.IsEnabled && !b.Option.IsLocked,
                    b.Option.Description?.GetFormattedText()),
            MegaCrit.Sts2.Core.Nodes.Screens.Map.NMapPoint m when m.State.ToString() == "Travelable" && m.Point != null =>
                new JournalChoice(Ref(m), m.Point.PointType.ToString(), true, $"column:{m.Point.coord.col}"),
            MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder c when c.CardModel != null =>
                new JournalChoice(Ref(c.CardModel), c.CardModel.Title, c.Hitbox.IsEnabled),
            _ => null
        }).OfType<JournalChoice>().OrderBy(c => c.Reference, StringComparer.Ordinal).Take(128).ToArray();
    }

    internal void Observe(RunState r, Player p, bool enabled)
    {
        var id = RunIdentity(r);
        if (run != id)
        {
            run = id;
            Journal = null;
            LastStateSequence = 0; observed.Clear(); Latest = null; gap = r.ActFloor > 1;
        }
        if (!enabled) { gap = true; LastStateSequence = 0; Latest = null; return; }
        Journal ??= new RunJournal(ProjectSettings.GlobalizePath("user://mod_configs/VakuusAgenticAuditor/runs"), id);
        var state = Capture(r, p);
        if (Journal!.State(state) is { } sequence)
        {
            if (LastStateSequence != 0)
                Journal.Transition(new(LastStateSequence, sequence, "Observed game-state change; causality not inferred."));
            LastStateSequence = sequence;
            Latest = state;
        }
        // The game history, unlike an HP delta, is direct evidence that a card
        // play completed. Deliberately do not infer potion use from belt deltas.
        if (CombatManager.Instance.IsInProgress)
            foreach (var entry in CombatManager.Instance.History.CardPlaysFinished)
                if (entry.CardPlay.Card.Owner == p && observed.Add(Ref(entry)))
                    Journal.ObservedAction(new("card-play-completed", Ref(entry.CardPlay.Card),
                        entry.CardPlay.Target?.CombatId.ToString(), "CombatManager.History.CardPlaysFinished"));
    }
    internal void Advice(string decision, string text, bool cursed, string[] actions, string? boundary, int? expectedHpLoss = null)
    {
        if (Journal != null && LastStateSequence > 0)
            Journal.Advice(new(decision, LastStateSequence, text, cursed, actions, boundary, expectedHpLoss));
    }
    internal string Assessment() => Journal == null ? "Run recording was unavailable or disabled. No turn-level conclusions are established."
        : JournalAnalysis.Write(RunJournal.Read(Journal.FilePath), Incomplete);
}
