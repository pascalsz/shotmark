using ShotMark.App.Localization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ShotMark.Core.Localization;
using ShotMark.Core.Settings;
using ShotMark.Core.Storage;

namespace ShotMark.App.Settings;

internal partial class SettingsWindow : Window
{
    private readonly AppSettings _original;
    private readonly Func<HotkeySetting, string?> _checkHotkey;
    private readonly string _automaticFolder;
    private HotkeySetting _hotkey;
    private HotkeySetting? _scrollHotkey;
    private string? _customFolder;

    private static string IdleHint => Strings.Settings_IdleHint;

    /// <summary>Beispiel-Dateiname für die Vorschau – immer im festen Schema, unabhängig von der Sprache.</summary>
    internal static string ExampleFileName(string pcName) => new CaptureName(DateTime.Now, PcName.ToSlug(pcName)).BaseName + ".png";

    /// <param name="checkHotkey">Prüft, ob das Kürzel frei ist; liefert sonst eine Fehlermeldung.</param>
    public SettingsWindow(AppSettings settings, string automaticFolder, bool isInstalledCopy, Func<HotkeySetting, string?> checkHotkey)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.NativeMethods.UseDarkTitleBar(this);
        _original = settings;
        _checkHotkey = checkHotkey;
        _automaticFolder = automaticFolder;
        _hotkey = settings.Hotkey;
        _scrollHotkey = settings.ScrollHotkey;
        _customFolder = settings.SaveFolder;

