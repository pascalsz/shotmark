using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace ShotMark.App;

/// <summary>
/// Start: eine Instanz pro Profil. Weitere Starts melden sich bei der laufenden Instanz:
/// ohne Argument → Editor zeigen, <c>--capture</c> → Aufnahme starten, <c>--exit</c> → geordnet beenden.
/// <c>--profile &lt;Ordner&gt;</c> nutzt ein eigenes Einstellungsverzeichnis (z. B. um einen zweiten PC zu simulieren).
/// </summary>
public partial class App : Application
{
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private EventWaitHandle? _captureSignal;
    private EventWaitHandle? _exitSignal;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        System.Windows.Forms.Application.EnableVisualStyles();

        var customProfile = ArgumentValue(e.Args, "--profile");
        var profile = customProfile is not null
            ? Path.GetFullPath(customProfile)
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ShotMark");
        Directory.CreateDirectory(profile);
        if (customProfile is not null) Log.FilePath = Path.Combine(profile, "log.txt");

        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(profile.ToLowerInvariant())))[..16];
        _instanceMutex = new Mutex(true, $@"Local\ShotMark-{id}", out var isFirstInstance);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\ShotMark-Show-{id}");
        _captureSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\ShotMark-Capture-{id}");
        _exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\ShotMark-Exit-{id}");
        var wantsCapture = e.Args.Contains("--capture", StringComparer.OrdinalIgnoreCase);
        var wantsExit = e.Args.Contains("--exit", StringComparer.OrdinalIgnoreCase);

        if (!isFirstInstance)
        {
            // Laufende Instanz darf ihr Fenster/Overlay in den Vordergrund holen (sonst kein Tastaturfokus für Esc)
            Native.NativeMethods.AllowSetForegroundWindow(Native.NativeMethods.ASFW_ANY);
            (wantsExit ? _exitSignal : wantsCapture ? _captureSignal : _showSignal).Set();
            Shutdown();
            return;
        }
        if (wantsExit)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("Unbehandelter Fehler", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unbeobachteter Task-Fehler", args.Exception);
            args.SetObserved();
        };

        _host = new AppHost(profile);
        SessionEnding += (_, _) => _host?.SaveBlocking();
        if (!_host.Start())
        {
            Shutdown();
            return;
        }

        ListenForSignals();
        if (wantsCapture) _host.StartCapture();
        else if (e.Args.Contains("--open-editor", StringComparer.OrdinalIgnoreCase)) _host.OpenEditor();
    }

    private void ListenForSignals()
    {
        var thread = new Thread(() =>
        {
            var handles = new WaitHandle[] { _showSignal!, _captureSignal!, _exitSignal! };
            while (true)
            {
                var index = WaitHandle.WaitAny(handles);
                Dispatcher.BeginInvoke(() =>
                {
                    switch (index)
                    {
                        case 0: _host?.OpenEditor(); break;
                        case 1: _host?.StartCapture(); break;
                        default: _host?.Exit(); break;
                    }
                });
                if (index == 2) return;
            }
        })
        {
            IsBackground = true,
            Name = "ShotMark-Signale",
        };
        thread.Start();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error("Fehler in der Oberfläche", e.Exception);
        e.Handled = true;
        MessageBox.Show($"Es ist ein unerwarteter Fehler aufgetreten:\n{e.Exception.Message}\n\nDetails stehen in {Log.FilePath}.",
            "ShotMark", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
