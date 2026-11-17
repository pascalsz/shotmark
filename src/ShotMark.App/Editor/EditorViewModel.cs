using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShotMark.App.Rendering;
using ShotMark.Core.Editing;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
using ShotMark.Core.Storage;

namespace ShotMark.App.Editor;

internal enum SaveStatus { Idle, Pending, Saving, Saved, Failed, Conflict }

internal sealed record ConflictInfo(string ModifiedOn, DateTimeOffset ModifiedAt);

internal sealed class EditorViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(1.2);

    private readonly IEditorHost _host;
    private readonly UndoStack<EditState> _undo = new();
    private readonly DispatcherTimer _autosave;
    private readonly Dictionary<ToolKind, ToolStyle> _toolStyles = Enum.GetValues<ToolKind>().ToDictionary(t => t, ToolDefaults.For);
    private EditState _state = new([], null);
    private ToolKind _tool = ToolKind.Rectangle;
    private ToolKind _toolBeforeCrop = ToolKind.Rectangle;
    private Guid? _selectedId;
    private bool _dirty;
    private Task _currentSave = Task.CompletedTask;
    private bool _saving;
    private bool _saveAgain;
    private string? _mergeKey;
    private DateTime _mergeUntil;
    private SaveStatus _saveStatus;
    private string _statusText = "";
    private ConflictInfo? _conflict;
    private string? _warning;
    private int _openRequest;

    public EditorViewModel(IEditorHost host)
    {
        _host = host;
        _autosave = new DispatcherTimer { Interval = AutosaveDelay };
        _autosave.Tick += (_, _) =>
        {
            _autosave.Stop();
            _ = SaveAsync();
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Annotationen oder Zuschnitt haben sich geändert (Leinwand neu zeichnen).</summary>
    public event Action? StateChanged;

    public ITextMeasurer Measurer { get; } = new WpfTextMeasurer();
    public OpenCapture? Capture { get; private set; }
    public EditState State => _state;
    public bool CanUndo => _undo.CanUndo;
    public bool CanRedo => _undo.CanRedo;
    public bool IsDirty => _dirty;
    public SaveStatus SaveStatus => _saveStatus;
    public string StatusText => _statusText;
    public ConflictInfo? Conflict => _conflict;
    public string? Warning => _warning;

    public ToolKind Tool
    {
        get => _tool;
        set
        {
            if (_tool == value) return;
            if (value == ToolKind.Crop) _toolBeforeCrop = _tool;
            _tool = value;
            if (Selected is { } s && value is not ToolKind.Select && ToolOf(s) != value) _selectedId = null;
            OnPropertyChanged();
            OnStyleTargetChanged();
        }
    }

    public Guid? SelectedId
    {
        get => _selectedId;
        set
        {
            if (_selectedId == value) return;
            _selectedId = value;
            OnPropertyChanged();
            OnStyleTargetChanged();
        }
    }

    public Annotation? Selected => _selectedId is { } id ? _state.Annotations.FirstOrDefault(a => a.Id == id) : null;

    /// <summary>Werkzeug, dessen Stil das Eigenschaften-Panel gerade zeigt (Auswahl hat Vorrang).</summary>
    public ToolKind StyleTarget => Selected is { } s ? ToolOf(s) : _tool;

    public ToolStyle CurrentStyle => Selected is { } s ? AnnotationGeometry.StyleOf(s) : _toolStyles[_tool];

    public ToolStyle StyleFor(ToolKind tool) => _toolStyles[tool];

    public static ToolKind ToolOf(Annotation annotation) => annotation switch
    {
        RectangleAnnotation => ToolKind.Rectangle,
        ArrowAnnotation => ToolKind.Arrow,
        CheckAnnotation => ToolKind.Check,
        CrossAnnotation => ToolKind.Cross,
        TextAnnotation => ToolKind.Text,
        BlurAnnotation => ToolKind.Blur,
        _ => ToolKind.Select,
    };

    /// <returns>false, wenn die Aufnahme nicht gewechselt wurde (neuere Anfrage oder Nutzer will Änderungen behalten).</returns>
    public async Task<bool> OpenAsync(OpenCapture capture, string? warning = null)
    {
        var request = ++_openRequest;
        if (!await SecureUnsavedAsync()) return false;
        // Zwei schnelle Klicks im Verlauf: nur die letzte Anfrage öffnet
        if (request != _openRequest) return false;
        Capture = capture;
        _state = EditState.From(capture.Document);
        _undo.Clear();
        _selectedId = null;
        _dirty = false;
        _mergeKey = null;
        _conflict = null;
        _warning = warning;
        if (_tool == ToolKind.Crop) _tool = _toolBeforeCrop;
        SetStatus(SaveStatus.Saved, $"Gespeichert ✓ {capture.Document.ModifiedAt.LocalDateTime:HH:mm}");
        OnPropertyChanged(nameof(Capture));
        OnPropertyChanged(nameof(Tool));
        OnPropertyChanged(nameof(Conflict));
        OnPropertyChanged(nameof(Warning));
        RaiseStateChanged();
        OnStyleTargetChanged();
        return true;
    }

    /// <summary>
    /// Sorgt dafür, dass nichts verloren geht, bevor die Aufnahme gewechselt oder ShotMark beendet wird:
    /// speichern; bei Konflikt als Kopie; ist der Ordner nicht erreichbar, lokal sichern; erst dann fragen.
    /// </summary>
    public async Task<bool> SecureUnsavedAsync()
    {
        await FlushAsync();
        if (Capture is null || !_dirty) return true;

        if (_conflict is not null)
        {
            await SaveAsCopyAsync();
            if (!_dirty)
            {
                _host.Notify("Die Aufnahme wurde inzwischen auf einem anderen PC geändert. Deine Version ist als Kopie gespeichert.");
                return true;
            }
        }

        if (await RescueLocallyAsync() is { } rescued)
        {
            _dirty = false;
            _host.Notify($"Der Speicherordner ist nicht erreichbar. Deine Aufnahme wurde lokal gesichert:\n{rescued}");
            return true;
        }

        return _host.ConfirmDiscard("Die Änderungen an der aktuellen Aufnahme konnten weder im Speicherordner noch lokal gesichert werden.\n\nTrotzdem verwerfen?");
    }

    private async Task<string?> RescueLocallyAsync()
    {
        var capture = Capture!;
        try
        {
            var document = _state.ApplyTo(capture.Document) with { Revision = 0, SaveId = Guid.Empty };
            var flat = Exporter.Flatten(capture.Original, document, Measurer, capture.Renderer);
            var store = new CaptureStore(_host.RescueFolder, PcName.ToSlug(_host.PcName));
            var paths = store.CreateNew(DateTime.Now);
            var pcName = _host.PcName;
            var outcome = await Task.Run(() =>
                store.Save(paths, document, capture.OriginalPng, Exporter.EncodePng(flat), pcName, DateTimeOffset.Now));
            return outcome is SaveOutcome.Saved ? paths.PngPath : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error("Lokale Sicherung fehlgeschlagen", e);
            return null;
        }
    }

    /// <summary>Abmelden/Herunterfahren: letzte Änderungen synchron speichern (kein await mehr möglich).</summary>
    public void SaveBlocking()
    {
        _autosave.Stop();
        if (Capture is not { } capture || !_dirty || _conflict is not null || _saving) return;
        var document = _state.ApplyTo(capture.Document);
        var flat = Exporter.Flatten(capture.Original, document, Measurer, capture.Renderer);
        if (_host.Store.Save(capture.Paths, document, capture.OriginalPng, Exporter.EncodePng(flat), _host.PcName, DateTimeOffset.Now)
            is SaveOutcome.Saved saved)
        {
            capture.Document = saved.Document;
            _dirty = false;
        }
    }

    public void Commit(EditState next, string? mergeKey = null)
    {
        if (Capture is null || next == _state) return;
        var merge = mergeKey is not null && mergeKey == _mergeKey && DateTime.UtcNow < _mergeUntil;
        if (!merge) _undo.Record(_state);
        _mergeKey = mergeKey;
        _mergeUntil = DateTime.UtcNow + MergeWindow;
        _state = next;
        AfterStateChange();
    }

    public void Undo()
    {
        if (_undo.Undo(_state) is not { } previous) return;
        _state = previous;
        _mergeKey = null;
        AfterStateChange();
    }

    public void Redo()
    {
        if (_undo.Redo(_state) is not { } next) return;
        _state = next;
        _mergeKey = null;
        AfterStateChange();
    }

    public void AddAnnotation(Annotation annotation)
    {
        Commit(_state with { Annotations = _state.Annotations.Add(annotation) });
        SelectedId = annotation.Id;
    }

    public void ReplaceAnnotation(Annotation updated, string? mergeKey = null)
    {
        var index = _state.Annotations.FindIndex(a => a.Id == updated.Id);
        if (index < 0) return;
        Commit(_state with { Annotations = _state.Annotations.SetItem(index, updated) }, mergeKey);
    }

    public void RemoveAnnotation(Guid id)
    {
        var index = _state.Annotations.FindIndex(a => a.Id == id);
        if (index >= 0) Commit(_state with { Annotations = _state.Annotations.RemoveAt(index) });
    }

    public void DeleteSelected()
    {
        if (Selected is { } s) RemoveAnnotation(s.Id);
    }

    public void Nudge(double dx, double dy)
    {
        if (Selected is { } s) ReplaceAnnotation(AnnotationGeometry.Translate(s, dx, dy), "nudge:" + s.Id);
    }

    public void SetColor(RgbColor color)
    {
        var target = StyleTarget;
        _toolStyles[target] = _toolStyles[target] with { Color = color };
        if (Selected is { } s) ReplaceAnnotation(AnnotationGeometry.WithStyle(s, color, null));
        OnPropertyChanged(nameof(CurrentStyle));
    }

    public void SetSize(double size)
    {
        var target = StyleTarget;
        _toolStyles[target] = _toolStyles[target] with { Size = size };
        if (Selected is { } s) ReplaceAnnotation(AnnotationGeometry.WithStyle(s, null, size), "size:" + s.Id);
        OnPropertyChanged(nameof(CurrentStyle));
    }

    public void ApplyCrop(RectD? crop)
    {
        if (Capture is null) return;
        var full = Capture.FullRect;
        var normalized = crop?.ToPixelGrid().Intersect(full);
        // Leerer Rahmen (nur geklickt): bisherigen Zuschnitt unverändert lassen
        if (normalized is not { Width: >= 2, Height: >= 2 } area)
        {
            Tool = _toolBeforeCrop;
            return;
        }
        Commit(_state with { Crop = area == full ? null : area });
        Tool = _toolBeforeCrop;
    }

    public void CancelCrop() => Tool = _toolBeforeCrop;

    public void RemoveCrop() => Commit(_state with { Crop = null });

    public BitmapSource RenderOutput()
    {
        var capture = Capture ?? throw new InvalidOperationException("Keine Aufnahme geöffnet");
        return Exporter.Flatten(capture.Original, _state.ApplyTo(capture.Document), Measurer, capture.Renderer);
    }

    public bool CopyToClipboard() => Capture is not null && ClipboardHelper.TrySetImage(RenderOutput());

    /// <summary>Speichert sofort (Strg+S, Schließen, Wechsel der Aufnahme).</summary>
    public async Task FlushAsync()
    {
        _autosave.Stop();
        while (_saving) await _currentSave;
        if (_dirty && _conflict is null) await SaveAsync();
    }

    public Task SaveAsync()
    {
        if (Capture is null || !_dirty || _conflict is not null) return Task.CompletedTask;
        if (_saving)
        {
            _saveAgain = true;
            return _currentSave;
        }
        _currentSave = SaveCoreAsync(Capture);
        return _currentSave;
    }

    private async Task SaveCoreAsync(OpenCapture capture)
    {
        _saving = true;
        _dirty = false;
        SetStatus(SaveStatus.Saving, "Speichert …");
        var document = _state.ApplyTo(capture.Document);
        try
        {
            var flat = Exporter.Flatten(capture.Original, document, Measurer, capture.Renderer);
            var store = _host.Store;
            var pcName = _host.PcName;
            var outcome = await Task.Run(() =>
                store.Save(capture.Paths, document, capture.OriginalPng, Exporter.EncodePng(flat), pcName, DateTimeOffset.Now));
            var isCurrent = ReferenceEquals(capture, Capture);
            switch (outcome)
            {
                case SaveOutcome.Saved saved:
                    capture.Document = saved.Document;
                    if (isCurrent && !_dirty) SetStatus(SaveStatus.Saved, $"Gespeichert ✓ {DateTime.Now:HH:mm}");
                    _host.CaptureSaved(capture.Paths.PngPath);
                    break;
                case SaveOutcome.Conflict conflict:
                    if (isCurrent)
                    {
                        _dirty = true;
                        _conflict = new ConflictInfo(conflict.ModifiedOn, conflict.ModifiedAt);
                        OnPropertyChanged(nameof(Conflict));
                        SetStatus(SaveStatus.Conflict, $"Nicht gespeichert – wurde auf „{conflict.ModifiedOn}“ geändert");
                    }
                    break;
                case SaveOutcome.Failed failed:
                    Log.Error("Speichern fehlgeschlagen: " + capture.Paths.PngPath, new IOException(failed.Message));
                    if (isCurrent)
                    {
                        _dirty = true;
                        SetStatus(SaveStatus.Failed, "Nicht gespeichert – erneut versuchen");
                    }
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error("Speichern fehlgeschlagen", e);
            _dirty = true;
            SetStatus(SaveStatus.Failed, "Nicht gespeichert – erneut versuchen");
        }
        finally
        {
            _saving = false;
            if (_saveAgain)
            {
                _saveAgain = false;
                if (_dirty && _conflict is null) _ = SaveAsync();
            }
        }
    }

    /// <summary>Erste Speicherung einer neuen Aufnahme ist fehlgeschlagen – beim nächsten Anlass erneut versuchen.</summary>
    public void MarkUnsaved()
    {
        _dirty = true;
        SetStatus(SaveStatus.Failed, "Nicht gespeichert – erneut versuchen");
    }

    /// <summary>Konflikt: Stand vom anderen PC laden, eigene Änderungen verwerfen.</summary>
    public async Task ReloadFromDiskAsync()
    {
        if (Capture is null) return;
        _dirty = false;
        _conflict = null;
        var loaded = await Task.Run(() => CaptureStore.Load(Capture.Paths.PngPath));
        await OpenAsync(OpenCapture.FromLoaded(loaded, _host.PcLabel), loaded.Warning);
    }

    /// <summary>Konflikt: eigenen Stand als neue Aufnahme speichern.</summary>
    public async Task SaveAsCopyAsync()
    {
        if (Capture is null) return;
        Capture.Paths = _host.Store.CreateNew(DateTime.Now);
        Capture.Document = Capture.Document with { Revision = 0, SaveId = Guid.Empty };
        _conflict = null;
        _dirty = true;
        OnPropertyChanged(nameof(Conflict));
        OnPropertyChanged(nameof(Capture));
        await SaveAsync();
    }

    private void AfterStateChange()
    {
        if (_selectedId is { } id && !_state.Annotations.Any(a => a.Id == id)) _selectedId = null;
        _dirty = true;
        SetStatus(SaveStatus.Pending, "Änderungen werden gespeichert …");
        _autosave.Stop();
        _autosave.Start();
        RaiseStateChanged();
        OnStyleTargetChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke();
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
    }

    private void OnStyleTargetChanged()
    {
        OnPropertyChanged(nameof(Selected));
        OnPropertyChanged(nameof(SelectedId));
        OnPropertyChanged(nameof(StyleTarget));
        OnPropertyChanged(nameof(CurrentStyle));
    }

    private void SetStatus(SaveStatus status, string text)
    {
        _saveStatus = status;
        _statusText = text;
        OnPropertyChanged(nameof(SaveStatus));
        OnPropertyChanged(nameof(StatusText));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
