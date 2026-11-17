using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ShotMark.App.Rendering;

internal static class ClipboardHelper
{
    /// <summary>Legt das Bild als Bitmap und als PNG ab (PNG behält in Office/Chat-Apps die volle Qualität).</summary>
    public static bool TrySetImage(BitmapSource image, byte[]? png = null)
    {
        png ??= Exporter.EncodePng(image);
        var data = new DataObject();
        data.SetImage(image);
        data.SetData("PNG", new MemoryStream(png), autoConvert: false);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetDataObject(data, copy: true);
                return true;
            }
            catch (ExternalException)
            {
                // Zwischenablage gerade von einer anderen App geöffnet
                Thread.Sleep(80);
            }
        }
        return false;
    }
}
