using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace ShotMark.Core.Storage;

public static partial class PcName
{
    public const int MaxSlugLength = 32;

    public static string ToSlug(string displayName)
    {
        var expanded = new StringBuilder();
        foreach (var ch in displayName.Trim())
        {
            expanded.Append(ch switch
            {
                'ä' => "ae", 'ö' => "oe", 'ü' => "ue", 'ß' => "ss",
                'Ä' => "Ae", 'Ö' => "Oe", 'Ü' => "Ue",
                _ => ch.ToString(),
            });
        }

        var result = new StringBuilder();
        foreach (var ch in expanded.ToString().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9') result.Append(ch);
            else if (ch is ' ' or '-' or '_' or '.') result.Append('-');
        }

        var slug = MultiDash().Replace(result.ToString(), "-").Trim('-');
        if (slug.Length > MaxSlugLength) slug = slug[..MaxSlugLength].TrimEnd('-');
        return slug.Length == 0 ? "PC" : slug;
    }

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiDash();
}
