namespace Auditor;

// Fractions of the space the artwork can travel, not absolute screen pixels.
internal sealed record BubbleSpot(float X, float Y)
{
    public static BubbleSpot From(float left, float top, float width, float height, float viewWidth, float viewHeight)
        => new(Fraction(left, width, viewWidth), Fraction(top, height, viewHeight));
    static float Fraction(float p, float size, float viewport) =>
        Math.Clamp((p - 6) / Math.Max(1, viewport - size - 12), 0, 1);
    public (float Left, float Top) Restore(float width, float height, float viewWidth, float viewHeight) =>
        (6 + Math.Clamp(X, 0, 1) * Math.Max(0, viewWidth - width - 12), 6 + Math.Clamp(Y, 0, 1) * Math.Max(0, viewHeight - height - 12));
    public static bool Moved(float dx, float dy) => dx * dx + dy * dy >= 36;
}
