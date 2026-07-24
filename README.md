# iTunes-Sync

Shows what's playing in iTunes as a Discord activity: "Listening to
`<artist>`", with live album art, track number, and a progress bar.

> Built with the help of [Claude Code](https://claude.com/claude-code)
> (Anthropic's AI coding agent), with live testing against a real
> Discord/iTunes session.

## Install

1. Create a Discord application at
   [discord.com/developers/applications](https://discord.com/developers/applications) →
   **New Application** → copy the **Application ID** from General
   Information. This name is what shows up after "Listening to".
2. Download `iTunes-Sync.zip` from the
   [latest release](../../releases/latest) and extract it.
3. Run `install.bat` and paste the Application ID when asked. This installs
   the app to `%LOCALAPPDATA%\iTunes-Sync`, sets it to start at login, and
   runs it immediately.

No bot or OAuth secret needed - just the Application ID.

To uninstall, run `uninstall.bat` from the same extracted folder. It stops
the app, removes the autostart shortcut, and deletes the installed files.

Re-running `install.bat` from a newer release upgrades the install in place
and keeps your existing Application ID.

## Autorun behavior

The app only connects to iTunes if iTunes is already running, so it's safe
to leave it starting at login even on days you don't use iTunes - it just
sits idle until you open iTunes yourself. It runs windowed (no console), so
nothing pops up at login.

## Build from source

```
dotnet run --project src\iTunesSync
```

Standalone exe:

```
dotnet publish src\iTunesSync -c Release -r win-x64 --self-contained false
```

Output: `src\iTunesSync\bin\Release\net8.0-windows\win-x64\publish\`.

Requires iTunes and Discord to both be running - checked at runtime, not
build time.

## How it works

- Polls iTunes over COM (`iTunes.Application`) every 2 seconds for the
  current track and position - no audio recognition needed, since iTunes
  already knows what's playing.
- Talks to Discord's local Rich Presence IPC (named pipe) directly instead
  of a wrapper library, since this needs `activity.type = 2` ("Listening
  to...") and a raw external image URL for `large_image` - neither exposed
  by most .NET wrapper libraries.
- Looks up cover art via Apple's public iTunes Search API (no auth needed),
  preferring an artist+album search over artist+track - matches iTunes' own
  tagging and avoids picking up a different single's cover for tracks also
  released standalone - falling back to a track-title search if that finds
  nothing. Passes the image URL straight through as `large_image`; Discord's
  RPC accepts a raw HTTPS URL there directly - undocumented, but confirmed
  working, and simpler than the official external-assets/OAuth flow other
  guides use.
- Sets the activity's `name` field to the artist, which drives the small
  tag next to your username in Discord's member/friend lists. It doesn't
  get a "Listening to" prefix there (only the full profile card adds that,
  via the `type: 2` verb) - prepending it manually would double up to
  "Listening to Listening to `<artist>`" on the full card.

## Known limitations

- `type: 2` for the "Listening to" wording isn't officially documented for
  third-party apps - Discord could restrict it in a future update, which
  would fall back to "Playing iTunes-Sync" with the same info otherwise
  intact.
- Album art depends on Apple's iTunes Search API matching the artist/album
  or artist/track - tracks not actually distributed on Apple Music (e.g.
  unreleased/leaked tracks with fan-applied tags) won't match under any
  search strategy and fall back to a static logo (`LargeImageKey` in
  `config.json`, if you've uploaded one under your Discord app's Rich
  Presence → Art Assets).
- **Known bug:** album art can still disappear intermittently on longer
  listening sessions. The app re-sends the activity every 60 seconds to
  keep externally-hosted images alive (they otherwise expire client-side
  after ~2 minutes), which fixed the common case, but it can still happen.
  Root cause not yet identified - possibly a separate, longer-lived cache
  or rate limit on Discord's side. Issues/contributions welcome.
