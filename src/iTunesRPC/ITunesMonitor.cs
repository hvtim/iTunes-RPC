using System.Diagnostics;
using System.Linq;

namespace iTunesRPC;

public enum PlaybackState
{
    Stopped,
    Playing,
    Paused
}

public record TrackInfo(string Name, string Artist, string Album, double DurationSeconds, double ElapsedSeconds, PlaybackState State, int TrackNumber, int TrackCount);

// Talks to iTunes via late-bound COM (Type.GetTypeFromProgID) so no interop assembly
// or type library reference is needed at build time - just iTunes installed at runtime.
public sealed class ITunesMonitor : IDisposable
{
    private readonly Type? _iTunesType;
    private dynamic? _iTunesApp;

    public ITunesMonitor()
    {
        _iTunesType = Type.GetTypeFromProgID("iTunes.Application");
    }

    private bool EnsureConnected()
    {
        if (_iTunesApp != null) return true;
        if (_iTunesType == null) return false;

        // COM-activating iTunes.Application silently launches iTunes.exe if it isn't
        // already running (standard COM automation behavior) - guard against that so
        // this app can sit idle at login without popping iTunes open on its own.
        if (!Process.GetProcessesByName("iTunes").Any()) return false;

        try
        {
            _iTunesApp = Activator.CreateInstance(_iTunesType);
            return _iTunesApp != null;
        }
        catch
        {
            return false;
        }
    }

    public TrackInfo? GetCurrentTrack()
    {
        if (!EnsureConnected()) return null;

        try
        {
            int rawState = (int)_iTunesApp!.PlayerState;
            var state = rawState switch
            {
                1 => PlaybackState.Playing,
                2 => PlaybackState.Paused,
                _ => PlaybackState.Stopped
            };

            if (state == PlaybackState.Stopped)
            {
                return new TrackInfo("", "", "", 0, 0, PlaybackState.Stopped, 0, 0);
            }

            var track = _iTunesApp.CurrentTrack;
            if (track == null)
            {
                return new TrackInfo("", "", "", 0, 0, PlaybackState.Stopped, 0, 0);
            }

            string name = track.Name ?? "";
            string artist = track.Artist ?? "";
            string album = track.Album ?? "";
            double duration = track.Duration;
            double elapsed = _iTunesApp.PlayerPosition;
            int trackNumber = track.TrackNumber;
            int trackCount = track.TrackCount;

            return new TrackInfo(name, artist, album, duration, elapsed, state, trackNumber, trackCount);
        }
        catch
        {
            // iTunes was closed mid-call, or the COM server died - drop the reference
            // so the next poll re-launches/re-attaches instead of throwing forever.
            _iTunesApp = null;
            return null;
        }
    }

    public void Dispose()
    {
        _iTunesApp = null;
    }
}
