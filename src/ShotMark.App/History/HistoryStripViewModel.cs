using ShotMark.App.Localization;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShotMark.Core.Geometry;
using ShotMark.Core.History;
using ShotMark.Core.Storage;

namespace ShotMark.App.History;

internal abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Record: gleiche Beschriftung = gleicher Eintrag, damit unveränderte Listen nicht neu aufgebaut werden.</summary>
internal sealed record DayHeaderVm(string Label)
{
    public override string ToString() => Label;
}

internal sealed class PcFilterVm(string? slug, string label) : ObservableObject
{
    private bool _isSelected;

    public string? Slug { get; } = slug;
    public string Label { get; } = label;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value) return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public override string ToString() => Label;
}

internal sealed class CaptureTileVm : ObservableObject
{
    private const int ThumbnailHeight = 152;

    private BitmapSource? _thumbnail;
    private PixelSize? _size;
    private bool _loading;
    private bool _loaded;
    private bool _reloadRequested;

    public CaptureTileVm(HistoryEntry entry, string pcLabel)
    {
        Entry = entry;
        PcLabel = pcLabel;
    }

    public HistoryEntry Entry { get; private set; }
    public string Path => Entry.PngPath;
    public string PcLabel { get; private set; }
    public bool IsOwnPc => Entry.IsOwnPc;
    public string TimeText => Entry.Timestamp.ToString("t", CultureInfo.CurrentCulture);

    public string ToolTipText
    {
        get
        {
            var text = Loc.Format(Strings.History_Tooltip, Entry.Timestamp, PcLabel);
            if (_size is { } size) text += $"\n{size.Width} × {size.Height} px";
            return text;
        }
    }

    public override string ToString() => Loc.Format(Strings.History_ItemName, Entry.Timestamp, PcLabel);

    public BitmapSource? Thumbnail
    {
        get
        {
            if (!_loaded && !_loading) _ = LoadAsync();
            return _thumbnail;
        }
    }

    public void Update(HistoryEntry entry, string pcLabel)
    {
        Entry = entry;
        if (PcLabel != pcLabel)
        {
            PcLabel = pcLabel;
            OnPropertyChanged(nameof(PcLabel));
            OnPropertyChanged(nameof(ToolTipText));
        }
    }

    /// <summary>Datei hat sich geändert (eigene Speicherung oder Sync) – Vorschau neu laden.</summary>
    public void Invalidate()
    {
        if (_loading)
        {
            _reloadRequested = true;
            return;
        }
        _loaded = false;
        OnPropertyChanged(nameof(Thumbnail));
    }

    private async Task LoadAsync()
    {
        _loading = true;
        (BitmapSource? Thumbnail, PixelSize? Size) result = (null, null);
        try
        {
            result = await ThumbnailLoader.LoadAsync(Path, ThumbnailHeight);
        }
        finally
        {
            _loading = false;
            _loaded = true;
        }
        _thumbnail = result.Thumbnail ?? _thumbnail;
        _size = result.Size ?? _size;
        OnPropertyChanged(nameof(Thumbnail));
        OnPropertyChanged(nameof(ToolTipText));
        if (_reloadRequested)
        {
            _reloadRequested = false;
            Invalidate();
        }
    }
}

