using System.Text.Json;
using ShotMark.Core.Model;
namespace ShotMark.Core.Storage;

public sealed record CapturePaths(string PngPath, string ProjectPath)
{
    public string BaseName => Path.GetFileNameWithoutExtension(PngPath);

    public static CapturePaths ForPng(string pngPath)
    {
        var folder = Path.GetDirectoryName(pngPath) ?? ".";
        return new CapturePaths(pngPath, Path.Combine(folder, CaptureStore.ProjectFolderName,
            Path.GetFileNameWithoutExtension(pngPath) + CaptureStore.ProjectExtension));
    }
}

public abstract record SaveOutcome
{
    public sealed record Saved(CaptureDocument Document) : SaveOutcome;
    public sealed record Conflict(string ModifiedOn, DateTimeOffset ModifiedAt, int DiskRevision) : SaveOutcome;
    public sealed record Failed(string Message) : SaveOutcome;
}

public sealed record LoadedCapture(CapturePaths Paths, CaptureDocument? Document, byte[] ImagePng, string? Warning);

public sealed class CaptureStore(string folder, string pcSlug)
{
    public const string ProjectFolderName = ".shotmark";
    public const string ProjectExtension = ".shotmark";

    private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);

    public string Folder { get; } = folder;
    public string PcSlug { get; } = pcSlug;

    public CapturePaths CreateNew(DateTime localTimestamp)
    {
        var timestamp = new DateTime(localTimestamp.Year, localTimestamp.Month, localTimestamp.Day,
            localTimestamp.Hour, localTimestamp.Minute, localTimestamp.Second);
        lock (_reserved)
        {
            for (var sequence = 1; ; sequence++)
            {
                var name = new CaptureName(timestamp, PcSlug, sequence).BaseName;
                var paths = new CapturePaths(Path.Combine(Folder, name + ".png"),
                    Path.Combine(Folder, ProjectFolderName, name + ProjectExtension));
                if (_reserved.Contains(paths.PngPath) || File.Exists(paths.PngPath) || File.Exists(paths.ProjectPath)) continue;
                _reserved.Add(paths.PngPath);
                return paths;
            }
        }
    }

    public const string NewerVersionMessage =
        "Diese Aufnahme wurde mit einer neueren ShotMark-Version gespeichert. Bitte ShotMark auf diesem PC aktualisieren.";

    public SaveOutcome Save(CapturePaths paths, CaptureDocument document, byte[] originalPng, byte[] flattenedPng, string pcName, DateTimeOffset now)
    {
        try
        {
            // Nie eine Projektdatei überschreiben, die wir nicht sicher verstanden haben
            switch (ReadDiskState(paths.ProjectPath))
            {
                case DiskState.Unavailable unavailable:
                    return new SaveOutcome.Failed(unavailable.Message);
                case DiskState.Newer:
                    return new SaveOutcome.Failed(NewerVersionMessage);
                case DiskState.Present { Document: var onDisk } when onDisk.Revision != document.Revision || onDisk.SaveId != document.SaveId:
                    return new SaveOutcome.Conflict(onDisk.ModifiedOn, onDisk.ModifiedAt, onDisk.Revision);
                case DiskState.Corrupt:
                    File.Copy(paths.ProjectPath, $"{paths.ProjectPath}.defekt-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
                    break;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(paths.PngPath)!);
            EnsureProjectFolder(Path.GetDirectoryName(paths.PngPath)!);
            var saved = document with
            {
                Revision = document.Revision + 1,
                SaveId = Guid.NewGuid(),
                ModifiedAt = now,
                ModifiedOn = pcName,
            };
            AtomicFile.Write(paths.PngPath, flattenedPng);
            AtomicFile.Write(paths.ProjectPath, ProjectFile.Write(saved, originalPng));
            return new SaveOutcome.Saved(saved);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new SaveOutcome.Failed(e.Message);
        }
    }

    public static LoadedCapture Load(string pngPath)
    {
        var paths = CapturePaths.ForPng(pngPath);
        string? warning = null;
        if (File.Exists(paths.ProjectPath))
        {
            try
            {
                var (document, original) = ProjectFile.Read(paths.ProjectPath);
                return new LoadedCapture(paths, document, original, null);
            }
            catch (NotSupportedException)
            {
                warning = NewerVersionMessage + " Bis dahin wird nur das fertige Bild angezeigt.";
            }
            catch (Exception e) when (e is IOException or InvalidDataException or JsonException)
            {
                warning = "Projektdatei konnte nicht gelesen werden – Markierungen sind eingebrannt und nicht mehr einzeln bearbeitbar.";
            }
        }
        return new LoadedCapture(paths, null, ReadAllBytesShared(pngPath), warning);
    }

    public static void EnsureProjectFolder(string captureFolder)
    {
        var info = Directory.CreateDirectory(Path.Combine(captureFolder, ProjectFolderName));
        if (!info.Attributes.HasFlag(FileAttributes.Hidden))
            info.Attributes |= FileAttributes.Hidden;
    }

    private abstract record DiskState
    {
        public sealed record Missing : DiskState;
        public sealed record Present(CaptureDocument Document) : DiskState;
        public sealed record Newer : DiskState;
        public sealed record Corrupt : DiskState;
        public sealed record Unavailable(string Message) : DiskState;
    }

    private static DiskState ReadDiskState(string projectPath)
    {
        if (!File.Exists(projectPath)) return new DiskState.Missing();
        try { return new DiskState.Present(ProjectFile.ReadDocument(projectPath)); }
        catch (NotSupportedException) { return new DiskState.Newer(); }
        catch (Exception e) when (e is InvalidDataException or JsonException) { return new DiskState.Corrupt(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return new DiskState.Unavailable(e.Message); }
    }

    private static byte[] ReadAllBytesShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
