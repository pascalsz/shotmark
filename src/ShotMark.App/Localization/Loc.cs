using System.Globalization;

namespace ShotMark.App.Localization;

/// <summary>Formatiert übersetzte Texte mit der Formatkultur der gewählten Sprache (Datum, Uhrzeit, Zahlen).</summary>
internal static class Loc
{
    public static string Format(string format, params object?[] args) => string.Format(CultureInfo.CurrentCulture, format, args);
}
