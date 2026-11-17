using System.Windows.Threading;
using ShotMark.Core.Updates;
using Velopack;

namespace ShotMark.App.Updates;

/// <summary>
/// Sucht im Hintergrund nach Updates, lädt sie still herunter und wendet sie an, sobald es niemanden stört:
/// im Leerlauf ohne offene Fenster sofort (mit Neustart), sonst beim Beenden oder beim nächsten Start.
/// Fehler (kein Netz, GitHub nicht erreichbar) werden nur protokolliert.
/// </summary>
internal sealed class AppUpdater : IDisposable
{
    private readonly UpdateManager _manager;
    private readonly Func<UpdateActivity> _activity;
    private readonly Func<Task<bool>> _prepareRestart;
    private readonly Func<bool> _enabled;
    private readonly DispatcherTimer _checkTimer;
    private readonly DispatcherTimer _restartTimer;
    private bool _checking;
    private bool _restarting;

    /// <param name="enabled">Einstellung „Automatisch nach Updates suchen“; bei jedem Durchlauf neu gelesen.</param>
    public AppUpdater(UpdateManager manager, Func<UpdateActivity> activity, Func<Task<bool>> prepareRestart, Func<bool> enabled)
    {
        _enabled = enabled;
        _manager = manager;
        _activity = activity;
        _prepareRestart = prepareRestart;
        _checkTimer = new DispatcherTimer { Interval = UpdatePolicy.FirstCheckDelay };
        _checkTimer.Tick += async (_, _) =>
        {
            _checkTimer.Interval = UpdatePolicy.CheckInterval;
            await CheckAsync();
        };
        _restartTimer = new DispatcherTimer { Interval = UpdatePolicy.RestartPollInterval };
        _restartTimer.Tick += async (_, _) => await TryRestartAsync();
    }

    public void Start()
    {
        _checkTimer.Start();
        if (_manager.UpdatePendingRestart is not null) _restartTimer.Start();
    }

    private async Task CheckAsync()
    {
        if (_checking || _restarting || !_enabled()) return;
        _checking = true;
        try
        {
            var update = await _manager.CheckForUpdatesAsync();
            if (update is null) return;
            if (_manager.UpdatePendingRestart?.Version == update.TargetFullRelease.Version)
            {
                _restartTimer.Start();
                return;
            }
            Log.Info($"Update {update.TargetFullRelease.Version} gefunden, wird heruntergeladen");
            await _manager.DownloadUpdatesAsync(update);
            Log.Info($"Update {update.TargetFullRelease.Version} bereit");
            _restartTimer.Start();
        }
        catch (Exception e)
        {
            Log.Error("Update-Prüfung fehlgeschlagen", e);
        }
        finally
        {
            _checking = false;
        }
    }

    private async Task TryRestartAsync()
    {
        if (_restarting || _checking) return;
        var pending = _manager.UpdatePendingRestart;
        if (pending is null)
        {
            _restartTimer.Stop();
            return;
        }
        if (!UpdatePolicy.CanRestartNow(_activity())) return;

        _restarting = true;
        _restartTimer.Stop();
        _checkTimer.Stop();
        if (!await _prepareRestart())
        {
            _restarting = false;
            _restartTimer.Start();
            _checkTimer.Start();
            return;
        }
        Log.Info($"Neustart für Update {pending.Version}");
        try
        {
            // Beendet den Prozess sofort, Velopack installiert und startet ShotMark neu
            _manager.ApplyUpdatesAndRestart(pending);
        }
        catch (Exception e)
        {
            Log.Error("Update konnte nicht angewendet werden", e);
            System.Windows.Application.Current.Shutdown();
        }
    }

    /// <summary>Beim normalen Beenden: ein bereitliegendes Update still installieren, ohne neu zu starten.</summary>
    public void ApplyPendingOnExit()
    {
        if (_restarting) return;
        try
        {
            if (_manager.UpdatePendingRestart is { } pending)
            {
                Log.Info($"Update {pending.Version} wird nach dem Beenden installiert");
                _manager.WaitExitThenApplyUpdates(pending, silent: true, restart: false);
            }
        }
        catch (Exception e)
        {
            Log.Error("Update beim Beenden fehlgeschlagen", e);
        }
    }

    public void Dispose()
    {
        _checkTimer.Stop();
        _restartTimer.Stop();
    }
}
