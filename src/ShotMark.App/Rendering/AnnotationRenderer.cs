using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;

namespace ShotMark.App.Rendering;

/// <summary>Zeichnet Markierungen in Bildkoordinaten. Wird für Editor und Export gleichermaßen genutzt.</summary>
internal sealed class AnnotationRenderer(BitmapSource original)
{
    // Weicher Schatten aus drei versetzten, halbtransparenten Lagen (ohne Blur-Effekt, damit Editor und Export identisch sind)
    private static readonly (double Dx, double Dy, byte Alpha, double Grow)[] ShadowLayers =
        [(1, 2, 40, 2), (2, 3, 30, 3), (3, 4, 20, 4)];

    private readonly Dictionary<(Int32Rect Area, int Block), BitmapSource> _pixelCache = [];

    public BitmapSource Original { get; } = original;

    public void DrawAll(DrawingContext dc, IEnumerable<Annotation> annotations)
    {
        foreach (var annotation in annotations)
            Draw(dc, annotation);
    }

    public void Draw(DrawingContext dc, Annotation annotation)
    {
        switch (annotation)
        {
            case RectangleAnnotation r:
                Stroke(dc, new RectangleGeometry(r.Rect.ToWpf()), r.Color, r.StrokeWidth, PenLineJoin.Miter, r.Shadow);
                break;
            case ArrowAnnotation a:
                Fill(dc, ArrowGeometry(a), a.Color, a.Shadow);
                break;
            case CheckAnnotation c:
                Stroke(dc, Polyline(ShapeBuilder.CheckPolyline(c.Rect)), c.Color, ShapeBuilder.CheckStrokeWidth(c.Rect), PenLineJoin.Round, c.Shadow, round: true);
                break;
            case CrossAnnotation x:
                Stroke(dc, CrossGeometry(x.Rect), x.Color, ShapeBuilder.CrossStrokeWidth(x.Rect), PenLineJoin.Round, x.Shadow, round: true);
                break;
            case TextAnnotation t:
                DrawText(dc, t);
                break;
            case BlurAnnotation b:
                DrawPixelated(dc, b);
                break;
        }
    }

