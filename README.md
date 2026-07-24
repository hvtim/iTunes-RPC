# iTunes-Sync

Shows what's playing in iTunes as a Discord activity: "Listening to
`<artist>`", with live album art, track number, and a progress bar.

> Built with the help of [Claude Code](https://claude.com/claude-code)
> (Anthropic's AI coding agent).

## Install

1. Create a Discord application at
   [discord.com/developers/applications](https://discord.com/developers/applications).
   Copy the Application ID from General Information. This name shows up
   after "Listening to".
2. Download `iTunes-Sync.zip` from the
   [latest release](../../releases/latest) and extract it.
3. Run `install.bat` and paste the Application ID when asked. This installs
   to `%LOCALAPPDATA%\iTunes-Sync`, sets it to start at login, and runs it.

No bot or OAuth secret needed. Just the Application ID.

To uninstall, run `uninstall.bat` from the same folder. It stops the app,
removes the autostart shortcut, and deletes the installed files.

Re-running `install.bat` from a newer release upgrades in place and keeps
your existing Application ID.

## Autorun behavior

The app only connects to iTunes if iTunes is already running. Safe to leave
enabled even on days you don't use iTunes: it sits idle until you open
iTunes yourself. Runs windowed (no console), so nothing pops up at login.

## Build from source

```
dotnet run --project src\iTunesSync
```

Standalone exe:

```
dotnet publish src\iTunesSync -c Release -r win-x64 --self-contained false
```

Output: `src\iTunesSync\bin\Release\net8.0-windows\win-x64\publish\`.

Requires iTunes and Discord both running, checked at runtime.

## How it works

- Polls iTunes over COM (`iTunes.Application`) every 2 seconds for the
  current track and position. No audio recognition; iTunes already knows
  what's playing.
- Talks to Discord's local Rich Presence IPC (named pipe) directly instead
  of a wrapper library. Needs `activity.type = 2` ("Listening to...") and a
  raw external image URL for `large_image`, neither exposed by most .NET
  wrapper libraries.
- Looks up cover art via Apple's iTunes Search API. Searches artist+album
  first (matches iTunes' own tagging, avoids a different single's cover),
  falls back to artist+track. The image URL is passed directly as
  `large_image`; Discord's RPC accepts a raw HTTPS URL there (undocumented,
  confirmed working).
- Sets the activity's `name` field to the artist. This drives the compact
  tag next to your username in Discord. It has no "Listening to" prefix
  there; only the full profile card adds that verb, via `type: 2`.

## Known limitations

- `type: 2` for "Listening to" isn't officially documented for third-party
  apps. Discord could restrict it in a future update, falling back to
  "Playing iTunes-Sync" with the same info otherwise intact.
- Album art depends on Apple's catalog matching the artist/album or
  artist/track. Tracks not distributed on Apple Music (unreleased/leaked
  tracks with fan-applied tags) won't match under any strategy and fall
  back to a static logo (`LargeImageKey` in `config.json`).
- Known bug: album art can still disappear intermittently on long sessions.
  A 60-second keepalive resend fixed the common case (art expiring after
  ~2 minutes) but not all cases. Root cause not confirmed. See
  [issue #1](../../issues/1).
