using ShotMark.Core.Geometry;
namespace ShotMark.Core.Tests.Geometry;

public class ConstrainTests
{
    [Fact]
    public void Square_uses_larger_side_and_keeps_direction()
    {
        Assert.Equal(new PointD(30, 30), Constrain.Square(new PointD(0, 0), new PointD(30, 10)));
        Assert.Equal(new PointD(-20, 20), Constrain.Square(new PointD(0, 0), new PointD(-20, 5)));
    }

    [Fact]
    public void Angle45_snaps_to_nearest_45_degrees()
    {
        var p = Constrain.Angle45(new PointD(0, 0), new PointD(100, 10));
        Assert.Equal(100.5, p.X, 1);
        Assert.Equal(0, p.Y, 6);
        var d = Constrain.Angle45(new PointD(0, 0), new PointD(50, 45));
        Assert.Equal(d.X, d.Y, 6);
    }
}
