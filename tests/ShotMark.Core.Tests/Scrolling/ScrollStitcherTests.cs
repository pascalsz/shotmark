using ShotMark.Core.Scrolling;
namespace ShotMark.Core.Tests.Scrolling;

public class ScrollStitcherTests
{
    private const int W = 320;
    private const int H = 400;

    private static PixelFrame Stitch(IEnumerable<PixelFrame> frames, StitchOptions? options = null)
    {
        var stitcher = new ScrollStitcher(options);
        foreach (var frame in frames) stitcher.Add(frame);
        return stitcher.Build();
    }

    [Fact]
    public void Even_steps_reproduce_the_page_exactly()
    {
        var page = SyntheticPage.Page(W, 1600);
        var offsets = new[] { 0, 200, 400, 600, 800, 1000, 1200 };
        var result = Stitch(offsets.Select(o => SyntheticPage.Viewport(page, o, H)));
        Assert.Equal(page.Height, result.Height);
        Assert.Equal(page.Pixels, result.Pixels);
    }

    [Fact]
    public void Uneven_steps_reproduce_the_page_exactly()
    {
        var page = SyntheticPage.Page(W, 1500, seed: 7);
        var offsets = new[] { 0, 77, 290, 291, 500, 733, 1001, 1100 };
        var result = Stitch(offsets.Select(o => SyntheticPage.Viewport(page, o, H)));
        Assert.Equal(1100 + H, result.Height);
        Assert.Equal(SyntheticPage.Rows(page, 0, 1100 + H).Pixels, result.Pixels);
    }

    [Fact]
    public void Small_pixel_noise_is_tolerated()
    {
        var page = SyntheticPage.Page(W, 1200, seed: 3);
        var offsets = new[] { 0, 180, 360, 540, 720, 800 };
        var frames = offsets.Select((o, i) => SyntheticPage.Viewport(page, o, H, noise: 3, noiseSeed: i));
        var result = Stitch(frames);
        Assert.Equal(page.Height, result.Height);
        Assert.True(SyntheticPage.MaxDifference(page, result) <= 3);
    }

    [Fact]
    public void Sticky_header_and_footer_appear_once()
    {
        var page = SyntheticPage.Page(W, 1300, seed: 5);
        var header = SyntheticPage.Solid(W, 48, 40, 90, 200);
        var footer = SyntheticPage.Solid(W, 30, 30, 30, 30);
        var content = H - 48 - 30;
        var offsets = new[] { 0, 150, 300, 450, 600, 750, 900, 1300 - content };
        var result = Stitch(offsets.Select(o => SyntheticPage.Viewport(page, o, H, header, footer)));
        var expected = SyntheticPage.Stack(header, page, footer);
        Assert.Equal(expected.Height, result.Height);
        Assert.Equal(expected.Pixels, result.Pixels);
    }

    [Fact]
    public void Scrollbar_column_is_cropped()
    {
        var page = SyntheticPage.Page(W, 1400, seed: 9);
        var offsets = new[] { 0, 200, 400, 600, 800, 1000 };
        var result = Stitch(offsets.Select(o => SyntheticPage.Viewport(page, o, H, scrollbarWidth: 14)));
        Assert.InRange(result.Width, W - 16, W - 14);
        Assert.Equal(page.Height, result.Height);
        Assert.Equal(SyntheticPage.Rows(page, 0, page.Height, result.Width).Pixels, result.Pixels);
    }

    [Fact]
    public void Identical_frames_are_reported_as_no_movement()
    {
        var page = SyntheticPage.Page(W, 800);
        var stitcher = new ScrollStitcher();
        Assert.Equal(StitchStatus.First, stitcher.Add(SyntheticPage.Viewport(page, 0, H)).Status);
        Assert.Equal(StitchStatus.NoMovement, stitcher.Add(SyntheticPage.Viewport(page, 0, H)).Status);
        Assert.Equal(H, stitcher.Height);
    }

    [Fact]
    public void Unrelated_frame_is_rejected_and_stitching_continues()
    {
        var page = SyntheticPage.Page(W, 1000, seed: 2);
        var other = SyntheticPage.Page(W, 1000, seed: 99);
        var stitcher = new ScrollStitcher();
        stitcher.Add(SyntheticPage.Viewport(page, 0, H));
        var rejected = stitcher.Add(SyntheticPage.Viewport(other, 300, H));
        Assert.Equal(StitchStatus.NoMatch, rejected.Status);
        var accepted = stitcher.Add(SyntheticPage.Viewport(page, 200, H));
        Assert.Equal(StitchStatus.Appended, accepted.Status);
        Assert.Equal(200, accepted.Shift);
        Assert.Equal(H + 200, stitcher.Height);
    }

