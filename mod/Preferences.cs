using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
namespace Auditor;

internal sealed class Preferences
{
    public string Mode { get; set; } = "Balanced";
    public string Voice { get; set; } = "Vakuu";
    public string Corner { get; set; } = "Upper left";
    public int Font { get; set; } = 18;
    public int Seconds { get; set; } = 9;
    public bool Rewards { get; set; } = true;
    public bool RewardBubble { get; set; } = true;
    public bool Events { get; set; } = true;
    public bool OtherChoices { get; set; } = true;
    public bool Praise { get; set; } = true;
    public bool Critique { get; set; } = true;
    public bool Combat { get; set; } = true;
    public bool CombatPotions { get; set; } = true;
    public bool Routes { get; set; } = true;
    public bool Camps { get; set; } = true;
    public bool Shops { get; set; } = true;
    public bool ExplicitPick { get; set; } = true;
    public bool ClearLeadOnly { get; set; }
    public bool OncePerTurn { get; set; } = true;
    public bool Muted { get; set; }
    public bool AllowClosestCatalog { get; set; }
    public bool CurseMode { get; set; }
    public bool OpenRunReview { get; set; } = true;
    // Local-only analysis capture. The host runtime owns the recorder itself.
    public bool RecordRun { get; set; } = true;
    // Opt-in local snapshot export; Entry owns writing the snapshot file.
    public bool PlaytestBridge { get; set; }
    // Serialized ownership record lets restoration remain safe across restarts.
    public Dictionary<string, string> PlaytestPresetBefore { get; set; } = new();
    public Dictionary<string, string> PlaytestPresetApplied { get; set; } = new();
    public Key SuggestionKey { get; set; } = Key.V;
    public Key SettingsKey { get; set; } = Key.F8;
    public Key MuteKey { get; set; } = Key.F6;
    public Dictionary<string, BubbleSpot> BubblePositions { get; set; } = new();
    static string PathName => ProjectSettings.GlobalizePath("user://mod_configs/VakuusAgenticAuditor/settings.json");
    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static Preferences Load()
    {
        try { return File.Exists(PathName) ? JsonSerializer.Deserialize<Preferences>(File.ReadAllText(PathName)) ?? new() : new(); }
        catch (Exception ex) { GD.PushWarning($"Auditor settings: {ex.Message}"); return new(); }
    }
    public void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(PathName)!); File.WriteAllText(PathName + ".tmp", JsonSerializer.Serialize(this, Json)); File.Move(PathName + ".tmp", PathName, true); }
        catch (Exception ex) { GD.PushWarning($"Auditor settings: {ex.Message}"); }
    }
}

