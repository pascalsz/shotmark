using ShotMark.App.Tray;

namespace ShotMark.App.Tests;

public class AppVersionTests
{
    [Fact]
    public void Build_metadata_is_dropped()
    {
        Assert.Equal("1.2.0", AppVersion.FromInformational("1.2.0+28216dc0a1b2"));
    }

    [Fact]
    public void Prerelease_label_is_kept()
    {
        Assert.Equal("1.3.0-beta.1", AppVersion.FromInformational("1.3.0-beta.1"));
    }

    [Fact]
    public void Missing_version_reads_as_unknown()
    {
        Assert.Equal("?", AppVersion.FromInformational(null));
        Assert.Equal("?", AppVersion.FromInformational(""));
    }

    [Fact]
    public void Local_builds_say_they_are_development_builds()
    {
        // Releases setzen die Version über publish.ps1 (-p:Version); ohne das soll nichts nach einer echten Version aussehen
        Assert.Equal("0.0.0-dev", AppVersion.Current);
    }

    [Theory]
    [InlineData("de-DE", "ShotMark 1.2.0 – Druck für Screenshot")]
    [InlineData("en-US", "ShotMark 1.2.0 – PrtScn for a screenshot")]
    public void Tray_tooltip_names_the_version(string culture, string expected)
    {
        using var _ = new CultureScope(culture);
        var label = culture == "de-DE" ? "Druck" : "PrtScn";
        Assert.Equal(expected, TrayIcon.TooltipText(label, "1.2.0"));
    }

    [Theory]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void Tray_tooltip_stays_within_the_windows_limit(string culture)
    {
        // NotifyIcon.Text wirft ab 128 Zeichen
        using var _ = new CultureScope(culture);
        var longest = TrayIcon.TooltipText("Strg+Umschalt+Alt+Win+Pause", "10.20.300-beta.12");
        Assert.True(longest.Length <= 127, $"{longest.Length} Zeichen: {longest}");
    }
}
