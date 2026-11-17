using ShotMark.App.Localization;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using ShotMark.App.Settings;
using ShotMark.Core.Settings;
using static ShotMark.App.Native.NativeMethods;

namespace ShotMark.App.Native;

/// <summary>Globales Tastenkürzel über ein unsichtbares Nachrichtenfenster.</summary>
internal sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0x5348;
    private readonly HwndSource _source;
    private bool _registered;

    public event Action? Pressed;

    public HotkeyManager()
    {
        _source = new HwndSource(new HwndSourceParameters("ShotMarkHotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = 0,
            ParentWindow = HWND_MESSAGE,
        });
        _source.AddHook(WndProc);
    }

    public bool TryRegister(HotkeySetting setting, out string? error)
    {
        Unregister();
        var modifiers = MOD_NOREPEAT
                        | (setting.Control ? MOD_CONTROL : 0)
                        | (setting.Shift ? MOD_SHIFT : 0)
                        | (setting.Alt ? MOD_ALT : 0)
                        | (setting.Windows ? MOD_WIN : 0);
        if (RegisterHotKey(_source.Handle, HotkeyId, modifiers, (uint)setting.VirtualKey))
        {
            _registered = true;
            error = null;
            return true;
        }

        var code = Marshal.GetLastWin32Error();
        var name = HotkeyText.Format(setting);
        error = code == ERROR_HOTKEY_ALREADY_REGISTERED
            ? Loc.Format(Strings.Hotkey_Taken, name)
            : Loc.Format(Strings.Hotkey_Failed, name, code);
        return false;
    }

    public void Unregister()
    {
        if (!_registered) return;
        UnregisterHotKey(_source.Handle, HotkeyId);
        _registered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
