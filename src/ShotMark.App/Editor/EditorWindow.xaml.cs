using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ShotMark.App.History;
using ShotMark.App.Rendering;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;

namespace ShotMark.App.Editor;

internal interface IEditorCommands
{
    string HotkeyLabel { get; }
    void StartCapture();
    void OpenFolder();
    void ShowSettings();
    Task OpenFromHistoryAsync(string pngPath);
}

internal partial class EditorWindow : Window
{
    private static readonly (RgbColor Color, string Name)[] SwatchColors =
    [
        (Palette.Red, "Rot"), (Palette.Green, "Grün"), (Palette.Yellow, "Gelb"),
        (Palette.Blue, "Blau"), (Palette.Black, "Schwarz"), (Palette.White, "Weiß"),
    ];

    private readonly EditorViewModel _vm;
    private readonly HistoryStripViewModel _history;
    private readonly IEditorCommands _commands;
    private readonly Dictionary<ToolKind, RadioButton> _toolButtons;
    private readonly List<RadioButton> _swatches = [];
    private readonly DispatcherTimer _copyFeedback;
    private bool _syncing;
    private bool _fitMode = true;
    private TextBox? _textEditor;
    private TextAnnotation? _editingText;
    private PointD _editingPosition;
    private bool _justCommittedText;
    private System.Windows.Media.Effects.Effect? _shadowEffect;
    private bool? _compact;
    private readonly Dictionary<FrameworkElement, double> _wideWidths = [];

    public EditorWindow(EditorViewModel vm, HistoryStripViewModel history, IEditorCommands commands)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.NativeMethods.UseDarkTitleBar(this);
        _vm = vm;
        _history = history;
        _commands = commands;
        _toolButtons = new Dictionary<ToolKind, RadioButton>
        {
            [ToolKind.Select] = ToolSelect, [ToolKind.Rectangle] = ToolRectangle, [ToolKind.Arrow] = ToolArrow,
            [ToolKind.Check] = ToolCheck, [ToolKind.Cross] = ToolCross, [ToolKind.Text] = ToolText,
            [ToolKind.Blur] = ToolBlur, [ToolKind.Crop] = ToolCrop,
        };
        _copyFeedback = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
        _copyFeedback.Tick += (_, _) =>
        {
            _copyFeedback.Stop();
            CopyLabel.Text = "Kopieren";
        };

        foreach (var (kind, button) in _toolButtons)
            System.Windows.Automation.AutomationProperties.SetName(button, ToolName(kind));
        System.Windows.Automation.AutomationProperties.SetName(UndoButton, "Rückgängig");
        System.Windows.Automation.AutomationProperties.SetName(RedoButton, "Wiederholen");
        System.Windows.Automation.AutomationProperties.SetName(NewCaptureButton, "Neue Aufnahme");
        System.Windows.Automation.AutomationProperties.SetName(CopyButton, "Bild kopieren");

        Board.ViewModel = vm;
        Board.TextEditRequested += BeginTextEdit;
        BuildSwatches();

        vm.PropertyChanged += OnViewModelPropertyChanged;
        vm.StateChanged += OnViewModelStateChanged;
        history.ItemsChanged += OnHistoryItemsChanged;
        FilterChips.ItemsSource = history.Filters;
        PreviewKeyDown += OnPreviewKeyDown;
        SizeChanged += (_, _) => UpdateCompactMode();

        EmptyHint.Text = $"Drücke {commands.HotkeyLabel}, um einen Bereich aufzunehmen. Die Aufnahme öffnet sich dann hier.";
        NewCaptureButton.ToolTip = $"Neue Aufnahme ({commands.HotkeyLabel}, Strg+N)";

