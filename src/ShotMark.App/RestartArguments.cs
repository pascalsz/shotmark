using System.Globalization;

namespace ShotMark.App;

/// <summary>
/// Neustart nach einem Sprachwechsel: Der neue Prozess bekommt dieselben Argumente (z. B. <c>--profile</c>) und
/// <c>--wait-for &lt;pid&gt;</c>, damit er nicht an der Einzelinstanz-Sperre der alten, noch endenden Instanz scheitert.
/// </summary>
internal static class RestartArguments
{
    private const string WaitForOption = "--wait-for";

    /// <summary>Einmalige Aktionen des ursprünglichen Starts – nach dem Sprachwechsel sollen sie nicht erneut ausgelöst werden.</summary>
    private static readonly string[] OneShotOptions = ["--capture", "--open-editor", "--exit"];

    public static string[] Build(string[] args, int pid)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], WaitForOption, StringComparison.OrdinalIgnoreCase))
            {
                i++; // Wert der alten Option überspringen
                continue;
            }
            if (OneShotOptions.Contains(args[i], StringComparer.OrdinalIgnoreCase)) continue;
            result.Add(args[i]);
        }
        result.Add(WaitForOption);
        result.Add(pid.ToString(CultureInfo.InvariantCulture));
        return [.. result];
    }

    public static int? WaitFor(string[] args)
    {
        var index = Array.FindIndex(args, a => string.Equals(a, WaitForOption, StringComparison.OrdinalIgnoreCase));
        return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], NumberStyles.None, CultureInfo.InvariantCulture, out var pid)
            ? pid
            : null;
    }
}
