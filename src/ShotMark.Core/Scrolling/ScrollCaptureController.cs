namespace ShotMark.Core.Scrolling;

public interface IFrameSource
{
    PixelFrame Capture();
}

public interface IScroller
{
    void ScrollDown(int notches);

    void ScrollUp(int notches);
}

public interface IUserActivity
{
    /// <summary>Der Nutzer hat selbst Maus oder Mausrad benutzt.</summary>
    bool UserTookOver { get; }
}

public interface IDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken token);
}

public sealed class TaskDelay : IDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
}

public enum ScrollEndReason { PageEnd, Stopped, MaxSize, ContentChanged, Error }

public sealed record ScrollProgress(int Height, bool Manual, string? Hint);

public sealed record ScrollCaptureResult(PixelFrame Image, ScrollEndReason Reason, bool WasManual, Exception? Error = null);

public sealed record ScrollCaptureOptions
{
    /// <summary>16.000 px: unter der Texturgrenze von Grafikkarten, damit der Editor das Bild sicher darstellen kann.</summary>
    public int MaxHeight { get; init; } = 16_000;

    public long MaxPixels { get; init; } = 40_000_000L;

    /// <summary>Eine Raste: Pixel pro Raste sind vorher unbekannt (bis ~150 px bei hoher Skalierung).</summary>
    public int InitialNotches { get; init; } = 1;

    public double TargetStepFraction { get; init; } = 0.6;
    public int MaxNotches { get; init; } = 25;
    public TimeSpan SettleDelay { get; init; } = TimeSpan.FromMilliseconds(120);
    public TimeSpan StablePoll { get; init; } = TimeSpan.FromMilliseconds(60);
    public TimeSpan StableTimeout { get; init; } = TimeSpan.FromSeconds(1);
    public TimeSpan ManualPoll { get; init; } = TimeSpan.FromMilliseconds(160);
    public int PageEndAfterStillSteps { get; init; } = 2;
    public int MaxMismatches { get; init; } = 3;
    public StitchOptions Stitch { get; init; } = new();
}

