using System.Globalization;

namespace ShotMark.Core.Localization;

/// <summary>
/// Wählt die Sprache der Oberfläche: Deutsch oder Englisch, nach Einstellung oder Windows-Anzeigesprache.
/// Jede andere Windows-Sprache bekommt Englisch.
/// </summary>
public static class UiLanguage
{
    public const string German = "de";
    public const string English = "en";

    /// <param name="setting"><c>"de"</c>, <c>"en"</c> oder <c>null</c> (automatisch). Unbekannte Werte gelten als automatisch.</param>
    /// <param name="windowsUi">Windows-Anzeigesprache.</param>
    /// <param name="windowsFormat">Regionalformat von Windows (Datum, Uhrzeit).</param>
    /// <returns>
    /// UI-Kultur für die Texte und Formatkultur für Datum und Uhrzeit. Das Regionalformat bleibt nur erhalten, wenn
    /// seine Sprache zur gewählten passt, damit keine fremdsprachigen Wochentage in den Sätzen landen.
    /// </returns>
    public static (CultureInfo Ui, CultureInfo Format) Resolve(string? setting, CultureInfo windowsUi, CultureInfo windowsFormat)
    {
        var language = setting is German or English
            ? setting
            : windowsUi.TwoLetterISOLanguageName == German ? German : English;
        var format = windowsFormat.TwoLetterISOLanguageName == language
            ? windowsFormat
            : CultureInfo.GetCultureInfo(language == German ? "de-DE" : "en-US");
        return (CultureInfo.GetCultureInfo(language), format);
    }
}
