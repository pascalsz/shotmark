using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using ShotMark.App.Localization;
using ShotMark.App.Updates;
using ShotMark.Core.Settings;
using Velopack;

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
    private AppUpdater? _updater;
    private bool _sessionEnding;
    private static bool _justUpdated;

    /// <summary>
    /// Velopack läuft vor WPF und führt die Installations-Hooks aus (diese beenden den Prozess danach selbst).
    /// Velopacks eigenes Anwenden beim Start ist aus: Es würde bei jedem weiteren Aufruf (<c>--capture</c>,
    /// Startmenü) eine laufende Instanz hart beenden, ohne dass sie offene Änderungen sichern kann.
    /// Bereitliegende Updates wendet <see cref="OnStartup"/> nur in der ersten Instanz an.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build()
            .OnFirstRun(_ =>
            {
                if (Installation.IsInstalled && Installation.Manager!.AppId == Installation.PackId) LegacyInstall.Remove();
            })
            .OnRestarted(_ => _justUpdated = true)
            .OnBeforeUninstallFastCallback(_ => Native.Autostart.Remove())
            .SetAutoApplyOnStartup(false)
            .Run();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

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
        // Vor dem ersten Fenster, damit schon der Erststart-Dialog in der richtigen Sprache erscheint
        Language.Apply(new SettingsStore(profile).Load().Language);

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
        if (ApplyPendingUpdate(e.Args)) return;

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Error("Unbehandelter Fehler", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error("Unbeobachteter Task-Fehler", args.Exception);
            args.SetObserved();
        };

        _host = new AppHost(profile, manageAutostart: customProfile is null);
        SessionEnding += (_, _) =>
        {
            _sessionEnding = true;
            _host?.SaveBlocking();
        };
        if (!_host.Start())
        {
            Shutdown();
            return;
        }

        ListenForSignals();
        StartUpdater();
        if (wantsCapture) _host.StartCapture();
        else if (e.Args.Contains("--open-editor", StringComparer.OrdinalIgnoreCase)) _host.OpenEditor();
    }

    /// <summary>
    /// Ein beim letzten Lauf heruntergeladenes Update vor allem anderen anwenden (der Normalfall nach dem Login).
    /// Es läuft noch nichts, also geht nichts verloren. Velopack startet ShotMark danach mit denselben Argumenten neu.
    /// </summary>
    private bool ApplyPendingUpdate(string[] args)
    {
        if (Installation.Manager?.UpdatePendingRestart is not { } pending) return false;
        try
        {
            Log.Info($"Wende bereitliegendes Update {pending.Version} an");
            // Beendet den Prozess (Mutex wird dabei frei), Velopack installiert und startet neu
            Installation.Manager.ApplyUpdatesAndRestart(pending, args);
            return true;
        }
        catch (Exception error)
        {
            // Lieber mit der alten Version weiterarbeiten als gar nicht starten
            Log.Error("Bereitliegendes Update konnte nicht angewendet werden", error);
            return false;
        }
    }

    private void StartUpdater()
    {
        if (Installation.Manager is not { } manager) return;
        if (_justUpdated) _host!.Notify(string.Format(CultureInfo.CurrentCulture, Strings.UpdatedNotice, Installation.Version));
        var host = _host!;
        _updater = new AppUpdater(manager, host.CurrentActivity, host.ShutdownAsync);
        _updater.Start();
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
        MessageBox.Show(ErrorText.WithDetails(ErrorText.Format(Strings.UnexpectedError, Log.FilePath), e.Exception.Message),
            "ShotMark", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updater?.Dispose();
        // Beim Abmelden/Herunterfahren keinen Updater mehr starten – das Update folgt beim nächsten Start
        if (!_sessionEnding) _updater?.ApplyPendingOnExit();
        _host?.Dispose();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}
