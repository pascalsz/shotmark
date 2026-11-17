using System.Text.Json;
namespace ShotMark.Core.Storage;

public static class DropboxLocator
{
    public static IEnumerable<string> DefaultInfoJsonPaths() =>
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Dropbox", "info.json"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dropbox", "info.json"),
    ];

    public static string? FindRoot(IEnumerable<string> infoJsonPaths)
    {
        foreach (var file in infoJsonPaths)
        {
            try
            {
                if (!File.Exists(file)) continue;
                using var json = JsonDocument.Parse(File.ReadAllText(file));
                foreach (var account in new[] { "personal", "business" })
                {
                    if (json.RootElement.TryGetProperty(account, out var entry)
                        && entry.TryGetProperty("path", out var pathElement)
                        && pathElement.GetString() is { Length: > 0 } path
                        && Directory.Exists(path))
                        return path;
                }
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
            {
            }
        }
        return null;
    }
}

public static class SaveFolderResolver
{
    public sealed record Result(string Folder, bool IsFallback);

    public static Result Resolve(string? configuredFolder, string? dropboxRoot, string picturesFolder)
    {
        if (!string.IsNullOrWhiteSpace(configuredFolder)) return new Result(configuredFolder, false);
        if (dropboxRoot is not null) return new Result(Path.Combine(dropboxRoot, "Screenshots"), false);
        return new Result(Path.Combine(picturesFolder, "ShotMark"), true);
    }
}
