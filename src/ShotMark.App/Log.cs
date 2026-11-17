using System.IO;

namespace ShotMark.App;

/// <summary>Einfaches Fehlerprotokoll pro PC, hilfreich bei Problemen auf anderen Rechnern.</summary>
internal static class Log
{
    private const long MaxBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static string FilePath { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShotMark", "log.txt");

    public static void Info(string message) => Write("INFO", message, null);

    public static void Error(string message, Exception? error) => Write("FEHLER", message, error);

    private static void Write(string level, string message, Exception? error)
    {
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".old", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{(error is null ? "" : Environment.NewLine + error)}{Environment.NewLine}");
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
