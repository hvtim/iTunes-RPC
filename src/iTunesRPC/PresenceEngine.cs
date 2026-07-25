namespace iTunesRPC;

// Runs the iTunes -> Discord polling loop on a background task, independent of
// whatever UI (tray icon, settings window) is driving it. Config can be swapped
// out live via UpdateConfig - the loop thread is the only writer to _discord, so
// a client-id change just sets a flag the loop checks, rather than racing a
// cross-thread field mutation.
public sealed class PresenceEngine : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(60);

    private readonly ITunesMonitor _iTunes = new();
    private readonly SmtcMonitor _smtc = new();
    private readonly AlbumArtLookup _artLookup = new();
    private DiscordIpcClient? _discord;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private volatile bool _reconnectRequested;
    private volatile bool _forceResend;

    private string? _lastSignature;
    private DateTimeOffset _lastSentAt = DateTimeOffset.MinValue;

    public AppConfig Config { get; private set; }
    public string Status { get; private set; } = "Starting...";
    public event Action? StatusChanged;

    public PresenceEngine(AppConfig config)
    {
        Config = config;
    }

    public void UpdateConfig(AppConfig config)
    {
        if (Config.ClientId != config.ClientId)
        {
            _reconnectRequested = true;
        }
        Config = config;

        // Cosmetic-only changes (track number, art mode, etc.) don't change the
        // track/artist/state signature the loop checks, so without this they'd
        // silently wait for the next real track change or the 60s keepalive
        // before actually reaching Discord.
        _forceResend = true;
    }

    public void Start()
    {
        if (_loopTask != null) return;
        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_loopTask != null)
        {
            try { await _loopTask; } catch (OperationCanceledException) { }
        }
        _discord?.ClearActivity();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (string.IsNullOrWhiteSpace(Config.ClientId) || Config.ClientId == "YOUR_DISCORD_CLIENT_ID_HERE")
            {
                SetStatus("Waiting for Discord Application ID");
                if (!await DelayAsync(1000, token)) return;
                continue;
            }

            if (_reconnectRequested)
            {
                _discord?.Dispose();
                _discord = null;
                _reconnectRequested = false;
            }

            _discord ??= new DiscordIpcClient(Config.ClientId);

            if (!Config.BroadcastEnabled)
            {
                if (_lastSignature != null && _discord.ClearActivity())
                {
                    _lastSignature = null;
                }
                SetStatus("Disabled");
                if (!await DelayAsync(Config.PollIntervalMs, token)) return;
                continue;
            }

            var track = string.IsNullOrWhiteSpace(Config.MediaSource) || Config.MediaSource == "iTunes"
                ? _iTunes.GetCurrentTrack()
                : await _smtc.GetCurrentTrackAsync(Config.MediaSource);

            if (track is null || track.State == PlaybackState.Stopped || string.IsNullOrEmpty(track.Name))
            {
                if (_lastSignature != null)
                {
                    bool cleared = _discord.ClearActivity();
                    if (cleared)
                    {
                        _lastSignature = null;
                        SetStatus("Nothing playing");
                        Log.Write("Cleared Discord activity (nothing playing).");
                    }
                    else
                    {
                        Log.Write("[warn] Failed to clear Discord activity - will retry.");
                    }
                }
                else if (Status != "Nothing playing")
                {
                    SetStatus("Nothing playing");
                }
            }
            else
            {
                string pausedTag = track.State == PlaybackState.Paused ? " (Paused)" : "";
                string trackNumberText = Config.ShowTrackNumber && track.TrackCount > 0
                    ? $" - Track {track.TrackNumber} / {track.TrackCount}"
                    : "";
                string state = $"{track.Artist}{pausedTag}{trackNumberText}";

                string signature = $"{track.Name}|{track.Artist}|{track.State}";
                var now = DateTimeOffset.UtcNow;

                bool contentChanged = signature != _lastSignature;
                bool refreshDue = now - _lastSentAt >= RefreshInterval;
                bool forceResend = _forceResend;

                if (contentChanged || refreshDue || forceResend)
                {
                    _forceResend = false;

                    var start = now.AddSeconds(-track.ElapsedSeconds);
                    DateTimeOffset? end = track.State == PlaybackState.Playing
                        ? start.AddSeconds(track.DurationSeconds)
                        : null;

                    string? artworkUrl = null;
                    string largeImageKey;
                    string artTag;

                    if (Config.ArtMode == "Custom" && !string.IsNullOrWhiteSpace(Config.CustomArtUrl))
                    {
                        largeImageKey = Config.CustomArtUrl;
                        artTag = " [custom art]";
                    }
                    else if (Config.ArtMode == "Off")
                    {
                        largeImageKey = Config.LargeImageKey;
                        artTag = "";
                    }
                    else
                    {
                        artworkUrl = await _artLookup.GetArtworkUrlAsync(track.Artist, track.Name, track.Album);
                        largeImageKey = artworkUrl ?? Config.LargeImageKey;
                        artTag = artworkUrl != null ? " [cover art]" : "";
                    }

                    bool sent = _discord.SetActivity(
                        name: track.Artist,
                        details: track.Name,
                        state: state,
                        start: start,
                        end: end,
                        largeImageKey: largeImageKey,
                        largeImageText: string.IsNullOrEmpty(track.Album) ? "Now Playing" : track.Album);

                    string kind = refreshDue && !contentChanged ? " (keepalive)" : "";
                    string art = artTag;

                    if (sent)
                    {
                        _lastSignature = signature;
                        _lastSentAt = now;
                        SetStatus($"{track.Name} - {track.Artist}");
                        Log.Write($"Updated: {track.Name} - {state}{art}{kind} [{largeImageKey}]");
                    }
                    else
                    {
                        Log.Write($"[warn] Failed to send activity update for {track.Name} - will retry next poll.");
                    }
                }
            }

            if (!await DelayAsync(Config.PollIntervalMs, token)) return;
        }
    }

    private static async Task<bool> DelayAsync(int milliseconds, CancellationToken token)
    {
        try
        {
            await Task.Delay(milliseconds, token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private void SetStatus(string status)
    {
        Status = status;
        StatusChanged?.Invoke();
    }

    public void Dispose()
    {
        _iTunes.Dispose();
        _artLookup.Dispose();
        _discord?.Dispose();
        _cts?.Dispose();
    }
}