/// <summary>Ablauf der Scroll-Aufnahme: scrollen, auf ruhiges Bild warten, aufnehmen, zusammensetzen, Ende erkennen.</summary>
public sealed class ScrollCaptureController(
    IFrameSource source, IScroller scroller, IUserActivity activity, IDelay delay, ScrollCaptureOptions? options = null)
{
    public const string HintManual = "Manuell – langsam selbst scrollen, dann Stopp";
    public const string HintNoReaction = "Keine Bewegung – mit dem Mausrad selbst scrollen oder Stopp";

    private readonly ScrollCaptureOptions _o = options ?? new ScrollCaptureOptions();

    public event Action<ScrollProgress>? Progress;

    public async Task<ScrollCaptureResult> RunAsync(CancellationToken stop)
    {
        var stitcher = new ScrollStitcher(_o.Stitch);
        var manual = false;
        string? hint = null;
        var notches = _o.InitialNotches;
        var still = 0;
        var failures = 0;
        var moved = false;

        // Das erste Bild immer vollständig aufnehmen, auch wenn sofort Stopp gedrückt wird
        stitcher.Add(await CaptureStableAsync(CancellationToken.None));
        Report();
        try
        {
            while (true)
            {
                stop.ThrowIfCancellationRequested();
                if (!manual && activity.UserTookOver)
                {
                    manual = true;
                    hint = HintManual;
                }

                if (manual)
                {
                    await delay.DelayAsync(_o.ManualPoll, stop);
                    var result = stitcher.Add(source.Capture());
                    if (result.Status == StitchStatus.Appended) failures = 0;
                    else if (result.Status == StitchStatus.NoMatch && ++failures >= _o.MaxMismatches)
                        return Finish(stitcher, ScrollEndReason.ContentChanged, manual);
                }
                else
                {
                    scroller.ScrollDown(notches);
                    await delay.DelayAsync(_o.SettleDelay, stop);
                    var result = stitcher.Add(await CaptureStableAsync(stop));
                    if (result.Status == StitchStatus.NoMovement)
                    {
                        // Manche Programme reagieren verzögert: noch einmal warten, bevor „keine Bewegung“ zählt
                        await delay.DelayAsync(_o.StableTimeout, stop);
                        result = stitcher.Add(await CaptureStableAsync(stop));
                    }

                    switch (result.Status)
                    {
                        case StitchStatus.Appended:
                            moved = true;
                            still = 0;
                            failures = 0;
                            notches = NextNotches(notches, result.Shift, stitcher.BandHeight);
                            break;
                        case StitchStatus.NoMovement:
                            failures = 0;
                            if (!moved)
                            {
                                manual = true;
                                hint = HintNoReaction;
                            }
                            else if (++still >= _o.PageEndAfterStillSteps)
                            {
                                return Finish(stitcher, ScrollEndReason.PageEnd, manual);
                            }
                            break;
                        case StitchStatus.NoMatch:
                            still = 0;
                            if (++failures >= _o.MaxMismatches) return Finish(stitcher, ScrollEndReason.ContentChanged, manual);
                            notches = await RecoverAsync(stitcher, notches, stop);
                            break;
                    }
                }

                if (stitcher.Height >= _o.MaxHeight || (long)stitcher.Height * stitcher.Width >= _o.MaxPixels)
                    return Finish(stitcher, ScrollEndReason.MaxSize, manual);
                Report();
            }
        }
        catch (OperationCanceledException)
        {
            // Was gerade sichtbar ist, gehört noch dazu – genau dann drückt man meist Stopp
            try
            {
                var last = await CaptureStableAsync(CancellationToken.None);
                if (stitcher.Add(last).Status == StitchStatus.NoMatch) stitcher.TryRebase(last);
            }
            catch (Exception e) when (e is not OutOfMemoryException)
            {
                return Finish(stitcher, ScrollEndReason.Stopped, manual, e);
            }
            return Finish(stitcher, ScrollEndReason.Stopped, manual);
        }
        catch (Exception e) when (e is not OutOfMemoryException)
        {
            return Finish(stitcher, ScrollEndReason.Error, manual, e);
        }

        void Report() => Progress?.Invoke(new ScrollProgress(stitcher.Height, manual, hint));
    }

    /// <summary>
    /// Nach einem unpassenden Teilbild: Schritt zurück, kleinere Schritte. Zeigt das Bild dann dieselbe Stelle mit
    /// verändertem Inhalt (nachgeladene Bilder), wird der neue Stand übernommen.
    /// </summary>
    private async Task<int> RecoverAsync(ScrollStitcher stitcher, int notches, CancellationToken stop)
    {
        scroller.ScrollUp(notches);
        await delay.DelayAsync(_o.SettleDelay, stop);
        var frame = await CaptureStableAsync(stop);
        if (stitcher.Add(frame).Status == StitchStatus.NoMatch) stitcher.TryRebase(frame);
        return Math.Max(1, notches / 2);
    }

    private static ScrollCaptureResult Finish(ScrollStitcher stitcher, ScrollEndReason reason, bool manual, Exception? error = null) =>
        new(stitcher.Build(), reason, manual, error);

    private int NextNotches(int notches, int shift, int bandHeight)
    {
        if (shift <= 0 || bandHeight <= 0) return notches;
        var pixelsPerNotch = shift / (double)notches;
        var target = _o.TargetStepFraction * bandHeight;
        return Math.Clamp((int)Math.Round(target / pixelsPerNotch), 1, _o.MaxNotches);
    }

    /// <summary>Wartet, bis zwei Aufnahmen hintereinander gleich sind (Scroll-Animation vorbei), höchstens StableTimeout.</summary>
    private async Task<PixelFrame> CaptureStableAsync(CancellationToken token)
    {
        var last = source.Capture();
        var polls = Math.Max(1, (int)(_o.StableTimeout.TotalMilliseconds / Math.Max(1, _o.StablePoll.TotalMilliseconds)));
        for (var i = 0; i < polls; i++)
        {
            await delay.DelayAsync(_o.StablePoll, token);
            var next = source.Capture();
            if (next.SameAs(last)) return next;
            last = next;
        }
        return last;
    }
}
