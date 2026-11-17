using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ShotMark.App.Rendering;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;

namespace ShotMark.App.Editor;

/// <summary>
/// Zeichenfläche des Editors: zeigt Bild + Markierungen gezoomt und setzt Mausgesten in
/// Zeichnen, Auswählen, Verschieben, Größe ändern und Zuschneiden um.
/// </summary>
internal sealed class AnnotationCanvas : FrameworkElement
{
    private const double HandleSize = 9;
    private const double HitTolerance = 6;

    private static readonly Brush Accent = Frozen(Color.FromRgb(0x2D, 0x8C, 0xFF));
    private static readonly Brush CropDim = Frozen(Color.FromArgb(150, 0, 0, 0));
    private static readonly Brush PillBrush = Frozen(Color.FromArgb(230, 24, 24, 24));
    private static readonly Pen SelectionPen = FrozenPen(new Pen(Accent, 1) { DashStyle = new DashStyle([4, 3], 0) });
    private static readonly Pen HandlePen = FrozenPen(new Pen(Accent, 1.5));
    private static readonly Pen CropPen = FrozenPen(new Pen(Brushes.White, 1.5));
    private static readonly Pen ThirdsPen = FrozenPen(new Pen(Frozen(Color.FromArgb(90, 255, 255, 255)), 1));

    private enum Interaction { None, Drawing, Moving, Resizing, CropNew, CropMove, CropResize }

    private EditorViewModel? _vm;
    private double _zoom = 1;
    private Interaction _mode;
    private PointD _dragStart;
    private Annotation? _dragOriginal;
    private Annotation? _preview;
    private HandleKind _handle;
    private RectD? _cropDraft;
    private RectD _cropAtDragStart;

