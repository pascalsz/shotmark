using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ShotMark.App.Editor;
using ShotMark.App.Rendering;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
using ShotMark.Core.Storage;

namespace ShotMark.App.Tests;

/// <summary>Kein Datenverlust beim Wechseln, Beenden und bei Fehlern im Speicherordner.</summary>
public class EditorViewModelTests
{
    private sealed class TestHost(CaptureStore store, string rescueFolder) : IEditorHost
    {
        public CaptureStore Store { get; } = store;
        public string PcName => "Büro";
        public string RescueFolder { get; } = rescueFolder;
        public List<string> Notifications { get; } = [];
        public bool AllowDiscard { get; set; }
        public int DiscardQuestions { get; private set; }
        public void CaptureSaved(string pngPath) { }
        public string PcLabel(string? slug) => slug ?? "unbekannt";
        public void Notify(string message) => Notifications.Add(message);

        public bool ConfirmDiscard(string message)
        {
            DiscardQuestions++;
            return AllowDiscard;
        }
    }

    private sealed class Folder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "shotmark-vm-tests", Guid.NewGuid().ToString("N"));
        public Folder() => Directory.CreateDirectory(Path);
        public string Sub(string name) => System.IO.Path.Combine(Path, name);

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
        }
    }

    private static byte[] Png()
    {
        var pixels = Enumerable.Repeat((byte)200, 40 * 30 * 4).ToArray();
        var bitmap = BitmapSource.Create(40, 30, 96, 96, PixelFormats.Bgra32, null, pixels, 40 * 4);
        return Exporter.EncodePng(bitmap);
    }

    /// <summary>Neue Aufnahme wie in AppHost: speichern und als <see cref="OpenCapture"/> öffnen.</summary>
    private static OpenCapture NewCapture(CaptureStore store, DateTime at)
    {
        var png = Png();
        var paths = store.CreateNew(at);
        var document = CaptureDocument.New(new DateTimeOffset(at), new CapturedOn("Büro", "PC1"), new PixelSize(40, 30));
        var capture = new OpenCapture(paths, document, png, Exporter.DecodeImage(png));
        if (store.Save(paths, document, png, png, "Büro", DateTimeOffset.Now) is SaveOutcome.Saved saved)
            capture.Document = saved.Document;
        return capture;
    }

    private static RectangleAnnotation Frame() => new(Guid.NewGuid(), new RectD(2, 2, 20, 10), Palette.Red, 4, true);

    [Fact]
    public void Switching_capture_during_conflict_saves_own_version_as_copy() => Sta.Run(() =>
    {
        DispatcherPump.Install();
        using var folder = new Folder();
        var store = new CaptureStore(folder.Sub("shots"), "Buero");
        var host = new TestHost(store, folder.Sub("rescue"));
        var vm = new EditorViewModel(host);
        var first = NewCapture(store, new DateTime(2026, 10, 19, 10, 0, 0));
        var originalPath = first.Paths.PngPath; // „Als Kopie speichern“ setzt first.Paths auf die Kopie
        Assert.True(DispatcherPump.Run(() => vm.OpenAsync(first)));

        // Laptop speichert dieselbe Aufnahme dazwischen
        new CaptureStore(store.Folder, "Laptop").Save(first.Paths, first.Document, first.OriginalPng, first.OriginalPng, "Laptop", DateTimeOffset.Now);
        var frame = Frame();
        vm.AddAnnotation(frame);
        DispatcherPump.Run(() => vm.FlushAsync());
        Assert.NotNull(vm.Conflict);

        var second = NewCapture(store, new DateTime(2026, 10, 19, 11, 0, 0));
        Assert.True(DispatcherPump.Run(() => vm.OpenAsync(second)));

        var copies = Directory.GetFiles(store.Folder, "*.png")
            .Where(p => p != originalPath && p != second.Paths.PngPath)
            .ToList();
        var copy = CaptureStore.Load(Assert.Single(copies));
        Assert.Contains(copy.Document!.Annotations, a => a.Id == frame.Id);
        Assert.Empty(CaptureStore.Load(originalPath).Document!.Annotations); // Laptop-Version unangetastet
        Assert.Single(host.Notifications);
        Assert.Equal(0, host.DiscardQuestions);
    });

    [Fact]
    public void Unreachable_folder_is_rescued_locally_before_switching() => Sta.Run(() =>
    {
        DispatcherPump.Install();
        using var folder = new Folder();
        var store = new CaptureStore(folder.Sub("shots"), "Buero");
        var host = new TestHost(store, folder.Sub("rescue"));
        var vm = new EditorViewModel(host);
        var capture = NewCapture(store, new DateTime(2026, 10, 19, 10, 0, 0));
        Assert.True(DispatcherPump.Run(() => vm.OpenAsync(capture)));

        // Speicherordner verschwindet (z. B. Laufwerk getrennt): an seiner Stelle liegt jetzt eine Datei
        Directory.Delete(store.Folder, recursive: true);
        File.WriteAllText(store.Folder, "kein Ordner");
        vm.AddAnnotation(Frame());
        DispatcherPump.Run(() => vm.FlushAsync());
        Assert.Equal(SaveStatus.Failed, vm.SaveStatus);

        Assert.True(DispatcherPump.Run(() => vm.SecureUnsavedAsync()));
        var rescued = Assert.Single(Directory.GetFiles(host.RescueFolder, "*.png"));
        Assert.Single(CaptureStore.Load(rescued).Document!.Annotations);
        Assert.Equal(0, host.DiscardQuestions);
    });

    [Fact]
    public void Keeps_capture_open_when_nothing_could_be_saved_and_user_declines() => Sta.Run(() =>
    {
        DispatcherPump.Install();
        using var folder = new Folder();
        var store = new CaptureStore(folder.Sub("shots"), "Buero");
        // Auch der Rettungsordner ist keine Option
        File.WriteAllText(folder.Sub("rescue"), "kein Ordner");
        var host = new TestHost(store, folder.Sub("rescue")) { AllowDiscard = false };
        var vm = new EditorViewModel(host);
        var first = NewCapture(store, new DateTime(2026, 10, 19, 10, 0, 0));
        var second = NewCapture(store, new DateTime(2026, 10, 19, 11, 0, 0));
        Assert.True(DispatcherPump.Run(() => vm.OpenAsync(first)));

        Directory.Delete(store.Folder, recursive: true);
        File.WriteAllText(store.Folder, "kein Ordner");
        vm.AddAnnotation(Frame());

        Assert.False(DispatcherPump.Run(() => vm.OpenAsync(second)));
        Assert.Same(first, vm.Capture);
        Assert.Single(vm.State.Annotations);
        Assert.Equal(1, host.DiscardQuestions);
    });

    [Fact]
    public void Only_the_latest_open_request_wins() => Sta.Run(() =>
    {
        DispatcherPump.Install();
        using var folder = new Folder();
        var store = new CaptureStore(folder.Sub("shots"), "Buero");
        var vm = new EditorViewModel(new TestHost(store, folder.Sub("rescue")));
        var first = NewCapture(store, new DateTime(2026, 10, 19, 10, 0, 0));
        var second = NewCapture(store, new DateTime(2026, 10, 19, 11, 0, 0));
        var third = NewCapture(store, new DateTime(2026, 10, 19, 12, 0, 0));
        Assert.True(DispatcherPump.Run(() => vm.OpenAsync(first)));
        vm.AddAnnotation(Frame()); // erzwingt eine Speicherung beim Wechsel

        var results = DispatcherPump.Run(() => Task.WhenAll(vm.OpenAsync(second), vm.OpenAsync(third)));
        Assert.Equal(new[] { false, true }, results);
        Assert.Same(third, vm.Capture);
        Assert.Single(CaptureStore.Load(first.Paths.PngPath).Document!.Annotations);
    });
}
