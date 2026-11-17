using ShotMark.Core.Settings;
namespace ShotMark.Core.Tests.Settings;

public class SettingsStoreTests
{
    [Fact]
    public void Missing_file_gives_defaults()
    {
        using var dir = new TempDir();
        var settings = new SettingsStore(dir.Path).Load();
        Assert.Null(settings.PcName);
        Assert.Equal(HotkeySetting.PrintScreen, settings.Hotkey);
        Assert.Equal(HotkeySetting.ScrollDefault, settings.ScrollHotkey);
        Assert.True(settings.StartWithWindows);
        Assert.True(settings.CopyToClipboard);
    }

    [Fact]
    public void Round_trip()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.Path);
        var settings = new AppSettings { PcName = "Büro", Hotkey = new HotkeySetting(0x53, true, true, false, false), SaveFolder = @"D:\X", StartWithWindows = false };
        store.Save(settings);
        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void Corrupt_file_gives_defaults()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.Path);
        File.WriteAllText(store.FilePath, "{kaputt");
        Assert.Equal(new AppSettings(), store.Load());
    }

    [Fact]
    public void Switched_off_scroll_hotkey_stays_off()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.Path);
        store.Save(new AppSettings { ScrollHotkey = null });
        Assert.Null(store.Load().ScrollHotkey);
    }

    [Fact]
    public void Older_settings_file_gets_default_scroll_hotkey()
    {
        using var dir = new TempDir();
        var store = new SettingsStore(dir.Path);
        File.WriteAllText(store.FilePath, """{ "pcName": "Büro", "hotkey": { "virtualKey": 80, "control": true, "shift": true, "alt": false, "windows": false } }""");
        var settings = store.Load();
        Assert.Equal(new HotkeySetting(80, true, true, false, false), settings.Hotkey);
        Assert.Equal(HotkeySetting.ScrollDefault, settings.ScrollHotkey);
    }
}
