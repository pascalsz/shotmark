using System.Drawing;
using System.Windows.Forms;

namespace ShotMark.App.Tray;

internal interface ITrayCommands
{
    void StartCaptureFromMenu();
    void StartScrollCaptureFromMenu();
    void OpenEditor();
    void OpenFolder();
    void ShowSettings();
    void Exit();
}

/// <summary>Symbol im Infobereich der Taskleiste mit Kontextmenü.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _captureItem;
    private readonly ToolStripMenuItem _scrollItem;
    private Action? _balloonAction;

    public TrayIcon(ITrayCommands commands, string hotkeyLabel, string? scrollHotkeyLabel)
    {
        var menu = new ContextMenuStrip { ShowImageMargin = false };
        _captureItem = new ToolStripMenuItem("", null, (_, _) => commands.StartCaptureFromMenu());
        _captureItem.Font = new Font(_captureItem.Font, System.Drawing.FontStyle.Bold);
        menu.Items.Add(_captureItem);
        _scrollItem = new ToolStripMenuItem("", null, (_, _) => commands.StartScrollCaptureFromMenu());
        menu.Items.Add(_scrollItem);
        menu.Items.Add("Editor öffnen", null, (_, _) => commands.OpenEditor());
        menu.Items.Add("Screenshot-Ordner öffnen", null, (_, _) => commands.OpenFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Einstellungen …", null, (_, _) => commands.ShowSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("ShotMark beenden", null, (_, _) => commands.Exit());

        _icon = new NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "ShotMark",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.BalloonTipClicked += (_, _) => _balloonAction?.Invoke();
        _icon.BalloonTipClosed += (_, _) => _balloonAction = null;
        _icon.MouseDoubleClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) commands.StartCaptureFromMenu();
        };
        SetHotkeyLabels(hotkeyLabel, scrollHotkeyLabel);
    }

    public void SetHotkeyLabels(string label, string? scrollLabel)
    {
        _captureItem.Text = $"Neue Aufnahme   ({label})";
        _scrollItem.Text = scrollLabel is null ? "Scroll-Aufnahme" : $"Scroll-Aufnahme   ({scrollLabel})";
        _icon.Text = $"ShotMark – {label} für Screenshot";
    }

    public void ShowInfo(string title, string text)
    {
        _balloonAction = null;
        _icon.ShowBalloonTip(6000, title, text, ToolTipIcon.Info);
    }

    /// <param name="onClick">Optional: wird ausgeführt, wenn die Meldung angeklickt wird.</param>
    public void ShowWarning(string text, Action? onClick = null)
    {
        _balloonAction = onClick;
        _icon.ShowBalloonTip(10000, "ShotMark", text, ToolTipIcon.Warning);
    }

    private static Icon LoadIcon()
    {
        var resource = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/ShotMark;component/Assets/ShotMark.ico"))
                       ?? throw new InvalidOperationException("Symbol fehlt");
        using var stream = resource.Stream;
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