/// <summary>Verlauf aller Aufnahmen im geteilten Ordner – von allen PCs.</summary>
internal sealed class HistoryStripViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _watcherRetry;
    private readonly Dictionary<string, CaptureTileVm> _tiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _changedPaths = new(StringComparer.OrdinalIgnoreCase);
    private FileSystemWatcher? _watcher;
    private IReadOnlyList<HistoryEntry> _entries = [];
    private IReadOnlyDictionary<string, PcInfo> _pcs = new Dictionary<string, PcInfo>();
    private IReadOnlyList<object> _items = [];
    private string _folder = "";
    private string _ownSlug = "";
    private string? _filterSlug;
    private int _refreshVersion;

    public HistoryStripViewModel(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _debounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(600) };
        _debounce.Tick += async (_, _) =>
        {
            _debounce.Stop();
            await RefreshAsync();
        };
        // Ordner vorübergehend nicht erreichbar (z. B. Dropbox noch nicht gestartet): später erneut versuchen
        _watcherRetry = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromSeconds(30) };
        _watcherRetry.Tick += (_, _) =>
        {
            _watcherRetry.Stop();
            RestartWatcher();
            _ = RefreshAsync();
        };
    }

    public event Action? ItemsChanged;

    public IReadOnlyList<object> Items => _items;
    public ObservableCollection<PcFilterVm> Filters { get; } = [];
    public string Folder => _folder;

    public string Summary
    {
        get
        {
            return SummaryText(_entries.Count, HistoryIndex.PcSlugs(_entries).Count);
        }
    }

    internal static string SummaryText(int count, int pcs)
    {
        var captures = count == 1 ? Strings.History_OneCapture : Loc.Format(Strings.History_Captures, count);
        return pcs > 1 ? Loc.Format(Strings.History_FromPcs, captures, pcs) : captures;
    }

    public void Configure(string folder, string ownSlug)
    {
        _folder = folder;
        _ownSlug = ownSlug;
        _tiles.Clear();
        RestartWatcher();
        _ = RefreshAsync();
    }

    public CaptureTileVm? TileFor(string pngPath) => _tiles.GetValueOrDefault(pngPath);

    public void NotifyChanged(string pngPath)
    {
        _changedPaths.Add(pngPath);
        _debounce.Stop();
        _debounce.Start();
    }

    public void SetFilter(string? slug)
    {
        _filterSlug = slug;
        foreach (var filter in Filters) filter.IsSelected = string.Equals(filter.Slug, slug, StringComparison.OrdinalIgnoreCase);
        Rebuild();
    }

    public async Task RefreshAsync()
    {
        var version = ++_refreshVersion;
        var folder = _folder;
        var own = _ownSlug;
        IReadOnlyList<HistoryEntry> entries;
        IReadOnlyDictionary<string, PcInfo> pcs;
        try
        {
            (entries, pcs) = await Task.Run(() => (HistoryIndex.Scan(folder, own), PcRegistry.LoadAll(folder)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error("Verlauf konnte nicht gelesen werden: " + folder, e);
            return;
        }
        if (version != _refreshVersion) return;
        _entries = entries;
        _pcs = pcs;
        foreach (var path in _changedPaths)
            if (_tiles.TryGetValue(path, out var tile)) tile.Invalidate();
        _changedPaths.Clear();
        RebuildFilters();
        Rebuild();
    }

    public string PcLabel(string? slug) =>
        slug is null ? Strings.History_UnknownPc : _pcs.TryGetValue(slug, out var info) ? info.DisplayName : slug;

    private void RebuildFilters()
    {
        var slugs = HistoryIndex.PcSlugs(_entries)
            .OrderByDescending(s => string.Equals(s, _ownSlug, StringComparison.OrdinalIgnoreCase))
            .ThenBy(PcLabel, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (_filterSlug is not null && !slugs.Contains(_filterSlug, StringComparer.OrdinalIgnoreCase)) _filterSlug = null;

        var wanted = new List<(string? Slug, string Label)> { (null, Strings.History_AllPcs) };
        wanted.AddRange(slugs.Select(s => ((string?)s, PcLabel(s))));
        if (Filters.Select(f => (f.Slug, f.Label)).SequenceEqual(wanted)) return;

        Filters.Clear();
        foreach (var (slug, label) in wanted)
            Filters.Add(new PcFilterVm(slug, label) { IsSelected = string.Equals(slug, _filterSlug, StringComparison.OrdinalIgnoreCase) });
    }

    private void Rebuild()
    {
        var items = new List<object>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in HistoryIndex.WithDaySeparators(HistoryIndex.Filter(_entries, _filterSlug), DateTime.Today))
        {
            switch (item)
            {
                case DayHeaderItem day:
                    items.Add(new DayHeaderVm(day.Label));
                    break;
                case CaptureItem capture:
                    var label = PcLabel(capture.Entry.PcSlug);
                    if (_tiles.TryGetValue(capture.Entry.PngPath, out var tile)) tile.Update(capture.Entry, label);
                    else _tiles[capture.Entry.PngPath] = tile = new CaptureTileVm(capture.Entry, label);
                    seen.Add(capture.Entry.PngPath);
                    items.Add(tile);
                    break;
            }
        }
        foreach (var path in _entries.Select(e => e.PngPath)) seen.Add(path);
        foreach (var stale in _tiles.Keys.Where(k => !seen.Contains(k)).ToList()) _tiles.Remove(stale);

        OnPropertyChanged(nameof(Summary));
        // Unveränderte Liste (z. B. nach eigenem Autosave) nicht neu binden – sonst springt die Leiste
        if (items.SequenceEqual(_items)) return;
        _items = items;
        OnPropertyChanged(nameof(Items));
        ItemsChanged?.Invoke();
    }

    private void RestartWatcher()
    {
        _watcher?.Dispose();
        _watcher = null;
        try
        {
            Directory.CreateDirectory(_folder);
            _watcher = new FileSystemWatcher(_folder, "*.png")
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            _watcher.Created += OnFileEvent;
            _watcher.Changed += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.Error += (_, e) =>
            {
                Log.Error("Ordnerüberwachung unterbrochen – wird neu gestartet", e.GetException());
                _dispatcher.BeginInvoke(() =>
                {
                    RestartWatcher();
                    _ = RefreshAsync(); // Ereignisse aus dem übergelaufenen Puffer nachholen
                });
            };
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception e) when (e is IOException or ArgumentException or UnauthorizedAccessException)
        {
            Log.Error("Ordnerüberwachung nicht möglich: " + _folder, e);
            _watcherRetry.Start();
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) =>
        _dispatcher.BeginInvoke(() => NotifyChanged(e.FullPath));

    public void Dispose()
    {
        _watcher?.Dispose();
        _debounce.Stop();
        _watcherRetry.Stop();
    }
}
