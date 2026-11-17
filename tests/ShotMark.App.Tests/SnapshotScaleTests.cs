namespace ShotMark.App.Tests;

public class SnapshotScaleTests
{
    [Theory]
    [InlineData(null, 1.0)]
    [InlineData("", 1.0)]
    [InlineData("abc", 1.0)]
    [InlineData("2", 2.0)]
    [InlineData("1.5", 1.5)]
    [InlineData("10", 4.0)]
    [InlineData("0.5", 1.0)]
    public void Parse_reads_and_clamps_the_factor(string? value, double expected)
    {
        Assert.Equal(expected, SnapshotScale.Parse(value));
    }

    [Fact]
    public void Parse_ignores_the_decimal_comma_of_german_formats()
    {
        using var _ = new CultureScope("de-DE");
        Assert.Equal(1.5, SnapshotScale.Parse("1.5"));
    }
}
