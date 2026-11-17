using ShotMark.App.History;
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
    public void Summary_in_english()
    {
        using var _ = new CultureScope("en-US");
        Assert.Equal("1 capture", HistoryStripViewModel.SummaryText(1, 1));
        Assert.Equal("3 captures from 2 PCs", HistoryStripViewModel.SummaryText(3, 2));
    }
}
