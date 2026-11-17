using ShotMark.Core.Storage;
namespace ShotMark.Core.Tests.Storage;

public class PcNameTests
{
    [Theory]
    [InlineData("Büro", "Buero")]
    [InlineData("Laptop Küche", "Laptop-Kueche")]
    [InlineData("DESKTOP-Office", "DESKTOP-Office")]
    [InlineData("Ärger_Ölß", "Aerger-Oelss")]
    [InlineData("Café", "Cafe")]
    [InlineData("a/b\\c:d", "abcd")]
    [InlineData("  --PC  2-- ", "PC-2")]
    [InlineData("   ", "PC")]
    [InlineData("Ein sehr sehr langer Name für einen Rechner", "Ein-sehr-sehr-langer-Name-fuer-e")]
    public void ToSlug(string input, string expected) => Assert.Equal(expected, PcName.ToSlug(input));
}
