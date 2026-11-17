using System.IO;
using System.Windows.Media.Imaging;
using ShotMark.Core.Geometry;

namespace ShotMark.App.History;

internal static class ThumbnailLoader
{
    private static readonly SemaphoreSlim Gate = new(4);

    public static async Task<(BitmapSource? Thumbnail, PixelSize? Size)> LoadAsync(string path, int decodeHeight)
    {
        await Gate.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var size = ReadPngSize(stream);
                stream.Position = 0;
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.DecodePixelHeight = decodeHeight;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
                return ((BitmapSource?)image, size);
            });
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            // Defekte, gesperrte oder gerade synchronisierte Datei: Kachel bleibt ohne Vorschau
            return (null, null);
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>Liest die Bildgröße direkt aus dem PNG-Kopf, ohne das Bild zu dekodieren.</summary>
    private static PixelSize? ReadPngSize(Stream stream)
    {
        Span<byte> header = stackalloc byte[24];
        if (stream.Read(header) < 24 || header[0] != 0x89 || header[1] != (byte)'P') return null;
        var width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
        var height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
        return new PixelSize(width, height);
    }
}
