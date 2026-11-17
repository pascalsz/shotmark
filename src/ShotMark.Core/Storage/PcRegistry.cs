using System.Text.Json;
namespace ShotMark.Core.Storage;

public sealed record PcInfo(string Slug, string DisplayName, string MachineName);

public static class PcRegistry
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public static string FolderFor(string captureFolder) => Path.Combine(captureFolder, CaptureStore.ProjectFolderName, "pcs");

    public static void Register(string captureFolder, PcInfo info)
    {
        CaptureStore.EnsureProjectFolder(captureFolder);
        var folder = Directory.CreateDirectory(FolderFor(captureFolder)).FullName;
        var path = Path.Combine(folder, info.Slug + ".json");
        var json = JsonSerializer.SerializeToUtf8Bytes(info, Options);
        if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(json)) return;
        AtomicFile.Write(path, json);
    }

    public static IReadOnlyDictionary<string, PcInfo> LoadAll(string captureFolder)
    {
        var result = new Dictionary<string, PcInfo>(StringComparer.OrdinalIgnoreCase);
        var folder = FolderFor(captureFolder);
        if (!Directory.Exists(folder)) return result;
        foreach (var file in Directory.EnumerateFiles(folder, "*.json"))
        {
            try
            {
                if (JsonSerializer.Deserialize<PcInfo>(File.ReadAllText(file), Options) is { Slug.Length: > 0 } info)
                    result[info.Slug] = info;
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            {
            }
        }
        return result;
    }

    public static PcInfo? FindConflict(string captureFolder, string slug, string machineName) =>
        LoadAll(captureFolder).TryGetValue(slug, out var existing)
        && !string.Equals(existing.MachineName, machineName, StringComparison.OrdinalIgnoreCase)
            ? existing
            : null;
}
