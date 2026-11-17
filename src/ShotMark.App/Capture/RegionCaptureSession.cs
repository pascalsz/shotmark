using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ShotMark.App.Native;

namespace ShotMark.App.Capture;

internal sealed record CroppedCapture(BitmapSource Image, DateTime Timestamp);

/// <summary>Ergebnis der Bereichsauswahl: normales Bild oder Auftrag für eine Scroll-Aufnahme.</summary>
internal abstract record RegionResult;

internal sealed record ImageResult(CroppedCapture Capture) : RegionResult;

/// <summary>Bereich in physischen Pixeln des virtuellen Bildschirms.</summary>
internal sealed record ScrollRequest(Int32Rect Region, DateTime Timestamp) : RegionResult;

/// <summary>Normales Bild oder Scroll-Aufnahme – steht schon beim Start fest (eigenes Kürzel bzw. Tray-Eintrag).</summary>
internal enum CaptureMode { Image, Scroll }

/// <summary>
/// Steuert eine Bereichsauswahl über alle Monitore. Alle Koordinaten sind physische Pixel des
/// virtuellen Bildschirms (über GetCursorPos), damit Monitore mit unterschiedlicher Skalierung
/// korrekt zusammenspielen. Beim Loslassen der Maus ist die Aufnahme fertig bzw. die Scroll-Aufnahme startet.
/// </summary>
internal sealed class RegionCaptureSession(ScreenSnapshot snapshot, CaptureMode mode = CaptureMode.Image)
{
    private const int MinSelection = 3;
    public const int MinScrollHeight = 100;
    private readonly List<OverlayWindow> _windows = [];
    private readonly TaskCompletionSource<RegionResult?> _result = new();
    private bool _finished;

    public ScreenSnapshot Snapshot { get; } = snapshot;
    public CaptureMode Mode { get; } = mode;
    public Point Cursor { get; private set; }
    public Point? Start { get; private set; }
    public bool IsDragging => Start is not null;

    /// <summary>Letzter Zug war für eine Scroll-Aufnahme zu niedrig – es gibt sonst keine Überlappung zwischen den Schritten.</summary>
    public bool TooShortForScroll { get; private set; }

    public Int32Rect? Selection => Start is { } start ? FromPoints(start, Cursor) : null;

    /// <summary>Endet mit dem Bild, dem Scroll-Auftrag oder <c>null</c> bei Abbruch.</summary>
    internal Task<RegionResult?> Completion => _result.Task;

    public Task<RegionResult?> RunAsync()
    {
        Cursor = NativeMethods.CursorPosition();
        foreach (var monitor in Snapshot.Monitors)
            _windows.Add(new OverlayWindow(this, monitor));
        foreach (var window in _windows)
            window.Show();
        var home = _windows.FirstOrDefault(w => w.Contains(Cursor)) ?? _windows[0];
        home.Activate();
        Keyboard.Focus(home);
        return Completion;
    }

    internal void PointerDown()
    {
        Cursor = NativeMethods.CursorPosition();
        Start = Cursor;
        Refresh();
    }

    internal void PointerMove()
    {
        var position = NativeMethods.CursorPosition();
        if (position == Cursor) return;
        Cursor = position;
        Refresh();
    }

    internal void PointerUp() => Release(NativeMethods.CursorPosition());

    /// <summary>Maus an <paramref name="cursor"/> (physische Pixel) losgelassen.</summary>
    internal void Release(Point cursor)
    {
        Cursor = cursor;
        var selection = Selection;
        Start = null;
        // Bloßer Klick ohne Ziehen: weiter auf eine Auswahl warten
        if (selection is not { Width: >= MinSelection, Height: >= MinSelection } rect)
        {
            Refresh();
            return;
        }
        if (Mode == CaptureMode.Image)
        {
            Finish(new ImageResult(Crop(rect)));
            return;
        }
        if (rect.Height < MinScrollHeight)
        {
            TooShortForScroll = true;
            Refresh();
            return;
        }
        Finish(new ScrollRequest(rect, Snapshot.Timestamp));
    }

    internal void Cancel() => Finish(null);

    /// <summary>Nur für Darstellungstests: Mausposition und Ziehbeginn setzen, ohne echte Eingabe.</summary>
    internal void SetPointerForTesting(Point? start, Point cursor, bool tooShortForScroll = false)
    {
        Start = start;
        Cursor = cursor;
        TooShortForScroll = tooShortForScroll;
    }

    private static Int32Rect FromPoints(Point a, Point b)
    {
        var x0 = (int)Math.Min(a.X, b.X);
        var y0 = (int)Math.Min(a.Y, b.Y);
        var x1 = (int)Math.Max(a.X, b.X);
        var y1 = (int)Math.Max(a.Y, b.Y);
        return new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    private CroppedCapture Crop(Int32Rect rect)
    {
        var bounds = Snapshot.VirtualBounds;
        var x = Math.Clamp(rect.X - bounds.X, 0, Snapshot.Image.PixelWidth - 1);
        var y = Math.Clamp(rect.Y - bounds.Y, 0, Snapshot.Image.PixelHeight - 1);
        var width = Math.Min(rect.Width, Snapshot.Image.PixelWidth - x);
        var height = Math.Min(rect.Height, Snapshot.Image.PixelHeight - y);
        // Kopie lösen, damit das große Gesamtbild freigegeben werden kann
        var copy = new WriteableBitmap(new CroppedBitmap(Snapshot.Image, new Int32Rect(x, y, width, height)));
        copy.Freeze();
        return new CroppedCapture(copy, Snapshot.Timestamp);
    }

    private void Finish(RegionResult? result)
    {
        if (_finished) return;
        _finished = true;
        foreach (var window in _windows)
            window.Close();
        _result.TrySetResult(result);
    }

    private void Refresh()
    {
        foreach (var window in _windows)
            window.Refresh();
    }
}
