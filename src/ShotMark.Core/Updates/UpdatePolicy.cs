namespace ShotMark.Core.Updates;

/// <summary>Was der Nutzer gerade tut – entscheidet, ob ShotMark für ein Update neu starten darf.</summary>
public readonly record struct UpdateActivity(
    bool Capturing,
    bool EditorOpen,
    bool UnsavedChanges,
    bool DialogOpen,
    TimeSpan UserIdle);

/// <summary>
/// Wann ShotMark nach Updates sucht und wann es ein heruntergeladenes Update anwendet.
/// Ein Neustart passiert nur, wenn er niemanden stört. Sonst wird das Update beim Beenden
/// oder beim nächsten Start angewendet.
/// </summary>
public static class UpdatePolicy
{
    /// <summary>Beim Windows-Login steht das Netz oft noch nicht, deshalb erst nach kurzer Pause prüfen.</summary>
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);

    /// <summary>ShotMark läuft oft tagelang im Infobereich, deshalb regelmäßig erneut prüfen.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    /// <summary>So lange muss der Nutzer weder Maus noch Tastatur benutzt haben.</summary>
    public static readonly TimeSpan RequiredIdle = TimeSpan.FromMinutes(5);

    /// <summary>Wie oft ein bereitliegendes Update auf einen passenden Moment wartet.</summary>
    public static readonly TimeSpan RestartPollInterval = TimeSpan.FromMinutes(1);

    public static bool CanRestartNow(UpdateActivity activity) =>
        !activity.Capturing
        && !activity.EditorOpen
        && !activity.UnsavedChanges
        && !activity.DialogOpen
        && activity.UserIdle >= RequiredIdle;
}
