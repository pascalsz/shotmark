using System.Globalization;
using ShotMark.Core.Localization;
namespace ShotMark.Core.Tests.Localization;

public class UiLanguageTests
{
    private static (string Ui, string Format) Resolve(string? setting, string windowsUi, string windowsFormat)
    {
        var (ui, format) = UiLanguage.Resolve(setting, new CultureInfo(windowsUi), new CultureInfo(windowsFormat));
        return (ui.Name, format.Name);
    }

    [Theory]
    [InlineData("de-AT", "de", "de-AT")]
    [InlineData("de-CH", "de", "de-CH")]
    [InlineData("en-GB", "en", "en-GB")]
    [InlineData("fr-FR", "en", "en-US")]
    public void Follows_Windows_when_automatic(string windows, string ui, string format)
    {
        Assert.Equal((ui, format), Resolve(null, windows, windows));
    }

    [Fact]
    public void Explicit_setting_wins_over_Windows()
    {
        Assert.Equal(("de", "de-DE"), Resolve("de", "en-US", "en-US"));
        Assert.Equal(("en", "en-US"), Resolve("en", "de-DE", "de-DE"));
    }

    [Theory]
    [InlineData("fr")]
    [InlineData("")]
    [InlineData("DE-xx")]
    public void Unknown_setting_counts_as_automatic(string setting)
    {
        Assert.Equal(("de", "de-DE"), Resolve(setting, "de-DE", "de-DE"));
    }

    [Fact]
    public void Regional_format_is_kept_only_when_its_language_matches()
    {
        // Englische Oberfläche, deutsches Regionalformat: keine deutschen Wochentage in englischen Sätzen
        Assert.Equal(("en", "en-US"), Resolve(null, "en-US", "de-DE"));
        Assert.Equal(("de", "de-AT"), Resolve(null, "de-DE", "de-AT"));
    }
}