internal sealed class Display
{
    internal Preferences P = Preferences.Load();
    readonly Label note = new() { MouseFilter = Control.MouseFilterEnum.Ignore, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    NSpeechBubbleVfx? speech;
    SpeechFit? fit;
    Vector2 lastAnchor;
    public bool Scrolls => fit?.Scrolls == true;
    internal RewardLayout? RewardSpace;
    internal string ViewType = "other";
    string shownView = "";
    Vector2? down;
    Vector2 offset;
    bool dragged;
    BubbleSpot? preview;
    readonly StyleBoxFlat corner;
    readonly PopupMenu menu = new() { HideOnItemSelection = false };
    readonly AcceptDialog keyPrompt = new() { Title = "Auditor keyboard shortcuts", OkButtonText = "Cancel" };
    Action<Key>? bind;
    Key waitForRelease = Key.None;
    internal event Func<bool>? SettingsPressed;
    readonly SettingsShortcut<Key> settingsShortcut = new();
    bool SettingsBlocked => KeyboardBusy || review.Visible;
    internal void SettingsController(bool pressed)
    {
        if (settingsShortcut.Controller(pressed, SettingsBlocked)) SettingsPressed?.Invoke();
    }
    void SettingsInput(Window source, InputEvent input)
    {
        if (input is not InputEventKey key) return;
        if (!key.Pressed)
        {
            if (waitForRelease == key.Keycode) waitForRelease = Key.None;
        }
        // Window input is delivered even when both edges fall between frames.
        // Track all keys, including keys being rebound; echoes and duplicate
        // delivery through an embedded popup must never toggle a second time.
        if (settingsShortcut.Key(key.Keycode, key.Pressed, key.Echo, P.SettingsKey, SettingsBlocked)
            && SettingsPressed?.Invoke() == true)
            source.SetInputAsHandled();
    }
    public bool KeyboardBusy
    {
        get
        {
            if (waitForRelease != Key.None && !Input.IsKeyPressed(waitForRelease)) waitForRelease = Key.None;
            return keyPrompt.Visible || waitForRelease != Key.None;
        }
    }
    public bool InSettings => menu.Visible || keyPrompt.Visible || review.Visible;
    readonly List<Action> actions = new();
    readonly AcceptDialog review = new() { Title = "Vakuu’s run review", OkButtonText = "Done" };
    readonly TextEdit reviewText = new() { Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new(360, 220) };
    string? latest;
    static string ReviewPath => ProjectSettings.GlobalizePath("user://mod_configs/VakuusAgenticAuditor/latest-run.txt");
    internal void AttachReview(Control screen, string report)
    {
        latest = report;
        try { Directory.CreateDirectory(Path.GetDirectoryName(ReviewPath)!); File.WriteAllText(ReviewPath + ".tmp", report); File.Move(ReviewPath + ".tmp", ReviewPath, true); }
        catch (IOException ex) { GD.PushWarning(ex.Message); }
        var button = new Button
        {
            Text = "Vakuu’s run review",
            FocusMode = Control.FocusModeEnum.All,
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 0,
            OffsetLeft = -224,
            OffsetRight = -20,
            OffsetTop = 20,
            OffsetBottom = 64
        };
        screen.AddChild(button); button.Pressed += ShowReview;
        button.ZIndex = 100;
        button.GrabFocus();
        if (P.OpenRunReview && !P.Muted && P.Mode != "Off") Callable.From(ShowReview).CallDeferred();
    }
    void ShowReview()
    {
        menu.Hide(); Hide();
        if (latest == null && File.Exists(ReviewPath)) latest = File.ReadAllText(ReviewPath);
        reviewText.Text = latest ?? "Finish a solo run with the Auditor enabled to see your review.";
        reviewText.ScrollVertical = 0;
        review.PopupCenteredRatio(.72f);
    }
    readonly CanvasLayer layer = new() { Layer = 80, Name = "VakuuAuditor" };
    readonly string catalogStatus;
    readonly string? richerStatus;
    public Display(SceneTree tree, string catalogStatus) : this(tree, catalogStatus, null) { }
    public Display(SceneTree tree, string catalogStatus, string? richerStatus)
    {
        this.catalogStatus = catalogStatus;
        this.richerStatus = richerStatus;
        tree.Root.AddChild(layer); layer.AddChild(note); layer.AddChild(menu); note.Hide();
        note.GetViewport().SizeChanged += () =>
        {
            if (fit == null) return;
            fit.Fit(note.GetViewportRect().Size, RewardSpace);
            PositionBubble(lastAnchor);
        };
        layer.AddChild(keyPrompt);
        layer.AddChild(review); review.AddChild(reviewText);
        keyPrompt.WindowInput += e =>
        {
            if (e is not InputEventKey { Pressed: true, Echo: false } k || bind == null) return;
            keyPrompt.SetInputAsHandled();
            var key = k.Keycode;
            if (key == Key.Escape) { keyPrompt.Hide(); return; }
            if (key is Key.None or Key.Shift or Key.Ctrl or Key.Alt or Key.Meta || k.IsCommandOrControlPressed() || k.AltPressed || k.MetaPressed || k.ShiftPressed)
            {
                keyPrompt.DialogText = "Choose a single key without modifiers."; return;
            }
            bind(key); waitForRelease = key; P.Save(); keyPrompt.Hide();
        };
        keyPrompt.VisibilityChanged += () =>
        {
            if (!keyPrompt.Visible && bind != null)
            {
                bind = null;
                Callable.From(() => { RefreshSettings(); menu.PopupCentered(); }).CallDeferred();
            }
        };
        // These are native Window signals: no custom Node/_Input registration.
        // Attach after the Display is fully initialized and its subscribers exist.
        Callable.From(() =>
        {
            foreach (var window in new Window[] { tree.Root, menu, keyPrompt, review })
                window.WindowInput += input => SettingsInput(window, input);
            tree.Root.FocusExited += settingsShortcut.Reset;
        }).CallDeferred();
        corner = new StyleBoxFlat { BgColor = new Color(.07f, .055f, .09f, .93f), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 };
        note.AddThemeStyleboxOverride("normal", corner);
        MakeDismissible(note);
        menu.IdPressed += id =>
        {
            actions[(int)id]();
            if (!menu.Visible) return; // Done
            P.Save();
            Hide();
            RefreshSettings();
        };
        menu.VisibilityChanged += () =>
        {
            if (menu.Visible) GD.Print("[VakuuAuditor] Settings popup visible");
        };
    }
    public bool Visible => note.Visible || GodotObject.IsInstanceValid(speech);
    void MakeDismissible(Control surface, Sprite2D? art = null)
    {
        surface.MouseFilter = Control.MouseFilterEnum.Stop;
        surface.GuiInput += e =>
        {
            var mouse = surface.GetViewport().GetMousePosition();
            if (fit?.Scrolls == true && e is InputEventPanGesture pan)
            {
                fit.Scroll(pan.Delta.Y); surface.AcceptEvent(); return;
            }
            if (fit?.Scrolls == true && e is InputEventMouseButton { Pressed: true } wheel
                && wheel.ButtonIndex is MouseButton.WheelDown or MouseButton.WheelUp)
            {
                fit.Scroll(wheel.ButtonIndex == MouseButton.WheelDown ? 1 : -1);
                surface.AcceptEvent(); return;
            }
            if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left } click)
            {
                surface.AcceptEvent();
                if (click.Pressed)
                {
                    down = mouse; dragged = false;
                    if (art != null) offset = mouse - (art.GlobalTransform * art.GetRect()).Position;
                }
                else
                {
                    if (down == null) return;
                    if (dragged && preview != null)
                    {
                        P.BubblePositions[shownView] = preview; P.Save();
                    }
                    else Hide();
                    down = null; preview = null; dragged = false;
                }
            }
            else if (art != null && e is InputEventMouseMotion && down is { } start)
            {
                var delta = mouse - start;
                if (!dragged && !BubbleSpot.Moved(delta.X, delta.Y)) return;
                surface.AcceptEvent();
                dragged = true;
                var rect = art.GlobalTransform * art.GetRect(); var view = surface.GetViewportRect().Size;
                preview = BubbleSpot.From(mouse.X - offset.X, mouse.Y - offset.Y, rect.Size.X, rect.Size.Y, view.X, view.Y);
                PositionBubble(mouse);
            }
        };
    }
    public void Hide()
    {
        down = null; dragged = false; preview = null;
        note.Hide();
        if (GodotObject.IsInstanceValid(speech)) { speech!.Hide(); speech.QueueFree(); }
        speech = null;
        fit = null;
    }
    public void Show(string body, Vector2? anchor)
    {
        Hide();
        if (P.RewardBubble)
        {
            shownView = ViewType;
            // A choice can disappear before the reaction is drawn. That changes
            // where Vakuu speaks, not which renderer displays the reaction.
            var point = anchor ?? note.GetViewportRect().Size * new Vector2(.5f, .42f);
            speech = NSpeechBubbleVfx.Create(body, DialogueSide.Left, point, 86400, VfxColor.Purple);
            if (speech != null)
            {
                layer.AddChild(speech);
                foreach (var child in speech.FindChildren("*", "Control", true, false).OfType<Control>())
                    child.MouseFilter = Control.MouseFilterEnum.Ignore;
                fit = new SpeechFit(speech, body, P.Font);
                fit.Fit(note.GetViewportRect().Size, RewardSpace);
                var art = fit.Art;
                var bounds = art.GetRect();
                var hit = new Control { Position = bounds.Position, Size = bounds.Size, FocusMode = Control.FocusModeEnum.None };
                art.AddChild(hit);
                MakeDismissible(hit, art);
                PositionBubble(point);
            }
            return;
        }
        note.Text = (P.Voice == "Vakuu" ? "Vakuu whispers" : "Auditor") + $"   ·   {OS.GetKeycodeString(P.SettingsKey)} settings\n" + body;
        int fontSize = Math.Clamp(P.Font, 12, 30);
        note.AddThemeFontSizeOverride("font_size", fontSize);
        var size = note.GetViewportRect().Size;
        var style = corner;
        float natural = note.Text.Split('\n').Max(line => note.GetThemeFont("font").GetStringSize(line, fontSize: fontSize).X);
        float width = Math.Min(Mathf.Ceil(natural + style.ContentMarginLeft + style.ContentMarginRight + 2),
            Math.Min(460, size.X * .40f));
        // ResetSize asks a wrapped Label for its minimum. Pin its real width
        // first, or it can measure the entire message one character per line.
        note.CustomMinimumSize = new(width, 0);
        note.Size = new(width, 0);
        note.ResetSize();
        note.Position = new(P.Corner.EndsWith("right") ? size.X - width - 14 : 14,
            P.Corner.StartsWith("Lower") ? size.Y - note.Size.Y - 16 : Math.Max(56, size.Y * .075f));
        if (RewardSpace is { } layout)
        {
            if (layout.Place(note.Size.X, note.Size.Y) is not { } place) return;
            note.Position = new(place.X, place.Y);
        }
        note.Show();
    }
    public void PositionBubble(Vector2 anchor)
    {
        var size = note.GetViewportRect().Size;
        if (!GodotObject.IsInstanceValid(speech) || fit == null) return;
        lastAnchor = anchor;
        // Native artwork is offset from its speech origin. Position using the
        // sprite bounds instead of stretching the bubble to its parent.
        var art = fit.Art;
        var rect = art.GlobalTransform * art.GetRect();
        var spot = preview ?? P.BubblePositions.GetValueOrDefault(shownView);
        if (spot != null)
        {
            var pos = spot.Restore(rect.Size.X, rect.Size.Y, size.X, size.Y);
            anchor = new(pos.Left + rect.Size.X / 2, pos.Top + rect.Size.Y + 10);
        }
        if (RewardSpace is { } layout)
        {
            // If the normal size won't fit, try a smaller native bubble before
            // giving up the space to the game's controls.
            var baseSize = rect.Size / speech!.Scale.X;
            foreach (var zoom in new[] { 1f, .85f, .72f })
            {
                var preferred = spot == null ? layout : layout with { Speaker = new(anchor.X, anchor.Y + 2) };
                if (preferred.Place(baseSize.X * zoom, baseSize.Y * zoom) is not { } place) continue;
                speech.Scale = new(zoom, zoom);
                rect = art.GlobalTransform * art.GetRect();
                speech.GlobalPosition += new Vector2(place.X, place.Y) - rect.Position;
                speech.Show(); return;
            }
            speech.Hide(); return;
        }
        var left = Math.Clamp(anchor.X - rect.Size.X * .5f, 6, Math.Max(6, size.X - rect.Size.X - 6));
        var top = Math.Clamp(anchor.Y - rect.Size.Y - 10, 6, Math.Max(6, size.Y - rect.Size.Y - 6));
        speech!.GlobalPosition += new Vector2(left, top) - rect.Position;
    }
    public void Settings()
    {
        if (menu.Visible) { menu.Hide(); return; }
        RefreshSettings();
        menu.PopupCentered();
    }
    void RefreshSettings()
    {
        actions.Clear();
        void Item(string s, Action a)
        {
            int index = actions.Count;
            if (index == menu.ItemCount) menu.AddItem(s, index);
            else menu.SetItemText(index, s);
            actions.Add(a);
        }
        void Toggle(string label, bool v, Action<bool> set) => Item((v ? "✓ " : "   ") + label, () => set(!v));
        static T Next<T>(T current, params T[] values) => values[(Array.IndexOf(values, current) + 1) % values.Length];
        void Shortcut(string label, Key old, Action<Key> set)
        {
            Item(label + ": " + OS.GetKeycodeString(old), () =>
            {
                menu.Hide();
                bind = key =>
                {
                    // Swapping preserves access to both actions.
                    if (P.SuggestionKey == key) P.SuggestionKey = old;
                    else if (P.SettingsKey == key) P.SettingsKey = old;
                    else if (P.MuteKey == key) P.MuteKey = old;
                    set(key);
                };
                keyPrompt.DialogText = $"{label}: press a new key, or Esc to cancel.\nChoosing an assigned Auditor key swaps the two shortcuts.";
                keyPrompt.PopupCentered();
            });
        }
        Toggle("Curse Mode · unreliable advice (scales with Ascension)", P.CurseMode, v => P.CurseMode = v);
        Toggle("Unknown patch: use closest catalog (restart required)", P.AllowClosestCatalog, v => P.AllowClosestCatalog = v);
        Item(catalogStatus, () => { });
        menu.SetItemDisabled(actions.Count - 1, true);
        if (!string.IsNullOrWhiteSpace(richerStatus))
        {
            Item(richerStatus, () => { }); menu.SetItemDisabled(actions.Count - 1, true);
        }
        Shortcut("Show / dismiss suggestion", P.SuggestionKey, k => P.SuggestionKey = k);
        Shortcut("Open settings", P.SettingsKey, k => P.SettingsKey = k);
        Shortcut("Mute / unmute", P.MuteKey, k => P.MuteKey = k);
        Item("Guidance: " + P.Mode, () => P.Mode = Next(P.Mode, "Off", "On demand", "Balanced", "Detailed"));
        Toggle("Quick mute", P.Muted, v => P.Muted = v);
        Toggle("Record game-state snapshots locally for end-of-run analysis (not inputs)", P.RecordRun, v => P.RecordRun = v);
        Toggle("Export playtest setup to local playtest-state.json (no gameplay control)", P.PlaytestBridge, v => P.PlaytestBridge = v);
        Item(P.PlaytestPresetApplied.Count > 0 ? "Restore playtest preset (field-specific)" : "Apply Detailed playtest preset", () =>
        {
            var state = new Dictionary<string, string>
            {
                [nameof(P.Mode)] = P.Mode,
                [nameof(P.Muted)] = P.Muted.ToString(),
                [nameof(P.CurseMode)] = P.CurseMode.ToString(),
                [nameof(P.AllowClosestCatalog)] = P.AllowClosestCatalog.ToString(),
            };
            var preset = new PlaytestPreset(P.PlaytestPresetBefore, P.PlaytestPresetApplied);
            var conflicts = preset.Active ? preset.Restore(state) : preset.Apply(state);
            P.Mode = state[nameof(P.Mode)];
            P.Muted = bool.Parse(state[nameof(P.Muted)]);
            P.CurseMode = bool.Parse(state[nameof(P.CurseMode)]);
            P.AllowClosestCatalog = bool.Parse(state[nameof(P.AllowClosestCatalog)]);
            P.Save();
            if (conflicts.Count > 0) GD.PushWarning("Playtest preset left externally changed settings untouched: " + string.Join(", ", conflicts));
        });
        Toggle("Open review after a run", P.OpenRunReview, v => P.OpenRunReview = v);
        Item("Read latest run review", ShowReview);
        Toggle("Before card picks", P.Rewards, v => P.Rewards = v);
        Toggle("Speech bubbles at choices", P.RewardBubble, v => P.RewardBubble = v);
        Toggle("Event choices", P.Events, v => P.Events = v);
        Toggle("Selection and reward screens", P.OtherChoices, v => P.OtherChoices = v);
        Toggle("After picks: encouragement", P.Praise, v => P.Praise = v);
        Toggle("After picks: tradeoffs", P.Critique, v => P.Critique = v);
        Toggle("Combat priorities", P.Combat, v => P.Combat = v);
        Toggle("When to use combat potions", P.CombatPotions, v => P.CombatPotions = v);
        Toggle("One automatic hint per turn", P.OncePerTurn, v => P.OncePerTurn = v);
        Toggle("Map", P.Routes, v => P.Routes = v);
        Toggle("Campfires", P.Camps, v => P.Camps = v);
        Toggle("Merchants", P.Shops, v => P.Shops = v);
        Toggle("Name the recommended pick", P.ExplicitPick, v => P.ExplicitPick = v);
        Toggle("Only show clear leads", P.ClearLeadOnly, v => P.ClearLeadOnly = v);
        Item("Tone: " + P.Voice, () => P.Voice = Next(P.Voice, "Vakuu", "Plain"));
        Item("Position: " + P.Corner, () => P.Corner = Next(P.Corner, "Upper left", "Upper right", "Lower right", "Lower left"));
        Item("Text size: " + P.Font, () => P.Font = Next(P.Font, 15, 18, 22, 26));
        Item("Show for: " + P.Seconds + "s", () => P.Seconds = Next(P.Seconds, 5, 9, 15, 30));
        Item("Reset bubble positions", () => P.BubblePositions.Clear());
        Item("Reset defaults", () => P = new());
        Item("Done", menu.Hide);
    }
}
