using ShotMark.Core.Geometry;
namespace ShotMark.Core.Model;

public enum ToolKind { Select, Rectangle, Arrow, Check, Cross, Text, Blur, Crop }

/// <summary>Size = Strichstärke (Rechteck/Pfeil), Kantenlänge (Haken/Kreuz), Schriftgröße (Text) oder Blockgröße (Unkenntlich).</summary>
public sealed record ToolStyle(RgbColor Color, double Size);

public static class ToolDefaults
{
    public static ToolStyle For(ToolKind tool) => tool switch
    {
        ToolKind.Rectangle => new(Palette.Red, 4),
        ToolKind.Arrow => new(Palette.Red, 6),
        ToolKind.Check => new(Palette.Green, 48),
        ToolKind.Cross => new(Palette.Red, 48),
        ToolKind.Text => new(Palette.Red, 20),
        ToolKind.Blur => new(Palette.Black, 12),
        _ => new(Palette.Red, 4),
    };
}
