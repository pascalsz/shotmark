using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ShotMark.App.Native.NativeMethods;

namespace ShotMark.App.Capture;

/// <summary>Randloses Vollbildfenster über genau einem Monitor mit eingefrorenem Bild und Auswahl-Ebene.</summary>
internal sealed class OverlayWindow : Window
{
    private readonly RegionCaptureSession _session;
    private readonly Int32Rect _monitor;
    private readonly OverlayLayer _layer;

    public OverlayWindow(RegionCaptureSession session, Int32Rect monitor)
    {
        _session = session;
        _monitor = monitor;
        Title = "ShotMark – Bereich auswählen";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        Cursor = Cursors.Cross;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = monitor.X;
        Top = monitor.Y;
        Width = monitor.Width;
        Height = monitor.Height;

        var image = new Image { Source = MonitorPart(session, monitor), Stretch = Stretch.Fill };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
        _layer = new OverlayLayer(session, monitor);
        Content = new Grid { Children = { image, _layer } };

        SourceInitialized += (_, _) => ApplyBounds();
        Loaded += (_, _) => ApplyBounds();
        DpiChanged += (_, _) => Dispatcher.BeginInvoke(ApplyBounds);
    }

    public bool Contains(Point physical) => OverlayLayer.Contains(_monitor, physical);

    public void Refresh() => _layer.InvalidateVisual();

    private static BitmapSource MonitorPart(RegionCaptureSession session, Int32Rect monitor)
    {
        var bounds = session.Snapshot.VirtualBounds;
        var image = session.Snapshot.Image;
        var x = Math.Clamp(monitor.X - bounds.X, 0, image.PixelWidth - 1);
        var y = Math.Clamp(monitor.Y - bounds.Y, 0, image.PixelHeight - 1);
        var part = new CroppedBitmap(image, new Int32Rect(x, y,
            Math.Min(monitor.Width, image.PixelWidth - x), Math.Min(monitor.Height, image.PixelHeight - y)));
        part.Freeze();
        return part;
    }

    // Exakt auf die physischen Monitorgrenzen setzen – unabhängig von der DPI-Umrechnung von WPF
    private void ApplyBounds()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero)
            SetWindowPos(hwnd, HWND_TOPMOST, _monitor.X, _monitor.Y, _monitor.Width, _monitor.Height, SWP_SHOWWINDOW);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        e.Handled = true;
        CaptureMouse();
        _session.PointerDown();
    }

    protected override void OnMouseMove(MouseEventArgs e) => _session.PointerMove();

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        e.Handled = true;
        _session.PointerUp();
    }

    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e)
    {
        _session.Cancel();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        _session.Cancel();
        e.Handled = true;
    }
}

/// <summary>Abdunklung, Auswahlrahmen, Fadenkreuz, Lupe und Beschriftungen.</summary>
internal sealed class OverlayLayer(RegionCaptureSession session, Int32Rect monitor) : FrameworkElement
{
    private const int MagnifierPixels = 15;
    private const double MagnifierDiameter = 120;
    private const double CursorGap = 22;

