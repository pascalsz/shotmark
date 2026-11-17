using ShotMark.Core.Storage;
namespace ShotMark.Core.Tests.Storage;

public class PcRegistryTests
{
    [Fact]
    public void Registers_and_loads_display_names()
    {
        using var dir = new TempDir();
        PcRegistry.Register(dir.Path, new PcInfo("Buero", "Büro", "DESKTOP-OFFICE"));
        PcRegistry.Register(dir.Path, new PcInfo("Laptop", "Laptop", "LAP-1"));
        var all = PcRegistry.LoadAll(dir.Path);
        Assert.Equal("Büro", all["buero"].DisplayName);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Detects_same_name_on_different_machine()
    {
        using var dir = new TempDir();
        PcRegistry.Register(dir.Path, new PcInfo("Laptop", "Laptop", "LAP-1"));
        Assert.NotNull(PcRegistry.FindConflict(dir.Path, "Laptop", "LAP-2"));
        Assert.Null(PcRegistry.FindConflict(dir.Path, "Laptop", "LAP-1"));
        Assert.Null(PcRegistry.FindConflict(dir.Path, "Buero", "LAP-2"));
    }

    [Fact]
    public void Ignores_broken_registry_files()
    {
        using var dir = new TempDir();
        Directory.CreateDirectory(Path.Combine(dir.Path, ".shotmark", "pcs"));
        File.WriteAllText(Path.Combine(dir.Path, ".shotmark", "pcs", "kaputt.json"), "{{{");
        Assert.Empty(PcRegistry.LoadAll(dir.Path));
    }
}
