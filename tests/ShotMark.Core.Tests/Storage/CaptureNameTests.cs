using ShotMark.Core.Storage;
namespace ShotMark.Core.Tests.Storage;

public class CaptureNameTests
{
    private static readonly DateTime T = new(2026, 10, 19, 15, 30, 12);

    [Fact]
    public void BaseName_is_sortable_and_contains_pc()
    {
        Assert.Equal("2026-10-19_15-30-12_Buero", new CaptureName(T, "Buero").BaseName);
        Assert.Equal("2026-10-19_15-30-12_Buero_2", new CaptureName(T, "Buero", 2).BaseName);
    }

    [Theory]
    [InlineData("2026-10-19_15-30-12_Buero", "Buero", 1)]
    [InlineData("2026-10-19_15-30-12_DESKTOP-Office_3", "DESKTOP-Office", 3)]
    public void TryParse_reads_valid_names(string name, string pc, int seq)
    {
        Assert.True(CaptureName.TryParse(name, out var parsed));
        Assert.Equal(T, parsed!.Timestamp);
        Assert.Equal(pc, parsed.PcSlug);
        Assert.Equal(seq, parsed.Sequence);
    }

    [Theory]
    [InlineData("2026-10-19_15-30-12_Buero (Konfliktkopie von Laptop 2026-10-19)")]
    [InlineData("Screenshot 2026-10-19 153012")]
    [InlineData("2026-13-45_15-30-12_Buero")]
    [InlineData("01-07-2026_21-48-51")]
    public void TryParse_rejects_foreign_names(string name)
    {
        Assert.False(CaptureName.TryParse(name, out var parsed));
        Assert.Null(parsed);
    }
}
