using ShotMark.Core.Localization;
using System.Globalization;
using ShotMark.Core.Storage;
namespace ShotMark.Core.History;

public sealed record HistoryEntry(string PngPath, DateTime Timestamp, string? PcSlug, bool IsOwnPc);

public abstract record HistoryItem;
public sealed record DayHeaderItem(DateTime Day, string Label) : HistoryItem;
public sealed record CaptureItem(HistoryEntry Entry) : HistoryItem;

public static class HistoryIndex
{

    public static IReadOnlyList<HistoryEntry> Scan(string folder, string ownPcSlug)
    {
        if (!Directory.Exists(folder)) return [];
        var entries = new List<HistoryEntry>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
        {
            if (CaptureName.TryParse(Path.GetFileNameWithoutExtension(path), out var name))
                entries.Add(new HistoryEntry(path, name.Timestamp, name.PcSlug,
                    string.Equals(name.PcSlug, ownPcSlug, StringComparison.OrdinalIgnoreCase)));
            else
                entries.Add(new HistoryEntry(path, File.GetLastWriteTime(path), null, false));
        }
        return entries
            .OrderByDescending(e => e.Timestamp)
            .ThenByDescending(e => e.PngPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IEnumerable<HistoryEntry> Filter(IEnumerable<HistoryEntry> entries, string? pcSlug) =>
        pcSlug is null ? entries : entries.Where(e => string.Equals(e.PcSlug, pcSlug, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<string> PcSlugs(IEnumerable<HistoryEntry> entries) =>
        entries.Select(e => e.PcSlug).OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string DayLabel(DateTime day, DateTime today)
    {
        var d = day.Date;
        if (d == today.Date) return CoreStrings.Today;
        if (d == today.Date.AddDays(-1)) return CoreStrings.Yesterday;
        var format = d.Year == today.Year ? CoreStrings.DayLabelThisYear : CoreStrings.DayLabelOtherYear;
        return d.ToString(format, CultureInfo.CurrentCulture);
    }

    public static IReadOnlyList<HistoryItem> WithDaySeparators(IEnumerable<HistoryEntry> sortedEntries, DateTime today)
    {
        var items = new List<HistoryItem>();
        DateTime? currentDay = null;
        foreach (var entry in sortedEntries)
        {
            if (currentDay != entry.Timestamp.Date)
            {
                currentDay = entry.Timestamp.Date;
                items.Add(new DayHeaderItem(currentDay.Value, DayLabel(currentDay.Value, today)));
            }
            items.Add(new CaptureItem(entry));
        }
        return items;
    }
}
