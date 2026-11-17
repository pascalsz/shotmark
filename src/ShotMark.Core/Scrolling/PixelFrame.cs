namespace ShotMark.Core.Scrolling;

/// <summary>Bild als BGRA-Bytes: 4 Byte pro Pixel, Zeilen ohne Auffüllung.</summary>
public sealed class PixelFrame
{
    public PixelFrame(int width, int height, byte[] pixels)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Bild ohne Fläche");
        if (pixels.Length != width * height * 4) throw new ArgumentException("Pixelpuffer passt nicht zur Bildgröße", nameof(pixels));
        Width = width;
        Height = height;
        Pixels = pixels;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Pixels { get; }
    public int Stride => Width * 4;

    public ReadOnlySpan<byte> Row(int y) => Pixels.AsSpan(y * Stride, Stride);

    public bool SameAs(PixelFrame other) =>
        Width == other.Width && Height == other.Height && Pixels.AsSpan().SequenceEqual(other.Pixels);
}
