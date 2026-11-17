using ShotMark.Core.Capture;
using ShotMark.Core.Settings;
namespace ShotMark.Core.Tests.Capture;

public class PreCaptureTrackerTests
{
    private const int LCtrl = 0xA2;
    private const int RCtrl = 0xA3;
    private const int LShift = 0xA0;
    private const int LAlt = 0xA4;
    private const int P = 0x50;
    private const int C = 0x43;
    private const int PrintScreen = 0x2C;
    private static readonly HotkeySetting CtrlShiftP = new(P, true, true, false, false);

    private static PreCaptureTracker Tracker(HotkeySetting? hotkey) => new() { Hotkey = hotkey };

    [Fact]
    public void Grabs_on_first_modifier_of_the_hotkey_and_keeps_it_through_the_chord()
    {
        var t = Tracker(CtrlShiftP);
        Assert.True(t.KeyDown(LCtrl, 10_000));
        Assert.False(t.KeyDown(LShift, 10_100));
        Assert.False(t.KeyDown(P, 10_200));
        Assert.True(t.IsValid(10_250));
    }

    [Fact]
    public void Order_of_modifiers_does_not_matter_and_right_hand_keys_count()
    {
        var t = Tracker(CtrlShiftP);
        Assert.True(t.KeyDown(LShift, 10_000));
        Assert.False(t.KeyDown(RCtrl, 10_100));
        t.KeyDown(P, 10_200);
        Assert.True(t.IsValid(10_200));
    }

    [Fact]
    public void Other_shortcut_with_the_same_modifier_discards_the_grab()
    {
        var t = Tracker(CtrlShiftP);
        Assert.True(t.KeyDown(LCtrl, 10_000));
        t.KeyDown(C, 10_100);
        Assert.False(t.IsValid(10_100));
    }

    [Fact]
    public void Main_key_without_all_modifiers_discards_the_grab()
    {
        // Strg+P (Drucken) ist nicht Strg+Umschalt+P
        var t = Tracker(CtrlShiftP);
        t.KeyDown(LCtrl, 10_000);
        t.KeyDown(P, 10_100);
        Assert.False(t.IsValid(10_100));
    }

    [Fact]
    public void Modifier_outside_the_hotkey_discards_the_grab()
    {
        var t = Tracker(CtrlShiftP);
        t.KeyDown(LCtrl, 10_000);
        t.KeyDown(LAlt, 10_050);
        Assert.False(t.IsValid(10_100));
    }

    [Fact]
    public void No_grab_while_typing()
    {
        // Umschalt für einen Großbuchstaben kurz nach dem vorigen Buchstaben
        var t = Tracker(CtrlShiftP);
        t.KeyDown(C, 10_000);
        t.KeyUp(C, 10_080);
        Assert.False(t.KeyDown(LShift, 10_200));
        Assert.False(t.IsValid(10_200));
    }

    [Fact]
    public void Grabs_again_after_a_pause()
    {
        var t = Tracker(CtrlShiftP);
        t.KeyDown(C, 10_000);
        t.KeyUp(C, 10_080);
        Assert.True(t.KeyDown(LShift, 10_000 + PreCaptureTracker.QuietMs));
    }

    [Fact]
    public void Key_repeat_does_not_grab_again()
    {
        var t = Tracker(CtrlShiftP);
        Assert.True(t.KeyDown(LCtrl, 10_000));
        Assert.False(t.KeyDown(LCtrl, 10_500));
        Assert.False(t.KeyDown(LCtrl, 11_500));
        Assert.True(t.IsValid(11_500));
    }

    [Fact]
    public void Grab_expires()
    {
        var t = Tracker(CtrlShiftP);
        t.KeyDown(LCtrl, 10_000);
        Assert.True(t.IsValid(10_000 + PreCaptureTracker.MaxAgeMs));
        Assert.False(t.IsValid(10_001 + PreCaptureTracker.MaxAgeMs));
    }

    [Fact]
    public void Releasing_keys_keeps_the_grab_because_the_hotkey_message_may_arrive_later()
    {
        var t = Tracker(CtrlShiftP);
        t.KeyDown(LCtrl, 10_000);
        t.KeyDown(LShift, 10_050);
        t.KeyDown(P, 10_100);
        t.KeyUp(P, 10_150);
        t.KeyUp(LShift, 10_160);
        t.KeyUp(LCtrl, 10_170);
        Assert.True(t.IsValid(10_200));
    }

    [Fact]
    public void New_chord_without_grab_drops_the_old_one()
    {
        // Strg kurz angetippt, gleich danach erneut: das alte Bild darf nicht für das neue Kürzel gelten
        var t = Tracker(CtrlShiftP);
        Assert.True(t.KeyDown(LCtrl, 10_000));
        t.KeyUp(LCtrl, 10_050);
        Assert.False(t.KeyDown(LCtrl, 10_200));
        Assert.False(t.IsValid(10_200));
    }

    [Fact]
    public void Hotkey_without_modifiers_grabs_on_the_key_itself_even_while_typing()
    {
        var t = Tracker(HotkeySetting.PrintScreen);
        t.KeyDown(C, 10_000);
        t.KeyUp(C, 10_050);
        Assert.True(t.KeyDown(PrintScreen, 10_100));
        Assert.True(t.IsValid(10_100));
    }

    [Fact]
    public void Hotkey_without_modifiers_ignores_modifier_presses()
    {
        var t = Tracker(HotkeySetting.PrintScreen);
        Assert.False(t.KeyDown(LShift, 10_000));
        Assert.False(t.KeyDown(PrintScreen, 10_100));
        Assert.False(t.IsValid(10_100));
    }

    [Fact]
    public void Main_key_alone_does_not_grab_when_the_hotkey_has_modifiers()
    {
        var t = Tracker(CtrlShiftP);
        Assert.False(t.KeyDown(P, 10_000));
    }

    [Fact]
    public void Consume_invalidates()
    {
        var t = Tracker(CtrlShiftP);
        t.KeyDown(LCtrl, 10_000);
        t.Reset();
        Assert.False(t.IsValid(10_000));
    }

    [Fact]
    public void Without_hotkey_nothing_is_grabbed()
    {
        var t = Tracker(null);
        Assert.False(t.KeyDown(LCtrl, 10_000));
        Assert.False(t.KeyDown(PrintScreen, 20_000));
    }

    [Fact]
    public void Changing_the_hotkey_drops_the_grab()
    {
        var t = Tracker(CtrlShiftP);
        t.KeyDown(LCtrl, 10_000);
        t.Hotkey = HotkeySetting.PrintScreen;
        Assert.False(t.IsValid(10_000));
    }

    [Fact]
    public void Stuck_key_from_a_missed_release_does_not_block_forever()
    {
        // Loslassen auf dem sicheren Desktop (UAC, Strg+Alt+Entf) sieht der Hook nicht
        var t = Tracker(CtrlShiftP);
        t.KeyDown(C, 10_000);
        Assert.True(t.KeyDown(LCtrl, 10_000 + PreCaptureTracker.StaleKeyMs + 1));
    }
}