    private static readonly Color AccentColor = Color.FromRgb(0xFF, 0x8A, 0x00);
    private static readonly Brush DimBrush = Frozen(new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)));
    private static readonly Brush AccentBrush = Frozen(new SolidColorBrush(AccentColor));
    private static readonly Brush ShadowBrush = Frozen(new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)));
    private static readonly Brush PillBrush = Frozen(new SolidColorBrush(Color.FromArgb(230, 24, 24, 24)));
    private static readonly Brush GridBrush = Frozen(new SolidColorBrush(Color.FromArgb(45, 0, 0, 0)));
    private static readonly Typeface LabelFace = new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    public static bool Contains(Int32Rect area, Point p) =>
        p.X >= area.X && p.X < area.X + area.Width && p.Y >= area.Y && p.Y < area.Y + area.Height;

    protected override void OnRender(DrawingContext dc)
    {
        var width = ActualWidth;
        var height = ActualHeight;
        if (width <= 0 || height <= 0) return;

        var px = width / monitor.Width; // ein physisches Pixel in DIP
        var full = new Rect(0, 0, width, height);
        Rect Local(Int32Rect r) => new((r.X - monitor.X) * px, (r.Y - monitor.Y) * px, r.Width * px, r.Height * px);

        Geometry dim = new RectangleGeometry(full);
        var selection = session.Selection;
        if (selection is { } selected)
            dim = new CombinedGeometry(GeometryCombineMode.Exclude, dim, new RectangleGeometry(Local(selected)));
        dc.DrawGeometry(DimBrush, null, dim);

        if (selection is { } sel)
        {
            var r = Local(sel);
            dc.DrawRectangle(null, new Pen(ShadowBrush, 3 * px), new Rect(r.X - 1.5 * px, r.Y - 1.5 * px, r.Width + 3 * px, r.Height + 3 * px));
            dc.DrawRectangle(null, new Pen(AccentBrush, px), new Rect(r.X - px / 2, r.Y - px / 2, r.Width + px, r.Height + px));
            DrawSelectionLabel(dc, r, sel, full);
        }

        if (!Contains(monitor, session.Cursor)) return;

        var cursorPixel = new Point(Math.Floor(session.Cursor.X), Math.Floor(session.Cursor.Y));
        var center = new Point((cursorPixel.X - monitor.X + 0.5) * px, (cursorPixel.Y - monitor.Y + 0.5) * px);

        if (!session.IsDragging)
        {
            DrawCrosshair(dc, center, full, px);
            DrawHint(dc, full, Hint());
        }
        DrawMagnifier(dc, center, cursorPixel, full);
    }

    private string Hint() => session.Mode switch
    {
        CaptureMode.Scroll when session.TooShortForScroll =>
            $"↓  Zu niedrig für eine Scroll-Aufnahme – mindestens {RegionCaptureSession.MinScrollHeight} px hoch aufziehen  ·  Esc bricht ab",
        CaptureMode.Scroll => "↓  Scroll-Aufnahme: Bereich über den Inhalt aufziehen, der gescrollt werden soll  ·  Esc bricht ab",
        _ => "Bereich aufziehen  ·  Esc oder Rechtsklick bricht ab",
    };

    private static void DrawCrosshair(DrawingContext dc, Point center, Rect full, double px)
    {
        var under = new Pen(ShadowBrush, 3 * px);
        var line = new Pen(AccentBrush, px);
        foreach (var pen in new[] { under, line })
        {
            dc.DrawLine(pen, new Point(0, center.Y), new Point(full.Width, center.Y));
            dc.DrawLine(pen, new Point(center.X, 0), new Point(center.X, full.Height));
        }
    }

    private void DrawMagnifier(DrawingContext dc, Point center, Point cursorPixel, Rect full)
    {
        var radius = MagnifierDiameter / 2;
        var label = session.Selection is { } s
            ? $"{s.Width} × {s.Height}"
            : $"{cursorPixel.X - session.Snapshot.VirtualBounds.X:0}, {cursorPixel.Y - session.Snapshot.VirtualBounds.Y:0}";
        var text = Label(label, 12);
        var labelHeight = text.Height + 8;

        var mx = center.X + CursorGap + radius;
        var my = center.Y + CursorGap + radius;
        if (mx + radius > full.Width - 4) mx = center.X - CursorGap - radius;
        if (my + radius + labelHeight + 6 > full.Height - 4) my = center.Y - CursorGap - radius - labelHeight - 6;
        var circle = new EllipseGeometry(new Point(mx, my), radius, radius);
        var cell = MagnifierDiameter / MagnifierPixels;

        dc.PushClip(circle);
        dc.DrawRectangle(Brushes.Black, null, circle.Bounds);
        var image = session.Snapshot.Image;
        var bounds = session.Snapshot.VirtualBounds;
        var half = MagnifierPixels / 2;
        var source = new Int32Rect((int)cursorPixel.X - bounds.X - half, (int)cursorPixel.Y - bounds.Y - half, MagnifierPixels, MagnifierPixels);
        var x0 = Math.Max(0, source.X);
        var y0 = Math.Max(0, source.Y);
        var x1 = Math.Min(image.PixelWidth, source.X + source.Width);
        var y1 = Math.Min(image.PixelHeight, source.Y + source.Height);
        if (x1 > x0 && y1 > y0)
        {
            var part = new CroppedBitmap(image, new Int32Rect(x0, y0, x1 - x0, y1 - y0));
            var group = new DrawingGroup();
            RenderOptions.SetBitmapScalingMode(group, BitmapScalingMode.NearestNeighbor);
            using (var gdc = group.Open())
            {
                gdc.DrawImage(part, new Rect(
                    mx - radius + (x0 - source.X) * cell,
                    my - radius + (y0 - source.Y) * cell,
                    (x1 - x0) * cell,
                    (y1 - y0) * cell));
            }
            dc.DrawDrawing(group);
        }

        var gridPen = new Pen(GridBrush, 1);
        for (var i = 1; i < MagnifierPixels; i++)
        {
            dc.DrawLine(gridPen, new Point(mx - radius + i * cell, my - radius), new Point(mx - radius + i * cell, my + radius));
            dc.DrawLine(gridPen, new Point(mx - radius, my - radius + i * cell), new Point(mx + radius, my - radius + i * cell));
        }
        dc.DrawRectangle(null, new Pen(AccentBrush, 1.5), new Rect(mx - cell / 2, my - cell / 2, cell, cell));
        dc.Pop();

        dc.DrawGeometry(null, new Pen(ShadowBrush, 4), circle);
        dc.DrawGeometry(null, new Pen(Brushes.White, 2), circle);

        var pill = new Rect(mx - text.Width / 2 - 8, my + radius + 6, text.Width + 16, labelHeight);
        dc.DrawRoundedRectangle(PillBrush, null, pill, 4, 4);
        dc.DrawText(text, new Point(pill.X + 8, pill.Y + 4));
    }

    private void DrawSelectionLabel(DrawingContext dc, Rect selection, Int32Rect size, Rect full)
    {
        var text = Label($"{size.Width} × {size.Height}", 12);
        var pillHeight = text.Height + 6;
        var y = selection.Y - pillHeight - 6;
        if (y < 4) y = selection.Y + 6;
        var x = Math.Min(Math.Max(4, selection.X), full.Width - text.Width - 20);
        var pill = new Rect(x, y, text.Width + 16, pillHeight);
        dc.DrawRoundedRectangle(PillBrush, null, pill, 4, 4);
        dc.DrawText(text, new Point(pill.X + 8, pill.Y + 3));
    }

    private void DrawHint(DrawingContext dc, Rect full, string hint)
    {
        var text = Label(hint, 13);
        var pill = new Rect((full.Width - text.Width) / 2 - 14, 18, text.Width + 28, text.Height + 12);
        // Scroll-Modus deutlich kennzeichnen – er startet nach dem Loslassen sofort
        dc.DrawRoundedRectangle(PillBrush, session.Mode == CaptureMode.Scroll ? new Pen(AccentBrush, 1.5) : null, pill, 6, 6);
        dc.DrawText(text, new Point(pill.X + 14, pill.Y + 6));
    }

    private FormattedText Label(string text, double size) =>
        new(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, LabelFace, size, Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static Brush Frozen(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
