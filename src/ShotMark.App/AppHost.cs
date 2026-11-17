using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShotMark.App.Capture;
using ShotMark.App.Editor;
using ShotMark.App.History;
using ShotMark.App.Localization;
using ShotMark.App.Native;
using ShotMark.App.Rendering;
using ShotMark.App.Settings;
using ShotMark.App.Tray;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
using ShotMark.Core.Scrolling;
using ShotMark.Core.Settings;
using ShotMark.Core.Storage;
using ShotMark.Core.Updates;
using PcNames = ShotMark.Core.Storage.PcName;

namespace ShotMark.App;

/// <summary>Verbindet Tray, Tastenkürzel, Aufnahme, Speichern und Editor.</summary>
internal sealed class AppHost : IEditorHost, IEditorCommands, ITrayCommands, IDisposable
{
    private const int MenuCloseDelayMs = 250;

    private readonly SettingsStore _settingsStore;
    private AppSettings _settings;
    private CaptureStore _store = null!;
    private string _pcName = "";
    private string _automaticFolder = "";
    private HotkeyManager? _hotkeys;
    private HotkeyManager? _scrollHotkeys;
    private bool _hotkeyActive;
    private bool _scrollHotkeyPending;
    private HotkeyPreCapture? _preCapture;
    private TrayIcon? _tray;
    private EditorViewModel? _editorVm;
    private HistoryStripViewModel? _history;
    private EditorWindow? _editor;
    private bool _capturing;
    private bool _exiting;
    private SettingsWindow? _settingsWindow;
    private DispatcherTimer? _hotkeyRetry;
    private CancellationTokenSource? _scrollStop;
    private readonly bool _manageAutostart;

    /// <param name="manageAutostart">Nur das Standardprofil trägt sich in den Autostart ein, nicht Testprofile (<c>--profile</c>).</param>
    public AppHost(string profileDirectory, bool manageAutostart = true)
    {
        _manageAutostart = manageAutostart;
        _settingsStore = new SettingsStore(profileDirectory);
        _settings = _settingsStore.Load();
    }

    public CaptureStore Store => _store;
    public string PcName => _pcName;

    public string RescueFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShotMark", "Nicht gespeichert");
    public string HotkeyLabel => HotkeyText.Format(_settings.Hotkey);
    private string? ScrollHotkeyLabel => _settings.ScrollHotkey is { } scroll ? HotkeyText.Format(scroll) : null;

    public bool Start()
    {
        var firstRun = string.IsNullOrWhiteSpace(_settings.PcName);
        if (firstRun)
        {
            var folder = ResolveFolder(_settings);
            var name = FirstRunWindow.Ask(System.Net.Dns.GetHostName(), folder.Folder, folder.IsFallback, Environment.MachineName);
            if (name is null) return false;
            _settings = _settings with { PcName = name };
            _settingsStore.Save(_settings);
        }

        _tray = new TrayIcon(this, HotkeyLabel, ScrollHotkeyLabel);
        ApplyFolderAndName(showFallbackWarning: true);
        _history = new HistoryStripViewModel(Dispatcher.CurrentDispatcher);
        _history.Configure(_store.Folder, _store.PcSlug);
        _editorVm = new EditorViewModel(this);

        _hotkeys = new HotkeyManager();
        _hotkeys.Pressed += StartCapture;
        _scrollHotkeys = new HotkeyManager();
        _scrollHotkeys.Pressed += StartScrollCapture;
        _preCapture = new HotkeyPreCapture();
        var hotkeyOk = RegisterHotkey(showWarning: true);
        ApplyAutostart();

        if (firstRun && hotkeyOk)
            _tray.ShowInfo(Strings.Ready_Title, Loc.Format(Strings.Ready_Text, HotkeyLabel));
        Log.Info($"Gestartet als „{_pcName}“, Ordner {_store.Folder}");
        return true;
    }

    // ---------- Aufnahme ----------

    // Während einer Scroll-Aufnahme wirken beide Aufnahme-Kürzel als Stopp.
    // Das beim ersten Tastendruck festgehaltene Bild enthält noch Tooltips, die das Programm beim Kürzel schon schließt.
    public void StartCapture()
    {
        var early = _preCapture?.TryTake();
        if (_scrollStop is { } running) running.Cancel();
        else _ = CaptureAsync(CaptureMode.Image, 0, early);
    }

    public void StartCaptureFromMenu()
    {
        if (_scrollStop is { } running) running.Cancel();
        else _ = CaptureAsync(CaptureMode.Image, MenuCloseDelayMs);
    }

