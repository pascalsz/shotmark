using System.IO;
using ShotMark.App.Native;

namespace ShotMark.App.Tests;

public sealed class AutostartTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "shotmark-autostart-" + Guid.NewGuid().ToString("N"));
    private readonly string _exe;

    public AutostartTests()
    {
        // Aufbau einer Velopack-Installation: Starter im Stammordner, Programm in current\
        Directory.CreateDirectory(Path.Combine(_root, "current"));
        _exe = Path.Combine(_root, "current", "ShotMark.exe");
        File.WriteAllText(_exe, "");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Uses_the_velopack_launcher_when_it_exists()
    {
        var launcher = Path.Combine(_root, "ShotMark.exe");
        File.WriteAllText(launcher, "");
        Assert.Equal(launcher, Autostart.LaunchTarget(_exe, _root));
    }

    [Fact]
    public void Falls_back_to_the_running_exe_without_launcher()
    {
        Assert.Equal(_exe, Autostart.LaunchTarget(_exe, _root));
    }

    [Fact]
    public void Install_root_is_the_folder_above_current()
    {
        Assert.Equal(_root, Autostart.InstallRoot(_exe));
    }

    [Fact]
    public void Exe_outside_current_has_no_install_root()
    {
        Assert.Null(Autostart.InstallRoot(Path.Combine(_root, "bin", "ShotMark.exe")));
    }

    [Fact]
    public void Falls_back_to_the_running_exe_without_install_root()
    {
        Assert.Equal(_exe, Autostart.LaunchTarget(_exe, null));
    }
}
