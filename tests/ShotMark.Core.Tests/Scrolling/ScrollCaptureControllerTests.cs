using ShotMark.Core.Scrolling;
namespace ShotMark.Core.Tests.Scrolling;

public class ScrollCaptureControllerTests
{
    private const int W = 300;
    private const int H = 360;

    /// <summary>Simuliert Bildschirm + Programm: Mausrad verschiebt den sichtbaren Ausschnitt einer langen Seite.</summary>
    private sealed class FakeScreen(PixelFrame page, int pixelsPerNotch, PixelFrame? header = null, PixelFrame? footer = null, int height = H)
        : IFrameSource, IScroller, IUserActivity, IDelay
    {
        private int? _pendingOffset;
        private int _capturesUntilApplied;

        public int Height { get; } = height;
        public int Offset { get; set; }
        public bool ReactsToWheel { get; init; } = true;

        /// <summary>Programm reagiert erst nach so vielen weiteren Aufnahmen auf das Mausrad.</summary>
        public int WheelLatency { get; init; }

        public bool Garbage { get; set; }
        public int GarbageCaptures { get; set; }
        public int? ThrowOnCapture { get; set; }
        public bool UserTookOver { get; set; }
        public int Captures { get; private set; }
        public int Scrolls { get; private set; }
        public Action<FakeScreen>? OnCapture { get; set; }
        public Action<FakeScreen>? OnDelay { get; set; }
        private int ContentHeight => Height - (header?.Height ?? 0) - (footer?.Height ?? 0);
        private int MaxOffset => page.Height - ContentHeight;

        public PixelFrame Capture()
        {
            Captures++;
            if (ThrowOnCapture == Captures) throw new InvalidOperationException("Bildschirm gesperrt");
            if (_pendingOffset is { } pending && _capturesUntilApplied-- <= 0)
            {
                Offset = pending;
                _pendingOffset = null;
            }
            OnCapture?.Invoke(this);
            if (Garbage || GarbageCaptures > 0)
            {
                if (GarbageCaptures > 0) GarbageCaptures--;
                return SyntheticPage.Viewport(SyntheticPage.Page(W, 2000, seed: 1000 + Captures), 0, Height);
            }
            return SyntheticPage.Viewport(page, Offset, Height, header, footer);
        }

        public void ScrollDown(int notches) => Move(notches * pixelsPerNotch);

        public void ScrollUp(int notches) => Move(-notches * pixelsPerNotch);

        private void Move(int pixels)
        {
            Scrolls++;
            if (!ReactsToWheel) return;
            var target = Math.Clamp((_pendingOffset ?? Offset) + pixels, 0, MaxOffset);
            if (WheelLatency == 0) Offset = target;
            else
            {
                _pendingOffset = target;
                _capturesUntilApplied = WheelLatency;
            }
        }

        public Task DelayAsync(TimeSpan duration, CancellationToken token)
        {
            OnDelay?.Invoke(this);
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private static ScrollCaptureController Controller(FakeScreen screen, ScrollCaptureOptions? options = null) =>
        new(screen, screen, screen, screen, options);

    [Fact]
    public async Task Scrolls_to_page_end_and_returns_the_whole_page()
    {
        var page = SyntheticPage.Page(W, 2400);
        var screen = new FakeScreen(page, pixelsPerNotch: 40);
        var result = await Controller(screen).RunAsync(CancellationToken.None);
        Assert.Equal(ScrollEndReason.PageEnd, result.Reason);
        Assert.False(result.WasManual);
        Assert.Equal(page.Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task Sticky_header_and_footer_are_kept_once()
    {
        var page = SyntheticPage.Page(W, 1800, seed: 3);
        var header = SyntheticPage.Solid(W, 50, 200, 120, 30);
        var footer = SyntheticPage.Solid(W, 24, 20, 20, 20);
        var screen = new FakeScreen(page, 35, header, footer);
        var result = await Controller(screen).RunAsync(CancellationToken.None);
        Assert.Equal(SyntheticPage.Stack(header, page, footer).Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task Step_size_adapts_towards_sixty_percent_of_the_region()
    {
        var page = SyntheticPage.Page(W, 5000, seed: 8);
        var screen = new FakeScreen(page, pixelsPerNotch: 20);
        var steps = 0;
        var controller = Controller(screen);
        controller.Progress += _ => steps++;
        await controller.RunAsync(CancellationToken.None);
        // 1 Raste à 20 px wären ~230 Schritte; angepasst (~216 px pro Schritt) deutlich weniger
        Assert.InRange(steps, 20, 40);
    }

    [Fact]
    public async Task Short_region_with_large_wheel_steps_reaches_the_end()
    {
        // z. B. Chrome bei 125–150 %: eine Raste ≈ 100–150 px, Bereich nur 300 px hoch
        var page = SyntheticPage.Page(W, 2200, seed: 21);
        var screen = new FakeScreen(page, pixelsPerNotch: 110, height: 300);
        var result = await Controller(screen).RunAsync(CancellationToken.None);
        Assert.Equal(ScrollEndReason.PageEnd, result.Reason);
        Assert.Equal(page.Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task One_changed_frame_does_not_end_the_capture()
    {
        var page = SyntheticPage.Page(W, 3000, seed: 22);
        var screen = new FakeScreen(page, 40);
        var controller = Controller(screen);
        var injected = false;
        controller.Progress += p =>
        {
            if (!injected && p.Height > 1000)
            {
                injected = true;
                screen.GarbageCaptures = 2; // z. B. Tooltip oder Animation genau während eines Schritts
            }
        };
        var result = await controller.RunAsync(CancellationToken.None);
        Assert.Equal(ScrollEndReason.PageEnd, result.Reason);
        Assert.Equal(page.Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task Stop_during_a_wait_keeps_the_part_that_was_just_scrolled_into_view()
    {
        var page = SyntheticPage.Page(W, 4000, seed: 23);
        var screen = new FakeScreen(page, 40);
        using var stop = new CancellationTokenSource();
        screen.OnDelay = s =>
        {
            if (s.Scrolls == 6) stop.Cancel(); // Stopp kommt, während auf das Bild nach dem 6. Schritt gewartet wird
        };
        var result = await Controller(screen).RunAsync(stop.Token);
        Assert.Equal(ScrollEndReason.Stopped, result.Reason);
        Assert.Equal(screen.Offset + H, result.Image.Height);
        Assert.Equal(SyntheticPage.Rows(page, 0, result.Image.Height).Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task Stop_returns_what_was_captured_so_far()
    {
        var page = SyntheticPage.Page(W, 3000, seed: 4);
        var screen = new FakeScreen(page, 40);
        using var stop = new CancellationTokenSource();
        var controller = Controller(screen);
        controller.Progress += p => { if (p.Height > 900) stop.Cancel(); };
        var result = await controller.RunAsync(stop.Token);
        Assert.Equal(ScrollEndReason.Stopped, result.Reason);
        Assert.InRange(result.Image.Height, 900, 1600);
        Assert.Equal(SyntheticPage.Rows(page, 0, result.Image.Height).Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task Max_height_ends_the_capture()
    {
        var page = SyntheticPage.Page(W, 6000, seed: 5);
        var screen = new FakeScreen(page, 40);
        var result = await Controller(screen, new ScrollCaptureOptions { MaxHeight = 1500 }).RunAsync(CancellationToken.None);
        Assert.Equal(ScrollEndReason.MaxSize, result.Reason);
        Assert.InRange(result.Image.Height, 1500, 1500 + H);
    }

    [Fact]
    public async Task Slow_app_is_not_mistaken_for_no_reaction()
    {
        var page = SyntheticPage.Page(W, 2400, seed: 24);
        var screen = new FakeScreen(page, 40) { WheelLatency = 3 };
        var result = await Controller(screen).RunAsync(CancellationToken.None);
        Assert.False(result.WasManual);
        Assert.Equal(ScrollEndReason.PageEnd, result.Reason);
        Assert.Equal(page.Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task Program_without_wheel_reaction_switches_to_manual_mode()
    {
        var page = SyntheticPage.Page(W, 2000, seed: 6);
        var screen = new FakeScreen(page, 40) { ReactsToWheel = false };
        using var stop = new CancellationTokenSource();
        var controller = Controller(screen);
        var hints = new List<string?>();
        controller.Progress += p =>
        {
            hints.Add(p.Hint);
            if (p.Manual) screen.OnCapture ??= s => s.Offset = Math.Min(page.Height - H, s.Offset + 90); // Nutzer scrollt selbst
            if (p.Height >= 1000) stop.Cancel();
        };
        var result = await controller.RunAsync(stop.Token);
        Assert.True(result.WasManual);
        Assert.Contains(ScrollCaptureController.HintNoReaction, hints);
        Assert.Equal(SyntheticPage.Rows(page, 0, result.Image.Height).Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task User_scrolling_switches_to_manual_mode()
    {
        var page = SyntheticPage.Page(W, 2000, seed: 7);
        var screen = new FakeScreen(page, 40);
        using var stop = new CancellationTokenSource();
        var controller = Controller(screen);
        controller.Progress += p =>
        {
            if (p.Height > 500) screen.UserTookOver = true;
            if (p.Manual && p.Height > 1200) stop.Cancel();
            if (p.Manual) screen.OnCapture ??= s => s.Offset = Math.Min(page.Height - H, s.Offset + 60);
        };
        var result = await controller.RunAsync(stop.Token);
        Assert.True(result.WasManual);
        Assert.Equal(ScrollEndReason.Stopped, result.Reason);
    }

    [Fact]
    public async Task Changing_content_ends_after_three_mismatches()
    {
        var page = SyntheticPage.Page(W, 3000, seed: 8);
        var screen = new FakeScreen(page, 40);
        var controller = Controller(screen);
        controller.Progress += p => { if (p.Height > 700) screen.Garbage = true; };
        var result = await controller.RunAsync(CancellationToken.None);
        Assert.Equal(ScrollEndReason.ContentChanged, result.Reason);
        Assert.Equal(SyntheticPage.Rows(page, 0, result.Image.Height).Pixels, result.Image.Pixels);
    }

    [Fact]
    public async Task Unexpected_error_returns_the_partial_result()
    {
        var page = SyntheticPage.Page(W, 3000, seed: 25);
        var screen = new FakeScreen(page, 40) { ThrowOnCapture = 12 };
        var result = await Controller(screen).RunAsync(CancellationToken.None);
        Assert.Equal(ScrollEndReason.Error, result.Reason);
        Assert.IsType<InvalidOperationException>(result.Error);
        Assert.True(result.Image.Height >= H);
        Assert.Equal(SyntheticPage.Rows(page, 0, result.Image.Height).Pixels, result.Image.Pixels);
    }
}