        SyncTool();
        SyncPanel();
        SyncStatus();
        SyncCapture();
        SyncUndo();
        SyncBanners();
        OnHistoryItemsChanged();
        UpdateCompactMode();
    }

    /// <summary>Unter ~1180 px Breite nur Symbole in der Werkzeugleiste, damit nichts überlappt.</summary>
    private void UpdateCompactMode()
    {
        var compact = ActualWidth is > 0 and < 1180 || (ActualWidth == 0 && Width < 1180);
        if (_compact == compact) return;
        _compact = compact;
        IEnumerable<FrameworkElement> buttons = [.. _toolButtons.Values, UndoButton, RedoButton, NewCaptureButton];
        foreach (var button in buttons)
        {
            if (!_wideWidths.ContainsKey(button)) _wideWidths[button] = button.Width;
            button.Width = compact ? 46 : _wideWidths[button];
            if (button is ContentControl { Content: Panel { Children.Count: > 1 } panel } && panel.Children[1] is TextBlock label)
                label.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        }
        CopyLabel.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        PanelColumn.Width = new GridLength(compact ? 220 : 252);
    }

    /// <summary>Nur beim Beenden von ShotMark wirklich schließen, sonst ausblenden.</summary>
    public bool AllowClose { get; set; }

    public void ShowAndActivate()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
        Topmost = true;
        Topmost = false;
        Board.Focus();
    }

    public void RefreshHotkeyLabel()
    {
        EmptyHint.Text = $"Drücke {_commands.HotkeyLabel}, um einen Bereich aufzunehmen. Die Aufnahme öffnet sich dann hier.";
        NewCaptureButton.ToolTip = $"Neue Aufnahme ({_commands.HotkeyLabel}, Strg+N)";
    }

    // ---------- Synchronisation ViewModel → Oberfläche ----------

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorViewModel.Tool):
                CommitTextEdit();
                SyncTool();
                SyncPanel();
                RefitLater();
                break;
            case nameof(EditorViewModel.Capture):
                CommitTextEdit(cancel: true);
                SyncCapture();
                _fitMode = true;
                RefitLater();
                SyncHistorySelection(scroll: true);
                break;
            case nameof(EditorViewModel.SelectedId):
            case nameof(EditorViewModel.CurrentStyle):
                SyncPanel();
                break;
            case nameof(EditorViewModel.StatusText):
            case nameof(EditorViewModel.SaveStatus):
                SyncStatus();
                break;
            case nameof(EditorViewModel.CanUndo):
            case nameof(EditorViewModel.CanRedo):
                SyncUndo();
                break;
            case nameof(EditorViewModel.Conflict):
            case nameof(EditorViewModel.Warning):
                SyncBanners();
                break;
        }
    }

    private void OnViewModelStateChanged()
    {
        UpdateSizeText();
        SyncPanel();
        if (_fitMode) RefitLater();
    }

    private void SyncTool()
    {
        _syncing = true;
        _toolButtons[_vm.Tool].IsChecked = true;
        _syncing = false;
    }

    private void SyncPanel()
    {
        var tool = _vm.Tool;
        var selected = _vm.Selected;
        var target = _vm.StyleTarget;
        PanelTitle.Text = _vm.Capture is null ? "Werkzeuge" : selected is not null ? $"{ToolName(target)} ausgewählt" : ToolName(tool);
        PanelHint.Text = _vm.Capture is null ? "Stehen bereit, sobald eine Aufnahme geöffnet ist."
            : selected is not null ? "Ziehen verschiebt, die Griffe ändern die Größe. Pfeiltasten schieben um 1 px, mit Umschalt um 10 px."
            : HintFor(tool);

        var showStyle = _vm.Capture is not null && target is not (ToolKind.Select or ToolKind.Crop);
        ColorSection.Visibility = showStyle && target != ToolKind.Blur ? Visibility.Visible : Visibility.Collapsed;
        SizeSection.Visibility = showStyle ? Visibility.Visible : Visibility.Collapsed;
        CropSection.Visibility = tool == ToolKind.Crop ? Visibility.Visible : Visibility.Collapsed;
        RemoveCropButton.IsEnabled = _vm.State.Crop is not null;
        SelectionSection.Visibility = selected is not null ? Visibility.Visible : Visibility.Collapsed;
        CropInfoSection.Visibility = tool != ToolKind.Crop && _vm.State.Crop is not null ? Visibility.Visible : Visibility.Collapsed;

        if (!showStyle) return;
        var style = _vm.CurrentStyle;
        var (label, min, max) = SizeSpec(target);
        _syncing = true;
        SizeLabel.Text = label;
        SizeSlider.Minimum = min;
        SizeSlider.Maximum = max;
        SizeSlider.Value = Math.Clamp(style.Size, min, max);
        SizeValue.Text = $"{style.Size:0} px";
        foreach (var swatch in _swatches) swatch.IsChecked = (RgbColor)swatch.Tag == style.Color;
        _syncing = false;
    }

    private void SyncStatus()
    {
        StatusText.Text = _vm.StatusText;
        StatusDot.Fill = _vm.SaveStatus switch
        {
            SaveStatus.Saved => (Brush)FindResource("OkBrush"),
            SaveStatus.Failed or SaveStatus.Conflict => (Brush)FindResource("DangerBrush"),
            _ => (Brush)FindResource("TextFaintBrush"),
        };
        RetryButton.Visibility = _vm.SaveStatus == SaveStatus.Failed ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncCapture()
    {
        var capture = _vm.Capture;
        var hasCapture = capture is not null;
        EmptyState.Visibility = hasCapture ? Visibility.Collapsed : Visibility.Visible;
        CanvasHost.Visibility = hasCapture ? Visibility.Visible : Visibility.Hidden;
        ZoomControls.Visibility = hasCapture ? Visibility.Visible : Visibility.Hidden;
        StatusDot.Visibility = hasCapture ? Visibility.Visible : Visibility.Hidden;
        CopyButton.IsEnabled = hasCapture;
        foreach (var button in _toolButtons.Values) button.IsEnabled = hasCapture;
        Title = hasCapture ? $"{Path.GetFileNameWithoutExtension(capture!.Paths.PngPath)} – ShotMark" : "ShotMark";
        FileText.Text = hasCapture
            ? $"{Path.GetFileName(capture!.Paths.PngPath)}   ·   aufgenommen auf {capture.Document.CapturedOn.PcName}, {capture.Document.CapturedAt.LocalDateTime:dd.MM.yyyy HH:mm}"
            : "";
        UpdateSizeText();
        SyncPanel();
    }

    private void UpdateSizeText()
    {
        if (_vm.Capture is not { } capture)
        {
            SizeText.Text = "";
            ZoomText.Text = "";
            return;
        }
        var output = _vm.State.Crop ?? capture.FullRect;
        SizeText.Text = $"{output.Width:0} × {output.Height:0} px";
    }

    private void SyncUndo()
    {
        UndoButton.IsEnabled = _vm.CanUndo;
        RedoButton.IsEnabled = _vm.CanRedo;
    }

    private void SyncBanners()
    {
        if (_vm.Conflict is { } conflict)
        {
            ConflictText.Text = $"Diese Aufnahme wurde inzwischen auf „{conflict.ModifiedOn}“ geändert ({conflict.ModifiedAt.LocalDateTime:dd.MM. HH:mm}). Deine Änderungen sind noch nicht gespeichert.";
            ConflictBanner.Visibility = Visibility.Visible;
        }
        else
        {
            ConflictBanner.Visibility = Visibility.Collapsed;
        }
        WarningText.Text = _vm.Warning ?? "";
        WarningBanner.Visibility = _vm.Warning is not null && _vm.Conflict is null ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string ToolName(ToolKind tool) => tool switch
    {
        ToolKind.Select => "Auswahl",
        ToolKind.Rectangle => "Rechteck",
        ToolKind.Arrow => "Pfeil",
        ToolKind.Check => "Haken",
        ToolKind.Cross => "Kreuz",
        ToolKind.Text => "Text",
        ToolKind.Blur => "Unkenntlich machen",
        ToolKind.Crop => "Zuschneiden",
        _ => "",
    };

    private static string HintFor(ToolKind tool) => tool switch
    {
        ToolKind.Select => "Markierung anklicken, um sie zu verschieben, zu ändern oder mit Entf zu löschen.",
        ToolKind.Rectangle => "Rahmen aufziehen. Mit Umschalt wird es ein Quadrat.",
        ToolKind.Arrow => "Vom Anfang zur Spitze ziehen. Mit Umschalt in 45°-Schritten.",
        ToolKind.Check => "Klicken setzt einen Haken, Ziehen bestimmt die Größe.",
        ToolKind.Cross => "Klicken setzt ein Kreuz, Ziehen bestimmt die Größe.",
        ToolKind.Text => "Ins Bild klicken und schreiben. Enter übernimmt, Umschalt+Enter macht eine neue Zeile.",
        ToolKind.Blur => "Bereich aufziehen, der verpixelt wird – z. B. Passwörter oder Namen.",
        ToolKind.Crop => "Rahmen aufziehen oder an den Griffen anpassen. Das Original bleibt erhalten.",
        _ => "",
    };

    private static (string Label, double Min, double Max) SizeSpec(ToolKind tool) => tool switch
    {
        ToolKind.Rectangle => ("Strichstärke", 1, 20),
        ToolKind.Arrow => ("Strichstärke", 2, 24),
        ToolKind.Check or ToolKind.Cross => ("Größe", 16, 200),
        ToolKind.Text => ("Schriftgröße", 10, 96),
        ToolKind.Blur => ("Blockgröße", 4, 40),
        _ => ("Größe", 1, 20),
    };

    private void BuildSwatches()
    {
        foreach (var (color, name) in SwatchColors)
        {
            var swatch = new RadioButton
            {
                Style = (Style)FindResource("Swatch"),
                GroupName = "Swatches",
                Background = color.ToBrush(),
                Tag = color,
                ToolTip = name,
            };
            System.Windows.Automation.AutomationProperties.SetName(swatch, name);
            swatch.Checked += (_, _) =>
            {
                if (!_syncing) _vm.SetColor(color);
            };
            _swatches.Add(swatch);
            Swatches.Children.Add(swatch);
        }
    }

    // ---------- Zoom ----------

    private void RefitLater()
    {
        if (_fitMode) Dispatcher.BeginInvoke(FitToWindow, DispatcherPriority.Loaded);
    }

    private void FitToWindow()
    {
        if (_vm.Capture is null) return;
        var view = Board.ViewRect;
        var availableWidth = Scroller.ActualWidth - 64;
        var availableHeight = Scroller.ActualHeight - 64;
        if (availableWidth <= 0 || availableHeight <= 0 || view.Width <= 0 || view.Height <= 0) return;
        // Lange Scroll-Aufnahmen auf die Breite einpassen (sonst wären sie winzig); nie über 100 % echte Pixel
        // Bei Breiteneinpassung erscheint der senkrechte Bildlaufbalken – seine Breite gleich mit abziehen
        var fit = view.Height > 2.5 * view.Width
            ? (availableWidth - 12) / view.Width
            : Math.Min(availableWidth / view.Width, availableHeight / view.Height);
        ApplyZoom(Math.Min(ActualSizeZoom, fit), fit: true);
    }

    /// <summary>Zoom, bei dem ein Bildpixel genau ein Bildschirmpixel ist (berücksichtigt die Windows-Skalierung).</summary>
    private double ActualSizeZoom => 1 / VisualTreeHelper.GetDpi(this).DpiScaleX;

    private void ApplyZoom(double zoom, bool fit)
    {
        CommitTextEdit();
        _fitMode = fit;
        Board.Zoom = Math.Clamp(zoom, 0.05, 8);
        ZoomText.Text = $"{Board.Zoom / ActualSizeZoom * 100:0} %";
        // Schatteneffekt nur für normal große Ansichten (Effekte auf sehr hohen Flächen sind teuer bzw. scheitern an der Texturgrenze)
        _shadowEffect ??= ImageShadow.Effect;
        ImageShadow.Effect = Board.ViewRect.Height * Board.Zoom > 6000 ? null : _shadowEffect;
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // Fenster auf Monitor mit anderer Skalierung verschoben: gleiche Pixelgröße beibehalten
        if (_fitMode) RefitLater();
        else if (_vm.Capture is not null) ApplyZoom(Board.Zoom * oldDpi.DpiScaleX / newDpi.DpiScaleX, fit: false);
    }

    private void OnScrollerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_fitMode) FitToWindow();
    }

    private void OnScrollerWheel(object sender, MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0 || _vm.Capture is null) return;
        ApplyZoom(Board.Zoom * Math.Pow(1.15, e.Delta / 120.0), fit: false);
        e.Handled = true;
    }

    private void OnZoomIn(object sender, RoutedEventArgs e) => ApplyZoom(Board.Zoom * 1.25, fit: false);
    private void OnZoomOut(object sender, RoutedEventArgs e) => ApplyZoom(Board.Zoom / 1.25, fit: false);
    private void OnZoomFit(object sender, RoutedEventArgs e) => FitToWindow();
    private void OnZoomActual(object sender, RoutedEventArgs e) => ApplyZoom(ActualSizeZoom, fit: false);

    // ---------- Text bearbeiten ----------

    private void BeginTextEdit(PointD position, TextAnnotation? existing)
    {
        CommitTextEdit();
        // Ein Klick, der nur die laufende Eingabe beendet, legt kein neues Textfeld an
        if (_justCommittedText && existing is null) return;

        _editingText = existing;
        _editingPosition = existing?.Position ?? position;
        var style = existing is not null ? AnnotationGeometry.StyleOf(existing) : _vm.StyleFor(ToolKind.Text);
        var zoom = Board.Zoom;
        var box = new TextBox
        {
            Text = existing?.Text ?? "",
            FontFamily = WpfTextMeasurer.FontFamily,
            FontWeight = FontWeights.Bold,
            FontSize = Math.Max(6, style.Size * zoom),
            Foreground = style.Color.ToBrush(),
            CaretBrush = style.Color.ToBrush(),
            Background = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255)),
            BorderBrush = (Brush)FindResource("AccentBrush"),
            Padding = new Thickness(0),
            MinWidth = 90,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.NoWrap,
        };
        System.Windows.Automation.AutomationProperties.SetName(box, "Text der Markierung");
        var screen = Board.ToScreen(_editingPosition);
        Canvas.SetLeft(box, screen.X - 3);
        Canvas.SetTop(box, screen.Y - 1);
        box.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                CommitTextEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CommitTextEdit(cancel: true);
                e.Handled = true;
            }
        };
        box.LostKeyboardFocus += (_, _) => CommitTextEdit();

        _textEditor = box;
        TextLayer.Children.Add(box);
        Board.HiddenId = existing?.Id;
        Board.InvalidateVisual();
        Dispatcher.BeginInvoke(() =>
        {
            box.Focus();
            if (existing is not null) box.SelectAll();
            else box.CaretIndex = box.Text.Length;
        }, DispatcherPriority.Input);
    }

    private void CommitTextEdit(bool cancel = false)
    {
        if (_textEditor is not { } box) return;
        _textEditor = null;
        TextLayer.Children.Remove(box);
        Board.HiddenId = null;

        if (!cancel)
        {
            var text = box.Text.TrimEnd('\r', '\n', ' ');
            if (_editingText is { } existing)
            {
                if (text.Length == 0) _vm.RemoveAnnotation(existing.Id);
                else if (text != existing.Text) _vm.ReplaceAnnotation(existing with { Text = text });
            }
            else if (text.Length > 0)
            {
                var style = _vm.StyleFor(ToolKind.Text);
                _vm.AddAnnotation(new TextAnnotation(Guid.NewGuid(), _editingPosition, text, style.Color, style.Size));
            }
        }
        _editingText = null;
        _justCommittedText = true;
        Dispatcher.BeginInvoke(() => _justCommittedText = false, DispatcherPriority.Background);
        Board.InvalidateVisual();
    }

    // ---------- Eingaben ----------

    private void OnToolChecked(object sender, RoutedEventArgs e)
    {
        if (_syncing || sender is not RadioButton { Tag: string tag }) return;
        _vm.Tool = Enum.Parse<ToolKind>(tag);
        Board.Focus();
    }

    private void OnSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // Feuert schon während InitializeComponent (Minimum/Value aus XAML), bevor das ViewModel gesetzt ist
        if (_syncing || SizeValue is null || _vm is null) return;
        var value = Math.Round(e.NewValue);
        SizeValue.Text = $"{value:0} px";
        _vm.SetSize(value);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_textEditor is not null && _textEditor.IsKeyboardFocusWithin) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        var shift = (modifiers & ModifierKeys.Shift) != 0;

        if ((modifiers & ModifierKeys.Control) != 0)
        {
            switch (key)
            {
                case Key.Z when shift: _vm.Redo(); break;
                case Key.Z: _vm.Undo(); break;
                case Key.Y: _vm.Redo(); break;
                case Key.C: Copy(); break;
                case Key.S: _ = _vm.FlushAsync(); break;
                case Key.N: _commands.StartCapture(); break;
                case Key.D0 or Key.NumPad0: FitToWindow(); break;
                case Key.D1 or Key.NumPad1: ApplyZoom(ActualSizeZoom, fit: false); break;
                case Key.OemPlus or Key.Add: ApplyZoom(Board.Zoom * 1.25, fit: false); break;
                case Key.OemMinus or Key.Subtract: ApplyZoom(Board.Zoom / 1.25, fit: false); break;
                default: return;
            }
            e.Handled = true;
            return;
        }

        if (modifiers is not (ModifierKeys.None or ModifierKeys.Shift) || _vm.Capture is null) return;
        if (Keyboard.FocusedElement is TextBox or Slider) return;
        var step = shift ? 10 : 1;
        switch (key)
        {
            case Key.Delete or Key.Back when _vm.Selected is not null: _vm.DeleteSelected(); break;
            case Key.Left when _vm.Selected is not null: _vm.Nudge(-step, 0); break;
            case Key.Right when _vm.Selected is not null: _vm.Nudge(step, 0); break;
            case Key.Up when _vm.Selected is not null: _vm.Nudge(0, -step); break;
            case Key.Down when _vm.Selected is not null: _vm.Nudge(0, step); break;
            case Key.Escape when _vm.Tool == ToolKind.Crop: _vm.CancelCrop(); break;
            case Key.Escape when _vm.SelectedId is not null: _vm.SelectedId = null; break;
            case Key.Enter when _vm.Tool == ToolKind.Crop: _vm.ApplyCrop(Board.CropDraft); break;
            case Key.V: _vm.Tool = ToolKind.Select; break;
            case Key.R: _vm.Tool = ToolKind.Rectangle; break;
            case Key.A: _vm.Tool = ToolKind.Arrow; break;
            case Key.H: _vm.Tool = ToolKind.Check; break;
            case Key.X: _vm.Tool = ToolKind.Cross; break;
            case Key.T: _vm.Tool = ToolKind.Text; break;
            case Key.U: _vm.Tool = ToolKind.Blur; break;
            case Key.C: _vm.Tool = ToolKind.Crop; break;
            default: return;
        }
        e.Handled = true;
    }

    private void Copy()
    {
        if (_vm.Capture is null) return;
        CommitTextEdit();
        CopyLabel.Text = _vm.CopyToClipboard() ? "Kopiert ✓" : "Zwischenablage belegt";
        _copyFeedback.Stop();
        _copyFeedback.Start();
    }

    private void OnNewCapture(object sender, RoutedEventArgs e) => _commands.StartCapture();
    private void OnUndo(object sender, RoutedEventArgs e) => _vm.Undo();
    private void OnRedo(object sender, RoutedEventArgs e) => _vm.Redo();
    private void OnCopy(object sender, RoutedEventArgs e) => Copy();
    private void OnOpenFolder(object sender, RoutedEventArgs e) => _commands.OpenFolder();
    private void OnSettings(object sender, RoutedEventArgs e) => _commands.ShowSettings();
    private void OnDelete(object sender, RoutedEventArgs e) => _vm.DeleteSelected();
    private void OnCropApply(object sender, RoutedEventArgs e) => _vm.ApplyCrop(Board.CropDraft);
    private void OnCropCancel(object sender, RoutedEventArgs e) => _vm.CancelCrop();
    private void OnCropRemove(object sender, RoutedEventArgs e) => _vm.RemoveCrop();
    private void OnRetrySave(object sender, RoutedEventArgs e) => _ = _vm.SaveAsync();
    private async void OnConflictReload(object sender, RoutedEventArgs e) => await _vm.ReloadFromDiskAsync();
    private async void OnConflictCopy(object sender, RoutedEventArgs e) => await _vm.SaveAsCopyAsync();
    private void OnDismissWarning(object sender, RoutedEventArgs e) => WarningBanner.Visibility = Visibility.Collapsed;

    // ---------- Verlauf ----------

    private void OnHistoryItemsChanged()
    {
        var viewer = FindDescendant<ScrollViewer>(HistoryList);
        var offset = viewer?.HorizontalOffset ?? 0;
        HistoryList.ItemsSource = _history.Items;
        if (viewer is not null && offset > 0)
            Dispatcher.BeginInvoke(() => viewer.ScrollToHorizontalOffset(offset), DispatcherPriority.Loaded);
        HistorySummary.Text = _history.Summary;
        HistoryEmpty.Visibility = _history.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        // Filter nur, wenn es wirklich mehrere PCs gibt („Alle PCs“ + mindestens zwei)
        FilterChips.Visibility = _history.Filters.Count > 2 ? Visibility.Visible : Visibility.Collapsed;
        SyncHistorySelection(scroll: false);
    }

    private void SyncHistorySelection(bool scroll)
    {
        _syncing = true;
        try
        {
            var tile = _vm.Capture is { } capture ? _history.TileFor(capture.Paths.PngPath) : null;
            HistoryList.SelectedItem = tile is not null && _history.Items.Contains(tile) ? tile : null;
            if (scroll && HistoryList.SelectedItem is not null) HistoryList.ScrollIntoView(HistoryList.SelectedItem);
        }
        finally
        {
            _syncing = false;
        }
    }

    private async void OnHistorySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_syncing || HistoryList.SelectedItem is not CaptureTileVm tile) return;
        if (_vm.Capture is { } current && string.Equals(current.Paths.PngPath, tile.Path, StringComparison.OrdinalIgnoreCase)) return;
        CommitTextEdit();
        await _commands.OpenFromHistoryAsync(tile.Path);
    }

    private void OnHistoryWheel(object sender, MouseWheelEventArgs e)
    {
        if (FindDescendant<ScrollViewer>(HistoryList) is not { } viewer) return;
        viewer.ScrollToHorizontalOffset(viewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void OnFilterChecked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { DataContext: PcFilterVm filter }) _history.SetFilter(filter.Slug);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            if (FindDescendant<T>(child) is { } nested) return nested;
        }
        return null;
    }

    // ---------- Schließen ----------

    protected override void OnClosing(CancelEventArgs e)
    {
        if (AllowClose)
        {
            base.OnClosing(e);
            return;
        }
        e.Cancel = true;
        _ = HideAfterSaveAsync();
    }

    private async Task HideAfterSaveAsync()
    {
        CommitTextEdit();
        await _vm.FlushAsync();
        if (_vm.IsDirty && _vm.Capture is not null)
        {
            var answer = MessageBox.Show(this,
                "Die letzten Änderungen konnten nicht gespeichert werden.\n\nTrotzdem schließen? Sie bleiben im Editor erhalten; spätestens beim Beenden sichert ShotMark sie lokal.",
                "ShotMark", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes) return;
        }
        Hide();
    }
}
