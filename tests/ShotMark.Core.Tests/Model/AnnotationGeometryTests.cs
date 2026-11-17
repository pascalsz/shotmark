using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
namespace ShotMark.Core.Tests.Model;

public class AnnotationGeometryTests
{
    private sealed class FixedMeasurer : ITextMeasurer
    {
        public SizeD Measure(string text, double fontSize) => new(text.Length * 10, fontSize * 1.3);
    }

    private static readonly ITextMeasurer M = new FixedMeasurer();
    private static RectangleAnnotation Rect() => new(Guid.NewGuid(), new RectD(100, 100, 200, 100), Palette.Red, 4, true);

    [Fact]
    public void Rectangle_is_hit_on_border_not_inside()
    {
        Assert.True(AnnotationGeometry.HitTest(Rect(), new PointD(101, 150), 5, M));
        Assert.False(AnnotationGeometry.HitTest(Rect(), new PointD(200, 150), 5, M));
        Assert.False(AnnotationGeometry.HitTest(Rect(), new PointD(50, 50), 5, M));
    }

    [Fact]
    public void Arrow_is_hit_near_its_line()
    {
        var a = new ArrowAnnotation(Guid.NewGuid(), new(0, 0), new(100, 0), Palette.Red, 6, true);
        Assert.True(AnnotationGeometry.HitTest(a, new PointD(50, 4), 5, M));
        Assert.False(AnnotationGeometry.HitTest(a, new PointD(50, 30), 5, M));
    }

    [Fact]
    public void Text_bounds_come_from_measurer()
    {
        var t = new TextAnnotation(Guid.NewGuid(), new(10, 20), "Hallo", Palette.Red, 20);
        Assert.Equal(new RectD(10, 20, 50, 26), AnnotationGeometry.Bounds(t, M));
        Assert.True(AnnotationGeometry.HitTest(t, new PointD(30, 30), 2, M));
    }

    [Fact]
    public void TopmostAt_prefers_last_added()
    {
        var blur1 = new BlurAnnotation(Guid.NewGuid(), new RectD(0, 0, 100, 100), 12);
        var blur2 = new BlurAnnotation(Guid.NewGuid(), new RectD(50, 50, 100, 100), 12);
        Assert.Same(blur2, AnnotationGeometry.TopmostAt([blur1, blur2], new PointD(60, 60), 2, M));
        Assert.Null(AnnotationGeometry.TopmostAt([blur1, blur2], new PointD(500, 500), 2, M));
    }

    [Fact]
    public void Translate_moves_every_type()
    {
        var arrow = new ArrowAnnotation(Guid.NewGuid(), new(0, 0), new(10, 10), Palette.Red, 6, true);
        var moved = Assert.IsType<ArrowAnnotation>(AnnotationGeometry.Translate(arrow, 5, -5));
        Assert.Equal(new PointD(5, -5), moved.From);
        Assert.Equal(new PointD(15, 5), moved.To);
        var text = Assert.IsType<TextAnnotation>(AnnotationGeometry.Translate(new TextAnnotation(Guid.NewGuid(), new(1, 1), "x", Palette.Red, 20), 2, 3));
        Assert.Equal(new PointD(3, 4), text.Position);
        Assert.Equal(new RectD(105, 95, 200, 100), ((RectangleAnnotation)AnnotationGeometry.Translate(Rect(), 5, -5)).Rect);
    }

    [Fact]
    public void Dragging_corner_past_opposite_corner_normalizes()
    {
        var r = (RectangleAnnotation)AnnotationGeometry.DragHandle(Rect(), HandleKind.BottomRight, new PointD(50, 50), false);
        Assert.Equal(new RectD(50, 50, 50, 50), r.Rect);
    }

    [Fact]
    public void Dragging_edge_changes_one_side_only()
    {
        var r = (RectangleAnnotation)AnnotationGeometry.DragHandle(Rect(), HandleKind.Right, new PointD(400, 999), false);
        Assert.Equal(new RectD(100, 100, 300, 100), r.Rect);
    }

    [Fact]
    public void Stamp_stays_square_when_resized()
    {
        var c = new CheckAnnotation(Guid.NewGuid(), new RectD(0, 0, 48, 48), Palette.Green, true);
        var r = (CheckAnnotation)AnnotationGeometry.DragHandle(c, HandleKind.BottomRight, new PointD(100, 60), false);
        Assert.Equal(new RectD(0, 0, 100, 100), r.Rect);
        Assert.Equal(4, AnnotationGeometry.Handles(c).Count);
    }

    [Fact]
    public void Arrow_has_two_endpoint_handles()
    {
        var a = new ArrowAnnotation(Guid.NewGuid(), new(0, 0), new(100, 0), Palette.Red, 6, true);
        var handles = AnnotationGeometry.Handles(a);
        Assert.Equal(new[] { HandleKind.ArrowFrom, HandleKind.ArrowTo }, handles.Select(h => h.Kind));
        var moved = (ArrowAnnotation)AnnotationGeometry.DragHandle(a, HandleKind.ArrowTo, new PointD(50, 50), false);
        Assert.Equal(new PointD(50, 50), moved.To);
    }

    [Fact]
    public void Too_small_resize_is_ignored()
    {
        var original = Rect();
        Assert.Same(original, AnnotationGeometry.DragHandle(original, HandleKind.Right, new PointD(101, 0), false));
    }

    [Fact]
    public void WithStyle_applies_color_and_size()
    {
        var r = (RectangleAnnotation)AnnotationGeometry.WithStyle(Rect(), Palette.Blue, 8);
        Assert.Equal(Palette.Blue, r.Color);
        Assert.Equal(8, r.StrokeWidth);
        var c = (CheckAnnotation)AnnotationGeometry.WithStyle(new CheckAnnotation(Guid.NewGuid(), new RectD(0, 0, 40, 40), Palette.Green, true), null, 60);
        Assert.Equal(new RectD(-10, -10, 60, 60), c.Rect);
        Assert.Equal(new ToolStyle(Palette.Green, 60), AnnotationGeometry.StyleOf(c));
    }
}
