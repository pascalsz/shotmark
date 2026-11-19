using System.IO;
using Microsoft.Win32;
using ShotMark.App.Updates;
using Velopack.Locators;

namespace ShotMark.App.Native;

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ShotMark";

    /// <summary>Nur die über Setup.exe installierte Kopie trägt sich ein – Entwicklungs-Builds verändern den Autostart nicht.</summary>
    public static bool IsInstalledCopy => Installation.IsInstalled;

    public static void Apply(bool enabled, string exePath)
    {
        if (!IsInstalledCopy) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, RunCommand(LaunchTarget(exePath, InstallRoot(exePath))));
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Nach der Anmeldung still im Tray starten; der Velopack-Starter reicht Argumente an die App weiter.</summary>
    internal static string RunCommand(string target) => $"\"{target}\" {StartupArguments.Background}";

    /// <summary>Stammordner der Velopack-Installation; ohne Locator der Ordner über <c>current\</c>.</summary>
    internal static string? InstallRoot(string exePath)
    {
        if (VelopackLocator.IsCurrentSet && VelopackLocator.Current.RootAppDir is { Length: > 0 } root) return root;
        var folder = Path.GetDirectoryName(exePath);
        return string.Equals(Path.GetFileName(folder), "current", StringComparison.OrdinalIgnoreCase)
            ? Path.GetDirectoryName(folder)
            : null;
    }

    /// <summary>
    /// Velopack legt im Stammordner einen Starter an, der immer die aktuelle Version in <c>current\</c> startet –
    /// auch wenn Windows mitten in einem Update hochfährt. Fehlt er, bleibt es beim laufenden Programm.
    /// </summary>
    internal static string LaunchTarget(string exePath, string? installRoot)
    {
        if (installRoot is null) return exePath;
        var launcher = Path.Combine(installRoot, Path.GetFileName(exePath));
        return File.Exists(launcher) ? launcher : exePath;
    }

    /// <summary>Bei der Deinstallation: Eintrag entfernen, damit Windows keine fehlende Datei starten will.</summary>
    public static void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
