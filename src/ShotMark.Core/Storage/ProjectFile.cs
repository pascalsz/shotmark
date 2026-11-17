using System.IO.Compression;
using System.Text;
using ShotMark.Core.Model;
namespace ShotMark.Core.Storage;

public static class ProjectFile
{
    public const string DocumentEntry = "document.json";
    public const string OriginalEntry = "original.png";

    public static byte[] Write(CaptureDocument document, byte[] originalPng)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry(DocumentEntry, CompressionLevel.Optimal).Open(), new UTF8Encoding(false)))
                writer.Write(DocumentJson.Serialize(document));
            using (var stream = zip.CreateEntry(OriginalEntry, CompressionLevel.NoCompression).Open())
                stream.Write(originalPng);
        }
        return buffer.ToArray();
    }

    public static (CaptureDocument Document, byte[] OriginalPng) Read(string path)
    {
        using var zip = Open(path);
        var document = ReadDocument(zip);
        var entry = zip.GetEntry(OriginalEntry) ?? throw new InvalidDataException("original.png fehlt");
        using var stream = entry.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return (document, copy.ToArray());
    }

    public static CaptureDocument ReadDocument(string path)
    {
        using var zip = Open(path);
        return ReadDocument(zip);
    }

    private static ZipArchive Open(string path)
    {
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try
        {
            return new ZipArchive(stream, ZipArchiveMode.Read);
        }
        catch
        {
            // Kein gültiges ZIP: Datei sofort freigeben, sonst bleibt sie bis zur Garbage Collection gesperrt
            stream.Dispose();
            throw;
        }
    }

    private static CaptureDocument ReadDocument(ZipArchive zip)
    {
        var entry = zip.GetEntry(DocumentEntry) ?? throw new InvalidDataException("document.json fehlt");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return DocumentJson.Deserialize(reader.ReadToEnd());
    }
}
