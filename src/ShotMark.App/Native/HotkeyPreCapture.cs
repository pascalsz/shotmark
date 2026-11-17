using System.Diagnostics;
using System.Runtime.InteropServices;
using ShotMark.Core.Capture;
using ShotMark.Core.Settings;
using static ShotMark.App.Native.NativeMethods;

namespace ShotMark.App.Native;

/// <summary>
/// Hält den Bildschirm fest, sobald die erste Taste des Aufnahme-Kürzels gedrückt wird – bevor das Programm unter der
/// Maus die Taste sieht und seinen Tooltip schließt. Löst das Kürzel danach aus, verwendet die Aufnahme dieses Bild.
/// Wann aufgenommen und wann verworfen wird, entscheidet <see cref="PreCaptureTracker"/>.
/// Der Low-Level-Hook läuft wie der Maushook der Scroll-Aufnahme in einem eigenen Thread mit Nachrichtenschleife.
/// Windows hält die Taste an, bis der Hook zurückkehrt; genau das garantiert, dass der Tooltip noch zu sehen ist.
/// </summary>
internal sealed class HotkeyPreCapture : IDisposable
{
    /// <summary>Maus seither bewegt: es kann ein anderer Tooltip offen sein, also lieber frisch aufnehmen.</summary>
    private const int MaxCursorDrift = 4;

    /// <summary>Langsamer als das: Windows könnte den Hook wegen Zeitüberschreitung still entfernen, daher neu setzen.</summary>
    private const long SlowGrabMs = 150;

    private const uint WM_REINSTALL = WM_APP + 1;
    private readonly object _gate = new();
    private readonly PreCaptureTracker _tracker = new();
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly Timer _expiry;
    private LowLevelKeyboardProc? _callback;
    private IntPtr _hook;
    private uint _threadId;
    private RawScreen? _raw;
    private POINT _cursorAtGrab;
    private volatile bool _paused;

    public HotkeyPreCapture()
    {
        _expiry = new Timer(_ => ReleaseIfExpired());
        _thread = new Thread(Run) { IsBackground = true, Name = "ShotMark-Tastaturhook" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(2));
    }

    /// <summary>Während einer laufenden Aufnahme nichts festhalten (Tasten werden trotzdem mitgezählt).</summary>
    public bool Paused
    {
        set
        {
            _paused = value;
            if (value) Discard();
        }
    }

    /// <summary>Registriertes Kürzel; null, solange keines aktiv ist (dann wird nie vorab aufgenommen).</summary>
    public void SetHotkey(HotkeySetting? hotkey)
    {
        lock (_gate)
        {
            _tracker.Hotkey = hotkey;
            Release();
        }
    }

    /// <summary>Für das gerade ausgelöste Kürzel festgehaltenes Bild; null, wenn keines gilt.</summary>
    public ScreenSnapshot? TryTake()
    {
        RawScreen? raw;
        lock (_gate)
        {
            raw = _raw;
            _raw = null;
            var valid = raw is not null && _tracker.IsValid(Environment.TickCount64) && !CursorMoved();
            _tracker.Reset();
            if (!valid)
            {
                raw?.Dispose();
                return null;
            }
        }
        using (raw)
            return raw!.ToSnapshot();
    }

    public void Discard()
    {
        lock (_gate)
        {
            _tracker.Reset();
            Release();
        }
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        _callback = OnKeyEvent; // Feld hält den Delegaten am Leben, solange der Hook aktiv ist
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) Log.Error($"Tastatur-Hook nicht gesetzt (Fehler {Marshal.GetLastWin32Error()}) – Tooltips werden evtl. nicht mit aufgenommen", null);
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE); // Nachrichtenschlange anlegen, bevor WM_QUIT kommen kann
        _ready.Set();
        while (GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            if (message.message != WM_REINSTALL) continue;
            if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
            _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _callback, GetModuleHandle(null), 0);
        }
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
    }

    private IntPtr OnKeyEvent(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0)
        {
            try
            {
                Handle(wParam.ToInt32(), Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam));
            }
            catch (Exception e)
            {
                // Nie aus dem Hook werfen: sonst hängt die Tastatur des Nutzers
                Log.Error("Vorab-Aufnahme fehlgeschlagen", e);
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void Handle(int message, KBDLLHOOKSTRUCT info)
    {
        var vk = (int)info.vkCode;
        var now = Environment.TickCount64;
        lock (_gate)
        {
            if (message is WM_KEYUP or WM_SYSKEYUP)
            {
                _tracker.KeyUp(vk, now);
                return;
            }
            if (message is not (WM_KEYDOWN or WM_SYSKEYDOWN)) return;

            if (!_tracker.KeyDown(vk, now))
            {
                if (_raw is not null && !_tracker.IsValid(now)) Release();
                return;
            }
            if (_paused)
            {
                _tracker.Reset();
                return;
            }

            var watch = Stopwatch.StartNew();
            Release();
            _raw = ScreenGrabber.Grab();
            GetCursorPos(out _cursorAtGrab);
            _expiry.Change(PreCaptureTracker.MaxAgeMs + 100, Timeout.Infinite);
            if (watch.ElapsedMilliseconds > SlowGrabMs)
            {
                Log.Info($"Vorab-Aufnahme dauerte {watch.ElapsedMilliseconds} ms – Tastatur-Hook wird neu gesetzt");
                PostThreadMessage(_threadId, WM_REINSTALL, IntPtr.Zero, IntPtr.Zero);
            }
        }
    }

    private bool CursorMoved()
    {
        GetCursorPos(out var now);
        return Math.Abs(now.X - _cursorAtGrab.X) > MaxCursorDrift || Math.Abs(now.Y - _cursorAtGrab.Y) > MaxCursorDrift;
    }

    private void ReleaseIfExpired()
    {
        lock (_gate)
            if (_raw is not null && !_tracker.IsValid(Environment.TickCount64)) Release();
    }

    /// <summary>Bild freigeben – bei mehreren 4K-Monitoren sind das schnell über 100 MB.</summary>
    private void Release()
    {
        _raw?.Dispose();
        _raw = null;
    }

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _expiry.Dispose();
        _ready.Dispose();
        lock (_gate)
            Release();
    }
}
