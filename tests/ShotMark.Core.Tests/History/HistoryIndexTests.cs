using System.Globalization;
using ShotMark.Core.History;
namespace ShotMark.Core.Tests.History;

public class HistoryIndexTests
{
    private static void Touch(TempDir dir, string name, DateTime? mtime = null)
    {
        var path = dir.File(name);
        File.WriteAllBytes(path, [0]);
        if (mtime is { } t) File.SetLastWriteTime(path, t);
    }

    [Fact]
    public void Scan_sorts_newest_first_and_marks_own_pc()
    {
        using var dir = new TempDir();
        Touch(dir, "2026-10-18_09-00-00_Laptop.png");
        Touch(dir, "2026-10-19_15-30-12_Buero.png");
        Touch(dir, "2026-10-19_15-30-12_Buero_2.png");
        Touch(dir, "notiz.txt");
        var entries = HistoryIndex.Scan(dir.Path, "Buero");
        Assert.Equal(3, entries.Count);
        Assert.EndsWith("_Buero_2.png", entries[0].PngPath);
        Assert.True(entries[0].IsOwnPc);
        Assert.Equal("Laptop", entries[2].PcSlug);
        Assert.False(entries[2].IsOwnPc);
    }

    [Fact]
    public void Foreign_png_uses_file_time_and_unknown_pc()
    {
        using var dir = new TempDir();
        Touch(dir, "Bildschirmfoto.png", new DateTime(2026, 1, 2, 3, 4, 5));
        var entry = Assert.Single(HistoryIndex.Scan(dir.Path, "Buero"));
        Assert.Null(entry.PcSlug);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), entry.Timestamp);
    }

    [Fact]
    public void Missing_folder_gives_empty_history()
        => Assert.Empty(HistoryIndex.Scan(@"Z:\gibt\es\nicht", "Buero"));

    [Fact]
    public void Filter_and_pc_list()
    {
        using var dir = new TempDir();
        Touch(dir, "2026-10-18_09-00-00_Laptop.png");
        Touch(dir, "2026-10-19_15-30-12_Buero.png");
        Touch(dir, "fremd.png");
        var entries = HistoryIndex.Scan(dir.Path, "Buero");
        Assert.Equal(new[] { "Buero", "Laptop" }, HistoryIndex.PcSlugs(entries));
        Assert.Single(HistoryIndex.Filter(entries, "laptop"));
        Assert.Equal(3, HistoryIndex.Filter(entries, null).Count());
    }

    [Theory]
    [InlineData("2026-10-19", "Heute")]
    [InlineData("2026-10-18", "Gestern")]
    [InlineData("2026-10-13", "Di 13.10.")]
    [InlineData("2026-10-12", "Mo 12.10.")]
    [InlineData("2025-12-31", "Mi 31.12.2025")]
    public void DayLabel(string day, string expected)
    {
        using var _ = new CultureScope("de-DE");
        Assert.Equal(expected, HistoryIndex.DayLabel(DateTime.Parse(day, CultureInfo.InvariantCulture), new DateTime(2026, 10, 19, 12, 0, 0)));
    }

    [Theory]
    [InlineData("2026-10-19", "Today")]
    [InlineData("2026-10-18", "Yesterday")]
    [InlineData("2026-10-13", "Tue Oct 13")]
    [InlineData("2025-12-31", "Wed Dec 31, 2025")]
    public void DayLabel_english(string day, string expected)
    {
        using var _ = new CultureScope("en-US");
        Assert.Equal(expected, HistoryIndex.DayLabel(DateTime.Parse(day, CultureInfo.InvariantCulture), new DateTime(2026, 10, 19, 12, 0, 0)));
    }

    [Fact]
    public void WithDaySeparators_inserts_header_per_day()
    {
        using var dir = new TempDir();
        Touch(dir, "2026-10-19_15-30-12_Buero.png");
        Touch(dir, "2026-10-19_10-00-00_Buero.png");
        Touch(dir, "2026-10-18_09-00-00_Laptop.png");
        using var _ = new CultureScope("de-DE");
        var items = HistoryIndex.WithDaySeparators(HistoryIndex.Scan(dir.Path, "Buero"), new DateTime(2026, 10, 19));
        Assert.Collection(items,
            i => Assert.Equal("Heute", Assert.IsType<DayHeaderItem>(i).Label),
            i => Assert.IsType<CaptureItem>(i),
            i => Assert.IsType<CaptureItem>(i),
            i => Assert.Equal("Gestern", Assert.IsType<DayHeaderItem>(i).Label),
            i => Assert.IsType<CaptureItem>(i));
    }
}