    public AnnotationCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
        RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
    }

    public event Action<PointD, TextAnnotation?>? TextEditRequested;

    /// <summary>Wird während der Textbearbeitung ausgeblendet.</summary>
    public Guid? HiddenId { get; set; }

    public RectD? CropDraft => _cropDraft;

    public EditorViewModel? ViewModel
    {
        get => _vm;
        set
        {
            if (_vm is not null)
            {
                _vm.StateChanged -= OnStateChanged;
                _vm.PropertyChanged -= OnViewModelPropertyChanged;
            }
            _vm = value;
            if (_vm is not null)
            {
                _vm.StateChanged += OnStateChanged;
                _vm.PropertyChanged += OnViewModelPropertyChanged;
            }
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public double Zoom
    {
        get => _zoom;
        set
        {
            if (Math.Abs(_zoom - value) < 1e-6) return;
            _zoom = value;
            RenderOptions.SetBitmapScalingMode(this, value >= 2 ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Sichtbarer Bildausschnitt in Bildkoordinaten (beim Zuschneiden das ganze Bild).</summary>
    public RectD ViewRect
    {
        get
        {
            if (_vm?.Capture is not { } capture) return new RectD(0, 0, 0, 0);
            return _vm.Tool == ToolKind.Crop ? capture.FullRect : _vm.State.Crop ?? capture.FullRect;
        }
    }

    public PointD ToImage(Point screen)
    {
        var view = ViewRect;
        return new PointD(screen.X / _zoom + view.X, screen.Y / _zoom + view.Y);
    }

    public Point ToScreen(PointD image)
    {
        var view = ViewRect;
        return new Point((image.X - view.X) * _zoom, (image.Y - view.Y) * _zoom);
    }

    public void CancelInteraction()
    {
        _mode = Interaction.None;
        _preview = null;
        _dragOriginal = null;
        ReleaseMouseCapture();
        InvalidateVisual();
    }

    private void OnStateChanged()
    {
        // „Zuschnitt entfernen“ oder Rückgängig während des Zuschneidens: Rahmen an den echten Zustand anpassen
        if (_vm?.Tool == ToolKind.Crop && _mode is not (Interaction.CropNew or Interaction.CropMove or Interaction.CropResize))
            _cropDraft = _vm.State.Crop ?? _vm.Capture?.FullRect;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(EditorViewModel.Tool):
                CancelInteraction();
                _cropDraft = _vm?.Tool == ToolKind.Crop ? _vm.State.Crop ?? _vm.Capture?.FullRect : null;
                InvalidateMeasure();
                InvalidateVisual();
                UpdateCursor(Mouse.GetPosition(this));
                break;
            case nameof(EditorViewModel.SelectedId):
            case nameof(EditorViewModel.Capture):
                InvalidateMeasure();
                InvalidateVisual();
                break;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_vm?.Capture is null) return new Size(0, 0);
        var view = ViewRect;
        return new Size(Math.Ceiling(view.Width * _zoom), Math.Ceiling(view.Height * _zoom));
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_vm?.Capture is not { } capture) return;
        var view = ViewRect;

        dc.DrawRectangle(Brushes.Transparent, null, new Rect(RenderSize));
        dc.PushTransform(new ScaleTransform(_zoom, _zoom));
        dc.PushTransform(new TranslateTransform(-view.X, -view.Y));
        dc.PushClip(new RectangleGeometry(view.ToWpf()));
        dc.DrawImage(capture.Original, capture.FullRect.ToWpf());
        foreach (var annotation in _vm.State.Annotations)
        {
            if (annotation.Id == HiddenId) continue;
            capture.Renderer.Draw(dc, _preview is not null && _preview.Id == annotation.Id ? _preview : annotation);
        }
        if (_mode == Interaction.Drawing && _preview is not null)
            capture.Renderer.Draw(dc, _preview);
        dc.Pop();
        dc.Pop();
        dc.Pop();

        if (_vm.Tool == ToolKind.Crop) DrawCropOverlay(dc, capture.FullRect);
        else if (ShownSelection is { } selected && selected.Id != HiddenId) DrawSelection(dc, selected);
    }

    private Annotation? ShownSelection =>
        _vm?.Selected is { } s ? (_preview is not null && _preview.Id == s.Id ? _preview : s) : null;

    private void DrawSelection(DrawingContext dc, Annotation annotation)
    {
        if (annotation is not ArrowAnnotation)
        {
            var bounds = AnnotationGeometry.Bounds(annotation, _vm!.Measurer);
            var a = ToScreen(bounds.TopLeft);
            var b = ToScreen(bounds.BottomRight);
            dc.DrawRectangle(null, SelectionPen, new Rect(Snap(a), Snap(b)));
        }
        foreach (var (_, position) in AnnotationGeometry.Handles(annotation))
            DrawHandle(dc, ToScreen(position), annotation is ArrowAnnotation);
    }

    private static void DrawHandle(DrawingContext dc, Point center, bool round)
    {
        var rect = new Rect(center.X - HandleSize / 2, center.Y - HandleSize / 2, HandleSize, HandleSize);
        if (round) dc.DrawEllipse(Brushes.White, HandlePen, center, HandleSize / 2 + 0.5, HandleSize / 2 + 0.5);
        else dc.DrawRectangle(Brushes.White, HandlePen, rect);
    }

    private void DrawCropOverlay(DrawingContext dc, RectD full)
    {
        var crop = _cropDraft ?? full;
        var outer = new Rect(ToScreen(full.TopLeft), ToScreen(full.BottomRight));
        var inner = new Rect(ToScreen(crop.TopLeft), ToScreen(crop.BottomRight));
        dc.DrawGeometry(CropDim, null, new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(outer), new RectangleGeometry(inner)));
        for (var i = 1; i <= 2; i++)
        {
            var x = inner.X + inner.Width * i / 3;
            var y = inner.Y + inner.Height * i / 3;
            dc.DrawLine(ThirdsPen, new Point(x, inner.Y), new Point(x, inner.Bottom));
            dc.DrawLine(ThirdsPen, new Point(inner.X, y), new Point(inner.Right, y));
        }
        dc.DrawRectangle(null, CropPen, inner);
        foreach (var (_, position) in CropHandles(crop))
            DrawHandle(dc, ToScreen(position), round: false);

        var label = new FormattedText($"{crop.Width:0} × {crop.Height:0}", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            new Typeface("Segoe UI"), 12, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        var pill = new Rect(inner.X + 8, inner.Y + 8, label.Width + 16, label.Height + 6);
        dc.DrawRoundedRectangle(PillBrush, null, pill, 4, 4);
        dc.DrawText(label, new Point(pill.X + 8, pill.Y + 3));
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_vm?.Capture is null) return;
        Focus();
        var screen = e.GetPosition(this);
        var p = ToImage(screen);
        e.Handled = true;

        if (_vm.Tool == ToolKind.Crop)
        {
            BeginCrop(screen, p);
            return;
        }

        if (e.ClickCount == 2 && TopmostAt(p) is TextAnnotation doubleClicked)
        {
            _vm.SelectedId = doubleClicked.Id;
            TextEditRequested?.Invoke(doubleClicked.Position, doubleClicked);
            return;
        }

        if (_vm.Selected is { } selected)
        {
            if (HandleAt(AnnotationGeometry.Handles(selected), screen) is { } handle)
            {
                Begin(Interaction.Resizing, p, selected);
                _handle = handle;
                return;
            }
            if (AnnotationGeometry.HitTest(selected, p, Tolerance, _vm.Measurer))
            {
                Begin(Interaction.Moving, p, selected);
                return;
            }
        }

        switch (_vm.Tool)
        {
            case ToolKind.Select:
                var hit = TopmostAt(p);
                _vm.SelectedId = hit?.Id;
                if (hit is not null) Begin(Interaction.Moving, p, hit);
                InvalidateVisual();
                return;
            case ToolKind.Text:
                var text = TopmostAt(p) as TextAnnotation;
                _vm.SelectedId = text?.Id;
                TextEditRequested?.Invoke(text?.Position ?? p, text);
                return;
            default:
                _vm.SelectedId = null;
                Begin(Interaction.Drawing, p, null);
                // Haken/Kreuz erscheinen schon beim Klick in Standardgröße
                _preview = AnnotationFactory.FromDrag(_vm.Tool, p, p, _vm.CurrentStyle, false);
                InvalidateVisual();
                return;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_vm?.Capture is null) return;
        var screen = e.GetPosition(this);
        var p = ToImage(screen);
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) != 0;
        var full = _vm.Capture.FullRect;

        switch (_mode)
        {
            case Interaction.Drawing:
                _preview = AnnotationFactory.FromDrag(_vm.Tool, _dragStart, p, _vm.CurrentStyle, shift);
                break;
            case Interaction.Moving:
                var dx = Math.Round(p.X - _dragStart.X);
                var dy = Math.Round(p.Y - _dragStart.Y);
                if (shift)
                {
                    if (Math.Abs(dx) > Math.Abs(dy)) dy = 0;
                    else dx = 0;
                }
                _preview = AnnotationGeometry.Translate(_dragOriginal!, dx, dy);
                break;
            case Interaction.Resizing:
                _preview = AnnotationGeometry.DragHandle(_dragOriginal!, _handle, p, shift);
                break;
            case Interaction.CropNew:
                _cropDraft = ClampCrop(RectD.FromPoints(_dragStart, p), full);
                break;
            case Interaction.CropMove:
                var moved = _cropAtDragStart.Offset(p.X - _dragStart.X, p.Y - _dragStart.Y);
                _cropDraft = moved with
                {
                    X = Math.Round(Math.Clamp(moved.X, 0, full.Width - moved.Width)),
                    Y = Math.Round(Math.Clamp(moved.Y, 0, full.Height - moved.Height)),
                };
                break;
            case Interaction.CropResize:
                _cropDraft = ClampCrop(ResizeRect(_cropAtDragStart, _handle, p), full);
                break;
            default:
                UpdateCursor(screen);
                return;
        }
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (_vm is null) return;
        var mode = _mode;
        var preview = _preview;
        var original = _dragOriginal;
        _mode = Interaction.None;
        _preview = null;
        _dragOriginal = null;
        ReleaseMouseCapture();

        switch (mode)
        {
            case Interaction.Drawing when preview is not null:
                _vm.AddAnnotation(preview);
                break;
            case Interaction.Moving or Interaction.Resizing when preview is not null && preview != original:
                _vm.ReplaceAnnotation(preview);
                break;
        }
        InvalidateVisual();
        UpdateCursor(e.GetPosition(this));
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (_mode is Interaction.Drawing or Interaction.Moving or Interaction.Resizing)
        {
            _mode = Interaction.None;
            _preview = null;
            InvalidateVisual();
        }
        else if (_mode != Interaction.None)
        {
            _mode = Interaction.None;
        }
        base.OnLostMouseCapture(e);
    }

    private void Begin(Interaction mode, PointD p, Annotation? original)
    {
        _mode = mode;
        _dragStart = p;
        _dragOriginal = original;
        _preview = null;
        CaptureMouse();
    }

    private void BeginCrop(Point screen, PointD p)
    {
        var full = _vm!.Capture!.FullRect;
        var crop = _cropDraft ?? full;
        _cropAtDragStart = crop;
        _dragStart = p;
        if (HandleAt(CropHandles(crop), screen) is { } handle)
        {
            _mode = Interaction.CropResize;
            _handle = handle;
        }
        else if (crop != full && crop.Contains(p))
        {
            _mode = Interaction.CropMove;
        }
        else
        {
            _mode = Interaction.CropNew;
            _cropDraft = new RectD(Math.Round(p.X), Math.Round(p.Y), 0, 0);
        }
        CaptureMouse();
        InvalidateVisual();
    }

    private double Tolerance => HitTolerance / _zoom;

    private Annotation? TopmostAt(PointD p) =>
        _vm is null ? null : AnnotationGeometry.TopmostAt(_vm.State.Annotations, p, Tolerance, _vm.Measurer);

    private HandleKind? HandleAt(IEnumerable<(HandleKind Kind, PointD Position)> handles, Point screen)
    {
        foreach (var (kind, position) in handles)
        {
            var s = ToScreen(position);
            if (Math.Abs(s.X - screen.X) <= HandleSize && Math.Abs(s.Y - screen.Y) <= HandleSize) return kind;
        }
        return null;
    }

    private static IReadOnlyList<(HandleKind Kind, PointD Position)> CropHandles(RectD crop) =>
        AnnotationGeometry.Handles(new BlurAnnotation(Guid.Empty, crop, 2));

    private static RectD ResizeRect(RectD rect, HandleKind handle, PointD p) =>
        ((BlurAnnotation)AnnotationGeometry.DragHandle(new BlurAnnotation(Guid.Empty, rect, 2), handle, p, false)).Rect;

    private static RectD ClampCrop(RectD rect, RectD full)
    {
        var r = rect.Intersect(full) ?? new RectD(Math.Clamp(rect.X, 0, full.Width), Math.Clamp(rect.Y, 0, full.Height), 0, 0);
        return new RectD(Math.Round(r.X), Math.Round(r.Y), Math.Round(r.Width), Math.Round(r.Height));
    }

    private void UpdateCursor(Point screen)
    {
        if (_vm?.Capture is null)
        {
            Cursor = Cursors.Arrow;
            return;
        }
        var p = ToImage(screen);
        if (_vm.Tool == ToolKind.Crop)
        {
            var crop = _cropDraft ?? _vm.Capture.FullRect;
            Cursor = HandleAt(CropHandles(crop), screen) is { } h ? CursorFor(h)
                : crop != _vm.Capture.FullRect && crop.Contains(p) ? Cursors.SizeAll
                : Cursors.Cross;
            return;
        }
        if (_vm.Selected is { } selected)
        {
            if (HandleAt(AnnotationGeometry.Handles(selected), screen) is { } handle)
            {
                Cursor = CursorFor(handle);
                return;
            }
            if (AnnotationGeometry.HitTest(selected, p, Tolerance, _vm.Measurer))
            {
                Cursor = Cursors.SizeAll;
                return;
            }
        }
        Cursor = _vm.Tool switch
        {
            ToolKind.Select => TopmostAt(p) is null ? Cursors.Arrow : Cursors.SizeAll,
            ToolKind.Text => TopmostAt(p) is TextAnnotation ? Cursors.Hand : Cursors.IBeam,
            _ => Cursors.Cross,
        };
    }

    private static Cursor CursorFor(HandleKind handle) => handle switch
    {
        HandleKind.TopLeft or HandleKind.BottomRight => Cursors.SizeNWSE,
        HandleKind.TopRight or HandleKind.BottomLeft => Cursors.SizeNESW,
        HandleKind.Top or HandleKind.Bottom => Cursors.SizeNS,
        HandleKind.Left or HandleKind.Right => Cursors.SizeWE,
        _ => Cursors.Cross,
    };

    private static Point Snap(Point p) => new(Math.Round(p.X) + 0.5, Math.Round(p.Y) + 0.5);

    private static Brush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Pen pen)
    {
        pen.Freeze();
        return pen;
    }
}
