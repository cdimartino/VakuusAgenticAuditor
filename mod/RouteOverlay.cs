using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace Auditor;

/// <summary>A quiet, non-interactive route trace drawn above the map.</summary>
internal static class RouteOverlay
{
    const float Dash = 9f, Gap = 7f;
    public static Control Show(NMapScreen map, IEnumerable<NMapPoint> route)
    {
        // Use native signals: this assembly does not require a registered C# Node script.
        var layer = new Control { Name = "VakuuRouteOverlay", MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 20, ClipContents = true };
        var nodes = route.Take(48).ToArray();
        map.AddChild(layer);
        layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var tree = map.GetTree();
        void Frame()
        {
            if (layer.IsQueuedForDeletion()) return;
            if (!map.IsOpen || !map.IsVisibleInTree()) { layer.QueueFree(); return; }
            layer.QueueRedraw();
        }
        tree.ProcessFrame += Frame;
        layer.TreeExiting += () => tree.ProcessFrame -= Frame;
        layer.Draw += () => Draw(layer, nodes);
        return layer;
    }

    static void Draw(Control layer, NMapPoint[] nodes)
    {
        var inverse = layer.GetGlobalTransformWithCanvas().AffineInverse();
        var points = new List<Vector2>(nodes.Length);
        foreach (var node in nodes)
        {
            if (!GodotObject.IsInstanceValid(node) || !node.IsVisibleInTree()) break;
            points.Add(inverse * (node.GetGlobalTransformWithCanvas() * (node.Size * .5f)));
        }
        if (points.Count < 2) return;

        // Warm, low-contrast dashes leave map labels and node artwork legible.
        var color = new Color(1f, .79f, .45f, .72f);
        float period = Dash + Gap;
        float distance = -(float)(Time.GetTicksMsec() % 2000) * .024f;
        for (int i = 1; i < points.Count; i++)
        {
            var a = points[i - 1]; var b = points[i];
            float length = a.DistanceTo(b);
            if (length < .1f) continue;
            var direction = (b - a) / length;
            float cursor = 0;
            while (cursor < length)
            {
                float phase = ((distance + cursor) % period + period) % period;
                float remaining = phase < Dash ? Dash - phase : period - phase;
                float end = Math.Min(length, cursor + remaining);
                if (phase < Dash && end > cursor)
                    layer.DrawLine(a + direction * cursor, a + direction * end, color, 2f, true);
                cursor = end + .001f;
            }
            distance += length;
        }
    }

}
