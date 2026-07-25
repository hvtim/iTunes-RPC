using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

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

    public ITunesMonitor()
    {
        _iTunesType = Type.GetTypeFromProgID("iTunes.Application");
    }

    public TrackInfo? GetCurrentTrack()
    {
        if (_iTunesType == null) return null;

        // COM-activating iTunes.Application silently launches iTunes.exe if it isn't
        // already running (standard COM automation behavior) - guard against that so
        // this app can sit idle at login without popping iTunes open on its own.
        if (!Process.GetProcessesByName("iTunes").Any()) return null;

        dynamic? iTunesApp = null;
        try
        {
            iTunesApp = Activator.CreateInstance(_iTunesType);
            if (iTunesApp == null) return null;

            int rawState = (int)iTunesApp.PlayerState;
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

            var track = iTunesApp.CurrentTrack;
            if (track == null)
            {
                return new TrackInfo("", "", "", 0, 0, PlaybackState.Stopped, 0, 0);
            }

            string name = track.Name ?? "";
            string artist = track.Artist ?? "";
            string album = track.Album ?? "";
            double duration = track.Duration;
            double elapsed = iTunesApp.PlayerPosition;
            int trackNumber = track.TrackNumber;
            int trackCount = track.TrackCount;

            Marshal.ReleaseComObject(track);

            return new TrackInfo(name, artist, album, duration, elapsed, state, trackNumber, trackCount);
        }
        catch
        {
            // iTunes was closed mid-call, or the COM server was otherwise unreachable.
            return null;
        }
        finally
        {
            // Release immediately after each poll instead of holding a persistent
            // reference - iTunes shows a "scripting interface in use" warning on quit
            // for as long as any external process keeps a live COM connection open,
            // so staying connected between polls made that warning appear on every
            // quit instead of only in the rare case a quit races an actual poll.
            if (iTunesApp != null)
            {
                Marshal.ReleaseComObject(iTunesApp);
            }
        }
    }

    public void Dispose()
    {
    }
}
