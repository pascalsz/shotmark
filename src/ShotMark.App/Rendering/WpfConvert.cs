using System.Windows;
using System.Windows.Media;
using ShotMark.Core.Geometry;

namespace ShotMark.App.Rendering;

internal static class WpfConvert
{
    public static Point ToWpf(this PointD p) => new(p.X, p.Y);

    public static PointD ToCore(this Point p) => new(p.X, p.Y);

    public static Rect ToWpf(this RectD r) => new(r.X, r.Y, Math.Max(0, r.Width), Math.Max(0, r.Height));

    public static Color ToWpf(this RgbColor c) => Color.FromRgb(c.R, c.G, c.B);

    public static SolidColorBrush ToBrush(this RgbColor c, double opacity = 1)
    {
        var brush = new SolidColorBrush(c.ToWpf()) { Opacity = opacity };
        brush.Freeze();
        return brush;
    }
}
