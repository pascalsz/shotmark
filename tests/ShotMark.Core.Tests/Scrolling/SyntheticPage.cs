using ShotMark.Core.Scrolling;
namespace ShotMark.Core.Tests.Scrolling;

/// <summary>Künstliche „Webseite“: Textblöcke, Leerzeilen, optional feste Kopf/Fußzeile und Scrollbalken.</summary>
internal static class SyntheticPage
{
    public static PixelFrame Page(int width, int height, int seed = 1)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var blank = y % 23 < 3; // Absatz-Abstand
            for (var x = 0; x < width; x++)
            {
                var i = (y * width + x) * 4;
                byte v = 255;
                if (!blank)
                {
                    var h = Hash(seed, y / 2, x / 6);
                    v = (byte)(h % 3 == 0 ? 30 + h % 60 : 235 + h % 20);
                }
                pixels[i] = v;
                pixels[i + 1] = (byte)Math.Min(255, v + (y % 7));
                pixels[i + 2] = v;
                pixels[i + 3] = 255;
            }
        }
        return new PixelFrame(width, height, pixels);
    }

    public static PixelFrame Solid(int width, int height, byte b, byte g, byte r)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b; pixels[i + 1] = g; pixels[i + 2] = r; pixels[i + 3] = 255;
        }
        return new PixelFrame(width, height, pixels);
    }

    /// <summary>Sichtbarer Ausschnitt ab <paramref name="offset"/>, wie ihn der Bildschirm zeigen würde.</summary>
    public static PixelFrame Viewport(PixelFrame page, int offset, int height, PixelFrame? header = null, PixelFrame? footer = null,
        int scrollbarWidth = 0, int noise = 0, int noiseSeed = 0)
    {
        var width = page.Width;
        var pixels = new byte[width * height * 4];
        var top = header?.Height ?? 0;
        var bottom = footer?.Height ?? 0;
        for (var y = 0; y < height; y++)
        {
            ReadOnlySpan<byte> source;
            if (y < top) source = header!.Row(y);
            else if (y >= height - bottom) source = footer!.Row(y - (height - bottom));
            else source = page.Row(Math.Min(page.Height - 1, offset + y - top));
            source.CopyTo(pixels.AsSpan(y * width * 4, width * 4));
        }
        if (scrollbarWidth > 0)
        {
            var contentHeight = height - top - bottom;
            var thumb = Math.Max(20, contentHeight * contentHeight / page.Height);
            var maxOffset = Math.Max(1, page.Height - contentHeight);
            var thumbTop = (int)((long)offset * (height - thumb) / maxOffset);
            for (var y = 0; y < height; y++)
            for (var x = width - scrollbarWidth; x < width; x++)
            {
                var i = (y * width + x) * 4;
                var v = (byte)(y >= thumbTop && y < thumbTop + thumb ? 90 : 225);
                pixels[i] = pixels[i + 1] = pixels[i + 2] = v;
            }
        }
        if (noise > 0)
        {
            var random = new Random(noiseSeed);
            for (var i = 0; i < pixels.Length; i++)
            {
                if (i % 4 == 3) continue;
                pixels[i] = (byte)Math.Clamp(pixels[i] + random.Next(-noise, noise + 1), 0, 255);
            }
        }
        return new PixelFrame(width, height, pixels);
    }

    /// <summary>Kopie von <paramref name="frame"/> mit <paramref name="box"/> an fester Bildschirmposition (schwebendes Element).</summary>
    public static PixelFrame Overlay(PixelFrame frame, PixelFrame box, int x, int y)
    {
        var pixels = (byte[])frame.Pixels.Clone();
        for (var row = 0; row < box.Height; row++)
            box.Row(row).CopyTo(pixels.AsSpan(((y + row) * frame.Width + x) * 4, box.Width * 4));
        return new PixelFrame(frame.Width, frame.Height, pixels);
    }

    public static PixelFrame Rows(PixelFrame frame, int start, int count, int? width = null)
    {
        var w = width ?? frame.Width;
        var pixels = new byte[w * count * 4];
        for (var y = 0; y < count; y++)
            frame.Row(start + y)[..(w * 4)].CopyTo(pixels.AsSpan(y * w * 4));
        return new PixelFrame(w, count, pixels);
    }

    public static PixelFrame Stack(params PixelFrame[] parts)
    {
        var width = parts[0].Width;
        var pixels = parts.SelectMany(p => p.Pixels).ToArray();
        return new PixelFrame(width, parts.Sum(p => p.Height), pixels);
    }

    /// <summary>Maximale Kanalabweichung zweier gleich großer Bilder (für Vergleiche mit Rauschen).</summary>
    public static int MaxDifference(PixelFrame a, PixelFrame b)
    {
        Assert.Equal((a.Width, a.Height), (b.Width, b.Height));
        var max = 0;
        for (var i = 0; i < a.Pixels.Length; i++)
            if (i % 4 != 3) max = Math.Max(max, Math.Abs(a.Pixels[i] - b.Pixels[i]));
        return max;
    }

    private static int Hash(int seed, int a, int b)
    {
        unchecked
        {
            var h = seed * 73856093 ^ a * 19349663 ^ b * 83492791;
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return h & 0x7fffffff;
        }
    }
}
