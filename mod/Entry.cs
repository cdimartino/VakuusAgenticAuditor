using Godot;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using MegaCrit.Sts2.Core.Nodes.Relics;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Runs;
namespace Auditor;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    static Display ui = null!;
    static Preferences P => ui.P;
    static ulong next, until, postUntil, detailUntil;
    static bool settingsKey, whyKey, muteKey;
    static string last = "", turn = "", post = "";
    static Verdict[] ranking = [];
    static string[] alternatives = [];
    static NCardRewardSelectionScreen? offer;
    static Control? cardRow;
    static Vector2? lastRewardPoint;
    static RunState? session;
    static readonly DecisionGate decisions = new();
    static object? postRoom;
    static readonly HashSet<string> errors = [];
    public static void Initialize()
    {
        new Harmony("vakuus.agentic.auditor").PatchAll(typeof(Entry).Assembly);
        var tree = (SceneTree)Engine.GetMainLoop();
        Callable.From(() => { ui = new Display(tree); tree.ProcessFrame += Frame; GD.Print("[VakuuAuditor] Ready"); }).CallDeferred();
    }
    static bool Solo() => RunManager.Instance.IsInProgress && !RunManager.Instance.NetService.Type.IsMultiplayer();
    [HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen.NGameOverScreen), "_Ready")]
    static class FinishedRun
    {
        static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen.NGameOverScreen __instance,
            RunState ____runState, RunHistory ____history) => Safe(() =>
            {
                if (ui == null || RunManager.Instance.NetService.Type.IsMultiplayer() || ____runState.Players.Count != 1) return;
                var p = ____runState.Players[0];
                var deck = new Deck(p.Deck.Cards.Select(c => Catalog.Get(c.Id.Entry, c.Title, c.EnergyCost.Canonical, c.IsUpgraded, c.Type == CardType.Attack,
                    c.GetType().Assembly == typeof(CardModel).Assembly)).ToArray(), p.Creature.CurrentHp, p.Creature.MaxHp, ____runState.CurrentActIndex + 1, false,
                    p.Relics.Select(x => x.Id.Entry).ToArray(), p.Character.Id.Entry);
                static string Label(string id) => System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(id.Replace('_', ' ').ToLowerInvariant());
                var path = new List<FloorResult>(); int act = 0, floor = 0;
                foreach (var floors in ____runState.MapPointHistory)
                {
                    act++;
                    foreach (var f in floors)
                    {
                        floor++;
                        var stats = f.PlayerStats.FirstOrDefault(); if (stats == null) continue;
                        var room = f.Rooms.LastOrDefault();
                        path.Add(new(act, floor, Label(room?.ModelId?.Entry ?? f.MapPointType.ToString()), stats.DamageTaken, stats.HpHealed,
                            stats.PotionUsed.Count, stats.GoldSpent, stats.CardsGained.Count, stats.CardsRemoved.Count, stats.UpgradedCards.Count, f.Rooms.Sum(x => x.TurnsTaken)));
                    }
                }
                var cause = ____history.KilledByEncounter?.Entry ?? ____history.KilledByEvent?.Entry;
                var report = RunReview.Write(deck, path.ToArray(), ____history.Win, ____history.WasAbandoned, cause == null ? null : Label(cause),
                    ____runState.AscensionLevel, p.Gold, p.Potions.Select(x => x.Title.GetFormattedText()).ToArray());
                ui.AttachReview(__instance, report);
            });
    }
    [HarmonyPatch(typeof(NSettingsScreen), "_Ready")]
    static class GeneralSettings
    {
        static void Postfix(NSettingsScreen __instance) => Safe(() =>
        {
            var content = __instance.GetNode<NSettingsPanel>("%GeneralSettings").Content;
            if (content.HasNode("VakuuAuditorSettings")) return;
            var button = new Button
            {
                Name = "VakuuAuditorSettings",
                Text = "Vakuu’s Agentic Auditor…",
                TooltipText = "Configure coaching and feedback",
                CustomMinimumSize = new Vector2(0, 42)
            };
            button.Pressed += () => ui?.Settings();
            content.AddChild(button);
        });
    }
    static void Safe(Action a)
    {
        try { a(); } catch (Exception ex) { ui?.Hide(); if (errors.Add(ex.GetType().Name + ex.Message)) GD.PushWarning("[VakuuAuditor] " + ex.Message); }
    }
    static Card Read(CardModel c)
    {
        var card = Catalog.Get(c.Id.Entry, c.Title, c.EnergyCost.GetWithModifiers(CostModifiers.All),
            c.IsUpgraded, c.Type == CardType.Attack, c.GetType().Assembly == typeof(CardModel).Assembly);
        if (card.Known && c.BaseStarCost > 0 && !card.Needs.Contains("stars"))
            card = card with { Needs = [.. card.Needs, "stars"] };
        return card;
    }
    static Deck DeckOf(RunState r, Player p)
    {
        var routes = r.CurrentMapPoint?.Children;
        return new(p.Deck.Cards.Select(Read).ToArray(), p.Creature.CurrentHp, p.Creature.MaxHp, r.CurrentActIndex + 1,
            routes?.Count > 0 && routes.All(n => n.PointType.ToString() == "Elite"), p.Relics.Select(v => v.Id.Entry).ToArray(), p.Character.Id.Entry);
    }

    [HarmonyPatch(typeof(NCardRewardSelectionScreen), "RefreshOptions")]
    static class RewardShown
    {
        static void Postfix(NCardRewardSelectionScreen __instance, IReadOnlyList<CardCreationResult> __0, IReadOnlyList<CardRewardAlternative> __1, Control ____cardRow)
        {
            Safe(() =>
            {
                decisions.Reopen(__instance);
                offer = null; ranking = []; postUntil = 0; lastRewardPoint = null;
                if (!Solo() || RunManager.Instance.DebugOnlyGetState() is not { } r || r.Players.Count != 1) return;
                session = r;
                alternatives = __1.Select(a => a.OptionId).ToArray();
                ranking = Coach.Rank(__0.Select(x => Read(x.Card)), DeckOf(r, r.Players[0]), alternatives.Any(IsSkip));
                offer = __instance; cardRow = ____cardRow; last = "";
            });
        }
    }
    static bool IsSkip(string id) => id.Equals("skip", StringComparison.OrdinalIgnoreCase);
    [HarmonyPatch(typeof(NCardRewardSelectionScreen), "SelectCard")]
    static class Pick
    {
        static void Prefix(NCardRewardSelectionScreen __instance, NCardHolder __0, TaskCompletionSource<int?> ____completionSource, out string? __state) =>
            __state = ReferenceEquals(__instance, offer) && ____completionSource?.Task.IsCompleted == false ? __0.CardModel?.Id.Entry : null;
        static void Postfix(string? __state, TaskCompletionSource<int?> ____completionSource)
        { if (__state != null) Safe(() => Feedback(__state)); }
    }
    [HarmonyPatch(typeof(NCardRewardSelectionScreen), "OnAlternateRewardSelected")]
    static class Alternative
    {
        static void Prefix(NCardRewardSelectionScreen __instance, int __0, TaskCompletionSource<int?> ____completionSource, out bool __state) =>
            __state = ReferenceEquals(__instance, offer) && ____completionSource?.Task.IsCompleted == false && __0 >= 0 && __0 < alternatives.Length && IsSkip(alternatives[__0]);
        static void Postfix(bool __state, TaskCompletionSource<int?> ____completionSource)
        { if (__state) Safe(() => Feedback("~skip")); }
    }
    static void Feedback(string id)
    {
        if (offer != null) decisions.Finish(offer);
        ui?.Hide();
        var picked = ranking.FirstOrDefault(v => v.Id == id);
        if (picked == null || ranking.Any(x => double.IsNaN(x.Score)) || ui == null || !Solo() || P.Mode is "Off" or "On demand" || P.Muted) return;
        bool good = ranking[0].Score - picked.Score <= .55;
        if (good ? !P.Praise : !P.Critique) return;
        decisions.NewReaction(); postRoom = session?.CurrentRoom;
        post = Coach.After(picked, ranking[0]); postUntil = Time.GetTicksMsec() + (ulong)Math.Clamp(P.Seconds, 2, 60) * 1000;
    }
    static bool Edge(bool now, ref bool before) { bool edge = now && !before; before = now; return edge; }
    static void Frame() => Safe(Tick);
    static void Tick()
    {
        var now = Time.GetTicksMsec();
        if (MegaCrit.Sts2.Core.Nodes.NGame.Instance?.Transition?.InTransition == true)
        {
            decisions.Transition(); ui.Hide(); postUntil = 0; return;
        }
        if (ui.KeyboardBusy) return;
        if (Edge(Input.IsKeyPressed(P.SettingsKey) || (Input.IsJoyButtonPressed(0, JoyButton.Back) && Input.IsJoyButtonPressed(0, JoyButton.LeftShoulder)), ref settingsKey)) ui.Settings();
        if (ui.InSettings) return;
        if (Edge(Input.IsKeyPressed(P.MuteKey), ref muteKey)) { P.Muted = !P.Muted; P.Save(); postUntil = 0; last = ""; }
        bool why = Edge(Input.IsKeyPressed(P.SuggestionKey) || (Input.IsJoyButtonPressed(0, JoyButton.Back) && Input.IsJoyButtonPressed(0, JoyButton.RightShoulder)), ref whyKey);
        if (why && ui.Visible) { ui.Hide(); return; }
        if (now < next && !why) return;
        next = now + 320;
        if (why) detailUntil = now + (ulong)Math.Clamp(P.Seconds, 2, 60) * 1000;
        if (P.Muted || P.Mode == "Off" || !Solo() || RunManager.Instance.DebugOnlyGetState() is not { } r || r.Players.Count != 1)
        { ui.Hide(); last = ""; postUntil = 0; return; }
        if (!ReferenceEquals(r, session)) { session = r; offer = null; postUntil = 0; turn = ""; }
        var p = r.Players[0]; var overlay = NOverlayStack.Instance?.Peek();
        if (!ReferenceEquals(r.CurrentRoom, postRoom) || NMapScreen.Instance?.IsOpen == true ||
            CombatManager.Instance.IsInProgress || decisions.LeftReactionScreen(overlay)) postUntil = 0;
        string? text = null, battleKey = null;
        bool reacting = false;
        Vector2? anchor = null;
        if (NMapScreen.Instance is { IsOpen: true } map)
        {
            if (P.Routes && !map.IsTraveling)
            {
                var routes = Visible<NMapPoint>(map).Where(x => x.State.ToString() == "Travelable" && x.Point != null).OrderBy(x => x.Point!.coord.col).ToArray();
                if (routes.Length > 1)
                {
                    var deck = DeckOf(r, p);
                    var hint = RouteCoach.Choose(routes.Select(x => Route(x.Point!, 4)).ToArray(), new(deck.Hp, deck.MaxHp, p.Gold, p.Potions.Count(),
                        deck.Cards.Count(c => c.Attack && !c.Id.StartsWith("STRIKE_")) >= 4));
                    if (hint != null) { text = hint.Text; anchor = Above([routes[hint.Index]]); }
                }
            }
        }
        else if (overlay is NCardRewardSelectionScreen cardScreen && P.Rewards)
        {
            if (!ReferenceEquals(offer, cardScreen) || ranking.Length == 0)
            {
                var visible = Visible<NCardHolder>(cardScreen).Where(h => h.CardModel != null).Select(h => Read(h.CardModel!)).ToArray();
                if (visible.Length > 0)
                {
                    bool skip = ReferenceEquals(offer, cardScreen) && alternatives.Any(IsSkip);
                    ranking = Coach.Rank(visible, DeckOf(r, p), skip);
                    offer = cardScreen;
                }
            }
            if (ranking.Length > 0) text = RewardText(now < detailUntil || P.Mode == "Detailed", now < detailUntil);
            if (P.RewardBubble && text != null)
            {
                anchor = RewardAnchor();
                if (anchor.HasValue) lastRewardPoint = anchor;
            }
        }
        else if (now < postUntil && overlay is NRewardsScreen)
        {
            text = post; reacting = true;
            if (overlay is NRewardsScreen rewards) anchor = Above(Visible<NRewardButton>(rewards).Where(b => b.IsEnabled));
            anchor ??= lastRewardPoint;
        }
        else if (overlay is Control screen && P.OtherChoices && overlay is not NCardRewardSelectionScreen &&
            Selection(screen, r, p) is { } choice)
            (text, anchor) = choice;
        else if (overlay == null && !(NMapScreen.Instance?.IsOpen ?? false))
        {
            if (r.CurrentRoom is CombatRoom && P.Combat)
            {
                if (NPlayerHand.Instance is { IsInCardSelection: true } hand) { text = "Follow the hand prompt: keep the cards you still need this turn, especially your last affordable defense."; anchor = Above(Visible<NCardHolder>(hand).Select(x => x.Hitbox)); }
                else
                {
                    (text, battleKey) = Battle(p, now < detailUntil || P.Mode == "Detailed");
                    if (text != null && P.RewardBubble && NPlayerHand.Instance is { } currentHand)
                        anchor = Above(Visible<NCardHolder>(currentHand).Select(h => h.Hitbox));
                }
            }
            if (r.CurrentRoom is EventRoom && P.Events && NEventRoom.Instance is { } ev)
            {
                var eventButtons = Visible<NEventOptionButton>(ev).ToArray();
                // The chosen button can disable before its siblings disappear.
                // Those siblings are no longer an offer to evaluate.
                var options = eventButtons.Any(b => b.Option?.WasChosen == true)
                    ? [] : eventButtons.Where(b => b.IsEnabled && b.Option is { IsLocked: false, IsProceed: false }).ToArray();
                if (options.Length > 0)
                {
                    anchor = Above(options);
                    if (options.All(b => b.Option.Relic != null))
                    {
                        var deck = p.Deck.Cards;
                        text = EventCoach.Ancient(options.Select(b => (b.Option.Relic!.Id.Entry, b.Option.Relic.Title.GetFormattedText())).ToArray(),
                            deck.Count(c => c.IsUpgradable && !c.Id.Entry.StartsWith("STRIKE_") && !c.Id.Entry.StartsWith("DEFEND_")),
                            deck.Count(c => !c.IsUpgraded && (c.Id.Entry.StartsWith("STRIKE_") || c.Id.Entry.StartsWith("DEFEND_"))),
                            deck.Count(c => Read(c).Cost >= 2), P.ExplicitPick);
                    }
                    text ??= EventCoach.Recommend(options.Select(b => new EventPick(b.Option.Title.GetFormattedText(),
                        b.Option.Description?.GetFormattedText() ?? "", b.Option.Relic?.Title.GetFormattedText())).ToArray(),
                        p.Creature.CurrentHp, p.Creature.MaxHp, p.Gold, P.ExplicitPick);
                }
            }
            if (r.CurrentRoom is RestSiteRoom camp && P.Camps && NRestSiteRoom.Instance is { } site)
            {
                var choices = Visible<NRestSiteButton>(site).Where(b => b.IsEnabled).ToArray();
                if (choices.Length > 0) { text = Camp(camp, p) ?? $"You’re at {p.Creature.CurrentHp}/{p.Creature.MaxHp} HP. Compare the available campfire actions before leaving."; anchor = Above(choices); }
            }
            if (r.CurrentRoom is MerchantRoom shop && P.Shops && NMerchantRoom.Instance?.Inventory is { IsOpen: true } inventory)
            {
                text = Shop(shop, r, p) ?? $"Your budget is {p.Gold} gold. Saving it for another merchant is also a choice.";
                anchor = Above(Visible<NClickableControl>(inventory).Where(x => x.IsEnabled));
            }
            if (r.CurrentRoom is TreasureRoom && P.OtherChoices && Visible<NTreasureRoom>(((SceneTree)Engine.GetMainLoop()).Root).FirstOrDefault() is { } treasure)
            {
                var relics = Visible<NTreasureRoomRelicHolder>(treasure).Where(h => h.IsEnabled && h.Relic?.Model != null).ToArray();
                if (relics.Length > 0)
                {
                    text = $"Inspect {relics[0].Relic!.Model!.Title.GetFormattedText()} and compare its trigger with the cards you play most often.";
                    anchor = Above(relics);
                }
            }
        }
        ui.RewardSpace = null;
        if (text != null && NMapScreen.Instance?.IsOpen != true && overlay is Control reward &&
            (reward is NRewardsScreen or NCardRewardSelectionScreen or NChooseACardSelectionScreen))
        {
            ui.RewardSpace = RewardSpace(reward, p);
            anchor = new Vector2(ui.RewardSpace.Speaker.X, ui.RewardSpace.Speaker.Y);
        }
        if (!P.RewardBubble) anchor = null;
        ui.ViewType = NMapScreen.Instance?.IsOpen == true ? "map" : overlay switch
        {
            NCardRewardSelectionScreen => "card rewards",
            NRewardsScreen => "rewards",
            NChooseARelicSelection => "relic choice",
            NCardGridSelectionScreen or NChooseACardSelectionScreen => "card selection",
            NChooseABundleSelectionScreen => "bundles",
            _ => r.CurrentRoom switch { CombatRoom => "fight", RestSiteRoom => "camp", MerchantRoom => "shop", EventRoom => "event", TreasureRoom => "treasure", _ => "other" }
        };
        var key = $"{r.CurrentActIndex}:{r.ActFloor}:{ui.ViewType}:{P.RewardBubble}:{P.CurseMode}:{text}";
        object? decision = NMapScreen.Instance?.IsOpen == true ? NMapScreen.Instance : (object?)overlay ?? r.CurrentRoom;
        if (battleKey == null && !decisions.Ready(decision, text, now, why)) { ui.Hide(); last = ""; return; }
        if (text == null) { ui.Hide(); last = ""; return; }
        bool changed = key != last;
        bool fresh = battleKey == null || decisions.FreshTurnHint(r.CurrentRoom, battleKey, text);
        if ((why || (changed && fresh && P.Mode != "On demand" && (battleKey == null || !P.OncePerTurn || turn != battleKey)))
            && (!reacting || decisions.MayDeliver(overlay!)))
        {
            string spoken = text;
            if (!reacting && P.CurseMode)
            {
                var cards = overlay is NCardRewardSelectionScreen && ui.ViewType == "card rewards" ? ranking : null;
                var identity = $"{r.Rng.StringSeed}:{r.CurrentActIndex}:{r.ActFloor}:{ui.ViewType}:{overlay?.GetType().Name}:{battleKey}";
                if (cards != null) identity += ":" + string.Join(",", cards.Select(x => x.Id).Order(StringComparer.Ordinal));
                spoken = Curse.Advice(text, true, r.AscensionLevel, identity, cards);
            }
            ui.Show(spoken, anchor); until = now + (ulong)Math.Clamp(P.Seconds, 2, 60) * 1000;
            if (reacting) decisions.Delivered(overlay!);
            if (battleKey != null) { turn = battleKey; decisions.MarkTurnHint(text); }
        }
        else if (changed || (now >= until && !ui.Scrolls)) ui.Hide();
        else if (ui.Visible && anchor is { } point) ui.PositionBubble(point);
        last = key;
    }
    static IEnumerable<T> Visible<T>(Node root) where T : Control
    {
        foreach (var child in root.GetChildren())
        {
            if (child is CanvasItem item && !item.IsVisibleInTree()) continue;
            if (child is T match) yield return match;
            foreach (var nested in Visible<T>(child)) yield return nested;
        }
    }
    static Vector2? Above(IEnumerable<Control> choices)
    {
        Rect2? group = null;
        foreach (var c in choices)
        {
            var rect = (c.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, c.Size)).Intersection(c.GetViewportRect());
            if (rect.HasArea()) group = group?.Merge(rect) ?? rect;
        }
        return group is { } g ? new Vector2(g.GetCenter().X, g.Position.Y) : null;
    }
    static (string, Vector2?)? Selection(Control screen, RunState r, Player p)
    {
        if (screen is NCardGridSelectionScreen or NChooseACardSelectionScreen)
        {
            var holders = Visible<NCardHolder>(screen).Where(h => h.CardModel != null).ToArray();
            if (holders.Length == 0) return null;
            var deck = DeckOf(r, p);
            var cards = holders.Select(h => Read(h.CardModel!)).ToArray();
            if (screen is NChooseACardSelectionScreen && cards.All(c => c.Known))
            {
                var best = Coach.Rank(cards, deck, false)[0];
                return ((P.ExplicitPick ? $"Take {best.Name}. " : "") + best.Reason, Above(holders.Select(h => h.Hitbox)));
            }
            if (screen is NDeckTransformSelectScreen && cards.FirstOrDefault(c => c.Id.StartsWith("STRIKE_")) is { } basic &&
                deck.Cards.Count(c => c.Attack && !c.Id.StartsWith("STRIKE_")) >= 3)
                return ((P.ExplicitPick ? $"Transform {basic.Name}. " : "") + "Your non-basic attacks already cover damage; keep your defense and synergy cards.", Above(holders.Select(h => h.Hitbox)));
            if (screen is NDeckUpgradeSelectScreen)
            {
                var best = holders.Select(h => h.CardModel!).Where(c => c.IsUpgradable).Select(c => (c, v: Coach.Assess(Read(c), deck)))
                    .Where(x => double.IsFinite(x.v.Score)).OrderByDescending(x => x.v.Score).FirstOrDefault();
                if (best.c != null) return ((P.ExplicitPick ? $"Upgrade {best.c.Title}. " : "") + best.v.Reason, Above(holders.Select(h => h.Hitbox)));
            }
            return null;
        }
        if (screen is NChooseABundleSelectionScreen) return ($"Compare the whole bundle against your {p.Deck.Cards.Count}-card deck. Extra cards change how often your best draw comes around.", Above(Visible<NCardBundle>(screen)));
        if (screen is NChooseARelicSelection)
        {
            var holders = Visible<NRelicBasicHolder>(screen).ToArray();
            var deck = DeckOf(r, p);
            var matches = holders.Select(h => h.Relic?.Model).OfType<RelicModel>()
                .Select(m => (model: m, fit: Coach.Relic(m.Id.Entry, deck)))
                .Where(x => x.fit.score > 0).OrderByDescending(x => x.fit.score).ToArray();
            if (matches.Length == 0) return null;
            var fit = matches[0];
            return ($"{(P.ExplicitPick ? fit.model.Title.GetFormattedText() + ": " : "")}{fit.fit.reason}", Above(holders));
        }
        if (screen is NRewardsScreen)
        {
            var buttons = Visible<NRewardButton>(screen).Where(b => b.IsEnabled).ToArray();
            var pots = buttons.Where(b => b.Reward is PotionReward { Potion: not null }).ToArray();
            if (pots.Length > 0)
            {
                var offers = pots.Select(b => ((PotionReward)b.Reward!).Potion!).Select(x => new Bottle(x.Id.Entry, x.Title.GetFormattedText())).ToArray();
                var belt = p.Potions.Select(x => new Bottle(x.Id.Entry, x.Title.GetFormattedText())).ToArray();
                var next = r.CurrentMapPoint?.Children;
                bool giantNext = next?.Count > 0 && next.All(n => n == r.Map.BossMapPoint)
                    && r.Act.BossEncounter.Id.Entry == "WATERFALL_GIANT_BOSS";
                var advice = PotionCoach.Recommend(offers, belt, p.PotionSlots.Count, giantNext, P.ExplicitPick);
                if (advice != null) return (advice, Above(pots));
                if (buttons.Length == pots.Length) return null;
            }
            return buttons.Length == 0 ? null : ($"Check the rewards before leaving. You have {p.Potions.Count()} potions and {p.Deck.Cards.Count} cards; you needn’t take every card.", Above(buttons));
        }
        // Additional selection overlays (including modded ones): use only visible
        // enabled choices and avoid mistaking pause/inspection screens for decisions.
        if (screen.GetType().Name.Contains("Select", StringComparison.OrdinalIgnoreCase) || screen.GetType().Name.Contains("Choose", StringComparison.OrdinalIgnoreCase))
        {
            var buttons = Visible<NClickableControl>(screen).Where(b => b.IsEnabled).ToArray();
            if (buttons.Length >= 2) return ($"Compare these options with the next fight in mind: {p.Creature.CurrentHp} HP, {p.Potions.Count()} potions available.", Above(buttons));
        }
        return null;
    }
    static Vector2? RewardAnchor()
    {
        if (cardRow == null || !GodotObject.IsInstanceValid(cardRow) || !cardRow.IsVisibleInTree()) return null;
        Rect2? bounds = null;
        foreach (var child in cardRow.GetChildren())
        {
            if (child is not NCardHolder holder || !holder.IsVisibleInTree() || holder.Hitbox is not { } hit || !hit.Size.IsFinite() || hit.Size.X <= 0 || hit.Size.Y <= 0) continue;
            var rect = hit.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, hit.Size);
            bounds = bounds?.Merge(rect) ?? rect;
        }
        return bounds is { } row ? new Vector2(row.GetCenter().X, row.Position.Y) : null;
    }
    static RewardLayout RewardSpace(Control screen, Player p)
    {
        var view = screen.GetViewportRect();
        var creature = NCombatRoom.Instance?.GetCreatureNode(p.Creature)?.Hitbox;
        Vector2 speaker = creature != null && creature.IsVisibleInTree() ? Above([creature]) ?? view.Size * new Vector2(.18f, .55f) : view.Size * new Vector2(.18f, .55f);
        static System.Drawing.RectangleF Rect(Rect2 r) => new(r.Position.X, r.Position.Y, r.Size.X, r.Size.Y);
        var blocked = new List<System.Drawing.RectangleF> { new(0, 0, view.Size.X, view.Size.Y * .17f) };
        void Block(Control? c)
        {
            if (c == null || !c.IsVisibleInTree()) return;
            var rect = (c.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, c.Size)).Intersection(view);
            if (rect.HasArea()) blocked.Add(Rect(rect));
        }
        foreach (var path in new[] { "Rewards", "Rewards/Background/Banner", "ProceedButton", "UI/Banner", "UI/RewardAlternatives", "UI/InspectPrompt" })
            Block(screen.GetNodeOrNull<Control>(path));
        foreach (var h in Visible<NCardHolder>(screen)) Block(h.Hitbox);
        foreach (var c in Visible<NClickableControl>(screen)) Block(c);
        return new(Rect(view), new(speaker.X, speaker.Y), blocked);
    }
    static string? RewardText(bool details, bool requested)
    {
        var top = ranking[0];
        if (ranking.Any(x => double.IsNaN(x.Score))) return "An unfamiliar card is on offer. I can’t reliably compare the whole set.";
        var close = ranking.Length > 1 && top.Score - ranking[1].Score <= .55;
        if (P.ClearLeadOnly && close && !requested) return null;
        string text = (P.ExplicitPick ? (close ? "Close call: " : P.Voice == "Vakuu" ? "I’d take " : "Consider ") + top.Name + ". " : "") + top.Reason;
        if (details || close) text += "\n" + string.Join("\n", ranking.Skip(1).Take(details ? 5 : 1).Select(x => $"{x.Name}: {x.Reason}"));
        return text.TrimEnd();
    }
    static RouteStop Route(MegaCrit.Sts2.Core.Map.MapPoint node, int depth) => new(node.PointType.ToString(),
        depth == 1 ? [] : node.Children.OrderBy(n => n.coord.col).Select(n => Route(n, depth - 1)).ToArray());
    static string? Camp(RestSiteRoom room, Player p)
    {
        var options = room.Options.Where(x => x.IsEnabled).Select(x => x.OptionId.ToUpperInvariant()).ToArray();
        if (!options.Contains("HEAL") || !options.Contains("SMITH")) return null;
        if (p.Creature.CurrentHp * 2 <= p.Creature.MaxHp) return $"Rest is worth considering at {p.Creature.CurrentHp}/{p.Creature.MaxHp} HP. An upgrade only helps once you draw it.";
        var deck = DeckOf(session!, p);
        var upgrade = p.Deck.Cards.Where(c => c.IsUpgradable && !c.Id.Entry.StartsWith("STRIKE_") && !c.Id.Entry.StartsWith("DEFEND_")).OrderByDescending(c => Coach.Assess(Read(c), deck).Score).FirstOrDefault();
        return upgrade == null ? null : $"You have health to spare. Inspect the upgrade on {upgrade.Title} before resting.";
    }
    static string? Shop(MerchantRoom room, RunState r, Player p)
    {
        var inv = NMerchantRoom.Instance?.Inventory?.Inventory; if (inv == null) return null;
        var deck = DeckOf(r, p);
        var best = inv.CardEntries.Where(e => e.IsStocked && e.EnoughGold && e.CreationResult?.Card != null)
            .Select(e => (v: Coach.Assess(Read(e.CreationResult!.Card), deck), cost: e.Cost)).Where(x => x.v.Score >= 1.6)
            .OrderByDescending(x => x.v.Score / (1 + x.cost / 100.0)).FirstOrDefault();
        if (best.v != null) return $"Among the affordable cards: {best.v.Name} ({best.cost} gold). {best.v.Reason}";
        if (inv.CardRemovalEntry is { IsStocked: true, EnoughGold: true } rem && deck.Cards.Any(x => x.Id.StartsWith("STRIKE_")))
            return $"Removal costs {rem.Cost} of your {p.Gold} gold. If damage is covered, thinning a basic Strike makes your other cards come up sooner.";
        return null;
    }
    static (string?, string?) Battle(Player p, bool details)
    {
        var state = CombatManager.Instance.DebugOnlyGetState(); var pc = p.PlayerCombatState;
        if (!CombatManager.Instance.IsInProgress || state?.CurrentSide != CombatSide.Player || pc?.Phase != PlayerTurnPhase.Play) return (null, null);
        var enemies = state.Enemies.Where(e => e.IsAlive).ToArray();
        if (p.Relics.Any(x => x.Id.Entry == "RUNIC_DOME") || enemies.Any(e => e.Monster?.NextMove == null)) return (null, null);
        if (enemies.Length == 0) return (null, null);
        var foes = enemies.Select(e => new Foe(e.Monster!.Title.GetFormattedText(), e.CurrentHp + e.Block,
            e.Monster.NextMove!.Intents.OfType<AttackIntent>().Sum(a => a.GetTotalDamage(state.PlayerCreatures, e)),
            new Status(Power(e, "WEAK_POWER"), Power(e, "VULNERABLE_POWER"), Power(e, "ARTIFACT_POWER")))).ToArray();
        static int Power(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, string id) =>
            creature.Powers.FirstOrDefault(z => z.Id.Entry == id)?.Amount ?? 0;
        var hand = pc.Hand.Cards.Where(c => c.CanPlay()).ToArray();
        var plays = new List<Play>();
        foreach (var c in hand)
        {
            var card = Read(c); if (!card.Known) continue;
            bool area = c.TargetType == TargetType.AllEnemies, single = c.TargetType == TargetType.AnyEnemy;
            var hits = Enumerable.Repeat(-1, enemies.Length).ToArray();
            int guard = 0;
            var vars = c.DynamicVars.Clone(c);
            for (int j = 0; j < (single || area ? enemies.Length : 1); j++)
            {
                var enemy = enemies[j];
                if (single && (!c.CanPlayTargeting(enemy) || card.Id == "NAIL" && Power(enemy, "IMPALED_POWER") == 0)) continue;
                c.UpdateDynamicVarPreview(CardPreviewMode.Normal, single || area ? enemy : null, vars);
                int Value(string name) => vars.TryGetValue(name, out var v) ? Math.Max(0, (int)v.PreviewValue) : 0;
                if (card.Has("guard")) guard = Value("Block");
                if (single || area)
                {
                    int repeats = card.Has("multiple hits") && vars.TryGetValue("HitCount", out var count) ? Math.Max(1, (int)count.BaseValue) : 1;
                    hits[j] = card.Attack ? repeats * Math.Max(Value("Damage"), Math.Max(Value("CalculatedDamage"), Value("OstyDamage"))) : 0;
                }
            }
            if (single && hits.All(n => n < 0)) continue;
            bool pivot = card.Has("draw") || card.Has("energy") || card.Has("discard") || card.Has("exhaust") || c.Type == CardType.Power;
            int Base(string name) => vars.TryGetValue(name, out var v) ? Math.Max(0, (int)v.BaseValue) : 0;
            int weak = card.Has("weak") ? Math.Max(Base("WeakPower"), Base("Power")) : 0;
            int vuln = card.Has("vulnerable") ? Math.Max(Base("VulnerablePower"), Base("Power")) : 0;
            int sad = card.Id switch { "VITRIOL" => 1, "JEST" or "SORROWFUL_STRIKE" => Base("Cards"), _ => 0 };
            if (guard == 0 && hits.All(n => n <= 0) && !pivot && weak + vuln + sad == 0) continue;
            plays.Add(new(c.Title, c.EnergyCost.CostsX ? 0 : Math.Max(0, c.EnergyCost.GetAmountToSpend()),
                c.HasStarCostX ? 0 : Math.Max(0, c.GetStarCostWithModifiers()), card.Has("shots") ? 1 : 0, guard, hits, area, pivot,
                c.Type == CardType.Power, c.EnergyCost.CostsX, c.HasStarCostX, weak, vuln, sad, card.Id == "PUNCH_OF_JOY",
                c.Type == CardType.Skill, card.Id switch { "FINISHER" => "attacks", "LUNAR_BLAST" => "skills", _ => "" }, card.Attack,
                card.Has("hp loss") ? Base("HpLoss") : 0));
        }
        var key = $"{session?.CurrentActIndex}:{session?.ActFloor}:{state.RoundNumber}";
        var played = CombatManager.Instance.History.CardPlaysFinished.Where(e => e.HappenedThisTurn(state) && e.CardPlay.Card.Owner == p)
            .Select(e => e.CardPlay.Card.Type).ToArray();
        var line = TurnCoach.Plan(plays.ToArray(), foes, pc.Energy, pc.Stars, pc.AvailableArrowCount, p.Creature.Block, p.Creature.CurrentHp,
            pc.Hand.Cards.Count(c => c.Id.Entry == "SADNESS"), played.Count(t => t == CardType.Attack), played.Count(t => t == CardType.Skill),
            Power(p.Creature, "ALL_IS_WELL_POWER"), pc.Hand.Cards.Count);
        string Describe(Step step)
        {
            var play = plays[step.Card]; if (step.Target < 0 || foes.Length == 1) return play.Name;
            var foe = foes[step.Target]; return $"{play.Name} on {foe.Name}" +
                (foes.Count(f => f.Name == foe.Name) > 1 ? $" ({enemies[step.Target].CurrentHp} HP)" : "");
        }
        string? text = line.Steps.Count == 0 ? (hand.Length > 0 ? "No clear line from damage and block alone. Inspect the other effects in your hand." : null)
            : $"Turn {state.RoundNumber}: {string.Join(" → ", line.Steps.Select(Describe))}.\n" +
            (line.EnemiesLeft == 0 ? "Projected lethal from current previews." : line.Joy > 0 ? "Turn Sadness into Joy. Play Joy and reassess the draw." : plays[line.Steps[^1].Card].Pivot ? "Reassess after that effect resolves." :
            $"About {line.Incoming} incoming damage left after block and projected kills.");
        if (P.CombatPotions && !CombatManager.Instance.PlayerActionsDisabled)
        {
            var belt = p.Potions.Where(b => !b.IsQueued && b.PassesCustomUsabilityCheck &&
                b.Usage != MegaCrit.Sts2.Core.Entities.Potions.PotionUsage.Automatic).Select(b =>
                {
                    var stat = b.Id.Entry is "BLOCK_POTION" or "SHIP_IN_A_BOTTLE" ? "Block" : "Damage";
                    int amount = b.DynamicVars.TryGetValue(stat, out var v) ? (int)v.BaseValue : 0;
                    return new CombatBottle(b.Id.Entry, b.Title.GetFormattedText(), amount,
                        enemies.Select(e => b.TargetType != TargetType.AnyEnemy || b.IsValidTarget(e)).ToArray());
                }).ToArray();
            var cards = pc.Hand.Cards;
            var fact = new BeltContext(pc.Energy, state.RoundNumber, cards.Count, pc.DrawPile.Cards.Count, pc.DiscardPile.Cards.Count,
                p.PotionSlots.Count - p.Potions.Count(), p.Creature.MaxHp - p.Creature.CurrentHp,
                cards.Count(c => c.Type is CardType.Status or CardType.Curse), cards.Count(c => c.IsUpgradable),
                cards.Count(c => !c.EnergyCost.CostsX && c.EnergyCost.GetWithModifiers(CostModifiers.All) > pc.Energy),
                cards.Count(c => c.GetStarCostWithModifiers() > pc.Stars), cards.Count(c => c.Id.Entry.Contains("STRIKE")),
                pc.OrbQueue?.Capacity > 0, hand.Any(k => Read(k).Has("Osty attacks")),
                cards.Any(c => c.Id.Entry == "SOVEREIGN_BLADE"), plays.Any(c => c.Arrows > 0), plays.Any(c => c.Converts));
            var hint = PotionCoach.Use(belt, foes, plays.ToArray(), line, p.Creature.CurrentHp,
                session?.CurrentRoom?.RoomType.ToString() is "Boss" or "Elite", fact);
            if (hint != null)
            {
                // Keep the potion instruction visible inside Vakuu's fixed-size bubble.
                text = line.Steps.Count == 0 ? hint : $"{hint}\n{string.Join(" → ", line.Steps.Select(Describe))}.";
                return (text, key);
            }
        }
        if (line.Joy > 0 && line.EnemiesLeft > 0 && line.Incoming < p.Creature.CurrentHp)
        {
            // Stop the advice at the first new draw rather than prescribing a
            // follow-up before we know which card Joy actually draws.
            int sadness = pc.Hand.Cards.Count(c => c.Id.Entry == "SADNESS");
            for (int i = 0; i < line.Steps.Count; i++)
            {
                var play = plays[line.Steps[i].Card];
                if (play.Converts && sadness > 0)
                    return ($"Turn {state.RoundNumber}: {string.Join(" → ", line.Steps.Take(i + 1).Select(Describe))} → Joy.\nDraw before committing to your next card.", key);
                sadness += play.Sadness;
            }
        }
        if (text != null && session?.CurrentRoom is CombatRoom room && Catalog.Bosses.TryGetValue(room.Encounter.Id.Entry, out var tactic)
            && (state.RoundNumber == 1 || details || line.EnemiesLeft == 0))
        {
            string moves = string.Join(" → ", line.Steps.Select(Describe));
            return (moves.Length > 0 ? $"{moves}.\n{tactic}" : tactic, key);
        }
        if (text != null && details)
        {
            string resource = p.Character.Id.Entry switch
            {
                "REGENT" => $"{pc.Stars} stars",
                "FOOL" => $"{pc.AvailableArrowCount} arrows available",
                "DEFECT" => $"{pc.OrbQueue?.Orbs.Count() ?? 0}/{pc.OrbQueue?.Capacity ?? 0} orb slots filled",
                "NECROBINDER" => $"Osty: {p.Osty?.CurrentHp ?? 0} HP",
                _ => $"{pc.ExhaustPile.Cards.Count} exhausted"
            };
            text += $"\n{resource} · {pc.DrawPile.Cards.Count} to draw · {pc.DiscardPile.Cards.Count} discarded";
        }
        return (text, key);
    }
}
