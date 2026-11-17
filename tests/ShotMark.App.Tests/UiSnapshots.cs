using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ShotMark.App.Capture;
using ShotMark.App.Editor;
using ShotMark.App.History;
using ShotMark.App.Native;
using ShotMark.App.Rendering;
using ShotMark.App.Settings;
using ShotMark.Core.Geometry;
using ShotMark.Core.Model;
using ShotMark.Core.Settings;
using ShotMark.Core.Storage;

namespace ShotMark.App.Tests;

/// <summary>
/// Rendert alle Fenster offscreen als PNG zur Sichtprüfung. Läuft nur, wenn die Umgebungsvariable
/// SHOTMARK_SNAPSHOT_DIR gesetzt ist (z. B. vor einem Release), sonst endet der Test sofort.
/// </summary>
public class UiSnapshots
{
    [Fact]
    public void Render_ui_snapshots()
    {
        var output = Environment.GetEnvironmentVariable("SHOTMARK_SNAPSHOT_DIR");
        if (string.IsNullOrWhiteSpace(output)) return;
        Sta.Run(() => new SnapshotRun(output).Execute());
    }

    private sealed class FakeHost(CaptureStore store) : IEditorHost, IEditorCommands
    {
        public EditorViewModel? Vm { get; set; }
        public CaptureStore Store { get; } = store;
        public string PcName => "Büro";
        public string HotkeyLabel => "Druck";
        public string RescueFolder => Path.Combine(Path.GetTempPath(), "shotmark-rescue");
        public void CaptureSaved(string pngPath) { }
        public string PcLabel(string? slug) => slug == "Buero" ? "Büro" : slug ?? "unbekannt";
        public void Notify(string message) { }
        public bool ConfirmDiscard(string message) => true;
        public void StartCapture() { }
        public void OpenFolder() { }
        public void ShowSettings() { }

        public async Task OpenFromHistoryAsync(string pngPath)
        {
            var loaded = CaptureStore.Load(pngPath);
            await Vm!.OpenAsync(OpenCapture.FromLoaded(loaded, PcLabel), loaded.Warning is { } w ? ShotMark.App.Localization.ErrorText.Describe(w) : null);
        }
    }

    private sealed class SnapshotRun(string output)
    {
        private readonly string _shots = Path.Combine(output, "shots");

        public void Execute()
        {
            DispatcherPump.Install();
            var app = Application.Current ?? new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/ShotMark;component/Theme/Theme.xaml"),
            });

            Directory.CreateDirectory(output);
            if (Directory.Exists(_shots)) Directory.Delete(_shots, recursive: true);
            var (buero, newest) = CreateSampleCaptures();

            // Editor mit allen Werkzeugen
            var host = new FakeHost(buero);
            var vm = new EditorViewModel(host);
            host.Vm = vm;
            var history = new HistoryStripViewModel(Dispatcher.CurrentDispatcher);
            history.Configure(_shots, "Buero");
            var editor = new EditorWindow(vm, history, host);
            ShowOffscreen(editor, 1320, 860);
            DispatcherPump.Run(() => host.OpenFromHistoryAsync(newest));
            AddAllAnnotations(vm);
            vm.Tool = ToolKind.Arrow;
            vm.SelectedId = vm.State.Annotations.OfType<ArrowAnnotation>().First().Id;
            DispatcherPump.Pump(1500);
            Save(editor, "01-editor.png");

            vm.SelectedId = null;
            vm.Tool = ToolKind.Text;
            DispatcherPump.Pump(300);
            Save(editor, "02-editor-text-tool.png");

            vm.Tool = ToolKind.Crop;
            DispatcherPump.Pump(300);
            Save(editor, "03-editor-crop.png");
            vm.CancelCrop();

            vm.ApplyCrop(new RectD(40, 30, 700, 420));
            vm.Tool = ToolKind.Select;
            DispatcherPump.Pump(300);
            Save(editor, "04-editor-cropped.png");
            vm.RemoveCrop();

