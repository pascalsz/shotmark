using Velopack;

namespace ShotMark.App.Updates;

/// <summary>
/// Installation über Velopack: <c>Setup.exe</c> installiert pro Benutzer nach
/// <c>%LOCALAPPDATA%\ShotMark.App\current\</c>, Updates kommen aus den GitHub-Releases.
/// </summary>
/// <remarks>
/// Die Paket-Id ist bewusst nicht „ShotMark“: Velopack löscht bei der Deinstallation den ganzen
/// Ordner <c>%LOCALAPPDATA%\&lt;Paket-Id&gt;</c>, und in <c>%LOCALAPPDATA%\ShotMark</c> liegen
/// Rettungskopien und Protokoll, die eine Deinstallation überstehen sollen.
/// </remarks>
internal static class Installation
{
    public const string PackId = "ShotMark.App";

    /// <summary>Statische Release-Adresse statt GitHub-API: kein Konto, kein Token, keine API-Limits.</summary>
    private const string DefaultFeed = "https://github.com/pascalsz/shotmark/releases/latest/download";

    /// <summary>Zum Testen lässt sich ein anderer Feed setzen, z. B. ein lokaler Ordner mit <c>vpk pack</c>-Ausgabe.</summary>
    public static string Feed =>
        Environment.GetEnvironmentVariable("SHOTMARK_UPDATE_FEED") is { Length: > 0 } custom ? custom : DefaultFeed;

    private static readonly Lazy<UpdateManager?> LazyManager = new(CreateManager);

    /// <summary>Null bei Entwicklungs-Builds, die nicht über Velopack installiert sind.</summary>
    public static UpdateManager? Manager => LazyManager.Value;

    public static bool IsInstalled => Manager is not null;

    public static string? Version => Manager?.CurrentVersion?.ToString();

    private static UpdateManager? CreateManager()
    {
        try
        {
            var manager = new UpdateManager(Feed);
            return manager.IsInstalled ? manager : null;
        }
        catch (Exception e)
        {
            Log.Error("Update-Verwaltung konnte nicht starten", e);
            return null;
        }
    }
}
