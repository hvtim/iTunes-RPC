using System.Text.Json;
using iTunesRPC;

var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
var config = LoadConfig(configPath);

if (string.IsNullOrWhiteSpace(config.ClientId) || config.ClientId == "YOUR_DISCORD_CLIENT_ID_HERE")
{
    Log.Write($"Set your Discord application Client ID in {configPath} before running.");
    Log.Write("See README.md for how to create a Discord application and get a Client ID.");
    return;
}

using var iTunes = new ITunesMonitor();
using var discord = new DiscordIpcClient(config.ClientId);
using var artLookup = new AlbumArtLookup();

string? lastSignature = null;
DateTimeOffset lastSentAt = DateTimeOffset.MinValue;

// Discord's client only seems to keep a directly-passed external image URL valid for
// a couple of minutes before it silently drops back to no image - re-sending the
// (unchanged) activity periodically keeps it alive without needing new content.
var refreshInterval = TimeSpan.FromSeconds(60);

Log.Write("iTunes-RPC running. Press Ctrl+C to exit.");

while (true)
{
    var track = iTunes.GetCurrentTrack();

    if (track is null || track.State == PlaybackState.Stopped || string.IsNullOrEmpty(track.Name))
    {
        if (lastSignature != null)
        {
            bool cleared = discord.ClearActivity();
            if (cleared)
            {
                lastSignature = null;
                Log.Write("Cleared Discord activity (nothing playing).");
            }
            else
            {
                Log.Write("[warn] Failed to clear Discord activity - will retry.");
            }
        }
    }
    else
    {
        string pausedTag = track.State == PlaybackState.Paused ? " (Paused)" : "";
        string trackNumberText = track.TrackCount > 0 ? $" • Track {track.TrackNumber} / {track.TrackCount}" : "";
        string state = $"{track.Artist}{pausedTag}{trackNumberText}";

        string signature = $"{track.Name}|{track.Artist}|{track.State}";
        var now = DateTimeOffset.UtcNow;

        bool contentChanged = signature != lastSignature;
        bool refreshDue = now - lastSentAt >= refreshInterval;

        if (contentChanged || refreshDue)
        {
            var start = now.AddSeconds(-track.ElapsedSeconds);
            DateTimeOffset? end = track.State == PlaybackState.Playing
                ? start.AddSeconds(track.DurationSeconds)
                : null;

            string? artworkUrl = await artLookup.GetArtworkUrlAsync(track.Artist, track.Name, track.Album);
            string largeImageKey = artworkUrl ?? config.LargeImageKey;

            bool sent = discord.SetActivity(
                name: track.Artist,
                details: track.Name,
                state: state,
                start: start,
                end: end,
                largeImageKey: largeImageKey,
                largeImageText: string.IsNullOrEmpty(track.Album) ? "iTunes" : track.Album);

            string kind = refreshDue && !contentChanged ? " (keepalive)" : "";
            string art = artworkUrl != null ? " [cover art]" : "";

            if (sent)
            {
                // Only mark this update as delivered on actual success - if the IPC
                // write silently failed (transient pipe hiccup), we want the next poll
                // (2s later) to retry immediately rather than waiting a full
                // refreshInterval, which is what let Discord's image validation lapse.
                lastSignature = signature;
                lastSentAt = now;
                Log.Write($"Updated: {track.Name} - {state}{art}{kind} [{largeImageKey}]");
            }
            else
            {
                Log.Write($"[warn] Failed to send activity update for {track.Name} - will retry next poll.");
            }
        }
    }

    await Task.Delay(config.PollIntervalMs);
}

static AppConfig LoadConfig(string path)
{
    if (!File.Exists(path))
    {
        var defaultConfig = new AppConfig();
        File.WriteAllText(path, JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions { WriteIndented = true }));
        return defaultConfig;
    }

    var json = File.ReadAllText(path);
    return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
}
