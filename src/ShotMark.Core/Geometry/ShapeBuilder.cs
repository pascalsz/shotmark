namespace ShotMark.Core.Geometry;

public static class ShapeBuilder
{
    public sealed record ArrowParts(PointD ShaftStart, PointD ShaftEnd, PointD[] Head);

    public static double HeadLength(double strokeWidth, double arrowLength) =>
        Math.Min(Math.Max(strokeWidth * 3.5, 14), arrowLength);

    public static ArrowParts Arrow(PointD from, PointD to, double strokeWidth)
    {
        var length = from.DistanceTo(to);
        var ux = length < 1e-9 ? 1 : (to.X - from.X) / length;
        var uy = length < 1e-9 ? 0 : (to.Y - from.Y) / length;
        var headLength = HeadLength(strokeWidth, length);
        var halfWidth = headLength * 0.55;
        var baseCenter = new PointD(to.X - ux * headLength, to.Y - uy * headLength);
        var normal = new PointD(-uy, ux);
        var head = new[] { to, baseCenter + normal * halfWidth, baseCenter - normal * halfWidth };
        var shaftEnd = new PointD(to.X - ux * headLength * 0.8, to.Y - uy * headLength * 0.8);
        return new ArrowParts(from, shaftEnd, head);
    }

    public static PointD[] CheckPolyline(RectD r) => [At(r, 0.14, 0.54), At(r, 0.40, 0.80), At(r, 0.88, 0.20)];

    public static double CheckStrokeWidth(RectD r) => Math.Max(2, Math.Min(r.Width, r.Height) * 0.14);

    public static (PointD A, PointD B)[] CrossLines(RectD r) =>
        [(At(r, 0.2, 0.2), At(r, 0.8, 0.8)), (At(r, 0.8, 0.2), At(r, 0.2, 0.8))];

    public static double CrossStrokeWidth(RectD r) => Math.Max(2, Math.Min(r.Width, r.Height) * 0.13);

    private static PointD At(RectD r, double fx, double fy) => new(r.X + r.Width * fx, r.Y + r.Height * fy);
}
