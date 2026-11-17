using System.Diagnostics;
using System.IO;

namespace ShotMark.App.Updates;

/// <summary>
/// Frühere Versionen wurden per <c>Installieren.cmd</c> nach <c>%LOCALAPPDATA%\Programs\ShotMark\</c> kopiert.
/// Beim ersten Start der Velopack-Installation wird diese alte Kopie geordnet beendet und entfernt.
/// Den Autostart-Eintrag biegt die neue Version beim Start selbst um (gleicher Name „ShotMark“).
/// </summary>
internal static class LegacyInstall
{
    private static readonly TimeSpan ExitTimeout = TimeSpan.FromSeconds(20);

    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ShotMark");

    public static void Remove()
    {
        var exe = Path.Combine(Folder, "ShotMark.exe");
        if (!File.Exists(exe)) return;
        try
        {
            var running = FindRunning(exe);
            if (running.Count > 0)
            {
                // Die alte Version sichert beim geordneten Beenden offene Änderungen
                using (var request = Process.Start(new ProcessStartInfo(exe, "--exit") { UseShellExecute = false }))
                    request?.WaitForExit((int)ExitTimeout.TotalMilliseconds);
                foreach (var process in running)
                {
                    if (!process.WaitForExit((int)ExitTimeout.TotalMilliseconds)) process.Kill();
                    process.Dispose();
                }
            }
            Directory.Delete(Folder, recursive: true);
            Log.Info("Alte Installation entfernt: " + Folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Log.Error("Alte Installation konnte nicht entfernt werden: " + Folder, e);
        }
    }

    private static List<Process> FindRunning(string exe)
    {
        var result = new List<Process>();
        foreach (var process in Process.GetProcessesByName("ShotMark"))
        {
            try
            {
                if (string.Equals(process.MainModule?.FileName, exe, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(process);
                    continue;
                }
            }
            catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                // Prozess bereits beendet oder nicht zugreifbar
            }
            process.Dispose();
        }
        return result;
    }
}
