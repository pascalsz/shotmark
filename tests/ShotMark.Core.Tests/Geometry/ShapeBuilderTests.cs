using ShotMark.Core.Geometry;
namespace ShotMark.Core.Tests.Geometry;

public class ShapeBuilderTests
{
    [Fact]
    public void Arrow_head_tip_is_target_point()
    {
        var parts = ShapeBuilder.Arrow(new PointD(0, 0), new PointD(200, 0), 6);
        Assert.Equal(new PointD(200, 0), parts.Head[0]);
        Assert.Equal(3, parts.Head.Length);
        Assert.True(parts.ShaftEnd.X < 200 && parts.ShaftEnd.X > 150);
        Assert.Equal(parts.Head[1].X, parts.Head[2].X, 6);
        Assert.Equal(-parts.Head[1].Y, parts.Head[2].Y, 6);
    }

    [Fact]
    public void Arrow_head_never_longer_than_arrow()
    {
        var parts = ShapeBuilder.Arrow(new PointD(0, 0), new PointD(10, 0), 6);
        Assert.True(parts.Head[1].X >= 0);
    }

    [Fact]
    public void Check_and_cross_stay_inside_rect()
    {
        var r = new RectD(10, 10, 48, 48);
        Assert.All(ShapeBuilder.CheckPolyline(r), p => Assert.True(r.Contains(p)));
        Assert.All(ShapeBuilder.CrossLines(r), l => { Assert.True(r.Contains(l.A)); Assert.True(r.Contains(l.B)); });
        Assert.True(ShapeBuilder.CheckStrokeWidth(r) > 4);
    }
}
