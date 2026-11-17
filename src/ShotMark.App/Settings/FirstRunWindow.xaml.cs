using System.Windows;
using System.Windows.Controls;
using ShotMark.Core.Storage;

namespace ShotMark.App.Settings;

internal partial class FirstRunWindow : Window
{
    private readonly string _captureFolder;
    private readonly string _machineName;
    private bool _warned;

    internal FirstRunWindow(string suggestedName, string captureFolder, bool isFallbackFolder, string machineName)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Native.NativeMethods.UseDarkTitleBar(this);
        _captureFolder = captureFolder;
        _machineName = machineName;
        FolderInfo.Text = isFallbackFolder
            ? $"Dropbox wurde auf diesem PC nicht gefunden. Screenshots landen vorerst in:\n{captureFolder}\nDas lässt sich später in den Einstellungen ändern."
            : $"Screenshots werden automatisch hier gespeichert:\n{captureFolder}";
        NameBox.Text = suggestedName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    public string? Result { get; private set; }

    public static string? Ask(string suggestedName, string captureFolder, bool isFallbackFolder, string machineName)
    {
        var window = new FirstRunWindow(suggestedName, captureFolder, isFallbackFolder, machineName);
        return window.ShowDialog() == true ? window.Result : null;
    }

    private void OnNameChanged(object sender, TextChangedEventArgs e)
    {
        _warned = false;
        Warning.Visibility = Visibility.Collapsed;
        OkButton.Content = "Los geht’s";
        var name = NameBox.Text.Trim();
        OkButton.IsEnabled = name.Length > 0;
        Preview.Text = name.Length > 0
            ? $"Dateinamen sehen dann so aus: {DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{PcName.ToSlug(name)}.png"
            : "Bitte einen Namen eingeben, z. B. „Büro“ oder „Laptop“.";
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) return;
        if (!_warned && PcRegistry.FindConflict(_captureFolder, PcName.ToSlug(name), _machineName) is { } other)
        {
            Warning.Text = $"Der Name „{other.DisplayName}“ wird schon vom PC „{other.MachineName}“ verwendet. Bitte wähle einen anderen Namen, damit sich die Aufnahmen nicht vermischen.";
            Warning.Visibility = Visibility.Visible;
            OkButton.Content = "Trotzdem verwenden";
            _warned = true;
            return;
        }
        Result = name;
        DialogResult = true;
    }
}
