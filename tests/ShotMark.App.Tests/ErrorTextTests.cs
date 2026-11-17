using ShotMark.App.Localization;
using ShotMark.Core.Storage;
namespace ShotMark.App.Tests;

public class ErrorTextTests
{
    [Fact]
    public void Newer_version_warning_is_translated()
    {
        using (new CultureScope("de-DE"))
            Assert.Contains("neueren ShotMark-Version", ErrorText.Describe(LoadWarning.NewerVersion));
        using (new CultureScope("en-US"))
            Assert.Contains("newer version of ShotMark", ErrorText.Describe(LoadWarning.NewerVersion));
    }

    [Fact]
    public void Raw_error_text_only_appears_as_a_details_line()
    {
        Assert.Equal("X\nDetails: Zugriff verweigert", ErrorText.WithDetails("X", "Zugriff verweigert"));
        Assert.Equal("X", ErrorText.WithDetails("X", null));
        Assert.Equal("X", ErrorText.WithDetails("X", " "));
    }

    [Fact]
    public void Save_failure_names_the_folder_and_keeps_details_apart()
    {
        using var _ = new CultureScope("en-US");
        var text = ErrorText.SaveFailed(new SaveOutcome.Failed(StorageError.FolderUnavailable, "Access is denied."), @"D:\Shots");
        Assert.Contains(@"D:\Shots", text);
        Assert.EndsWith("\nDetails: Access is denied.", text);
    }
}
