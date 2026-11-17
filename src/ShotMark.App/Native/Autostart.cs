using Microsoft.Win32;
using ShotMark.App.Updates;

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
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Bei der Deinstallation: Eintrag entfernen, damit Windows keine fehlende Datei starten will.</summary>
    public static void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