        NameBox.Text = settings.PcName ?? "";
        HotkeyBox.Text = HotkeyText.Format(_hotkey);
        HotkeyHint.Text = IdleHint;
        UpdateScrollHotkey();
        AutostartBox.IsChecked = settings.StartWithWindows;
        AutostartBox.IsEnabled = isInstalledCopy;
        AutostartHint.Text = isInstalledCopy
            ? ""
            : Strings.Settings_AutostartHint;
        AutostartHint.Visibility = isInstalledCopy ? Visibility.Collapsed : Visibility.Visible;
        ClipboardBox.IsChecked = settings.CopyToClipboard;
        (settings.Language switch
        {
            UiLanguage.German => LanguageGerman,
            UiLanguage.English => LanguageEnglish,
            _ => LanguageAuto,
        }).IsChecked = true;
        UpdateFolder();
    }

    public AppSettings? Result { get; private set; }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        NamePreview.Text = name.Length > 0
            ? Loc.Format(Strings.Settings_NamePreview, ExampleFileName(name))
            : Strings.Settings_EnterName;
    }

    private void OnHotkeyFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        HotkeyHint.Text = sender == ScrollHotkeyBox
            ? Strings.Settings_PressScrollHotkey
            : Strings.Settings_PressCaptureHotkey;

    private void OnHotkeyBlur(object sender, KeyboardFocusChangedEventArgs e) => HotkeyHint.Text = IdleHint;

    private void OnHotkeyKeyDown(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.Tab or Key.Escape || HotkeyText.IsModifier(key)) return;
        e.Handled = true;
        Record(key, sender == ScrollHotkeyBox);
    }

    // Windows meldet die Druck-Taste nur beim Loslassen
    private void OnHotkeyKeyUp(object sender, KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key != Key.Snapshot) return;
        e.Handled = true;
        Record(key, sender == ScrollHotkeyBox);
    }

    private void Record(Key key, bool forScroll)
    {
        var modifiers = Keyboard.Modifiers;
        var candidate = new HotkeySetting(
            KeyInterop.VirtualKeyFromKey(key),
            (modifiers & ModifierKeys.Control) != 0,
            (modifiers & ModifierKeys.Shift) != 0,
            (modifiers & ModifierKeys.Alt) != 0,
            Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin));

        var standalone = key is Key.Snapshot or Key.Pause or Key.Scroll or (>= Key.F1 and <= Key.F24);
        if (!standalone && !(candidate.Control || candidate.Alt || candidate.Windows))
        {
            HotkeyHint.Text = Strings.Settings_NeedModifier;
            return;
        }
        if (candidate == (forScroll ? _hotkey : _scrollHotkey))
        {
            HotkeyHint.Text = forScroll
                ? Strings.Settings_TakenByCapture
                : Strings.Settings_TakenByScroll;
            return;
        }
        if (forScroll)
        {
            _scrollHotkey = candidate;
            UpdateScrollHotkey();
        }
        else
        {
            _hotkey = candidate;
            HotkeyBox.Text = HotkeyText.Format(_hotkey);
        }
        HotkeyHint.Text = Strings.Settings_Accepted;
    }

    private void OnResetHotkey(object sender, RoutedEventArgs e)
    {
        if (_scrollHotkey == HotkeySetting.PrintScreen)
        {
            HotkeyHint.Text = Strings.Settings_PrintScreenTakenByScroll;
            return;
        }
        _hotkey = HotkeySetting.PrintScreen;
        HotkeyBox.Text = HotkeyText.Format(_hotkey);
    }

    /// <summary>Scroll-Kürzel aus- bzw. wieder auf den Vorschlag einschalten – im Tray-Menü bleibt die Scroll-Aufnahme immer.</summary>
    private void OnToggleScrollHotkey(object sender, RoutedEventArgs e)
    {
        if (_scrollHotkey is not null) _scrollHotkey = null;
        else if (_hotkey == HotkeySetting.ScrollDefault)
        {
            HotkeyHint.Text = Strings.Settings_TakenByCaptureRetry;
            return;
        }
        else _scrollHotkey = HotkeySetting.ScrollDefault;
        UpdateScrollHotkey();
    }

    private void UpdateScrollHotkey()
    {
        ScrollHotkeyBox.Text = _scrollHotkey is { } scroll ? HotkeyText.Format(scroll) : Strings.Settings_ScrollOff;
        ScrollToggleButton.Content = _scrollHotkey is null ? HotkeyText.Format(HotkeySetting.ScrollDefault) : Strings.Settings_TurnOff;
        ScrollToggleButton.ToolTip = _scrollHotkey is null
            ? Strings.Settings_ScrollOnTip
            : Strings.Settings_ScrollOffTip;
    }

    private void OnPickFolder(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = Strings.Settings_PickFolderTitle,
            InitialDirectory = _customFolder ?? _automaticFolder,
        };
        if (dialog.ShowDialog(this) == true)
        {
            _customFolder = dialog.FolderName;
            UpdateFolder();
        }
    }

    private void OnAutoFolder(object sender, RoutedEventArgs e)
    {
        _customFolder = null;
        UpdateFolder();
    }

    private void UpdateFolder()
    {
        FolderBox.Text = _customFolder ?? _automaticFolder;
        FolderHint.Text = _customFolder is null
            ? Strings.Settings_FolderAuto
            : Strings.Settings_FolderCustom;
        AutoFolderButton.Visibility = _customFolder is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0)
        {
            ShowError(Strings.Settings_NameRequired);
            return;
        }
        if (_hotkey != _original.Hotkey && _checkHotkey(_hotkey) is { } hotkeyError)
        {
            ShowError(hotkeyError);
            return;
        }
        if (_scrollHotkey is { } scroll && scroll != _original.ScrollHotkey && _checkHotkey(scroll) is { } scrollError)
        {
            ShowError(Loc.Format(Strings.Settings_ScrollError, scrollError));
            return;
        }
        Result = _original with
        {
            PcName = name,
            Hotkey = _hotkey,
            ScrollHotkey = _scrollHotkey,
            SaveFolder = _customFolder,
            StartWithWindows = AutostartBox.IsChecked == true,
            CopyToClipboard = ClipboardBox.IsChecked == true,
            Language = LanguageGerman.IsChecked == true ? UiLanguage.German : LanguageEnglish.IsChecked == true ? UiLanguage.English : null,
        };
        DialogResult = true;
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorBox.Visibility = Visibility.Visible;
    }
}
