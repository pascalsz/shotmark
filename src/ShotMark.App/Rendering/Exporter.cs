using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;

namespace ShotMark.App.Rendering;

internal static class Exporter
{
    /// <summary>Rendert Original + Markierungen 1:1 und schneidet auf den Zuschnitt zu.</summary>
    public static BitmapSource Flatten(BitmapSource original, CaptureDocument document, ITextMeasurer measurer, AnnotationRenderer? renderer = null)
    {
        var width = original.PixelWidth;
        var height = original.PixelHeight;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(original, new Rect(0, 0, width, height));
            (renderer ?? new AnnotationRenderer(original)).DrawAll(dc, document.Annotations);
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        target.Freeze();

        var output = OutputPixelRect(document, width, height);
        if (output.X == 0 && output.Y == 0 && output.Width == width && output.Height == height) return target;
        var cropped = new CroppedBitmap(target, output);
        cropped.Freeze();
        return cropped;
    }

    public static Int32Rect OutputPixelRect(CaptureDocument document, int width, int height)
    {
        var full = new RectD(0, 0, width, height);
        var rect = document.Crop is { } crop ? crop.ToPixelGrid().Intersect(full) ?? full : full;
        return new Int32Rect((int)rect.X, (int)rect.Y, Math.Max(1, (int)rect.Width), Math.Max(1, (int)rect.Height));
    }

    public static byte[] EncodePng(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var buffer = new MemoryStream();
        encoder.Save(buffer);
        return buffer.ToArray();
    }

    /// <summary>
    /// Dekodiert ein PNG zu einem freien Pixelbild. Ein Decoder-Frame bleibt an den <see cref="BitmapDecoder"/> und damit
    /// an den dekodierenden Thread gebunden – auch eingefroren. Wird im Hintergrund dekodiert, scheitert sonst später jedes
    /// Einfrieren eines daraus abgeleiteten Bildes auf dem UI-Thread (z. B. beim Verpixeln).
    /// </summary>
    public static BitmapSource DecodeImage(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var stride = (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * frame.PixelHeight];
        frame.CopyPixels(pixels, stride, 0);
        var image = BitmapSource.Create(frame.PixelWidth, frame.PixelHeight, frame.DpiX, frame.DpiY, frame.Format, frame.Palette, pixels, stride);
        image.Freeze();
        return image;
    }
}