            history.SetFilter("Laptop");
            DispatcherPump.Pump(300);
            Save(editor, "05-editor-filter-laptop.png");
            history.SetFilter(null);

            editor.Width = 900;
            editor.Height = 620;
            DispatcherPump.Pump(600);
            Save(editor, "06-editor-narrow.png");
            editor.Width = 1320;
            editor.Height = 860;

            // Konflikt: anderer PC speichert dazwischen
            DispatcherPump.Run(() => vm.FlushAsync());
            var capture = vm.Capture!;
            new CaptureStore(_shots, "Laptop").Save(capture.Paths, capture.Document, capture.OriginalPng, capture.OriginalPng, "Laptop", DateTimeOffset.Now);
            vm.AddAnnotation(new RectangleAnnotation(Guid.NewGuid(), new RectD(640, 410, 220, 70), Palette.Red, 4, true));
            DispatcherPump.Run(() => vm.FlushAsync());
            DispatcherPump.Pump(400);
            Save(editor, "07-editor-conflict.png");
            editor.AllowClose = true;
            editor.Close();

            // Leerer Editor
            var emptyHost = new FakeHost(buero);
            var emptyVm = new EditorViewModel(emptyHost);
            emptyHost.Vm = emptyVm;
            var emptyHistory = new HistoryStripViewModel(Dispatcher.CurrentDispatcher);
            emptyHistory.Configure(Path.Combine(output, "empty-folder"), "Buero");
            var empty = new EditorWindow(emptyVm, emptyHistory, emptyHost);
            ShowOffscreen(empty, 1100, 720);
            DispatcherPump.Pump(800);
            Save(empty, "08-editor-empty.png");
            empty.AllowClose = true;
            empty.Close();

            // Einstellungen und Ersteinrichtung
            var settings = new SettingsWindow(new AppSettings { PcName = "Büro" }, @"C:\Users\anna\Dropbox\Screenshots", false, _ => null);
            ShowOffscreen(settings, settings.Width, double.NaN);
            DispatcherPump.Pump(300);
            Save(settings, "09-settings.png");
            settings.Close();

            var firstRun = new FirstRunWindow("DESKTOP-Office", @"C:\Users\anna\Dropbox\Screenshots", false, "TEST");
            ShowOffscreen(firstRun, firstRun.Width, double.NaN);
            DispatcherPump.Pump(300);
            Save(firstRun, "10-first-run.png");
            firstRun.Close();

            // Aufnahme-Overlay (eingefrorener „Bildschirm“ = gerenderter Editor)
            var screen = Exporter.DecodeImage(File.ReadAllBytes(Path.Combine(output, "01-editor.png")));
            var monitor = new Int32Rect(0, 0, screen.PixelWidth, screen.PixelHeight);
            var snapshot = new ScreenSnapshot(screen, monitor, [monitor], DateTime.Now);
            var session = new RegionCaptureSession(snapshot);
            session.SetPointerForTesting(null, new Point(520, 330));
            SaveElement(OverlayStack(session, monitor, screen), monitor, "11-overlay-hover.png");
            session.SetPointerForTesting(new Point(380, 140), new Point(1010, 560));
            SaveElement(OverlayStack(session, monitor, screen), monitor, "12-overlay-drag.png");
            var scrollSession = new RegionCaptureSession(snapshot, CaptureMode.Scroll);
            scrollSession.SetPointerForTesting(null, new Point(700, 420));
            SaveElement(OverlayStack(scrollSession, monitor, screen), monitor, "13-overlay-scroll.png");
            scrollSession.SetPointerForTesting(null, new Point(700, 420), tooShortForScroll: true);
            SaveElement(OverlayStack(scrollSession, monitor, screen), monitor, "13-overlay-scroll-too-short.png");

