namespace ShotMark.App.Tests;

public class StartupArgumentsTests
{
    [Fact]
    public void Plain_start_opens_the_window()
    {
        // Startmenü, Desktop-Verknüpfung, Doppelklick auf die exe: wer ShotMark selbst startet, will es sehen
        Assert.Equal(StartupAction.OpenEditor, StartupArguments.Parse([]));
        Assert.Equal(StartupAction.OpenEditor, StartupArguments.Parse(["--profile", @"C:\p"]));
    }

    [Fact]
    public void Background_start_stays_in_the_tray()
    {
        // Autostart und Neustart nach einem Update
        Assert.Equal(StartupAction.Background, StartupArguments.Parse(["--background"]));
        Assert.Equal(StartupAction.Background, StartupArguments.Parse(["--BACKGROUND"]));
    }

    [Fact]
    public void Capture_wins_over_background()
    {
        Assert.Equal(StartupAction.Capture, StartupArguments.Parse(["--capture"]));
        Assert.Equal(StartupAction.Capture, StartupArguments.Parse(["--background", "--capture"]));
    }
}
