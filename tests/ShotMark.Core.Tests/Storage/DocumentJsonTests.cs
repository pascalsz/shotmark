using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
using ShotMark.Core.Storage;
namespace ShotMark.Core.Tests.Storage;

public class DocumentJsonTests
{
    internal static CaptureDocument Sample() => CaptureDocument.New(
            new DateTimeOffset(2026, 10, 19, 15, 30, 12, TimeSpan.FromHours(2)),
            new CapturedOn("Büro", "DESKTOP-OFFICE"),
            new PixelSize(1018, 1061)) with
        {
            Revision = 3,
            Crop = new RectD(10, 10, 500, 400),
            Annotations =
            [
                new RectangleAnnotation(Guid.NewGuid(), new RectD(52, 156, 694, 101), Palette.Red, 4, true),
                new ArrowAnnotation(Guid.NewGuid(), new PointD(900, 80), new PointD(760, 190), Palette.Red, 6, true),
                new CheckAnnotation(Guid.NewGuid(), new RectD(20, 20, 48, 48), Palette.Green, true),
                new CrossAnnotation(Guid.NewGuid(), new RectD(80, 20, 48, 48), Palette.Red, true),
                new TextAnnotation(Guid.NewGuid(), new PointD(60, 300), "Hier klicken", Palette.Red, 20),
                new BlurAnnotation(Guid.NewGuid(), new RectD(300, 400, 200, 30), 12),
            ],
        };

    [Fact]
    public void Round_trips_all_annotation_types()
    {
        var doc = Sample();
        var back = DocumentJson.Deserialize(DocumentJson.Serialize(doc));
        Assert.True(doc.Annotations.SequenceEqual(back.Annotations));
        Assert.Equal(doc with { Annotations = back.Annotations }, back);
    }

    [Fact]
    public void Json_uses_readable_type_names_and_hex_colors()
    {
        var json = DocumentJson.Serialize(Sample());
        Assert.Contains("\"type\": \"rectangle\"", json);
        Assert.Contains("\"type\": \"check\"", json);
        Assert.Contains("\"color\": \"#FC4242\"", json);
        Assert.Contains("\"pcName\": \"Büro\"", json);
        Assert.DoesNotContain("\"right\"", json);
    }

    [Fact]
    public void Newer_format_version_is_rejected()
    {
        var json = DocumentJson.Serialize(Sample()).Replace("\"formatVersion\": 1", "\"formatVersion\": 2");
        Assert.Throws<NotSupportedException>(() => DocumentJson.Deserialize(json));
    }
}
