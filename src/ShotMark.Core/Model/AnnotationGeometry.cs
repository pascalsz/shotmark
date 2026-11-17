using ShotMark.Core.Geometry;
namespace ShotMark.Core.Model;

public interface ITextMeasurer
{
    SizeD Measure(string text, double fontSize);
}

public enum HandleKind { TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, ArrowFrom, ArrowTo }

public static class AnnotationGeometry
{
    public const double MinSize = 4;

    public static RectD Bounds(Annotation annotation, ITextMeasurer measurer) => annotation switch
    {
        RectangleAnnotation r => r.Rect,
        ArrowAnnotation a => RectD.FromPoints(a.From, a.To),
        StampAnnotation s => s.Rect,
        TextAnnotation t => TextBounds(t, measurer),
        BlurAnnotation b => b.Rect,
        _ => throw new ArgumentOutOfRangeException(nameof(annotation)),
    };

    public static RectD TextBounds(TextAnnotation text, ITextMeasurer measurer)
    {
        var size = measurer.Measure(text.Text, text.FontSize);
        return new RectD(text.Position.X, text.Position.Y, size.Width, size.Height);
    }

    public static bool HitTest(Annotation annotation, PointD p, double tolerance, ITextMeasurer measurer) => annotation switch
    {
        RectangleAnnotation r => IsNearBorder(r.Rect, p, tolerance + r.StrokeWidth / 2),
        ArrowAnnotation a => DistanceToSegment(p, a.From, a.To) <= tolerance + a.StrokeWidth,
        _ => Bounds(annotation, measurer).Inflate(tolerance).Contains(p),
    };

    public static Annotation? TopmostAt(IReadOnlyList<Annotation> annotations, PointD p, double tolerance, ITextMeasurer measurer)
    {
        for (var i = annotations.Count - 1; i >= 0; i--)
            if (HitTest(annotations[i], p, tolerance, measurer))
                return annotations[i];
        return null;
    }

    public static Annotation Translate(Annotation annotation, double dx, double dy)
    {
        var delta = new PointD(dx, dy);
        return annotation switch
        {
            RectangleAnnotation r => r with { Rect = r.Rect.Offset(dx, dy) },
            ArrowAnnotation a => a with { From = a.From + delta, To = a.To + delta },
            CheckAnnotation c => c with { Rect = c.Rect.Offset(dx, dy) },
            CrossAnnotation x => x with { Rect = x.Rect.Offset(dx, dy) },
            TextAnnotation t => t with { Position = t.Position + delta },
            BlurAnnotation b => b with { Rect = b.Rect.Offset(dx, dy) },
            _ => throw new ArgumentOutOfRangeException(nameof(annotation)),
        };
    }

    public static IReadOnlyList<(HandleKind Kind, PointD Position)> Handles(Annotation annotation) => annotation switch
    {
        ArrowAnnotation a => [(HandleKind.ArrowFrom, a.From), (HandleKind.ArrowTo, a.To)],
        StampAnnotation s => CornerHandles(s.Rect),
        RectangleAnnotation r => AllRectHandles(r.Rect),
        BlurAnnotation b => AllRectHandles(b.Rect),
        _ => [],
    };

    public static Annotation DragHandle(Annotation annotation, HandleKind handle, PointD p, bool constrain)
    {
        if (annotation is ArrowAnnotation arrow)
        {
            return handle switch
            {
                HandleKind.ArrowFrom => arrow with { From = constrain ? Constrain.Angle45(arrow.To, p) : p },
                HandleKind.ArrowTo => arrow with { To = constrain ? Constrain.Angle45(arrow.From, p) : p },
                _ => arrow,
            };
        }

        if (RectOf(annotation) is not { } r) return annotation;

        RectD resized;
        var isCorner = handle is HandleKind.TopLeft or HandleKind.TopRight or HandleKind.BottomLeft or HandleKind.BottomRight;
        if (isCorner && (annotation is StampAnnotation || constrain))
        {
            var anchor = OppositeCorner(r, handle);
            resized = RectD.FromPoints(anchor, Constrain.Square(anchor, p));
        }
        else
        {
            double left = r.X, top = r.Y, right = r.Right, bottom = r.Bottom;
            switch (handle)
            {
                case HandleKind.TopLeft: left = p.X; top = p.Y; break;
                case HandleKind.Top: top = p.Y; break;
                case HandleKind.TopRight: right = p.X; top = p.Y; break;
                case HandleKind.Right: right = p.X; break;
                case HandleKind.BottomRight: right = p.X; bottom = p.Y; break;
                case HandleKind.Bottom: bottom = p.Y; break;
                case HandleKind.BottomLeft: left = p.X; bottom = p.Y; break;
                case HandleKind.Left: left = p.X; break;
            }
            resized = RectD.FromPoints(new PointD(left, top), new PointD(right, bottom));
        }

        return resized.Width < MinSize || resized.Height < MinSize ? annotation : WithRect(annotation, resized);
    }