            // Panel während der Scroll-Aufnahme (automatisch und manuell) vor dunklem Hintergrund
            var scrollFrame = new ScrollFrameWindow(new Int32Rect(0, 0, 400, 300));
            scrollFrame.Update(new ShotMark.Core.Scrolling.ScrollProgress(3240, false, null));
            SaveOnBackdrop(scrollFrame.PanelContentForTesting, "14-scroll-panel.png");
            scrollFrame.Update(new ShotMark.Core.Scrolling.ScrollProgress(5120, true, ShotMark.Core.Scrolling.ScrollCaptureController.HintNoReaction));
            SaveOnBackdrop(scrollFrame.PanelContentForTesting, "15-scroll-panel-manual.png");
            scrollFrame.Dispose();
        }

        private void SaveOnBackdrop(FrameworkElement element, string name)
        {
            var host = new Border { Background = new SolidColorBrush(Color.FromRgb(0xE8, 0xEA, 0xEE)), Padding = new Thickness(24), Child = element };
            host.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var size = host.DesiredSize;
            host.Arrange(new Rect(size));
            host.UpdateLayout();
            var bitmap = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(host);
            File.WriteAllBytes(Path.Combine(output, name), Exporter.EncodePng(bitmap));
            host.Child = null;
        }

        private (CaptureStore Buero, string NewestPath) CreateSampleCaptures()
        {
            var buero = new CaptureStore(_shots, "Buero");
            var laptop = new CaptureStore(_shots, "Laptop");
            PcRegistry.Register(_shots, new PcInfo("Buero", "Büro", "PC-1"));
            PcRegistry.Register(_shots, new PcInfo("Laptop", "Laptop", "PC-2"));
            var now = DateTime.Now;
            var samples = new (CaptureStore Store, string Pc, DateTime At, Color Accent)[]
            {
                (laptop, "Laptop", now.AddDays(-6).Date.AddHours(14), Color.FromRgb(0x7A, 0x5A, 0xF8)),
                (buero, "Büro", now.AddDays(-1).Date.AddHours(9).AddMinutes(12), Color.FromRgb(0x0E, 0x9F, 0x6E)),
                (laptop, "Laptop", now.AddDays(-1).Date.AddHours(17).AddMinutes(40), Color.FromRgb(0xE8, 0x7B, 0x12)),
                (laptop, "Laptop", now.AddMinutes(-50), Color.FromRgb(0x2D, 0x7F, 0xF9)),
                (buero, "Büro", now.AddMinutes(-20), Color.FromRgb(0xD9, 0x3A, 0x6A)),
                (buero, "Büro", now.AddMinutes(-2), Color.FromRgb(0x1F, 0x6F, 0x8B)),
            };
            string newest = "";
            foreach (var (store, pc, at, accent) in samples)
            {
                var image = SamplePage(accent, at);
                var png = Exporter.EncodePng(image);
                var paths = store.CreateNew(at);
                var doc = CaptureDocument.New(new DateTimeOffset(at), new CapturedOn(pc, pc), new PixelSize(image.PixelWidth, image.PixelHeight));
                store.Save(paths, doc, png, png, pc, new DateTimeOffset(at));
                newest = paths.PngPath;
            }
            return (buero, newest);
        }

        private static void AddAllAnnotations(EditorViewModel vm)
        {
            vm.AddAnnotation(new RectangleAnnotation(Guid.NewGuid(), new RectD(46, 132, 420, 74), Palette.Red, 4, true));
            vm.AddAnnotation(new ArrowAnnotation(Guid.NewGuid(), new PointD(700, 360), new PointD(560, 432), Palette.Red, 6, true));
            vm.AddAnnotation(new CheckAnnotation(Guid.NewGuid(), new RectD(486, 140, 52, 52), Palette.Green, true));
            vm.AddAnnotation(new CrossAnnotation(Guid.NewGuid(), new RectD(486, 236, 52, 52), Palette.Red, true));
            vm.AddAnnotation(new TextAnnotation(Guid.NewGuid(), new PointD(600, 300), "Hier klicken", Palette.Red, 22));
            vm.AddAnnotation(new BlurAnnotation(Guid.NewGuid(), new RectD(60, 340, 260, 34), 10));
        }

        /// <summary>Synthetische Webseite als Aufnahme-Inhalt.</summary>
        private static BitmapSource SamplePage(Color accent, DateTime at)
        {
            const int w = 900, h = 560;
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var face = new Typeface("Segoe UI");
                var bold = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
                FormattedText T(string s, double size, Brush b, Typeface? f = null) =>
                    new(s, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, f ?? face, size, b, 1.0);
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xF4, 0xF5, 0xF7)), null, new Rect(0, 0, w, h));
                dc.DrawRectangle(new SolidColorBrush(accent), null, new Rect(0, 0, w, 56));
                dc.DrawText(T("Kundenportal", 20, Brushes.White, bold), new Point(28, 14));
                dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(0xDD, 0xDF, 0xE4)), 1), new Rect(28, 84, 844, 440), 10, 10);
                dc.DrawText(T("Dokumente fragen", 26, Brushes.Black, bold), new Point(56, 98));
                dc.DrawText(T("Ihre Fragen", 16, Brushes.Black, bold), new Point(60, 146));
                dc.DrawText(T("Sie haben zu diesem Objekt noch keine Frage gestellt.", 14, Brushes.DimGray), new Point(60, 172));
                dc.DrawRoundedRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(56, 226, 788, 90), 8, 8);
                dc.DrawText(T("Frage zu Gutachten, Besichtigung oder Risiken", 14, Brushes.Gray), new Point(72, 240));
                dc.DrawText(T("Kontonummer: DE12 3456 7890 1234 5678", 14, Brushes.Black), new Point(64, 346));
                dc.DrawRoundedRectangle(new SolidColorBrush(accent), null, new Rect(644, 420, 200, 44), 8, 8);
                dc.DrawText(T("Fragen", 16, Brushes.White, bold), new Point(718, 430));
                dc.DrawText(T($"Aufnahme {at:dd.MM. HH:mm}", 12, Brushes.Gray), new Point(64, 486));
            }
            var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static FrameworkElement OverlayStack(RegionCaptureSession session, Int32Rect monitor, BitmapSource screen)
        {
            var grid = new Grid { Width = monitor.Width, Height = monitor.Height };
            grid.Children.Add(new Image { Source = screen, Stretch = Stretch.Fill });
            grid.Children.Add(new OverlayLayer(session, monitor));
            return grid;
        }

        private static void ShowOffscreen(Window window, double width, double height)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            // -32000 ist die Windows-Koordinate für minimierte Fenster – daher ein anderer Wert
            window.Left = SystemParameters.VirtualScreenLeft - window.Width - 4000;
            window.Top = 0;
            window.Width = width;
            if (!double.IsNaN(height)) window.Height = height;
            window.ShowInTaskbar = false;
            window.ShowActivated = false;
            window.Show();
            DispatcherPump.Pump(200);
        }

        private void Save(Window window, string name)
        {
            window.UpdateLayout();
            // Wurzel des Fenster-Templates = Client-Bereich inklusive Außenabständen des Inhalts
            var content = (FrameworkElement)VisualTreeHelper.GetChild(window, 0);
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var dc = background.RenderOpen())
                dc.DrawRectangle(window.Background, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            bitmap.Render(background);
            bitmap.Render(content);
            File.WriteAllBytes(Path.Combine(output, name), Exporter.EncodePng(bitmap));
        }

        private void SaveElement(FrameworkElement element, Int32Rect size, string name)
        {
            element.Measure(new Size(size.Width, size.Height));
            element.Arrange(new Rect(0, 0, size.Width, size.Height));
            element.UpdateLayout();
            var bitmap = new RenderTargetBitmap(size.Width, size.Height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(element);
            File.WriteAllBytes(Path.Combine(output, name), Exporter.EncodePng(bitmap));
        }
    }
}
