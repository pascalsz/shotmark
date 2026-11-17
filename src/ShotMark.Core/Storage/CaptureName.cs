using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
namespace ShotMark.Core.Storage;

public sealed partial record CaptureName(DateTime Timestamp, string PcSlug, int Sequence = 1)
{
    private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

    public string BaseName =>
        Timestamp.ToString(TimestampFormat, CultureInfo.InvariantCulture) + "_" + PcSlug + (Sequence > 1 ? "_" + Sequence : "");

    public static bool TryParse(string fileNameWithoutExtension, [NotNullWhen(true)] out CaptureName? name)
    {
        name = null;
        var match = Pattern().Match(fileNameWithoutExtension);
        if (!match.Success) return false;
        if (!DateTime.TryParseExact(match.Groups["ts"].Value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            return false;
        var sequence = match.Groups["n"].Success ? int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) : 1;
        name = new CaptureName(timestamp, match.Groups["pc"].Value, Math.Max(1, sequence));
        return true;
    }

    [GeneratedRegex(@"^(?<ts>\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})_(?<pc>[A-Za-z0-9-]+)(?:_(?<n>\d{1,4}))?$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
