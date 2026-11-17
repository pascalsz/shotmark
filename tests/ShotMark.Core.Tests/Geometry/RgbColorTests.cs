using System.Text.Json;
using ShotMark.Core.Geometry;
namespace ShotMark.Core.Tests.Geometry;

public class RgbColorTests
{
    [Theory]
    [InlineData("#FC4242", 0xFC, 0x42, 0x42)]
    [InlineData("fc4242", 0xFC, 0x42, 0x42)]
    [InlineData("#FFFC4242", 0xFC, 0x42, 0x42)]
    public void Parse_accepts_rgb_and_argb(string input, byte r, byte g, byte b)
        => Assert.Equal(new RgbColor(r, g, b), RgbColor.Parse(input));

    [Theory]
    [InlineData("#12345")]
    [InlineData("rot")]
    public void Parse_rejects_garbage(string input) => Assert.Throws<FormatException>(() => RgbColor.Parse(input));

    [Fact]
    public void Serializes_as_hex_string()
    {
        var json = JsonSerializer.Serialize(Palette.Red);
        Assert.Equal("\"#FC4242\"", json);
        Assert.Equal(Palette.Red, JsonSerializer.Deserialize<RgbColor>(json));
    }
}
