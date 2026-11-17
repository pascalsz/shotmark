using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
namespace ShotMark.Core.Tests.Model;

public class AnnotationFactoryTests
{
    private static readonly ToolStyle Red4 = new(Palette.Red, 4);

    [Fact]
    public void Rectangle_from_drag_uses_style_and_shadow()
    {
        var a = Assert.IsType<RectangleAnnotation>(AnnotationFactory.FromDrag(ToolKind.Rectangle, new(10, 10), new(110, 60), Red4, false));
        Assert.Equal(new RectD(10, 10, 100, 50), a.Rect);
        Assert.Equal(Palette.Red, a.Color);
        Assert.Equal(4, a.StrokeWidth);
        Assert.True(a.Shadow);
    }

    [Fact]
    public void Tiny_drag_creates_nothing_for_rectangle_arrow_blur()
    {
        Assert.Null(AnnotationFactory.FromDrag(ToolKind.Rectangle, new(10, 10), new(12, 11), Red4, false));
        Assert.Null(AnnotationFactory.FromDrag(ToolKind.Arrow, new(10, 10), new(12, 11), Red4, false));
        Assert.Null(AnnotationFactory.FromDrag(ToolKind.Blur, new(10, 10), new(12, 11), Red4, false));
    }

    [Fact]
    public void Click_with_check_tool_places_default_size_centered()
    {
        var a = Assert.IsType<CheckAnnotation>(AnnotationFactory.FromDrag(ToolKind.Check, new(100, 100), new(101, 100), new ToolStyle(Palette.Green, 48), false));
        Assert.Equal(new RectD(76, 76, 48, 48), a.Rect);
    }

    [Fact]
    public void Dragging_cross_makes_square()
    {
        var a = Assert.IsType<CrossAnnotation>(AnnotationFactory.FromDrag(ToolKind.Cross, new(0, 0), new(80, 30), Red4, false));
        Assert.Equal(a.Rect.Width, a.Rect.Height);
    }

    [Fact]
    public void Shift_rectangle_is_square()
    {
        var a = Assert.IsType<RectangleAnnotation>(AnnotationFactory.FromDrag(ToolKind.Rectangle, new(0, 0), new(80, 30), Red4, true));
        Assert.Equal(80, a.Rect.Height);
    }

    [Fact]
    public void Blur_uses_size_as_block_size()
    {
        var a = Assert.IsType<BlurAnnotation>(AnnotationFactory.FromDrag(ToolKind.Blur, new(0, 0), new(80, 30), new ToolStyle(Palette.Black, 12), false));
        Assert.Equal(12, a.BlockSize);
    }

    [Fact]
    public void Select_text_and_crop_tools_create_nothing()
    {
        Assert.Null(AnnotationFactory.FromDrag(ToolKind.Select, new(0, 0), new(80, 30), Red4, false));
        Assert.Null(AnnotationFactory.FromDrag(ToolKind.Text, new(0, 0), new(80, 30), Red4, false));
        Assert.Null(AnnotationFactory.FromDrag(ToolKind.Crop, new(0, 0), new(80, 30), Red4, false));
    }
}