    public static ToolStyle StyleOf(Annotation annotation) => annotation switch
    {
        RectangleAnnotation r => new(r.Color, r.StrokeWidth),
        ArrowAnnotation a => new(a.Color, a.StrokeWidth),
        StampAnnotation s => new(s.Color, s.Rect.Width),
        TextAnnotation t => new(t.Color, t.FontSize),
        BlurAnnotation b => new(Palette.Black, b.BlockSize),
        _ => throw new ArgumentOutOfRangeException(nameof(annotation)),
    };

    public static Annotation WithStyle(Annotation annotation, RgbColor? color, double? size) => annotation switch
    {
        RectangleAnnotation r => r with { Color = color ?? r.Color, StrokeWidth = size ?? r.StrokeWidth },
        ArrowAnnotation a => a with { Color = color ?? a.Color, StrokeWidth = size ?? a.StrokeWidth },
        CheckAnnotation c => c with { Color = color ?? c.Color, Rect = size is { } s ? Resize(c.Rect, s) : c.Rect },
        CrossAnnotation x => x with { Color = color ?? x.Color, Rect = size is { } s ? Resize(x.Rect, s) : x.Rect },
        TextAnnotation t => t with { Color = color ?? t.Color, FontSize = size ?? t.FontSize },
        BlurAnnotation b => b with { BlockSize = size is { } s ? Math.Max(2, (int)Math.Round(s)) : b.BlockSize },
        _ => throw new ArgumentOutOfRangeException(nameof(annotation)),
    };

    public static double DistanceToSegment(PointD p, PointD a, PointD b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared < 1e-9) return p.DistanceTo(a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return p.DistanceTo(new PointD(a.X + t * dx, a.Y + t * dy));
    }

    private static RectD? RectOf(Annotation annotation) => annotation switch
    {
        RectangleAnnotation r => r.Rect,
        StampAnnotation s => s.Rect,
        BlurAnnotation b => b.Rect,
        _ => null,
    };

    private static Annotation WithRect(Annotation annotation, RectD rect) => annotation switch
    {
        RectangleAnnotation r => r with { Rect = rect },
        CheckAnnotation c => c with { Rect = rect },
        CrossAnnotation x => x with { Rect = rect },
        BlurAnnotation b => b with { Rect = rect },
        _ => annotation,
    };

    private static RectD Resize(RectD rect, double side) =>
        new(rect.Center.X - side / 2, rect.Center.Y - side / 2, side, side);

    private static PointD OppositeCorner(RectD r, HandleKind handle) => handle switch
    {
        HandleKind.TopLeft => r.BottomRight,
        HandleKind.TopRight => new PointD(r.X, r.Bottom),
        HandleKind.BottomRight => r.TopLeft,
        HandleKind.BottomLeft => new PointD(r.Right, r.Y),
        _ => r.Center,
    };

    private static (HandleKind, PointD)[] CornerHandles(RectD r) =>
    [
        (HandleKind.TopLeft, r.TopLeft), (HandleKind.TopRight, new PointD(r.Right, r.Y)),
        (HandleKind.BottomRight, r.BottomRight), (HandleKind.BottomLeft, new PointD(r.X, r.Bottom)),
    ];

    private static (HandleKind, PointD)[] AllRectHandles(RectD r) =>
    [
        (HandleKind.TopLeft, r.TopLeft), (HandleKind.Top, new PointD(r.Center.X, r.Y)),
        (HandleKind.TopRight, new PointD(r.Right, r.Y)), (HandleKind.Right, new PointD(r.Right, r.Center.Y)),
        (HandleKind.BottomRight, r.BottomRight), (HandleKind.Bottom, new PointD(r.Center.X, r.Bottom)),
        (HandleKind.BottomLeft, new PointD(r.X, r.Bottom)), (HandleKind.Left, new PointD(r.X, r.Center.Y)),
    ];

    private static bool IsNearBorder(RectD r, PointD p, double tolerance)
    {
        if (!r.Inflate(tolerance).Contains(p)) return false;
        var inner = r.Inflate(-tolerance);
        return inner.IsEmpty || !inner.Contains(p);
    }
}
