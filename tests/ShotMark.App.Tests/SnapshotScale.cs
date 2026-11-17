using System.Globalization;

namespace ShotMark.App.Tests;

/// <summary>
/// Faktor für schärfere Snapshots (z. B. 2 für die Webseite) aus <c>SHOTMARK_SNAPSHOT_SCALE</c>.
/// Ungültige Werte ergeben 1, sonst wird auf 1–4 begrenzt.
/// </summary>
internal static class SnapshotScale
{
    public static double Parse(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale) && double.IsFinite(scale)
            ? Math.Clamp(scale, 1, 4)
            : 1;

    public static double FromEnvironment() => Parse(Environment.GetEnvironmentVariable("SHOTMARK_SNAPSHOT_SCALE"));
}
