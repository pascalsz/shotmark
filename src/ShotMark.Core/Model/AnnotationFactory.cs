using ShotMark.Core.Geometry;
namespace ShotMark.Core.Model;

public static class AnnotationFactory
{
    public const double MinDragSize = 4;
    private const double StampClickThreshold = 8;

    public static Annotation? FromDrag(ToolKind tool, PointD start, PointD end, ToolStyle style, bool constrain)
    {
        switch (tool)
        {
            case ToolKind.Rectangle:
            {
                var rect = RectD.FromPoints(start, constrain ? Constrain.Square(start, end) : end);
                return rect.Width < MinDragSize || rect.Height < MinDragSize
                    ? null
                    : new RectangleAnnotation(Guid.NewGuid(), rect, style.Color, style.Size, true);
            }
            case ToolKind.Arrow:
            {
                var tip = constrain ? Constrain.Angle45(start, end) : end;
                return start.DistanceTo(tip) < MinDragSize
                    ? null
                    : new ArrowAnnotation(Guid.NewGuid(), start, tip, style.Color, style.Size, true);
            }
            case ToolKind.Check:
            case ToolKind.Cross:
            {
                var rect = RectD.FromPoints(start, Constrain.Square(start, end));
                if (rect.Width < StampClickThreshold)
                    rect = new RectD(start.X - style.Size / 2, start.Y - style.Size / 2, style.Size, style.Size);
                return tool == ToolKind.Check
                    ? new CheckAnnotation(Guid.NewGuid(), rect, style.Color, true)
                    : new CrossAnnotation(Guid.NewGuid(), rect, style.Color, true);
            }
            case ToolKind.Blur:
            {
                var rect = RectD.FromPoints(start, end);
                return rect.Width < MinDragSize || rect.Height < MinDragSize
                    ? null
                    : new BlurAnnotation(Guid.NewGuid(), rect, Math.Max(2, (int)Math.Round(style.Size)));
            }
            default:
                return null;
        }
    }
}
