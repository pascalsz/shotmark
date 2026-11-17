using System.Globalization;
using ShotMark.Core.Storage;

namespace ShotMark.App.Localization;

/// <summary>
/// Übersetzte Fehlermeldungen. Der Rohtext einer Ausnahme (Sprache von Windows) erscheint nie als eigener Satz,
/// sondern höchstens als abgesetzte Zeile „Details: …“.
/// </summary>
internal static class ErrorText
{
    public static string Describe(LoadWarning warning) => warning switch
    {
        LoadWarning.NewerVersion => Strings.WarningNewerVersion,
        _ => Strings.WarningProjectUnreadable,
    };

    /// <param name="followUp">Optional ein weiterer Satz, z. B. wohin die Aufnahme stattdessen gesichert wurde.</param>
    public static string SaveFailed(SaveOutcome.Failed failed, string folder, string? followUp = null)
    {
        var sentence = failed.Error switch
        {
            StorageError.FolderUnavailable => Format(Strings.SaveFolderUnavailable, folder),
            StorageError.NewerVersion => Strings.SaveNewerVersion,
            _ => Format(Strings.SaveWriteFailed, folder),
        };
        return WithDetails(followUp is null ? sentence : sentence + " " + followUp, failed.Detail);
    }

    public static string WithDetails(string message, string? detail) =>
        string.IsNullOrWhiteSpace(detail) ? message : message + "\n" + Format(Strings.ErrorDetails, detail.Trim());

    private static string Format(string format, params object?[] args) => Loc.Format(format, args);
}
