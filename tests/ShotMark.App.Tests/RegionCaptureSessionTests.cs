using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShotMark.App.Capture;
using ShotMark.App.Native;

namespace ShotMark.App.Tests;

/// <summary>Normales Kürzel nimmt beim Loslassen sofort auf; die Scroll-Aufnahme ist ein eigener Modus.</summary>
public class RegionCaptureSessionTests
{
    private static RegionCaptureSession Session(CaptureMode mode)
    {
        var bitmap = BitmapSource.Create(800, 600, 96, 96, PixelFormats.Bgr32, null, new byte[800 * 600 * 4], 800 * 4);
        bitmap.Freeze();
        var screen = new Int32Rect(0, 0, 800, 600);
        return new RegionCaptureSession(new ScreenSnapshot(bitmap, screen, [screen], new DateTime(2026, 10, 26, 9, 30, 0)), mode);
    }

    private static RegionCaptureSession Drag(CaptureMode mode, Point from, Point to)
    {
        var session = Session(mode);
        session.SetPointerForTesting(from, to);
        session.Release(to);
        return session;
    }

    [Fact]
    public void Image_mode_captures_on_release_without_confirmation() => Sta.Run(() =>
    {
        var session = Drag(CaptureMode.Image, new Point(100, 50), new Point(299, 149));

        Assert.True(session.Completion.IsCompleted);
        var image = Assert.IsType<ImageResult>(session.Completion.Result);
        Assert.Equal(200, image.Capture.Image.PixelWidth);
        Assert.Equal(100, image.Capture.Image.PixelHeight);
    });

    [Fact]
    public void Image_mode_never_starts_a_scroll_capture() => Sta.Run(() =>
    {
        var session = Drag(CaptureMode.Image, new Point(0, 0), new Point(499, 499));
        Assert.IsType<ImageResult>(session.Completion.Result);
    });

    [Fact]
    public void Scroll_mode_starts_scrolling_on_release() => Sta.Run(() =>
    {
        var session = Drag(CaptureMode.Scroll, new Point(100, 50), new Point(399, 449));

        var request = Assert.IsType<ScrollRequest>(session.Completion.Result);
        Assert.Equal(new Int32Rect(100, 50, 300, 400), request.Region);
        Assert.Equal(new DateTime(2026, 10, 26, 9, 30, 0), request.Timestamp);
    });

    [Fact]
    public void Scroll_mode_keeps_overlay_open_for_too_low_area() => Sta.Run(() =>
    {
        var session = Drag(CaptureMode.Scroll, new Point(100, 50), new Point(399, 120));

        Assert.False(session.Completion.IsCompleted);
        Assert.True(session.TooShortForScroll);
        Assert.Null(session.Selection);

        session.SetPointerForTesting(new Point(100, 50), new Point(399, 300), tooShortForScroll: true);
        session.Release(new Point(399, 300));
        Assert.IsType<ScrollRequest>(session.Completion.Result);
    });

    [Fact]
    public void Click_without_drag_keeps_waiting() => Sta.Run(() =>
    {
        var session = Drag(CaptureMode.Image, new Point(100, 50), new Point(101, 51));
        Assert.False(session.Completion.IsCompleted);
    });

    [Fact]
    public void Cancel_ends_without_result() => Sta.Run(() =>
    {
        var session = Session(CaptureMode.Scroll);
        session.Cancel();
        Assert.Null(session.Completion.Result);
    });
}
