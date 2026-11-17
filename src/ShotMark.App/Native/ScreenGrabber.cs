using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ShotMark.App.Native.NativeMethods;

namespace ShotMark.App.Native;

/// <summary>Eingefrorenes Bild des gesamten virtuellen Bildschirms in physischen Pixeln.</summary>
internal sealed record ScreenSnapshot(BitmapSource Image, Int32Rect VirtualBounds, IReadOnlyList<Int32Rect> Monitors, DateTime Timestamp);

/// <summary>
/// Rohes GDI-Bild des Bildschirms: nur kopiert, noch nicht umgewandelt. Geht in wenigen Millisekunden und von jedem
/// Thread aus, deshalb auch im Tastatur-Hook verwendbar; die Umwandlung folgt später auf dem UI-Thread.
/// </summary>
internal sealed class RawScreen(IntPtr bitmap, Int32Rect bounds, DateTime timestamp) : IDisposable
{
    private IntPtr _bitmap = bitmap;

    public DateTime Timestamp { get; } = timestamp;

    public ScreenSnapshot ToSnapshot()
    {
        ObjectDisposedException.ThrowIf(_bitmap == IntPtr.Zero, this);
        var source = Imaging.CreateBitmapSourceFromHBitmap(_bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        // Bgr32: Bereiche ohne Monitor (versetzte Bildschirme) werden schwarz statt durchsichtig, PNGs werden kleiner
        var image = new WriteableBitmap(new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0));
        image.Freeze();

        var monitors = System.Windows.Forms.Screen.AllScreens
            .Select(s => new Int32Rect(s.Bounds.X, s.Bounds.Y, s.Bounds.Width, s.Bounds.Height))
            .ToList();
        return new ScreenSnapshot(image, bounds, monitors, Timestamp);
    }

    public void Dispose()
    {
        if (_bitmap == IntPtr.Zero) return;
        DeleteObject(_bitmap);
        _bitmap = IntPtr.Zero;
    }
}

internal static class ScreenGrabber
{
    public static ScreenSnapshot CaptureVirtualScreen()
    {
        using var raw = Grab();
        return raw.ToSnapshot();
    }

    public static RawScreen Grab()
    {
        var x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        var y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        var width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        var height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        var timestamp = DateTime.Now;

        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, SRCCOPY | CAPTUREBLT);
            SelectObject(memoryDc, previous);
            var raw = new RawScreen(bitmap, new Int32Rect(x, y, width, height), timestamp);
            bitmap = IntPtr.Zero;
            return raw;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}
