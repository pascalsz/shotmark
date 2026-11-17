using System.Text;
using System.Text.Json;
using ShotMark.Core.Storage;
namespace ShotMark.Core.Settings;

public sealed record HotkeySetting(int VirtualKey, bool Control, bool Shift, bool Alt, bool Windows)
{
    public static HotkeySetting PrintScreen { get; } = new(0x2C, false, false, false, false);

    /// <summary>Strg+Umschalt+L – Vorschlag für die Scroll-Aufnahme.</summary>
    public static HotkeySetting ScrollDefault { get; } = new(0x4C, true, true, false, false);
}

public sealed record AppSettings
{
    public string? PcName { get; init; }
    public HotkeySetting Hotkey { get; init; } = HotkeySetting.PrintScreen;

    /// <summary>Eigenes Kürzel für die Scroll-Aufnahme; <c>null</c> = aus (dann nur über das Tray-Menü).</summary>
    public HotkeySetting? ScrollHotkey { get; init; } = HotkeySetting.ScrollDefault;
    public string? SaveFolder { get; init; }
    public bool StartWithWindows { get; init; } = true;
    public bool CopyToClipboard { get; init; } = true;

    /// <summary>Automatisch bei GitHub nach Updates suchen (abschaltbar, siehe Datenschutzhinweis).</summary>
    public bool CheckForUpdates { get; init; } = true;

    /// <summary>Sprache der Oberfläche: <c>"de"</c>, <c>"en"</c> oder <c>null</c> = Windows-Sprache folgen.</summary>
    public string? Language { get; init; }
}

public sealed class SettingsStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public string FilePath { get; } = Path.Combine(directory, "settings.json");

    public AppSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options) ?? new AppSettings()
                : new AppSettings();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        AtomicFile.Write(FilePath, Encoding.UTF8.GetBytes(JsonSerializer.Serialize(settings, Options)));
    }
}
