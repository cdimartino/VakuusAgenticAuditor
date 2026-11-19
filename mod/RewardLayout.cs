using System.Drawing;
namespace Auditor;

internal record RewardLayout(RectangleF View, PointF Speaker, List<RectangleF> Blocked)
{
    internal PointF? Place(float w, float h)
    {
        // Try the speaker first, then the nearest obstacle edge. Include a
        // margin for Vakuu's shadow and bobbing animation.
        var safe = RectangleF.Inflate(View, -10, -10);
        if (w > safe.Width || h > safe.Height) return null;
        var desired = new PointF(Speaker.X - w / 2, Speaker.Y - h - 12);
        var avoid = Blocked.Select(b => RectangleF.Inflate(b, 10, 10)).ToArray();
        var xs = avoid.SelectMany(b => new[] { b.Left - w, b.Right }).Append(desired.X).Append(safe.Left).Append(safe.Right - w);
        var ys = avoid.SelectMany(b => new[] { b.Top - h, b.Bottom }).Append(desired.Y).Append(safe.Top).Append(safe.Bottom - h);
        return (from x in xs.Distinct()
                from y in ys.Distinct()
                let rect = new RectangleF(x, y, w, h)
                where safe.Contains(rect) && !avoid.Any(b => b.IntersectsWith(rect))
                orderby (x - desired.X) * (x - desired.X) + (y - desired.Y) * (y - desired.Y)
                select (PointF?)rect.Location).FirstOrDefault();
    }
}
