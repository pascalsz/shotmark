using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using ShotMark.Core.Localization;

namespace ShotMark.App.Localization;

/// <summary>Setzt einmal pro Prozess die Sprache der Oberfläche (Texte) und die Formatkultur (Datum, Uhrzeit).</summary>
internal static class Language
{
    private static bool _applied;

    /// <param name="setting">Einstellung <c>"de"</c>/<c>"en"</c> oder <c>null</c> für die Windows-Sprache.</param>
    public static void Apply(string? setting)
    {
        if (_applied) return;
        _applied = true;
        var (ui, format) = UiLanguage.Resolve(setting, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);
        CultureInfo.DefaultThreadCurrentUICulture = ui;
        CultureInfo.DefaultThreadCurrentCulture = format;
        CultureInfo.CurrentUICulture = ui;
        CultureInfo.CurrentCulture = format;
        // WPF-Bindungen formatieren sonst mit en-US, unabhängig von der Thread-Kultur
        FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(format.IetfLanguageTag)));
    }
}
