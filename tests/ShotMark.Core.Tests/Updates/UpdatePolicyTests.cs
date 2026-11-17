using ShotMark.Core.Updates;
namespace ShotMark.Core.Tests.Updates;

public class UpdatePolicyTests
{
    private static readonly UpdateActivity Quiet = new(
        Capturing: false, EditorOpen: false, UnsavedChanges: false, DialogOpen: false, UserIdle: TimeSpan.FromMinutes(10));

    [Fact]
    public void Restarts_when_nothing_is_open_and_the_user_is_away()
    {
        Assert.True(UpdatePolicy.CanRestartNow(Quiet));
    }

    [Fact]
    public void Waits_while_the_user_is_active()
    {
        Assert.False(UpdatePolicy.CanRestartNow(Quiet with { UserIdle = TimeSpan.FromSeconds(30) }));
        Assert.True(UpdatePolicy.CanRestartNow(Quiet with { UserIdle = UpdatePolicy.RequiredIdle }));
    }

    [Fact]
    public void Never_interrupts_a_capture()
    {
        Assert.False(UpdatePolicy.CanRestartNow(Quiet with { Capturing = true }));
    }

    [Fact]
    public void Never_closes_an_open_editor_or_dialog()
    {
        Assert.False(UpdatePolicy.CanRestartNow(Quiet with { EditorOpen = true }));
        Assert.False(UpdatePolicy.CanRestartNow(Quiet with { DialogOpen = true }));
    }

    [Fact]
    public void Never_restarts_with_unsaved_changes()
    {
        Assert.False(UpdatePolicy.CanRestartNow(Quiet with { UnsavedChanges = true }));
    }

    [Fact]
    public void Checks_soon_after_start_and_then_regularly()
    {
        // Beim Windows-Login steht das Netz oft noch nicht – deshalb nicht sofort prüfen
        Assert.InRange(UpdatePolicy.FirstCheckDelay, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
        Assert.InRange(UpdatePolicy.CheckInterval, TimeSpan.FromHours(1), TimeSpan.FromHours(24));
    }
}
