using System.Runtime.InteropServices;
using System.Windows;
using ShotMark.Core.Scrolling;
using static ShotMark.App.Native.NativeMethods;

namespace ShotMark.App.Native;

/// <summary>Nimmt einen Bildschirmbereich (physische Pixel) per GDI als BGRA-Pixel auf – threadunabhängig, ohne WPF.</summary>
internal sealed class RegionFrameSource(Int32Rect region) : IFrameSource
{
    public PixelFrame Capture()
    {
        var width = region.Width;
        var height = region.Height;
        var screenDc = GetDC(IntPtr.Zero);
        var memoryDc = CreateCompatibleDC(screenDc);
        var bitmap = CreateCompatibleBitmap(screenDc, width, height);
        var previous = SelectObject(memoryDc, bitmap);
        try
        {
            // Ohne CAPTUREBLT: kein Flackern des Mauszeigers; eigene Fenster sind zusätzlich vom Foto ausgeschlossen
            BitBlt(memoryDc, 0, 0, width, height, screenDc, region.X, region.Y, SRCCOPY);
            SelectObject(memoryDc, previous);
            var info = new BITMAPINFOHEADER
            {
                biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                biWidth = width,
                biHeight = -height, // von oben nach unten
                biPlanes = 1,
                biBitCount = 32,
            };
            var pixels = new byte[width * height * 4];
            GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref info, DIB_RGB_COLORS);
            for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            return new PixelFrame(width, height, pixels);
        }
        finally
        {
            DeleteObject(bitmap);
            DeleteDC(memoryDc);
            ReleaseDC(IntPtr.Zero, screenDc);
        }
    }
}

/// <summary>
/// Scrollt mit simuliertem Mausrad. Der Zeiger steht dafür am rechten Rand des Bereichs (meist über dem
/// Scrollbalken, wo Webseiten keine Hover-Effekte zeigen) und wird am Ende zurückgesetzt.
/// </summary>
internal sealed class WindowsScroller : IScroller, IDisposable
{
    private const int WheelDelta = 120;
    private readonly POINT _original;

    public WindowsScroller(Int32Rect region)
    {
        GetCursorPos(out _original);
        var x = region.Width > 60 ? region.X + region.Width - 12 : region.X + region.Width / 2;
        SetCursorPos(x, region.Y + region.Height / 2);
    }

    /// <summary>Hat der Nutzer übernommen, bleibt der Zeiger, wo er ihn hingeschoben hat.</summary>
    public bool RestoreCursor { get; set; } = true;

    public void ScrollDown(int notches) => Wheel(notches, -WheelDelta);

    public void ScrollUp(int notches) => Wheel(notches, WheelDelta);

    // Einzelne Rasten statt eines großen Deltas – manche Programme werten nur das Vorzeichen aus
    private static void Wheel(int notches, int delta)
    {
        if (notches <= 0) return;
        var inputs = new INPUT[notches];
        for (var i = 0; i < notches; i++)
        {
            inputs[i] = new INPUT
            {
                type = INPUT_MOUSE,
                mi = new MOUSEINPUT { mouseData = unchecked((uint)delta), dwFlags = MOUSEEVENTF_WHEEL },
            };
        }
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    public void Dispose()
    {
        if (RestoreCursor) SetCursorPos(_original.X, _original.Y);
    }
}

/// <summary>
/// Erkennt, wenn der Nutzer während der Scroll-Aufnahme selbst Maus oder Mausrad benutzt (Low-Level-Hook).
/// Simulierte Eingaben (LLMHF_INJECTED) zählen nicht; kleine Wackler unter 8 px auch nicht.
/// Der Hook läuft in einem eigenen Thread mit Nachrichtenschleife: hängt die Oberfläche kurz, entfernt Windows
/// den Hook nicht wegen Zeitüberschreitung, und die Maus des Nutzers ruckelt nicht.
/// </summary>
internal sealed class UserActivityMonitor : IUserActivity, IDisposable
{
    private const int MoveThreshold = 8;
    private readonly Thread _thread;
    private readonly ManualResetEventSlim _ready = new();
    private readonly long _armedAt = Environment.TickCount64 + 300;
    private LowLevelMouseProc? _callback;
    private IntPtr _hook;
    private uint _threadId;
    private POINT? _origin;
    private volatile bool _tookOver;

    public UserActivityMonitor()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "ShotMark-Maushook" };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(2));
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        _callback = OnMouseEvent; // Feld hält den Delegaten am Leben, solange der Hook aktiv ist
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _callback, GetModuleHandle(null), 0);
        PeekMessage(out _, IntPtr.Zero, 0, 0, PM_NOREMOVE); // Nachrichtenschlange anlegen, bevor WM_QUIT kommen kann
        _ready.Set();
        while (GetMessage(out _, IntPtr.Zero, 0, 0) > 0)
        {
        }
        if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook);
    }

    public bool UserTookOver => _tookOver;

    private IntPtr OnMouseEvent(int code, IntPtr wParam, IntPtr lParam)
    {
        if (code >= 0 && !_tookOver && Environment.TickCount64 >= _armedAt)
        {
            var info = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            if ((info.flags & LLMHF_INJECTED) == 0)
            {
                var message = wParam.ToInt32();
                if (message == WM_MOUSEWHEEL || message == WM_LBUTTONDOWN || message == WM_RBUTTONDOWN)
                {
                    _tookOver = true;
                }
                else if (message == WM_MOUSEMOVE)
                {
                    _origin ??= info.pt;
                    var dx = info.pt.X - _origin.Value.X;
                    var dy = info.pt.Y - _origin.Value.Y;
                    if (dx * dx + dy * dy > MoveThreshold * MoveThreshold) _tookOver = true;
                }
            }
        }
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (_threadId != 0) PostThreadMessage(_threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
        _thread.Join(TimeSpan.FromSeconds(2));
        _ready.Dispose();
    }
}
