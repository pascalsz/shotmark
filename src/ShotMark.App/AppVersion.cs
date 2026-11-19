using System.Reflection;

namespace ShotMark.App;

/// <summary>
/// Versionsnummer für die Anzeige. Releases bekommen sie aus dem Tag (<c>publish.ps1 -Version</c>),
/// lokale Builds heißen <c>0.0.0-dev</c> (siehe ShotMark.App.csproj).
/// </summary>
internal static class AppVersion
{
    public static string Current { get; } = FromInformational(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>„1.2.0+abc123“ → „1.2.0“; Build-Metadaten interessieren beim Anzeigen niemanden.</summary>
    internal static string FromInformational(string? informational) =>
        string.IsNullOrEmpty(informational) ? "?" : informational.Split('+')[0];
}
