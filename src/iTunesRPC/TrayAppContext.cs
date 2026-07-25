using System.Diagnostics;

namespace iTunesRPC;

public sealed class TrayAppContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly PresenceEngine _engine;
    private readonly string _configPath;
    private readonly string _logPath;
    private SettingsForm? _settingsForm;

    public TrayAppContext(AppConfig config, string configPath)
    {
        _configPath = configPath;
        _logPath = Path.Combine(AppContext.BaseDirectory, "itunes-rpc.log");

        _engine = new PresenceEngine(config);
        _engine.StatusChanged += OnStatusChanged;

        _statusItem = new ToolStripMenuItem("Starting...") { Enabled = false };
        var settingsItem = new ToolStripMenuItem("Settings...");
        settingsItem.Click += (_, _) => OpenSettings();
        var logItem = new ToolStripMenuItem("Open log");
        logItem.Click += (_, _) => OpenLog();
        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += async (_, _) => await ExitAsync();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(settingsItem);
        menu.Items.Add(logItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new NotifyIcon
        {
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            Text = "iTunes-RPC",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => OpenSettings();

        _engine.Start();

        bool needsSetup = string.IsNullOrWhiteSpace(config.ClientId) || config.ClientId == "YOUR_DISCORD_CLIENT_ID_HERE";
        if (needsSetup)
        {
            OpenSettings();
        }
    }

    private void OnStatusChanged()
    {
        string status = _engine.Status;
        _statusItem.Text = status.Length > 60 ? status[..60] : status;

        string tooltip = "iTunes-RPC: " + status;
        _trayIcon.Text = tooltip.Length > 63 ? tooltip[..63] : tooltip;
    }

    private void OpenSettings()
    {
        if (_settingsForm is { IsDisposed: false })
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
            {
                _settingsForm.WindowState = FormWindowState.Normal;
            }
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_engine.Config, _configPath);
        _settingsForm.ConfigSaved += config => _engine.UpdateConfig(config);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
    }

    private void OpenLog()
    {
        if (File.Exists(_logPath))
        {
            Process.Start(new ProcessStartInfo(_logPath) { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show("No log file yet.", "iTunes-RPC");
        }
    }

    private async Task ExitAsync()
    {
        _trayIcon.Visible = false;
        _settingsForm?.Close();
        await _engine.StopAsync();
        _engine.Dispose();
        Application.Exit();
    }
}