    public void StartScrollCapture()
    {
        if (_scrollStop is { } running) running.Cancel();
        else _ = CaptureAsync(CaptureMode.Scroll, 0);
    }

    public void StartScrollCaptureFromMenu()
    {
        if (_scrollStop is { } running) running.Cancel();
        else _ = CaptureAsync(CaptureMode.Scroll, MenuCloseDelayMs);
    }

    private async Task CaptureAsync(CaptureMode mode, int delayMs, ScreenSnapshot? early = null)
    {
        if (_capturing || _exiting) return;
        _capturing = true;
        if (_preCapture is not null) _preCapture.Paused = true;
        try
        {
            if (delayMs > 0) await Task.Delay(delayMs);
            var snapshot = early ?? ScreenGrabber.CaptureVirtualScreen();
            switch (await new RegionCaptureSession(snapshot, mode).RunAsync())
            {
                case ImageResult image:
                    await SaveAndOpenAsync(image.Capture);
                    break;
                case ScrollRequest scroll:
                    await ScrollCaptureAsync(scroll);
                    break;
            }
        }
        catch (Exception e)
        {
            Log.Error("Aufnahme fehlgeschlagen", e);
            _tray?.ShowWarning(ErrorText.WithDetails(Strings.CaptureFailed, e.Message));
        }
        finally
        {
            _capturing = false;
            if (_preCapture is not null) _preCapture.Paused = false;
        }
    }

    /// <summary>
    /// Scroll-Aufnahme: Rahmen + Panel zeigen, Esc vorübergehend global belegen, Ablauf im Hintergrund laufen lassen
    /// und das Ergebnis wie eine normale Aufnahme speichern.
    /// </summary>
    private async Task ScrollCaptureAsync(ScrollRequest request)
    {
        using var stop = new CancellationTokenSource();
        ScrollCaptureResult result;
        var frame = new ScrollFrameWindow(request.Region);
        var escape = new HotkeyManager();
        _scrollStop = stop;
        try
        {
            frame.StopRequested += () => stop.Cancel();
            escape.Pressed += () => stop.Cancel();
            frame.SetEscAvailable(escape.TryRegister(new HotkeySetting(0x1B, false, false, false, false), out _));
            frame.Show();
            // Overlay muss vollständig verschwunden sein, bevor das erste Teilbild entsteht
            await Task.Delay(180);

            var scroller = new WindowsScroller(request.Region);
            var activity = new UserActivityMonitor();
            try
            {
                var controller = new ScrollCaptureController(new RegionFrameSource(request.Region), scroller, activity, new TaskDelay());
                var dispatcher = Dispatcher.CurrentDispatcher;
                controller.Progress += progress => dispatcher.BeginInvoke(() => frame.Update(progress));
                result = await Task.Run(() => controller.RunAsync(stop.Token));
            }
            finally
            {
                scroller.RestoreCursor = !activity.UserTookOver;
                activity.Dispose();
                scroller.Dispose();
            }
        }
        finally
        {
            // Esc und Rahmen sofort freigeben – nicht erst nach dem Speichern
            _scrollStop = null;
            escape.Dispose();
            frame.Dispose();
        }

        Log.Info($"Scroll-Aufnahme beendet ({result.Reason}, {result.Image.Width} × {result.Image.Height} px, manuell: {result.WasManual})");
        if (result.Error is { } error) Log.Error("Scroll-Aufnahme mit Fehler beendet", error);
        var bitmap = BitmapSource.Create(result.Image.Width, result.Image.Height, 96, 96, PixelFormats.Bgr32, null,
            result.Image.Pixels, result.Image.Stride);
        bitmap.Freeze();
        var note = result.Reason switch
        {
            ScrollEndReason.MaxSize => Loc.Format(Strings.Scroll_MaxSize, result.Image.Height),
            ScrollEndReason.ContentChanged when result.WasManual =>
                Strings.Scroll_TooFast,
            ScrollEndReason.ContentChanged =>
                Strings.Scroll_ContentChanged,
            ScrollEndReason.Error => Strings.Scroll_Error,
            _ => null,
        };
        await SaveAndOpenAsync(new CroppedCapture(bitmap, request.Timestamp), note);
    }

