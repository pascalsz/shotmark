using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace ShotMark.Core.Geometry;

[JsonConverter(typeof(RgbColorJsonConverter))]
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Parse(string value)
    {
        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 8) hex = hex[2..]; // #AARRGGBB, Alpha wird ignoriert
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            throw new FormatException($"Ungültige Farbe: '{value}'");
        return new RgbColor((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    public string ToHex() => $"#{R:X2}{G:X2}{B:X2}";

    public override string ToString() => ToHex();
}

public sealed class RgbColorJsonConverter : JsonConverter<RgbColor>
{
    public override RgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var text = reader.GetString() ?? throw new JsonException("Farbe fehlt");
        try { return RgbColor.Parse(text); }
        catch (FormatException e) { throw new JsonException(e.Message, e); }
    }

    public override void Write(Utf8JsonWriter writer, RgbColor value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToHex());
}

public static class Palette
{
    public static readonly RgbColor Red = new(0xFC, 0x42, 0x42);
    public static readonly RgbColor Green = new(0x2E, 0xAD, 0x4B);
    public static readonly RgbColor Yellow = new(0xFF, 0xC4, 0x00);
    public static readonly RgbColor Blue = new(0x2D, 0x7F, 0xF9);
    public static readonly RgbColor Black = new(0x1A, 0x1A, 0x1A);
    public static readonly RgbColor White = new(0xFF, 0xFF, 0xFF);
    public static IReadOnlyList<RgbColor> All { get; } = [Red, Green, Yellow, Blue, Black, White];
}
