using System.IO;
using System.Windows.Media.Imaging;
using ShotMark.App.Rendering;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
using ShotMark.Core.Storage;

namespace ShotMark.App.Editor;

/// <summary>Die gerade im Editor geöffnete Aufnahme.</summary>
internal sealed class OpenCapture
{
    public OpenCapture(CapturePaths paths, CaptureDocument document, byte[] originalPng, BitmapSource original)
    {
        Paths = paths;
        Document = document;
        OriginalPng = originalPng;
        Original = original;
        Renderer = new AnnotationRenderer(original);
    }

    public CapturePaths Paths { get; set; }

    /// <summary>Zuletzt gespeicherter/geladener Stand; <see cref="CaptureDocument.Revision"/> ist die Basis für die Konfliktprüfung.</summary>
    public CaptureDocument Document { get; set; }

    public byte[] OriginalPng { get; }
    public BitmapSource Original { get; }
    public AnnotationRenderer Renderer { get; }

    public RectD FullRect => new(0, 0, Original.PixelWidth, Original.PixelHeight);

    /// <param name="pcLabel">Anzeigename zu einem PC-Kürzel (z. B. „Buero“ → „Büro“).</param>
    public static OpenCapture FromLoaded(LoadedCapture loaded, Func<string?, string> pcLabel)
    {
        var image = Exporter.DecodeImage(loaded.ImagePng);
        var size = new PixelSize(image.PixelWidth, image.PixelHeight);
        if (loaded.Document is { } document)
            return new OpenCapture(loaded.Paths, document with { ImageSize = size }, loaded.ImagePng, image);

        // PNG ohne Projektdatei: Markierungen sind eingebrannt, ab jetzt wird ein Projekt angelegt
        var name = CaptureName.TryParse(loaded.Paths.BaseName, out var parsed) ? parsed : null;
        var capturedAt = name is not null
            ? new DateTimeOffset(name.Timestamp)
            : new DateTimeOffset(File.GetLastWriteTime(loaded.Paths.PngPath));
        var created = CaptureDocument.New(capturedAt, new CapturedOn(pcLabel(name?.PcSlug), ""), size);
        return new OpenCapture(loaded.Paths, created, loaded.ImagePng, image);
    }
}

internal interface IEditorHost
{
    CaptureStore Store { get; }
    string PcName { get; }

    /// <summary>Lokaler Ordner für Notfall-Sicherungen, wenn der Speicherordner nicht erreichbar ist.</summary>
    string RescueFolder { get; }

    void CaptureSaved(string pngPath);
    string PcLabel(string? slug);
    void Notify(string message);
    bool ConfirmDiscard(string message);
}