    private async Task SaveAndOpenAsync(CroppedCapture capture, string? note = null)
    {
        // PNG im Hintergrund kodieren – bei langen Scroll-Aufnahmen dauert das spürbar
        var image = capture.Image;
        var (png, original) = await Task.Run(() =>
        {
            var encoded = Exporter.EncodePng(image);
            return (encoded, Exporter.DecodeImage(encoded));
        });
        var document = CaptureDocument.New(new DateTimeOffset(capture.Timestamp),
            new CapturedOn(_pcName, Environment.MachineName), new PixelSize(original.PixelWidth, original.PixelHeight));
        var store = _store;
        var pcName = _pcName;
        var paths = store.CreateNew(capture.Timestamp);
        var outcome = await Task.Run(() => store.Save(paths, document, png, png, pcName, DateTimeOffset.Now));

        string? warning = null;
        var unsaved = false;
        if (outcome is SaveOutcome.Failed failed)
        {
            // Speicherordner nicht beschreibbar: sofort lokal sichern, damit keine Aufnahme verloren geht
            Log.Error($"Neue Aufnahme nicht gespeichert ({failed.Error})", new IOException(failed.Detail));
            var rescueStore = new CaptureStore(RescueFolder, store.PcSlug);
            var rescuePaths = rescueStore.CreateNew(capture.Timestamp);
            outcome = await Task.Run(() => rescueStore.Save(rescuePaths, document, png, png, pcName, DateTimeOffset.Now));
            if (outcome is SaveOutcome.Saved)
            {
                paths = rescuePaths;
                warning = ErrorText.SaveFailed(failed, store.Folder, Loc.Format(Strings.SaveRescuedLocally, rescuePaths.PngPath));
            }
            else
            {
                unsaved = true;
                warning = ErrorText.SaveFailed(failed, store.Folder, Strings.SaveKeepsOpen);
            }
        }
        var open = new OpenCapture(paths, document, png, original);
        if (outcome is SaveOutcome.Saved saved) open.Document = saved.Document;
        if (note is not null) warning = warning is null ? note : note + "\n" + warning;

        if (_settings.CopyToClipboard) ClipboardHelper.TrySetImage(original, png);

        var editor = EnsureEditor();
        if (!await _editorVm!.OpenAsync(open, warning))
        {
            _tray?.ShowInfo("ShotMark", Strings.Notice_CaptureSaved);
            return;
        }
        if (unsaved) _editorVm.MarkUnsaved();
        _history!.NotifyChanged(paths.PngPath);
        editor.ShowAndActivate();
    }

    // ---------- Editor ----------

    private EditorWindow EnsureEditor() => _editor ??= new EditorWindow(_editorVm!, _history!, this);

    public void OpenEditor()
    {
        var editor = EnsureEditor();
        editor.ShowAndActivate();
        if (_editorVm!.Capture is null && _history!.Items.OfType<CaptureTileVm>().FirstOrDefault() is { } newest)
            _ = OpenFromHistoryAsync(newest.Path);
    }

