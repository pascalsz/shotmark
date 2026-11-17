using System.IO;
using Microsoft.Win32;

namespace ShotMark.App.Native;

internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ShotMark";

    public static string InstallFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "ShotMark");

    /// <summary>Nur die installierte Kopie trägt sich ein – Entwicklungs-Builds verändern den Autostart nicht.</summary>
    public static bool IsInstalledCopy(string exePath) =>
        string.Equals(Path.GetDirectoryName(Path.GetFullPath(exePath)), InstallFolder, StringComparison.OrdinalIgnoreCase);

    public static void Apply(bool enabled, string exePath)
    {
        if (!IsInstalledCopy(exePath)) return;
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{exePath}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
