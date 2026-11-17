using System.Collections;
using System.Globalization;
using System.Resources;
using ShotMark.Core.Localization;
namespace ShotMark.Core.Tests.Localization;

public class ResourceCompletenessTests
{
    [Fact]
    public void German_translates_every_core_text() => AssertComplete(CoreStrings.ResourceManager);

    /// <summary>Jeder englische Text hat eine deutsche Übersetzung, kein Text ist leer.</summary>
    internal static void AssertComplete(ResourceManager resources)
    {
        var english = Entries(resources.GetResourceSet(CultureInfo.InvariantCulture, true, true)!);
        var german = Entries(resources.GetResourceSet(new CultureInfo("de"), true, false)
                             ?? throw new Xunit.Sdk.XunitException("Deutsche Ressourcen fehlen"));
        Assert.NotEmpty(english);
        Assert.Empty(english.Keys.Except(german.Keys));
        Assert.Empty(german.Keys.Except(english.Keys));
        Assert.All(english.Concat(german), e => Assert.False(string.IsNullOrWhiteSpace(e.Value), $"„{e.Key}“ ist leer"));
    }

    private static Dictionary<string, string?> Entries(ResourceSet set) =>
        set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => e.Value as string);
}
