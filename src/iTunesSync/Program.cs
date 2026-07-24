using System.Text.Json;
using iTunesSync;

var configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
var config = LoadConfig(configPath);

if (string.IsNullOrWhiteSpace(config.ClientId) || config.ClientId == "YOUR_DISCORD_CLIENT_ID_HERE")
{
    Console.WriteLine($"Set your Discord application Client ID in {configPath} before running.");
    Console.WriteLine("See README.md for how to create a Discord application and get a Client ID.");
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

Console.WriteLine("iTunes-Sync running. Press Ctrl+C to exit.");

while (true)
{
    var track = iTunes.GetCurrentTrack();

    if (track is null || track.State == PlaybackState.Stopped || string.IsNullOrEmpty(track.Name))
    {
        if (lastSignature != null)
        {
            discord.ClearActivity();
            lastSignature = null;
            Console.WriteLine("Cleared Discord activity (nothing playing).");
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

            discord.SetActivity(
                name: track.Artist,
                details: track.Name,
                state: state,
                start: start,
                end: end,
                largeImageKey: largeImageKey,
                largeImageText: string.IsNullOrEmpty(track.Album) ? "iTunes" : track.Album);

            lastSignature = signature;
            lastSentAt = now;
            Console.WriteLine($"Updated: {track.Name} - {state}{(artworkUrl != null ? " [cover art]" : "")}{(refreshDue && !contentChanged ? " (keepalive)" : "")}");
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