    private static void Stroke(DrawingContext dc, Geometry geometry, RgbColor color, double width, PenLineJoin join, bool shadow, bool round = false)
    {
        var cap = round ? PenLineCap.Round : PenLineCap.Flat;
        if (shadow)
        {
            foreach (var (dx, dy, alpha, grow) in ShadowLayers)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)), width + grow)
                {
                    LineJoin = join, StartLineCap = cap, EndLineCap = cap,
                };
                dc.PushTransform(new TranslateTransform(dx, dy));
                dc.DrawGeometry(null, pen, geometry);
                dc.Pop();
            }
        }
        dc.DrawGeometry(null, new Pen(color.ToBrush(), width) { LineJoin = join, StartLineCap = cap, EndLineCap = cap }, geometry);
    }

    private static void Fill(DrawingContext dc, Geometry geometry, RgbColor color, bool shadow)
    {
        if (shadow)
        {
            foreach (var (dx, dy, alpha, grow) in ShadowLayers)
            {
                var brush = new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0));
                dc.PushTransform(new TranslateTransform(dx, dy));
                dc.DrawGeometry(brush, new Pen(brush, grow) { LineJoin = PenLineJoin.Round }, geometry);
                dc.Pop();
            }
        }
        var fill = color.ToBrush();
        // Dünner Rand in gleicher Farbe glättet die Kanten
        dc.DrawGeometry(fill, new Pen(fill, 1) { LineJoin = PenLineJoin.Round }, geometry);
    }

    /// <summary>Pfeil als ein gefülltes Polygon: leicht verjüngter Schaft plus Spitze.</summary>
    internal static Geometry ArrowGeometry(ArrowAnnotation a)
    {
        var parts = ShapeBuilder.Arrow(a.From, a.To, a.StrokeWidth);
        var tip = parts.Head[0];
        var left = parts.Head[1];
        var right = parts.Head[2];
        var baseCenter = new PointD((left.X + right.X) / 2, (left.Y + right.Y) / 2);
        var length = a.From.DistanceTo(baseCenter);
        var ux = length < 1e-9 ? 0 : (baseCenter.X - a.From.X) / length;
        var uy = length < 1e-9 ? 0 : (baseCenter.Y - a.From.Y) / length;
        var normal = new PointD(-uy, ux);
        var tailHalf = a.StrokeWidth * 0.3;
        var neckHalf = a.StrokeWidth * 0.5;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure((a.From + normal * tailHalf).ToWpf(), isFilled: true, isClosed: true);
            ctx.PolyLineTo(
            [
                (baseCenter + normal * neckHalf).ToWpf(),
                left.ToWpf(),
                tip.ToWpf(),
                right.ToWpf(),
                (baseCenter - normal * neckHalf).ToWpf(),
                (a.From - normal * tailHalf).ToWpf(),
            ], isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry Polyline(PointD[] points)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0].ToWpf(), isFilled: false, isClosed: false);
            ctx.PolyLineTo(points.Skip(1).Select(p => p.ToWpf()).ToList(), isStroked: true, isSmoothJoin: true);
        }
        geometry.Freeze();
        return geometry;
    }

    private static Geometry CrossGeometry(RectD rect)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            foreach (var (a, b) in ShapeBuilder.CrossLines(rect))
            {
                ctx.BeginFigure(a.ToWpf(), isFilled: false, isClosed: false);
                ctx.LineTo(b.ToWpf(), isStroked: true, isSmoothJoin: false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    private void DrawText(DrawingContext dc, TextAnnotation t)
    {
        if (string.IsNullOrEmpty(t.Text)) return;
        var formatted = WpfTextMeasurer.Create(t.Text, t.FontSize, t.Color.ToBrush());
        var origin = new Point(t.Position.X + WpfTextMeasurer.Padding, t.Position.Y + WpfTextMeasurer.Padding);
        var geometry = formatted.BuildGeometry(origin);
        // Weiße Kontur zuerst, damit der Text auf jedem Hintergrund lesbar bleibt
        dc.DrawGeometry(null, new Pen(Brushes.White, Math.Max(2, t.FontSize * 0.18)) { LineJoin = PenLineJoin.Round }, geometry);
        dc.DrawGeometry(t.Color.ToBrush(), null, geometry);
    }

    private void DrawPixelated(DrawingContext dc, BlurAnnotation b)
    {
        var bounds = new RectD(0, 0, Original.PixelWidth, Original.PixelHeight);
        if (b.Rect.ToPixelGrid().Intersect(bounds) is not { } area) return;
        var rect = new Int32Rect((int)area.X, (int)area.Y, (int)area.Width, (int)area.Height);
        if (rect.Width <= 0 || rect.Height <= 0) return;

        var block = Math.Max(2, b.BlockSize);
        var smallWidth = Math.Max(1, (int)Math.Ceiling(rect.Width / (double)block));
        var smallHeight = Math.Max(1, (int)Math.Ceiling(rect.Height / (double)block));
        var small = Pixelated(rect, block, smallWidth, smallHeight);

        var group = new DrawingGroup();
        RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.NearestNeighbor);
        using (var gdc = group.Open())
            gdc.DrawImage(small, new Rect(rect.X, rect.Y, smallWidth * block, smallHeight * block));

        dc.PushClip(new RectangleGeometry(new Rect(rect.X, rect.Y, rect.Width, rect.Height)));
        dc.DrawDrawing(group);
        dc.Pop();
    }

    private BitmapSource Pixelated(Int32Rect rect, int block, int smallWidth, int smallHeight)
    {
        if (_pixelCache.TryGetValue((rect, block), out var cached)) return cached;
        if (_pixelCache.Count > 64) _pixelCache.Clear();
        var crop = new CroppedBitmap(Original, rect);
        var small = new TransformedBitmap(crop, new ScaleTransform(smallWidth / (double)rect.Width, smallHeight / (double)rect.Height));
        small.Freeze();
        _pixelCache[(rect, block)] = small;
        return small;
    }
}
