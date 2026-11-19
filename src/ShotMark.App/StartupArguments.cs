namespace ShotMark.App;

internal enum StartupAction
{
    /// <summary>Editorfenster öffnen – wer ShotMark selbst startet (Startmenü, Verknüpfung), will es sehen.</summary>
    OpenEditor,
    /// <summary>Aufnahme-Overlay starten (<c>--capture</c>, z. B. aus einer eigenen Verknüpfung).</summary>
    Capture,
    /// <summary>Still im Tray warten (<c>--background</c>: Autostart, Neustart nach einem Update).</summary>
    Background,
}

/// <summary>Was ein Start ohne laufende Instanz tun soll.</summary>
internal static class StartupArguments
{
    public const string Background = "--background";

    public static StartupAction Parse(string[] args) =>
        Has(args, "--capture") ? StartupAction.Capture
        : Has(args, Background) ? StartupAction.Background
        : StartupAction.OpenEditor;

    private static bool Has(string[] args, string option) => args.Contains(option, StringComparer.OrdinalIgnoreCase);
}
