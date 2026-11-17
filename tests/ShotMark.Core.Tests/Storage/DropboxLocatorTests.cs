using ShotMark.Core.Storage;
namespace ShotMark.Core.Tests.Storage;

public class DropboxLocatorTests
{
    private static string Info(TempDir dir, string name, string json)
    {
        var path = dir.File(name);
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public void Prefers_personal_account()
    {
        using var dir = new TempDir();
        var personal = Directory.CreateDirectory(dir.File("Dropbox")).FullName;
        var business = Directory.CreateDirectory(dir.File("Dropbox (Firma)")).FullName;
        var info = Info(dir, "info.json", $$$"""{"personal":{"path":{{{System.Text.Json.JsonSerializer.Serialize(personal)}}}},"business":{"path":{{{System.Text.Json.JsonSerializer.Serialize(business)}}}}}""");
        Assert.Equal(personal, DropboxLocator.FindRoot([info]));
    }

    [Fact]
    public void Uses_business_when_no_personal()
    {
        using var dir = new TempDir();
        var business = Directory.CreateDirectory(dir.File("Dropbox (Firma)")).FullName;
        var info = Info(dir, "info.json", $$$"""{"business":{"path":{{{System.Text.Json.JsonSerializer.Serialize(business)}}}}}""");
        Assert.Equal(business, DropboxLocator.FindRoot([info]));
    }

    [Fact]
    public void Skips_missing_corrupt_and_nonexistent_paths()
    {
        using var dir = new TempDir();
        var corrupt = Info(dir, "corrupt.json", "{nope");
        var wrongShape = Info(dir, "array.json", "[1,2]");
        var gone = Info(dir, "gone.json", """{"personal":{"path":"Z:\\gibt-es-nicht"}}""");
        Assert.Null(DropboxLocator.FindRoot([dir.File("missing.json"), corrupt, wrongShape, gone]));
    }

    [Fact]
    public void Resolver_prefers_configured_then_dropbox_then_pictures()
    {
        Assert.Equal(new SaveFolderResolver.Result(@"X:\Eigen", false), SaveFolderResolver.Resolve(@"X:\Eigen", @"C:\Dropbox", @"C:\Bilder"));
        Assert.Equal(new SaveFolderResolver.Result(@"C:\Dropbox\Screenshots", false), SaveFolderResolver.Resolve(null, @"C:\Dropbox", @"C:\Bilder"));
        Assert.Equal(new SaveFolderResolver.Result(@"C:\Bilder\ShotMark", true), SaveFolderResolver.Resolve("  ", null, @"C:\Bilder"));
    }
}
