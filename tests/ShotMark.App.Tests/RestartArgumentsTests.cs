namespace ShotMark.App.Tests;

public class RestartArgumentsTests
{
    [Fact]
    public void Build_keeps_arguments_and_adds_the_old_process()
    {
        Assert.Equal(["--profile", @"C:\p", "--wait-for", "42"], RestartArguments.Build(["--profile", @"C:\p"], 42));
    }

    [Fact]
    public void Build_drops_one_shot_actions()
    {
        // Wer per Verknüpfung mit --capture gestartet hat, soll nach dem Sprachwechsel kein Aufnahme-Overlay bekommen
        Assert.Equal(["--profile", "p", "--wait-for", "42"], RestartArguments.Build(["--capture", "--profile", "p", "--open-editor", "--exit"], 42));
    }

    [Fact]
    public void Build_replaces_an_earlier_wait()
    {
        Assert.Equal(["--wait-for", "42"], RestartArguments.Build(["--wait-for", "7"], 42));
    }

    [Theory]
    [InlineData(new[] { "--wait-for", "42" }, 42)]
    [InlineData(new[] { "--wait-for", "x" }, null)]
    [InlineData(new[] { "--wait-for" }, null)]
    [InlineData(new string[0], null)]
    public void WaitFor_reads_the_process_id(string[] args, int? expected)
    {
        Assert.Equal(expected, RestartArguments.WaitFor(args));
    }
}
