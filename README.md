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

Only the Application ID is required.

To uninstall, run `uninstall.bat` from the same folder. It stops the app,
removes the autostart shortcut, and deletes the installed files.

Re-running `install.bat` from a newer release upgrades in place and keeps
your existing Application ID.

## Autorun behavior

The app connects to iTunes only when iTunes is already running, and sits
idle otherwise. Runs windowed, with no console.

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

- Polls iTunes every 2 seconds for the current track and position.
- Sends it to Discord as a "Listening to" Rich Presence activity.
- Looks up cover art via Apple's iTunes Search API (album, then track).
- Shows the artist in Discord's compact status tag next to your username.

## Known limitations

- The "Listening to" wording isn't officially supported for third-party
  apps and could change in a future Discord update.
- Album art requires a match on Apple's catalog. Unreleased or mistagged
  tracks fall back to a static logo.
- Album art can still disappear intermittently on long sessions. See
  [issue #1](../../issues/1).
