namespace iTunesRPC;

public class AppConfig
{
    public string ClientId { get; set; } = "YOUR_DISCORD_CLIENT_ID_HERE";
    public string LargeImageKey { get; set; } = "logo";
    public int PollIntervalMs { get; set; } = 2000;
    public bool BroadcastEnabled { get; set; } = true;
    public bool ShowTrackNumber { get; set; } = true;

    // "Auto" (Apple Search API lookup), "Custom" (always use CustomArtUrl), or
    // "Off" (always use LargeImageKey, no lookups).
    public string ArtMode { get; set; } = "Auto";
    public string CustomArtUrl { get; set; } = "";

    // "iTunes" (COM automation), or an SMTC app user model id (e.g. "vlc.exe")
    // for any other app reporting now-playing info to Windows.
    public string MediaSource { get; set; } = "iTunes";
}
