using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShotMark.App.Rendering;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;

namespace ShotMark.App.Tests;

public class ExporterTests
{
    private static BitmapSource Checkerboard(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            var v = (byte)((x / 4 + y / 4) % 2 == 0 ? 255 : 0);
            var i = (y * width + x) * 4;
            pixels[i] = pixels[i + 1] = pixels[i + 2] = v;
            pixels[i + 3] = 255;
        }
        var bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static (byte B, byte G, byte R) Pixel(BitmapSource bitmap, int x, int y)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        var buffer = new byte[4];
        converted.CopyPixels(new Int32Rect(x, y, 1, 1), buffer, 4, 0);
        return (buffer[0], buffer[1], buffer[2]);
    }

    private static CaptureDocument Doc(int width, int height) =>
        CaptureDocument.New(DateTimeOffset.Now, new CapturedOn("Test", "TEST"), new PixelSize(width, height));

    [Fact]
    public void Rectangle_border_is_red_and_inside_untouched() => Sta.Run(() =>
    {
        var original = Checkerboard(200, 100);
        var doc = Doc(200, 100) with { Annotations = [new RectangleAnnotation(Guid.NewGuid(), new RectD(20, 20, 100, 50), Palette.Red, 4, false)] };
        var flat = Exporter.Flatten(original, doc, new WpfTextMeasurer());
        Assert.Equal(200, flat.PixelWidth);
        var border = Pixel(flat, 20, 40);
        Assert.True(border.R > 200 && border.G < 100, $"Rand ist nicht rot: {border}");
        Assert.Equal(Pixel(original, 60, 40), Pixel(flat, 60, 40));
    });

    [Fact]
    public void Shadow_darkens_pixels_below_rectangle() => Sta.Run(() =>
    {
        var blank = BitmapSource.Create(120, 120, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 120 * 120 * 4).ToArray(), 120 * 4);
        blank.Freeze();
        var doc = Doc(120, 120) with { Annotations = [new RectangleAnnotation(Guid.NewGuid(), new RectD(20, 20, 60, 60), Palette.Red, 4, true)] };
        var flat = Exporter.Flatten(blank, doc, new WpfTextMeasurer());
        var belowBottomEdge = Pixel(flat, 50, 84);
        Assert.True(belowBottomEdge.R < 250, $"Kein Schatten unter dem Rahmen: {belowBottomEdge}");
    });

    [Fact]
    public void Blur_changes_the_region() => Sta.Run(() =>
    {
        var original = Checkerboard(64, 64);
        var doc = Doc(64, 64) with { Annotations = [new BlurAnnotation(Guid.NewGuid(), new RectD(0, 0, 32, 32), 16)] };
        var flat = Exporter.Flatten(original, doc, new WpfTextMeasurer());
        Assert.Equal(Pixel(flat, 0, 0), Pixel(flat, 5, 5));
        Assert.NotEqual(Pixel(original, 5, 5), Pixel(flat, 5, 5));
        Assert.Equal(Pixel(original, 50, 50), Pixel(flat, 50, 50));
    });

    [Fact]
    public void Crop_defines_output_size() => Sta.Run(() =>
    {
        var doc = Doc(200, 100) with { Crop = new RectD(10, 10, 50, 40) };
        var flat = Exporter.Flatten(Checkerboard(200, 100), doc, new WpfTextMeasurer());
        Assert.Equal((50, 40), (flat.PixelWidth, flat.PixelHeight));
    });

    [Fact]
    public void Text_check_cross_and_arrow_render_in_their_color() => Sta.Run(() =>
    {
        var blank = BitmapSource.Create(300, 200, 96, 96, PixelFormats.Bgra32, null, Enumerable.Repeat((byte)255, 300 * 200 * 4).ToArray(), 300 * 4);
        blank.Freeze();
        var doc = Doc(300, 200) with
        {
            Annotations =
            [
                new CheckAnnotation(Guid.NewGuid(), new RectD(0, 0, 100, 100), Palette.Green, false),
                new ArrowAnnotation(Guid.NewGuid(), new PointD(120, 150), new PointD(290, 150), Palette.Red, 8, false),
            ],
        };
        var flat = Exporter.Flatten(blank, doc, new WpfTextMeasurer());
        var checkCorner = Pixel(flat, 40, 80); // Knick des Hakens (0.40, 0.80)
        Assert.True(checkCorner.G > 120 && checkCorner.R < 100, $"Haken nicht grün: {checkCorner}");
        var shaft = Pixel(flat, 200, 150);
        Assert.True(shaft.R > 200 && shaft.G < 100, $"Pfeil nicht rot: {shaft}");
    });

    [Fact]
    public void Png_round_trip_keeps_size() => Sta.Run(() =>
    {
        var png = Exporter.EncodePng(Checkerboard(30, 20));
        var back = Exporter.DecodeImage(png);
        Assert.Equal((30, 20), (back.PixelWidth, back.PixelHeight));
    });

    [Fact]
    public void Pixelate_works_on_an_image_decoded_on_another_thread() => Sta.Run(() =>
    {
        // Wie beim Speichern einer Aufnahme: PNG im Hintergrund kodieren und dekodieren, im Editor verpixeln
        var png = Exporter.EncodePng(Checkerboard(64, 64));
        var original = Task.Run(() => Exporter.DecodeImage(png)).Result;
        var doc = Doc(64, 64) with { Annotations = [new BlurAnnotation(Guid.NewGuid(), new RectD(0, 0, 64, 64), 16)] };
        var flat = Exporter.Flatten(original, doc, new WpfTextMeasurer());
        // Ein 16er-Block über 4er-Schachbrett wird einfarbig grau statt schwarz/weiß
        Assert.Equal(Pixel(flat, 1, 1), Pixel(flat, 5, 1));
    });
}