    [Fact]
    public void Shift_is_reported_for_learning_the_step_size()
    {
        var page = SyntheticPage.Page(W, 1000, seed: 4);
        var stitcher = new ScrollStitcher();
        stitcher.Add(SyntheticPage.Viewport(page, 0, H));
        var result = stitcher.Add(SyntheticPage.Viewport(page, 123, H));
        Assert.Equal(StitchStatus.Appended, result.Status);
        Assert.Equal(123, result.Shift);
        Assert.Equal(123, result.AddedRows);
    }

    [Fact]
    public void Very_narrow_regions_do_not_crash()
    {
        var page = SyntheticPage.Page(20, 900, seed: 6);
        var result = Stitch(new[] { 0, 150, 300 }.Select(o => SyntheticPage.Viewport(page, o, H)));
        Assert.Equal(300 + H, result.Height);
    }

    [Fact]
    public void Floating_element_does_not_break_stitching()
    {
        // z. B. Chat-Knopf: bleibt beim Scrollen an derselben Bildschirmstelle
        var page = SyntheticPage.Page(W, 1600, seed: 11);
        var bubble = SyntheticPage.Solid(48, 48, 200, 60, 20);
        var offsets = new[] { 0, 200, 400, 600, 800, 1000, 1200 };
        var result = Stitch(offsets.Select(o => SyntheticPage.Overlay(SyntheticPage.Viewport(page, o, H), bubble, 220, 300)));
        Assert.Equal(page.Height, result.Height);
        // Außerhalb der Spalten des Knopfs ist das Ergebnis exakt die Seite
        for (var y = 0; y < page.Height; y++)
        {
            Assert.True(page.Row(y)[..(220 * 4)].SequenceEqual(result.Row(y)[..(220 * 4)]), $"Zeile {y} links");
            Assert.True(page.Row(y)[(268 * 4)..].SequenceEqual(result.Row(y)[(268 * 4)..]), $"Zeile {y} rechts");
        }
    }

    [Fact]
    public void Header_that_appears_after_the_first_scroll_is_tolerated()
    {
        // Kopfzeile ist oben transparent und wird nach dem ersten Scrollen undurchsichtig
        var page = SyntheticPage.Page(W, 1500, seed: 12);
        var header = SyntheticPage.Solid(W, 56, 50, 50, 50);
        var offsets = new[] { 0, 100, 300, 500, 700, 900, 1100 };
        var frames = offsets.Select((o, i) => i == 0
            ? SyntheticPage.Viewport(page, o, H)
            : SyntheticPage.Overlay(SyntheticPage.Viewport(page, o, H), header, 0, 0));
        var result = Stitch(frames);
        Assert.Equal(page.Height, result.Height);
        Assert.Equal(page.Pixels, result.Pixels);
    }

    [Fact]
    public void Rebase_keeps_newer_content_at_the_same_position()
    {
        // Bild lädt nach: gleiche Scrollposition, aber ein Block hat sich verändert
        var page = SyntheticPage.Page(W, 1200, seed: 13);
        var stitcher = new ScrollStitcher();
        stitcher.Add(SyntheticPage.Viewport(page, 0, H));
        Assert.Equal(StitchStatus.Appended, stitcher.Add(SyntheticPage.Viewport(page, 200, H)).Status);
        var loaded = SyntheticPage.Overlay(SyntheticPage.Viewport(page, 200, H), SyntheticPage.Solid(W, 110, 10, 160, 10), 0, 240);
        Assert.Equal(StitchStatus.NoMatch, stitcher.Add(loaded).Status);
        Assert.True(stitcher.TryRebase(loaded));
        var result = stitcher.Build();
        Assert.Equal(200 + H, result.Height);
        Assert.True(loaded.Row(260).SequenceEqual(result.Row(200 + 260)));
        Assert.True(page.Row(100).SequenceEqual(result.Row(100)));
    }

    [Fact]
    public void Rebase_refuses_an_unrelated_frame()
    {
        var page = SyntheticPage.Page(W, 1200, seed: 14);
        var stitcher = new ScrollStitcher();
        stitcher.Add(SyntheticPage.Viewport(page, 0, H));
        Assert.False(stitcher.TryRebase(SyntheticPage.Viewport(SyntheticPage.Page(W, 1200, seed: 77), 300, H)));
    }

    [Fact]
    public void Single_frame_builds_itself()
    {
        var page = SyntheticPage.Page(W, 500);
        var frame = SyntheticPage.Viewport(page, 0, H);
        Assert.Equal(frame.Pixels, Stitch([frame]).Pixels);
    }
}
