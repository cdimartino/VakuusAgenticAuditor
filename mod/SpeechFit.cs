using Godot;

namespace Auditor;

// Keep the game's artwork and animation, but let the wrapped text determine
// its bounds. Stretch the art's parent, never the font or the animated sprite.
internal sealed class SpeechFit
{
    internal readonly Sprite2D Art;
    readonly Node2D frame = new();
    readonly RichTextLabel text;
    readonly Vector2 native;
    internal bool Scrolls { get; private set; }

    internal SpeechFit(Node scene, string body, int font)
    {
        Art = scene.GetNode<Sprite2D>("%Bubble");
        var original = scene.GetNode<RichTextLabel>("%Text");
        original.Hide();
        var parent = Art.GetParent();
        parent.AddChild(frame);
        parent.MoveChild(frame, Art.GetIndex());
        Art.Reparent(frame, false);
        native = (Art.Transform * Art.GetRect()).Size;
        text = new RichTextLabel
        {
            Text = body,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        parent.AddChild(text);
        text.AddThemeFontOverride("normal_font", original.GetThemeFont("normal_font"));
        // Native speech uses a material to darken the game's cream UI text.
        // Purple art is translucent and sits over wildly different game scenes.
        // Near-black ink disappears in the dark forest shots; use warm ivory
        // with a subtle dark outline so the copy stays legible over every scene.
        text.AddThemeColorOverride("default_color", new Color("fff4d6"));
        text.AddThemeColorOverride("font_outline_color", new Color("24182e"));
        text.AddThemeConstantOverride("outline_size", 2);
        text.AddThemeConstantOverride("line_separation", 3);
        text.AddThemeFontSizeOverride("normal_font_size", Math.Clamp(font + 2, 14, 32));
    }

    internal void Scroll(float direction) => text.GetVScrollBar().Value += direction * 60;

    internal void Fit(Vector2 view, RewardLayout? rewards)
    {
        // The oval narrows at its corners; its tail is not usable text area.
        const float innerX = .76f, innerY = .56f;
        float maxW = Math.Max(1, view.X - 24), maxH = Math.Max(1, view.Y - 24);
        float best = -1;
        Vector2 chosen = Vector2.One;
        foreach (float width in new[] { native.X, 480, 640, 800, maxW, 240, 180 }.Select(w => Math.Min(w, maxW)).Distinct())
        {
            text.ScrollActive = false;
            text.Size = new(Math.Max(1, Mathf.Floor(width * innerX)), 1);
            var needed = Math.Max(native.Y, Mathf.Ceil((text.GetContentHeight() + 8) / innerY));
            var height = Math.Min(needed, maxH);
            if (rewards != null)
            {
                if (rewards.Place(width, height) == null)
                {
                    float lo = 0, hi = height;
                    for (int i = 0; i < 9; i++)
                    {
                        float mid = (lo + hi) / 2;
                        if (rewards.Place(width, mid) != null) lo = mid; else hi = mid;
                    }
                    height = Mathf.Floor(lo);
                }
            }
            if (height * innerY < text.GetThemeFont("normal_font").GetHeight(text.GetThemeFontSize("normal_font_size")) + 4) continue;
            // Prefer full copy, then a broad readable card rather than the
            // tall, narrow pill produced by choosing the native width first.
            float shape = 1 / (1 + Math.Abs(width / Math.Max(1, height) - 1.25f));
            float score = height >= needed
                ? 3 + shape + 1 / (1 + width * height / 10000)
                : height / needed + shape * .1f;
            if (score > best) { best = score; chosen = new(width, height); }
        }
        // No legal space: PositionBubble hides the bubble rather than covering a choice.
        if (best < 0) chosen = native;
        frame.Scale = chosen / native;
        var rect = frame.Transform * (Art.Transform * Art.GetRect());
        text.Size = (chosen * new Vector2(innerX, innerY)).Floor().Max(Vector2.One);
        text.Position = rect.GetCenter() - text.Size / 2 - new Vector2(0, chosen.Y * .085f);
        Scrolls = text.GetContentHeight() + 4 > text.Size.Y;
        text.ScrollActive = Scrolls;
    }
}
