using ShotMark.App.History;
using ShotMark.App.Localization;
namespace ShotMark.App.Tests;

public class HistoryTextTests
{
    [Fact]
    public void Summary_in_german()
    {
        using var _ = new CultureScope("de-DE");
        Assert.Equal("1 Aufnahme", HistoryStripViewModel.SummaryText(1, 1));
        Assert.Equal("3 Aufnahmen von 2 PCs", HistoryStripViewModel.SummaryText(3, 2));
    }

    [Fact]
    public void English_times_follow_the_regional_clock()
    {
        // Englische Oberfläche mit britischem Format: 24-Stunden-Uhr wie überall sonst in ShotMark
        using var _ = new CultureScope("en-GB");
        var at = new DateTime(2026, 11, 17, 14, 30, 5);
        Assert.DoesNotContain("PM", Loc.Format(Strings.History_Tooltip, at, "Office"));
        Assert.Contains("14:30", Loc.Format(Strings.History_Tooltip, at, "Office"));
        Assert.DoesNotContain("PM", Loc.Format(Strings.Editor_Conflict, "Laptop", at));
        Assert.Contains("14:30", Loc.Format(Strings.Editor_Conflict, "Laptop", at));
    }

    [Fact]
    public void Summary_in_english()
    {
        using var _ = new CultureScope("en-US");
        Assert.Equal("1 capture", HistoryStripViewModel.SummaryText(1, 1));
        Assert.Equal("3 captures from 2 PCs", HistoryStripViewModel.SummaryText(3, 2));
    }
}
