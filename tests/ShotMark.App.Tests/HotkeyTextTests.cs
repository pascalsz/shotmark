using ShotMark.App.Settings;
using ShotMark.Core.Settings;
namespace ShotMark.App.Tests;

public class HotkeyTextTests
{
    private static readonly HotkeySetting CtrlShiftL = new(0x4C, true, true, false, false);

    [Fact]
    public void German_key_names()
    {
        using var _ = new CultureScope("de-DE");
        Assert.Equal("Strg+Umschalt+L", HotkeyText.Format(CtrlShiftL));
        Assert.Equal("Druck", HotkeyText.Format(HotkeySetting.PrintScreen));
    }

    [Fact]
    public void English_key_names()
    {
        using var _ = new CultureScope("en-US");
        Assert.Equal("Ctrl+Shift+L", HotkeyText.Format(CtrlShiftL));
        Assert.Equal("PrtScn", HotkeyText.Format(HotkeySetting.PrintScreen));
    }
}
