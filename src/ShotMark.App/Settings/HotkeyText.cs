using System.Windows.Input;
using ShotMark.Core.Settings;

namespace ShotMark.App.Settings;

internal static class HotkeyText
{
    public static string Format(HotkeySetting hotkey)
    {
        var parts = new List<string>();
        if (hotkey.Control) parts.Add("Strg");
        if (hotkey.Shift) parts.Add("Umschalt");
        if (hotkey.Alt) parts.Add("Alt");
        if (hotkey.Windows) parts.Add("Win");
        parts.Add(KeyName(KeyInterop.KeyFromVirtualKey(hotkey.VirtualKey)));
        return string.Join("+", parts);
    }

    public static string KeyName(Key key) => key switch
    {
        Key.Snapshot => "Druck",
        Key.Space => "Leertaste",
        Key.Insert => "Einfg",
        Key.Delete => "Entf",
        Key.Home => "Pos1",
        Key.End => "Ende",
        Key.PageUp => "Bild auf",
        Key.PageDown => "Bild ab",
        Key.Pause => "Pause",
        Key.Scroll => "Rollen",
        >= Key.D0 and <= Key.D9 => ((char)('0' + (key - Key.D0))).ToString(),
        >= Key.NumPad0 and <= Key.NumPad9 => "Num " + (key - Key.NumPad0),
        _ => key.ToString(),
    };

    public static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
        or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin or Key.System;
}
