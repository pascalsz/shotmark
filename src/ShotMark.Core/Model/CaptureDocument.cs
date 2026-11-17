using System.Collections.Immutable;
using System.Text.Json.Serialization;
using ShotMark.Core.Geometry;
namespace ShotMark.Core.Model;

public sealed record CapturedOn(string PcName, string MachineName);

public sealed record CaptureDocument
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;
    public required DateTimeOffset CapturedAt { get; init; }
    public required CapturedOn CapturedOn { get; init; }
    public int Revision { get; init; }

    /// <summary>Neu bei jeder Speicherung – erkennt auch zwei PCs, die dieselbe Revision gespeichert haben.</summary>
    public Guid SaveId { get; init; }
    public DateTimeOffset ModifiedAt { get; init; }
    public string ModifiedOn { get; init; } = "";
    public required PixelSize ImageSize { get; init; }
    public RectD? Crop { get; init; }
    public ImmutableList<Annotation> Annotations { get; init; } = [];

    [JsonIgnore]
    public RectD OutputRect => Crop ?? new RectD(0, 0, ImageSize.Width, ImageSize.Height);

    public static CaptureDocument New(DateTimeOffset capturedAt, CapturedOn capturedOn, PixelSize imageSize) => new()
    {
        CapturedAt = capturedAt,
        CapturedOn = capturedOn,
        ImageSize = imageSize,
        ModifiedAt = capturedAt,
        ModifiedOn = capturedOn.PcName,
    };
}
