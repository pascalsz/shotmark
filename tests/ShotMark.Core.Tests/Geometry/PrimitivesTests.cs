using ShotMark.Core.Geometry;
namespace ShotMark.Core.Tests.Geometry;

public class PrimitivesTests
{
    [Fact]
    public void FromPoints_normalizes_any_drag_direction()
    {
        var r = RectD.FromPoints(new PointD(50, 40), new PointD(10, 100));
        Assert.Equal(new RectD(10, 40, 40, 60), r);
    }

    [Fact]
    public void Contains_includes_edges()
    {
        var r = new RectD(0, 0, 10, 10);
        Assert.True(r.Contains(new PointD(10, 10)));
        Assert.False(r.Contains(new PointD(10.1, 5)));
    }

    [Fact]
    public void Intersect_returns_null_when_disjoint()
    {
        Assert.Null(new RectD(0, 0, 10, 10).Intersect(new RectD(20, 20, 5, 5)));
        Assert.Equal(new RectD(5, 5, 5, 5), new RectD(0, 0, 10, 10).Intersect(new RectD(5, 5, 10, 10)));
    }

    [Fact]
    public void ToPixelGrid_expands_to_whole_pixels()
    {
        Assert.Equal(new RectD(1, 2, 4, 4), new RectD(1.4, 2.6, 3.2, 3.1).ToPixelGrid());
    }
}
