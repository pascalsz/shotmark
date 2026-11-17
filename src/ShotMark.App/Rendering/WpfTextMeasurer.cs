using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;

namespace ShotMark.App.Rendering;

/// <summary>Misst und formatiert Text für Text-Markierungen. Bildpixel = DIP (96 DPI).</summary>
internal sealed class WpfTextMeasurer : ITextMeasurer
{
    public const double Padding = 2;

    public static readonly Typeface Typeface =
        new(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);

    public static readonly FontFamily FontFamily = new("Segoe UI");

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public SizeD Measure(string text, double fontSize)
    {
        var formatted = Create(text.Length == 0 ? " " : text, fontSize, Brushes.Black);
        return new SizeD(formatted.WidthIncludingTrailingWhitespace + Padding * 2, formatted.Height + Padding * 2);
    }

    public static FormattedText Create(string text, double fontSize, Brush brush) =>
        new(text, German, FlowDirection.LeftToRight, Typeface, fontSize, brush, pixelsPerDip: 1.0);
}
