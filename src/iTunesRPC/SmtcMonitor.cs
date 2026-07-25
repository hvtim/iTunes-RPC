using Windows.Media.Control;

namespace iTunesRPC;

// Reads now-playing info via Windows' System Media Transport Controls - the same
// system behind the volume flyout's "now playing" widget. Unlike iTunes'
// COM automation, this is how most apps (VLC, browsers, foobar2000, etc.) report
// playback, since very few implement a dedicated automation server like iTunes does.
// Spotify is filtered out everywhere here since it has its own official Discord
// integration already - showing it through this app would just be redundant.
public sealed class SmtcMonitor
{
    private const string SpotifyAppId = "Spotify.exe";

    public async Task<List<(string Id, string DisplayName)>> GetAvailableSourcesAsync()
    {
        var result = new List<(string, string)>();

        try
        {
            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            foreach (var session in manager.GetSessions())
            {
                string id = session.SourceAppUserModelId;
                if (string.IsNullOrWhiteSpace(id) || IsSpotify(id)) continue;
                result.Add((id, PrettifyAppId(id)));
            }
        }
        catch
        {
            // SMTC unavailable (very old Windows build, or the API threw) - callers
            // just see an empty list and fall back to iTunes-only.
        }

        return result;
    }

    public async Task<TrackInfo?> GetCurrentTrackAsync(string appId)
    {
        try
        {
            if (IsSpotify(appId)) return null;

            var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            var session = manager.GetSessions()
                .FirstOrDefault(s => string.Equals(s.SourceAppUserModelId, appId, StringComparison.OrdinalIgnoreCase));

            if (session == null) return null;

            var playback = session.GetPlaybackInfo();
            var state = playback.PlaybackStatus switch
            {
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
                _ => PlaybackState.Stopped
            };

            if (state == PlaybackState.Stopped)
            {
                return new TrackInfo("", "", "", 0, 0, PlaybackState.Stopped, 0, 0);
            }

            var props = await session.TryGetMediaPropertiesAsync();
            var timeline = session.GetTimelineProperties();

            double duration = (timeline.EndTime - timeline.StartTime).TotalSeconds;
            double position = timeline.Position.TotalSeconds;

            // SMTC only reports position at discrete update points, not a continuous
            // stream - while playing, extrapolate from how long it's been since that
            // last update rather than showing a position that's stuck between polls.
            double elapsed = state == PlaybackState.Playing
                ? position + (DateTimeOffset.Now - timeline.LastUpdatedTime).TotalSeconds
                : position;
            elapsed = Math.Max(0, duration > 0 ? Math.Min(elapsed, duration) : elapsed);

            return new TrackInfo(
                props.Title ?? "",
                props.Artist ?? "",
                props.AlbumTitle ?? "",
                duration,
                elapsed,
                state,
                0, 0);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsSpotify(string appId) =>
        string.Equals(appId, SpotifyAppId, StringComparison.OrdinalIgnoreCase);

    private static string PrettifyAppId(string appId)
    {
        string name = appId.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? appId[..^4]
            : appId;
        return name.Length > 0 ? char.ToUpper(name[0]) + name[1..] : name;
    }
}
