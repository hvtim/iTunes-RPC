using System.Text.Json;

namespace iTunesRPC;

public sealed class SettingsForm : Form
{
    // WinForms' built-in AutoScaleMode misbehaved badly when combined with the
    // app's per-monitor-v2 DPI awareness (clipped/overlapping layout), so this
    // scales every literal coordinate manually against the real DPI instead -
    // more code, but predictable.
    private static readonly float DpiScale = GetDpiScale();

    private static float GetDpiScale()
    {
        using var g = Graphics.FromHwnd(IntPtr.Zero);
        return g.DpiX / 96f;
    }

    private static int S(int value) => (int)Math.Round(value * DpiScale);

    private sealed record MediaSourceOption(string Id, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly TextBox _clientIdBox = new();
    private readonly ComboBox _mediaSourceCombo = new();
    private readonly CheckBox _broadcastBox = new();
    private readonly CheckBox _trackNumberBox = new();

    private readonly RadioButton _artAutoRadio = new();
    private readonly RadioButton _artCustomRadio = new();
    private readonly RadioButton _artOffRadio = new();
    private readonly TextBox _customArtUrlBox = new();
    private readonly TextBox _imageKeyBox = new();

    private readonly NumericUpDown _pollIntervalBox = new();
    private readonly CheckBox _autoLaunchBox = new();
    private readonly Button _saveButton = new();
    private readonly System.Windows.Forms.Timer _savedFeedbackTimer = new() { Interval = 1300 };
    private readonly ToolTip _clientIdTooltip = new();

    private readonly string _configPath;
    private readonly string _currentMediaSource;

    public event Action<AppConfig>? ConfigSaved;

    public SettingsForm(AppConfig config, string configPath)
    {
        _configPath = configPath;
        _currentMediaSource = string.IsNullOrWhiteSpace(config.MediaSource) ? "iTunes" : config.MediaSource;

        Text = "iTunes-RPC Settings";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.None;
        ClientSize = new Size(S(400), S(560));
        Font = new Font("Segoe UI", 9.5f);
        Padding = new Padding(S(12));

        var clientIdLabel = new Label { Text = "Discord Application ID", Left = S(16), Top = S(14), AutoSize = true };
        _clientIdBox.Left = S(16); _clientIdBox.Top = S(36); _clientIdBox.Width = S(196); _clientIdBox.Height = S(24);
        _clientIdBox.PasswordChar = '*';
        _clientIdBox.Text = config.ClientId == "YOUR_DISCORD_CLIENT_ID_HERE" ? "" : config.ClientId;

        const string clientIdTip = "This is a public app identifier, not a secret - masked here only as a convenience, e.g. when screen sharing.";
        _clientIdTooltip.SetToolTip(clientIdLabel, clientIdTip);
        _clientIdTooltip.SetToolTip(_clientIdBox, clientIdTip);

        var showButton = new Button { Text = "Show", Left = S(218), Top = S(35), Width = S(78), Height = S(26) };
        showButton.MouseDown += (_, _) => _clientIdBox.PasswordChar = '\0';
        showButton.MouseUp += (_, _) => _clientIdBox.PasswordChar = '*';
        showButton.MouseLeave += (_, _) => _clientIdBox.PasswordChar = '*';

        var pasteButton = new Button { Text = "Paste", Left = S(302), Top = S(35), Width = S(78), Height = S(26) };
        pasteButton.Click += (_, _) =>
        {
            if (Clipboard.ContainsText())
            {
                _clientIdBox.Text = Clipboard.GetText().Trim();
            }
        };

        var mediaSourceLabel = new Label { Text = "Media source", Left = S(16), Top = S(70), AutoSize = true };
        _mediaSourceCombo.Left = S(16); _mediaSourceCombo.Top = S(92); _mediaSourceCombo.Width = S(280); _mediaSourceCombo.Height = S(24);
        _mediaSourceCombo.DropDownStyle = ComboBoxStyle.DropDownList;
        _mediaSourceCombo.Items.Add(new MediaSourceOption("iTunes", "iTunes"));
        _mediaSourceCombo.SelectedIndex = 0;

        var refreshButton = new Button { Text = "Refresh", Left = S(304), Top = S(91), Width = S(80), Height = S(26) };
        refreshButton.Click += async (_, _) => await PopulateMediaSourcesAsync();

        _broadcastBox.Text = "Broadcast now playing to Discord";
        _broadcastBox.Left = S(16); _broadcastBox.Top = S(128); _broadcastBox.AutoSize = true;
        _broadcastBox.Checked = config.BroadcastEnabled;

        _trackNumberBox.Text = "Show track number (e.g. Track 2 / 11)";
        _trackNumberBox.Left = S(16); _trackNumberBox.Top = S(154); _trackNumberBox.AutoSize = true;
        _trackNumberBox.Checked = config.ShowTrackNumber;

        var artSectionLabel = new Label
        {
            Text = "ALBUM ART",
            Left = S(16), Top = S(190), AutoSize = true,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            ForeColor = Theme.MutedText
        };

        var artPanel = new Panel { Left = S(16), Top = S(212), Width = S(368), Height = S(190), BorderStyle = BorderStyle.FixedSingle };

        _artAutoRadio.Text = "Automatic (look up cover art)";
        _artAutoRadio.Left = S(12); _artAutoRadio.Top = S(12); _artAutoRadio.AutoSize = true;

        _artCustomRadio.Text = "Custom image URL (512x512 recommended)";
        _artCustomRadio.Left = S(12); _artCustomRadio.Top = S(40); _artCustomRadio.AutoSize = true;

        _customArtUrlBox.Left = S(32); _customArtUrlBox.Top = S(66); _customArtUrlBox.Width = S(320); _customArtUrlBox.Height = S(24);
        _customArtUrlBox.Text = config.CustomArtUrl;
        _customArtUrlBox.PlaceholderText = "https://example.com/image.png";

        _artOffRadio.Text = "Static logo only (no lookups)";
        _artOffRadio.Left = S(12); _artOffRadio.Top = S(98); _artOffRadio.AutoSize = true;

        var imageKeyLabel = new Label { Text = "Static logo key (fallback image)", Left = S(12), Top = S(128), AutoSize = true };
        _imageKeyBox.Left = S(12); _imageKeyBox.Top = S(150); _imageKeyBox.Width = S(340); _imageKeyBox.Height = S(24);
        _imageKeyBox.Text = config.LargeImageKey;

        artPanel.Controls.AddRange(new Control[]
        {
            _artAutoRadio, _artCustomRadio, _customArtUrlBox, _artOffRadio, imageKeyLabel, _imageKeyBox
        });

        switch (config.ArtMode)
        {
            case "Custom": _artCustomRadio.Checked = true; break;
            case "Off": _artOffRadio.Checked = true; break;
            default: _artAutoRadio.Checked = true; break;
        }

        _customArtUrlBox.Enabled = _artCustomRadio.Checked;
        _artCustomRadio.CheckedChanged += (_, _) => _customArtUrlBox.Enabled = _artCustomRadio.Checked;

        var pollLabel = new Label { Text = "Poll interval (ms)", Left = S(16), Top = S(420), AutoSize = true };
        _pollIntervalBox.Left = S(16); _pollIntervalBox.Top = S(442); _pollIntervalBox.Width = S(130); _pollIntervalBox.Height = S(24);
        _pollIntervalBox.Minimum = 500; _pollIntervalBox.Maximum = 60000; _pollIntervalBox.Increment = 500;
        _pollIntervalBox.Value = Math.Clamp(config.PollIntervalMs, 500, 60000);

        _autoLaunchBox.Text = "Start automatically when you log in";
        _autoLaunchBox.Left = S(16); _autoLaunchBox.Top = S(482); _autoLaunchBox.AutoSize = true;
        _autoLaunchBox.Checked = AutoLaunch.IsEnabled;

        _saveButton.Text = "Save";
        _saveButton.Left = S(212); _saveButton.Top = S(516); _saveButton.Width = S(84); _saveButton.Height = S(30);
        _saveButton.Click += (_, _) => Save();

        var closeButton = new Button { Text = "Close", Left = S(304), Top = S(516), Width = S(84), Height = S(30) };
        closeButton.Click += (_, _) => Close();

        _savedFeedbackTimer.Tick += (_, _) =>
        {
            _savedFeedbackTimer.Stop();
            _saveButton.Text = "Save";
            _saveButton.Enabled = true;
        };

        Controls.AddRange(new Control[]
        {
            clientIdLabel, _clientIdBox, showButton, pasteButton, mediaSourceLabel, _mediaSourceCombo, refreshButton,
            _broadcastBox, _trackNumberBox, artSectionLabel, artPanel,
            pollLabel, _pollIntervalBox, _autoLaunchBox, _saveButton, closeButton
        });

        AcceptButton = _saveButton;
        CancelButton = closeButton;

        Theme.Apply(this);

        Load += async (_, _) => await PopulateMediaSourcesAsync();
    }

    private async Task PopulateMediaSourcesAsync()
    {
        var smtc = new SmtcMonitor();
        var sources = await smtc.GetAvailableSourcesAsync();

        string selectedId = _mediaSourceCombo.SelectedItem is MediaSourceOption current
            ? current.Id
            : _currentMediaSource;

        _mediaSourceCombo.Items.Clear();
        _mediaSourceCombo.Items.Add(new MediaSourceOption("iTunes", "iTunes"));
        foreach (var (id, displayName) in sources)
        {
            _mediaSourceCombo.Items.Add(new MediaSourceOption(id, displayName));
        }

        for (int i = 0; i < _mediaSourceCombo.Items.Count; i++)
        {
            if (_mediaSourceCombo.Items[i] is MediaSourceOption option && option.Id == selectedId)
            {
                _mediaSourceCombo.SelectedIndex = i;
                return;
            }
        }

        _mediaSourceCombo.SelectedIndex = 0;
    }

    private void Save()
    {
        string artMode = _artCustomRadio.Checked ? "Custom" : _artOffRadio.Checked ? "Off" : "Auto";
        string mediaSource = _mediaSourceCombo.SelectedItem is MediaSourceOption selected ? selected.Id : "iTunes";

        var config = new AppConfig
        {
            ClientId = string.IsNullOrWhiteSpace(_clientIdBox.Text) ? "YOUR_DISCORD_CLIENT_ID_HERE" : _clientIdBox.Text.Trim(),
            LargeImageKey = string.IsNullOrWhiteSpace(_imageKeyBox.Text) ? "logo" : _imageKeyBox.Text.Trim(),
            PollIntervalMs = (int)_pollIntervalBox.Value,
            BroadcastEnabled = _broadcastBox.Checked,
            ShowTrackNumber = _trackNumberBox.Checked,
            ArtMode = artMode,
            CustomArtUrl = _customArtUrlBox.Text.Trim(),
            MediaSource = mediaSource
        };

        File.WriteAllText(_configPath, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        AutoLaunch.SetEnabled(_autoLaunchBox.Checked);

        ConfigSaved?.Invoke(config);

        _saveButton.Enabled = false;
        _saveButton.Text = "Saved";
        _savedFeedbackTimer.Stop();
        _savedFeedbackTimer.Start();
    }
}
