using System.Windows.Input;
using ShotMark.App.Localization;
using ShotMark.Core.Settings;

namespace ShotMark.App.Settings;

internal static class HotkeyText
{
    public static string Format(HotkeySetting hotkey)
    {
        var parts = new List<string>();
        if (hotkey.Control) parts.Add(Strings.Key_Ctrl);
        if (hotkey.Shift) parts.Add(Strings.Key_Shift);
        if (hotkey.Alt) parts.Add(Strings.Key_Alt);
        if (hotkey.Windows) parts.Add(Strings.Key_Win);
        parts.Add(KeyName(KeyInterop.KeyFromVirtualKey(hotkey.VirtualKey)));
        return string.Join("+", parts);
    }

    public static string KeyName(Key key) => key switch
    {
        Key.Snapshot => Strings.Key_PrintScreen,
        Key.Space => Strings.Key_Space,
        Key.Insert => Strings.Key_Insert,
        Key.Delete => Strings.Key_Delete,
        Key.Home => Strings.Key_Home,
        Key.End => Strings.Key_End,
        Key.PageUp => Strings.Key_PageUp,
        Key.PageDown => Strings.Key_PageDown,
        Key.Pause => Strings.Key_Pause,
        Key.Scroll => Strings.Key_ScrollLock,
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => Loc.Format(Strings.Key_NumPad, key - Key.NumPad0),
        _ => key.ToString(),
    };

    public static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;
}
