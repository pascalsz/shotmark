using System.Text.Encodings.Web;
using System.Text.Json;
using ShotMark.Core.Model;
namespace ShotMark.Core.Storage;

public static class DocumentJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        AllowOutOfOrderMetadataProperties = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // Umlaute lesbar in der Datei
    };

    public static string Serialize(CaptureDocument document) => JsonSerializer.Serialize(document, Options);

    public static CaptureDocument Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<CaptureDocument>(json, Options) ?? throw new JsonException("Leeres Dokument");
        if (document.FormatVersion > CaptureDocument.CurrentFormatVersion)
            throw new NotSupportedException($"Projektformat {document.FormatVersion} ist neuer als diese ShotMark-Version.");
        return document;
    }
}
