using System.Collections.Immutable;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
namespace ShotMark.Core.Editing;

public sealed record EditState(ImmutableList<Annotation> Annotations, RectD? Crop)
{
    public static EditState From(CaptureDocument document) => new(document.Annotations, document.Crop);

    public CaptureDocument ApplyTo(CaptureDocument document) => document with { Annotations = Annotations, Crop = Crop };
}
