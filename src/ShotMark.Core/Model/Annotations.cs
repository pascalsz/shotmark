using System.Text.Json.Serialization;
using ShotMark.Core.Geometry;
namespace ShotMark.Core.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(RectangleAnnotation), "rectangle")]
[JsonDerivedType(typeof(ArrowAnnotation), "arrow")]
[JsonDerivedType(typeof(CheckAnnotation), "check")]
[JsonDerivedType(typeof(CrossAnnotation), "cross")]
[JsonDerivedType(typeof(TextAnnotation), "text")]
[JsonDerivedType(typeof(BlurAnnotation), "blur")]
public abstract record Annotation(Guid Id);

public sealed record RectangleAnnotation(Guid Id, RectD Rect, RgbColor Color, double StrokeWidth, bool Shadow) : Annotation(Id);

public sealed record ArrowAnnotation(Guid Id, PointD From, PointD To, RgbColor Color, double StrokeWidth, bool Shadow) : Annotation(Id);

public abstract record StampAnnotation(Guid Id, RectD Rect, RgbColor Color, bool Shadow) : Annotation(Id);

public sealed record CheckAnnotation(Guid Id, RectD Rect, RgbColor Color, bool Shadow) : StampAnnotation(Id, Rect, Color, Shadow);

public sealed record CrossAnnotation(Guid Id, RectD Rect, RgbColor Color, bool Shadow) : StampAnnotation(Id, Rect, Color, Shadow);

public sealed record TextAnnotation(Guid Id, PointD Position, string Text, RgbColor Color, double FontSize) : Annotation(Id);

public sealed record BlurAnnotation(Guid Id, RectD Rect, int BlockSize) : Annotation(Id);
