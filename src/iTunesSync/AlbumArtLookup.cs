using System.Text.Json;

namespace iTunesSync;

// Looks up official cover art via Apple's public iTunes Search API (no auth needed)
// so we have a real internet-reachable image URL per track - Discord can't render
// artwork bytes pulled straight out of iTunes' local library.
public sealed class AlbumArtLookup : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(5) };
    private readonly Dictionary<string, string?> _cache = new();

    public async Task<string?> GetArtworkUrlAsync(string artist, string track, string album)
    {
        string key = $"{artist}|{track}|{album}";
        if (_cache.TryGetValue(key, out var cached)) return cached;

        // Prefer an album-title search when we have one: it matches iTunes' own
        // tagging (the actual album this track is filed under), avoids picking up
        // a different single/compilation's cover art for songs that were also
        // released as a single, and sidesteps track titles with unusual formatting
        // (stylized/leetspeak, odd punctuation) that can miss a song-title search
        // entirely. Every track on an album shares the same cover art anyway.
        string? url = !string.IsNullOrWhiteSpace(album)
            ? await LookupAsync($"{artist} {album}", "album")
            : null;

        url ??= await LookupAsync($"{artist} {track}", "song");

        _cache[key] = url;
        return url;
    }

    private async Task<string?> LookupAsync(string term, string entity)
    {
        try
        {
            string encodedTerm = Uri.EscapeDataString(term.Trim());
            string requestUrl = $"https://itunes.apple.com/search?term={encodedTerm}&entity={entity}&limit=1";

            using var response = await _http.GetAsync(requestUrl);
            if (!response.IsSuccessStatusCode) return null;

            using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);

            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                return null;

            if (!results[0].TryGetProperty("artworkUrl100", out var artProp)) return null;

            string art = artProp.GetString() ?? "";
            if (string.IsNullOrEmpty(art)) return null;

            // Apple's CDN accepts an arbitrary size baked into the filename - ask for
            // something bigger than the default 100x100 thumbnail.
            return art.Replace("100x100bb", "512x512bb");
        }
        catch
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