    public async Task OpenFromHistoryAsync(string pngPath)
    {
        try
        {
            var loaded = await Task.Run(() => CaptureStore.Load(pngPath));
            await _editorVm!.OpenAsync(OpenCapture.FromLoaded(loaded, PcLabel), loaded.Warning is { } w ? ErrorText.Describe(w) : null);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException)
        {
            Log.Error("Öffnen fehlgeschlagen: " + pngPath, e);
            MessageBox.Show(_editor!, ErrorText.WithDetails(Strings.OpenFailed, e.Message), "ShotMark", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public void CaptureSaved(string pngPath) => _history?.NotifyChanged(pngPath);

    public string PcLabel(string? slug) => _history?.PcLabel(slug) ?? slug ?? Strings.History_UnknownPc;

    public void Notify(string message) => _tray?.ShowInfo("ShotMark", message);

    public bool ConfirmDiscard(string message)
    {
        var answer = _editor is { IsVisible: true } owner
            ? MessageBox.Show(owner, message, "ShotMark", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            : MessageBox.Show(message, "ShotMark", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        return answer == MessageBoxResult.Yes;
    }

    /// <summary>Abmelden oder Herunterfahren von Windows.</summary>
    public void SaveBlocking() => _editorVm?.SaveBlocking();

    public void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_store.Folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_store.Folder}\"") { UseShellExecute = true });
        }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception)
        {
            Log.Error("Ordner öffnen fehlgeschlagen", e);
        }
    }

    // ---------- Einstellungen ----------

    public void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }
        // Während der Eingabe eines neuen Kürzels darf das alte keine Aufnahme auslösen
        _hotkeys?.Unregister();
        _scrollHotkeys?.Unregister();
        _hotkeyActive = false;
        _scrollHotkeyPending = false;
        _preCapture?.SetHotkey(null);
        _hotkeyRetry?.Stop();
        var window = new SettingsWindow(_settings, _automaticFolder, Autostart.IsInstalledCopy && _manageAutostart, CheckHotkey);
        if (_editor is { IsVisible: true }) window.Owner = _editor;
        _settingsWindow = window;
        var accepted = window.ShowDialog() == true;
        _settingsWindow = null;
        var restart = false;
        if (accepted && window.Result is { } result)
        {
            var locationChanged = result.SaveFolder != _settings.SaveFolder || result.PcName != _settings.PcName;
            restart = Language.WouldChange(result.Language);
            _settings = result;
            _settingsStore.Save(_settings);
            if (locationChanged)
            {
                ApplyFolderAndName(showFallbackWarning: true);
                _history!.Configure(_store.Folder, _store.PcSlug);
            }
            ApplyAutostart();
            _tray?.SetHotkeyLabels(HotkeyLabel, ScrollHotkeyLabel);
            _editor?.RefreshHotkeyLabel();
        }
        if (restart)
        {
            RestartRequested?.Invoke();
            return;
        }
        RegisterHotkey(showWarning: true);
    }

    /// <summary>Die Sprache wurde geändert – die App startet sich neu (siehe <see cref="App"/>).</summary>
    public event Action? RestartRequested;

    private string? CheckHotkey(HotkeySetting candidate)
    {
        if (_hotkeys is null) return null;
        var ok = _hotkeys.TryRegister(candidate, out var error);
        _hotkeys.Unregister();
        return ok ? null : error;
    }

    /// <summary>Beide Kürzel registrieren; belegte werden regelmäßig erneut versucht. Ergebnis: Aufnahme-Kürzel aktiv.</summary>
    private bool RegisterHotkey(bool showWarning)
    {
        if (_hotkeys is null) return false;
        _hotkeyActive = _hotkeys.TryRegister(_settings.Hotkey, out var error);
        if (_hotkeyActive) _preCapture?.SetHotkey(_settings.Hotkey);
        else Log.Error(error ?? "Tastenkürzel nicht registriert", null);

        var scrollError = RegisterScrollHotkey();
        if (scrollError is not null) Log.Error(scrollError, null);

        if (_hotkeyActive && !_scrollHotkeyPending) _hotkeyRetry?.Stop();
        else StartHotkeyRetry();

        if (!showWarning) return _hotkeyActive;
        // Nur eine Meldung zeigen – das Aufnahme-Kürzel ist wichtiger
        if (!_hotkeyActive) ShowHotkeyWarning(error);
        else if (scrollError is not null) _tray?.ShowWarning(Loc.Format(Strings.Hotkey_ScrollStillInTray, scrollError));
        return _hotkeyActive;
    }

    /// <summary>Scroll-Kürzel registrieren, falls eingeschaltet; liefert die Fehlermeldung, wenn es belegt ist.</summary>
    private string? RegisterScrollHotkey()
    {
        _scrollHotkeyPending = false;
        _scrollHotkeys?.Unregister();
        if (_scrollHotkeys is null || _settings.ScrollHotkey is not { } scroll || scroll == _settings.Hotkey) return null;
        if (_scrollHotkeys.TryRegister(scroll, out var error)) return null;
        _scrollHotkeyPending = true;
        return error ?? Strings.Hotkey_ScrollNotRegistered;
    }

    private void ShowHotkeyWarning(string? error)
    {
        if (error is null) return;
        if (_settings.Hotkey == HotkeySetting.PrintScreen && WindowsUsesPrintScreenForSnipping())
        {
            _tray?.ShowWarning(
                Strings.Hotkey_SnippingTool,
                () => OpenUri("ms-settings:easeofaccess-keyboard"));
        }
        else
        {
            _tray?.ShowWarning(Loc.Format(Strings.Hotkey_CaptureStillInTray, error));
        }
    }

    /// <summary>
    /// Belegtes Kürzel regelmäßig erneut versuchen – z. B. wenn beim Windows-Start ein anderes Programm schneller war
    /// oder der Nutzer die Screenshot-Funktion von Dropbox/OneDrive gerade ausgeschaltet hat.
    /// </summary>
    private void StartHotkeyRetry()
    {
        if (_hotkeyRetry is null)
        {
            _hotkeyRetry = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            _hotkeyRetry.Tick += (_, _) =>
            {
                if (_hotkeys is null || _scrollHotkeys is null || _settingsWindow is not null) return;
                if (!_hotkeyActive && _hotkeys.TryRegister(_settings.Hotkey, out _))
                {
                    _hotkeyActive = true;
                    _preCapture?.SetHotkey(_settings.Hotkey);
                    Log.Info($"Tastenkürzel „{HotkeyLabel}“ ist jetzt registriert");
                    _tray?.ShowInfo("ShotMark", Loc.Format(Strings.Notice_HotkeyActive, HotkeyLabel));
                }
                if (_scrollHotkeyPending && _settings.ScrollHotkey is { } scroll && _scrollHotkeys.TryRegister(scroll, out _))
                {
                    _scrollHotkeyPending = false;
                    Log.Info($"Tastenkürzel „{ScrollHotkeyLabel}“ für die Scroll-Aufnahme ist jetzt registriert");
                }
                if (_hotkeyActive && !_scrollHotkeyPending) _hotkeyRetry.Stop();
            };
        }
        _hotkeyRetry.Start();
    }

    /// <summary>Windows 11: „Druck-Taste zum Öffnen der Bildschirmaufnahme verwenden“ (ohne Eintrag ab Windows 11 standardmäßig an).</summary>
    private static bool WindowsUsesPrintScreenForSnipping()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Control Panel\Keyboard");
            return key?.GetValue("PrintScreenKeyForSnippingEnabled") switch
            {
                int value => value != 0,
                null => Environment.OSVersion.Version.Build >= 22000,
                _ => false,
            };
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

    private static void OpenUri(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            Log.Error("Konnte nicht öffnen: " + uri, e);
        }
    }

    private SaveFolderResolver.Result ResolveFolder(AppSettings settings)
    {
        var dropbox = DropboxLocator.FindRoot(DropboxLocator.DefaultInfoJsonPaths());
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        _automaticFolder = SaveFolderResolver.Resolve(null, dropbox, pictures).Folder;
        return SaveFolderResolver.Resolve(settings.SaveFolder, dropbox, pictures);
    }

    private void ApplyFolderAndName(bool showFallbackWarning)
    {
        _pcName = _settings.PcName!;
        var slug = PcNames.ToSlug(_pcName);
        var folder = ResolveFolder(_settings);
        _store = new CaptureStore(folder.Folder, slug);
        try
        {
            Directory.CreateDirectory(folder.Folder);
            PcRegistry.Register(folder.Folder, new PcInfo(slug, _pcName, Environment.MachineName));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Log.Error("Speicherordner nicht beschreibbar: " + folder.Folder, e);
            _tray?.ShowWarning(Loc.Format(Strings.Folder_NotWritable, folder.Folder));
        }
        if (folder.IsFallback && showFallbackWarning)
            _tray?.ShowWarning(Loc.Format(Strings.Folder_NoDropbox, folder.Folder));
    }

    private void ApplyAutostart()
    {
        if (!_manageAutostart) return;
        try
        {
            if (Environment.ProcessPath is { } exe) Autostart.Apply(_settings.StartWithWindows, exe);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            Log.Error("Autostart konnte nicht gesetzt werden", e);
        }
    }

    // ---------- Beenden ----------

    public async void Exit()
    {
        if (await ShutdownAsync()) Application.Current.Shutdown();
    }

    /// <summary>Sichert offene Änderungen und räumt Fenster, Tray und Kürzel ab. False, wenn das Sichern abgelehnt wurde.</summary>
    public async Task<bool> ShutdownAsync()
    {
        if (_exiting) return false;
        _exiting = true;
        if (_editorVm is not null && !await _editorVm.SecureUnsavedAsync())
        {
            _exiting = false;
            return false;
        }
        if (_editor is not null)
        {
            _editor.AllowClose = true;
            _editor.Close();
        }
        Dispose();
        return true;
    }

    // ---------- Updates ----------

    /// <summary>Für die Entscheidung, ob ein Update-Neustart gerade niemanden stört.</summary>
    public UpdateActivity CurrentActivity() => new(
        Capturing: _capturing || _scrollStop is not null,
        EditorOpen: _editor is { IsVisible: true },
        UnsavedChanges: _editorVm?.IsDirty == true,
        // Einstellungen, Erststart-Dialog, Aufnahme-Overlays – jedes sichtbare Fenster zählt als „beschäftigt“
        DialogOpen: _settingsWindow is not null || Application.Current.Windows.OfType<Window>().Any(w => w.IsVisible),
        UserIdle: NativeMethods.UserIdleTime());

    public void Dispose()
    {
        _hotkeyRetry?.Stop();
        _hotkeys?.Dispose();
        _hotkeys = null;
        _scrollHotkeys?.Dispose();
        _scrollHotkeys = null;
        _preCapture?.Dispose();
        _preCapture = null;
        _tray?.Dispose();
        _tray = null;
        _history?.Dispose();
    }
}
