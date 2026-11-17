namespace ShotMark.Core.Geometry;

public static class Constrain
{
    public static PointD Square(PointD start, PointD current)
    {
        var dx = current.X - start.X;
        var dy = current.Y - start.Y;
        var side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new PointD(start.X + (dx < 0 ? -side : side), start.Y + (dy < 0 ? -side : side));
    }

    public static PointD Angle45(PointD start, PointD current)
    {
        var length = start.DistanceTo(current);
        if (length < 1e-9) return current;
        var angle = Math.Atan2(current.Y - start.Y, current.X - start.X);
        var snapped = Math.Round(angle / (Math.PI / 4)) * (Math.PI / 4);
        return new PointD(start.X + Math.Cos(snapped) * length, start.Y + Math.Sin(snapped) * length);
    }
}
