using ShotMark.Core.Storage;
namespace ShotMark.Core.Tests.Storage;

public class AtomicFileTests
{
    [Fact]
    public void Writes_and_replaces_content_without_leaving_temp_files()
    {
        using var dir = new TempDir();
        var path = dir.File("a.png");
        AtomicFile.Write(path, [1, 2, 3]);
        AtomicFile.Write(path, [4, 5]);
        Assert.Equal(new byte[] { 4, 5 }, File.ReadAllBytes(path));
        Assert.Single(Directory.GetFiles(dir.Path));
    }

    [Fact]
    public void Retries_then_throws_when_target_stays_locked()
    {
        using var dir = new TempDir();
        var path = dir.File("locked.png");
        File.WriteAllBytes(path, [0]);
        var sleeps = 0;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() => AtomicFile.Write(path, [9], attempts: 3, sleep: _ => sleeps++));
        }
        Assert.Equal(2, sleeps);
        Assert.Single(Directory.GetFiles(dir.Path));
    }
}
