using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ShotMark.Core.Scrolling;
using static ShotMark.App.Native.NativeMethods;

namespace ShotMark.App.Capture;

/// <summary>
/// Während der Scroll-Aufnahme: orangefarbener Rahmen außerhalb des Bereichs (klick-durchlässig) und ein kleines
/// Panel mit Fortschritt und Stopp. Beide Fenster aktivieren sich nie und sind von Bildschirmfotos ausgeschlossen.
/// </summary>
internal sealed class ScrollFrameWindow : IDisposable
{
    public const string AutomaticHint = "Scrollt automatisch – das Seitenende wird erkannt.";

    private const int FrameGap = 8; // physische Pixel zwischen Bereich und Fensterkante, Rahmen liegt sicher außerhalb
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");
    private readonly Int32Rect _region;
    private readonly Window _frame;
    private readonly Window _panel;
    private readonly TextBlock _title;
    private readonly TextBlock _height;
    private readonly TextBlock _hint;
    private readonly Button _stop;

    public ScrollFrameWindow(Int32Rect region)
    {
        _region = region;
        var accent = new SolidColorBrush(Color.FromRgb(0xFF, 0x8A, 0x00));
        accent.Freeze();

        _frame = HelperWindow("ShotMark – Scroll-Rahmen");
        _frame.Content = new Border { BorderBrush = accent, BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(2) };
        _frame.SourceInitialized += (_, _) =>
        {
            MakeHelperWindow(_frame, clickThrough: true);
            PlaceFrame();
        };
        _frame.DpiChanged += (_, _) => _frame.Dispatcher.BeginInvoke(PlaceFrame);

        _title = new TextBlock { Text = "Scroll-Aufnahme", FontWeight = FontWeights.SemiBold, FontSize = 13.5 };
        _height = new TextBlock
        {
            Text = "0 px",
            FontSize = 12.5,
            Foreground = (Brush)Application.Current.FindResource("TextDimBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _hint = new TextBlock
        {
            Text = AutomaticHint,
            FontSize = 12,
            Foreground = (Brush)Application.Current.FindResource("TextDimBrush"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 6, 0, 0),
            LineHeight = 17,
        };
        var stop = _stop = new Button
        {
            Content = "Stopp  (Esc)",
            Style = (Style)Application.Current.FindResource("PrimaryButton"),
            Margin = new Thickness(0, 12, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        stop.Click += (_, _) => StopRequested?.Invoke();
        System.Windows.Automation.AutomationProperties.SetName(stop, "Scroll-Aufnahme stoppen");

        var header = new DockPanel();
        DockPanel.SetDock(_height, Dock.Right);
        header.Children.Add(_height);
        header.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children =
            {
                new System.Windows.Shapes.Ellipse { Width = 8, Height = 8, Fill = accent, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center },
                _title,
            },
        });

        _panel = HelperWindow("ShotMark – Scroll-Aufnahme");
        _panel.SizeToContent = SizeToContent.WidthAndHeight;
        _panel.Content = new Border
        {
            Width = 264,
            Padding = new Thickness(16, 14, 16, 14),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(0xF4, 0x23, 0x23, 0x26)),
            BorderBrush = (Brush)Application.Current.FindResource("LineBrush"),
            BorderThickness = new Thickness(1),
            Child = new StackPanel { Children = { header, _hint, stop } },
        };
        _panel.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        _panel.SetResourceReference(Control.FontFamilyProperty, "UiFont");
        _panel.SourceInitialized += (_, _) => MakeHelperWindow(_panel, clickThrough: false);
        _panel.Loaded += (_, _) => PlacePanel();
        _panel.DpiChanged += (_, _) => _panel.Dispatcher.BeginInvoke(PlacePanel);
    }

    public event Action? StopRequested;

    private FrameworkElement? _panelContentForTesting;

    /// <summary>Nur für Darstellungstests: Panel-Inhalt (einmalig vom Fenster gelöst) ohne Fenster rendern.</summary>
    internal FrameworkElement PanelContentForTesting
    {
        get
        {
            if (_panelContentForTesting is null)
            {
                _panelContentForTesting = (FrameworkElement)_panel.Content;
                _panel.Content = null;
            }
            return _panelContentForTesting;
        }
    }

    public void Show()
    {
        _frame.Show();
        _panel.Show();
    }

    /// <summary>Esc ließ sich nicht global belegen (anderes Programm): Knopf ohne Esc-Hinweis.</summary>
    public void SetEscAvailable(bool available) => _stop.Content = available ? "Stopp  (Esc)" : "Stopp";

    public void Update(ScrollProgress progress)
    {
        _height.Text = $"{progress.Height.ToString("N0", German)} px";
        _title.Text = progress.Manual ? "Scroll-Aufnahme · manuell" : "Scroll-Aufnahme";
        _hint.Text = progress.Hint ?? AutomaticHint;
    }

    public void Dispose()
    {
        _frame.Close();
        _panel.Close();
    }

    private static Window HelperWindow(string title) => new()
    {
        Title = title,
        WindowStyle = WindowStyle.None,
        AllowsTransparency = true,
        Background = Brushes.Transparent,
        ResizeMode = ResizeMode.NoResize,
        Topmost = true,
        ShowInTaskbar = false,
        ShowActivated = false,
        WindowStartupLocation = WindowStartupLocation.Manual,
        Left = -10000,
        Top = -10000,
    };

    private void PlaceFrame()
    {
        var hwnd = new WindowInteropHelper(_frame).Handle;
        if (hwnd == IntPtr.Zero) return;
        SetWindowPos(hwnd, HWND_TOPMOST, _region.X - FrameGap, _region.Y - FrameGap,
            _region.Width + 2 * FrameGap, _region.Height + 2 * FrameGap, SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    /// <summary>Rechts neben den Bereich, sonst links, darunter oder innen oben rechts – immer auf demselben Monitor.</summary>
    private void PlacePanel()
    {
        var hwnd = new WindowInteropHelper(_panel).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dpi = VisualTreeHelper.GetDpi(_panel);
        var width = (int)Math.Ceiling(_panel.ActualWidth * dpi.DpiScaleX);
        var height = (int)Math.Ceiling(_panel.ActualHeight * dpi.DpiScaleY);
        var screen = System.Windows.Forms.Screen.FromRectangle(
            new System.Drawing.Rectangle(_region.X, _region.Y, _region.Width, _region.Height)).WorkingArea;

        const int gap = 14;
        int x = _region.X + _region.Width + gap, y = _region.Y;
        if (x + width > screen.Right)
        {
            x = _region.X - gap - width;
            if (x < screen.Left)
            {
                x = Math.Max(screen.Left, _region.X + _region.Width - width);
                y = _region.Y + _region.Height + gap;
                if (y + height > screen.Bottom)
                {
                    x = _region.X + _region.Width - width - gap;
                    y = _region.Y + gap;
                }
            }
        }
        y = Math.Clamp(y, screen.Top, Math.Max(screen.Top, screen.Bottom - height));
        SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }
}
