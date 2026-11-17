using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
using ShotMark.Core.Storage;
namespace ShotMark.Core.Tests.Storage;

public class CaptureStoreTests
{
    private static readonly DateTime T = new(2026, 10, 19, 15, 30, 12);
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 15, 31, 0, TimeSpan.FromHours(2));
    private static readonly byte[] Original = [1, 2, 3];
    private static readonly byte[] Flat = [4, 5, 6];

    private static CaptureDocument Doc() =>
        CaptureDocument.New(Now, new CapturedOn("Büro", "PC1"), new PixelSize(10, 10));

    [Fact]
    public void CreateNew_adds_suffix_when_name_taken()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var first = store.CreateNew(T);
        File.WriteAllBytes(first.PngPath, [0]);
        var second = store.CreateNew(T);
        Assert.EndsWith("2026-10-19_15-30-12_Buero.png", first.PngPath);
        Assert.EndsWith("2026-10-19_15-30-12_Buero_2.png", second.PngPath);
        Assert.Equal(Path.Combine(dir.Path, ".shotmark", "2026-10-19_15-30-12_Buero_2.shotmark"), second.ProjectPath);
    }

    [Fact]
    public void CreateNew_never_hands_out_the_same_name_twice()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        Assert.NotEqual(store.CreateNew(T).PngPath, store.CreateNew(T).PngPath);
    }

    [Fact]
    public void Save_writes_png_and_hidden_project_and_bumps_revision()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        var outcome = Assert.IsType<SaveOutcome.Saved>(store.Save(paths, Doc(), Original, Flat, "Büro", Now));
        Assert.Equal(1, outcome.Document.Revision);
        Assert.Equal("Büro", outcome.Document.ModifiedOn);
        Assert.Equal(Flat, File.ReadAllBytes(paths.PngPath));
        Assert.True(new DirectoryInfo(Path.Combine(dir.Path, ".shotmark")).Attributes.HasFlag(FileAttributes.Hidden));
        var loaded = CaptureStore.Load(paths.PngPath);
        Assert.Equal(Original, loaded.ImagePng);
        Assert.Equal(1, loaded.Document!.Revision);
        Assert.Null(loaded.Warning);
    }

    [Fact]
    public void Save_reports_conflict_when_other_pc_saved_in_between()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        var mine = ((SaveOutcome.Saved)store.Save(paths, Doc(), Original, Flat, "Büro", Now)).Document;
        // Laptop lädt Revision 1 und speichert Revision 2
        var laptop = new CaptureStore(dir.Path, "Laptop");
        laptop.Save(paths, mine, Original, Flat, "Laptop", Now.AddMinutes(1));
        // Büro speichert weiter auf Basis von Revision 1
        var conflict = Assert.IsType<SaveOutcome.Conflict>(store.Save(paths, mine, Original, [7], "Büro", Now.AddMinutes(2)));
        Assert.Equal("Laptop", conflict.ModifiedOn);
        Assert.Equal(2, conflict.DiskRevision);
        Assert.Equal(Flat, File.ReadAllBytes(paths.PngPath));
    }

    [Fact]
    public void Save_reports_conflict_when_both_pcs_saved_the_same_revision()
    {
        using var dir = new TempDir();
        var buero = new CaptureStore(dir.Path, "Buero");
        var paths = buero.CreateNew(T);
        var shared = ((SaveOutcome.Saved)buero.Save(paths, Doc(), Original, Flat, "Büro", Now)).Document;
        // Beide PCs speichern auf Basis derselben Revision, bevor Dropbox synchronisiert.
        // Dropbox behält die Laptop-Datei; Büro hat lokal schon Revision 2 gespeichert.
        var bueroLocal = shared with { Revision = shared.Revision + 1, SaveId = Guid.NewGuid() };
        new CaptureStore(dir.Path, "Laptop").Save(paths, shared, Original, [8], "Laptop", Now.AddMinutes(1));
        var conflict = Assert.IsType<SaveOutcome.Conflict>(buero.Save(paths, bueroLocal, Original, [9], "Büro", Now.AddMinutes(2)));
        Assert.Equal("Laptop", conflict.ModifiedOn);
        Assert.Equal(new byte[] { 8 }, File.ReadAllBytes(paths.PngPath));
    }

    [Fact]
    public void Every_save_gets_a_new_save_id()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        var first = ((SaveOutcome.Saved)store.Save(paths, Doc(), Original, Flat, "Büro", Now)).Document;
        var second = ((SaveOutcome.Saved)store.Save(paths, first, Original, Flat, "Büro", Now)).Document;
        Assert.NotEqual(Guid.Empty, first.SaveId);
        Assert.NotEqual(first.SaveId, second.SaveId);
    }

    [Fact]
    public void Save_refuses_to_overwrite_project_from_newer_version()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        store.Save(paths, Doc(), Original, Flat, "Büro", Now);
        var newer = ProjectFile.Write(Doc() with { FormatVersion = CaptureDocument.CurrentFormatVersion + 1, Revision = 5 }, Original);
        File.WriteAllBytes(paths.ProjectPath, newer);
        var outcome = Assert.IsType<SaveOutcome.Failed>(store.Save(paths, Doc(), Original, [7], "Büro", Now));
        Assert.Equal(StorageError.NewerVersion, outcome.Error);
        Assert.Equal(newer, File.ReadAllBytes(paths.ProjectPath));
        Assert.Equal(Flat, File.ReadAllBytes(paths.PngPath));
    }

    [Fact]
    public void Save_fails_without_overwriting_when_project_is_locked()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        var saved = ((SaveOutcome.Saved)store.Save(paths, Doc(), Original, Flat, "Büro", Now)).Document;
        var before = File.ReadAllBytes(paths.ProjectPath);
        using (new FileStream(paths.ProjectPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var failed = Assert.IsType<SaveOutcome.Failed>(store.Save(paths, saved, Original, [7], "Büro", Now));
            Assert.Equal(StorageError.FolderUnavailable, failed.Error);
            Assert.False(string.IsNullOrWhiteSpace(failed.Detail));
        }
        Assert.Equal(before, File.ReadAllBytes(paths.ProjectPath));
        Assert.Equal(Flat, File.ReadAllBytes(paths.PngPath));
    }

    [Fact]
    public void Save_keeps_a_backup_before_replacing_a_corrupt_project()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        CaptureStore.EnsureProjectFolder(dir.Path);
        File.WriteAllText(paths.ProjectPath, "kein zip");
        Assert.IsType<SaveOutcome.Saved>(store.Save(paths, Doc(), Original, Flat, "Büro", Now));
        var backup = Assert.Single(Directory.GetFiles(Path.Combine(dir.Path, ".shotmark"), "*.defekt-*"));
        Assert.Equal("kein zip", File.ReadAllText(backup));
        Assert.Equal(Original, CaptureStore.Load(paths.PngPath).ImagePng);
    }

    [Fact]
    public void Load_explains_project_from_newer_version()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        store.Save(paths, Doc(), Original, Flat, "Büro", Now);
        File.WriteAllBytes(paths.ProjectPath, ProjectFile.Write(Doc() with { FormatVersion = 99 }, Original));
        var loaded = CaptureStore.Load(paths.PngPath);
        Assert.Null(loaded.Document);
        Assert.Equal(LoadWarning.NewerVersion, loaded.Warning);
    }

    [Fact]
    public void Load_without_project_returns_png_only()
    {
        using var dir = new TempDir();
        var png = Path.Combine(dir.Path, "fremd.png");
        File.WriteAllBytes(png, Flat);
        var loaded = CaptureStore.Load(png);
        Assert.Null(loaded.Document);
        Assert.Equal(Flat, loaded.ImagePng);
        Assert.Null(loaded.Warning);
    }

    [Fact]
    public void Load_with_corrupt_project_warns_and_falls_back_to_png()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        File.WriteAllBytes(paths.PngPath, Flat);
        CaptureStore.EnsureProjectFolder(dir.Path);
        File.WriteAllText(paths.ProjectPath, "kein zip");
        var loaded = CaptureStore.Load(paths.PngPath);
        Assert.Null(loaded.Document);
        Assert.Equal(Flat, loaded.ImagePng);
        Assert.Equal(LoadWarning.ProjectUnreadable, loaded.Warning);
    }

    [Fact]
    public void Save_works_again_after_project_was_deleted_elsewhere()
    {
        using var dir = new TempDir();
        var store = new CaptureStore(dir.Path, "Buero");
        var paths = store.CreateNew(T);
        var saved = ((SaveOutcome.Saved)store.Save(paths, Doc(), Original, Flat, "Büro", Now)).Document;
        File.Delete(paths.ProjectPath);
        Assert.IsType<SaveOutcome.Saved>(store.Save(paths, saved, Original, Flat, "Büro", Now));
    }
}
