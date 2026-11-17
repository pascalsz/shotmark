using ShotMark.App.Localization;
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
            ? Loc.Format(Strings.FirstRun_NoDropbox, captureFolder)
            : Loc.Format(Strings.FirstRun_Folder, captureFolder);
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
        OkButton.Content = Strings.FirstRun_Start;
        var name = NameBox.Text.Trim();
        OkButton.IsEnabled = name.Length > 0;
        Preview.Text = name.Length > 0
            ? Loc.Format(Strings.FirstRun_NamePreview, SettingsWindow.ExampleFileName(name))
            : Strings.FirstRun_EnterName;
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        var name = NameBox.Text.Trim();
        if (name.Length == 0) return;
        if (!_warned && PcRegistry.FindConflict(_captureFolder, PcName.ToSlug(name), _machineName) is { } other)
        {
            Warning.Text = Loc.Format(Strings.FirstRun_NameTaken, other.DisplayName, other.MachineName);
            Warning.Visibility = Visibility.Visible;
            OkButton.Content = Strings.FirstRun_UseAnyway;
            _warned = true;
            return;
        }
        Result = name;
        DialogResult = true;
    }
}
