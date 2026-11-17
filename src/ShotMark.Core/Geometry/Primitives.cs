using System.Text.Json.Serialization;
namespace ShotMark.Core.Geometry;

public readonly record struct PointD(double X, double Y)
{
    public static PointD operator +(PointD a, PointD b) => new(a.X + b.X, a.Y + b.Y);
    public static PointD operator -(PointD a, PointD b) => new(a.X - b.X, a.Y - b.Y);
    public static PointD operator *(PointD a, double f) => new(a.X * f, a.Y * f);

    public double DistanceTo(PointD other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

public readonly record struct SizeD(double Width, double Height);

public readonly record struct PixelSize(int Width, int Height);

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    [JsonIgnore] public double Right => X + Width;
    [JsonIgnore] public double Bottom => Y + Height;
    [JsonIgnore] public PointD TopLeft => new(X, Y);
    [JsonIgnore] public PointD BottomRight => new(Right, Bottom);
    [JsonIgnore] public PointD Center => new(X + Width / 2, Y + Height / 2);
    [JsonIgnore] public bool IsEmpty => Width <= 0 || Height <= 0;

    public static RectD FromPoints(PointD a, PointD b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    public bool Contains(PointD p) => p.X >= X && p.X <= Right && p.Y >= Y && p.Y <= Bottom;

    public RectD Inflate(double amount) => new(X - amount, Y - amount, Width + 2 * amount, Height + 2 * amount);

    public RectD Offset(double dx, double dy) => this with { X = X + dx, Y = Y + dy };

    public RectD? Intersect(RectD other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        return right > left && bottom > top ? new RectD(left, top, right - left, bottom - top) : null;
    }

    public RectD ToPixelGrid()
    {
        var left = Math.Floor(X);
        var top = Math.Floor(Y);
        return new RectD(left, top, Math.Ceiling(Right) - left, Math.Ceiling(Bottom) - top);
    }
}
