# iTunes-Sync

Shows what's playing in iTunes as a Discord activity ("Listening to
`<artist>`"), with live per-track album art, track number (e.g. "Track 2 /
11"), and a real progress bar - confirmed working end to end.

> Built with the help of [Claude Code](https://claude.com/claude-code)
> (Anthropic's AI coding agent) - code, install/uninstall scripts, and this
> README were AI-assisted, with live testing against a real Discord/iTunes
> session along the way.

## How it works

- Polls iTunes directly over COM (`iTunes.Application`) every 2 seconds for
  the current track and playback position - no audio recognition needed,
  since iTunes already knows exactly what's playing.
- Talks to Discord's local Rich Presence IPC (named pipe) directly, instead
  of using a wrapper library, because this needs `activity.type = 2`
  ("Listening to...") and a raw external image URL for `large_image`,
  neither of which most .NET wrapper libraries expose.
- Looks up official cover art per track via Apple's public iTunes Search API
  (no auth needed), matched on artist + track name, and passes that image
  URL straight through as `large_image` - Discord's RPC accepts a raw HTTPS
  URL there directly (undocumented but confirmed working; no need for the
  more involved "external assets" API/OAuth dance some other guides use).

**Note on the "Listening to" label:** setting `type: 2` is how community
projects get this wording for non-Spotify apps - not officially documented
for third-party Rich Presence, so Discord could restrict it in a future
client update. If that ever happens, activity would fall back to showing as
"Playing iTunes-Sync" with the same info.

**Note on the compact member-list tag:** the activity's `name` field (set to
the current artist) drives the small tag next to your username in
member/friend lists - confirmed working, so it shows the artist dynamically
instead of a static app name. It doesn't include a "Listening to" prefix
there (only the full profile card does, via the `type: 2` verb prepended to
`name`) - prepending it manually would double up to "Listening to Listening
to `<artist>`" on the full card, so it's left off in the compact tag by
design.

## One-time setup

1. **Create a Discord application** (this gives you a Client ID Discord uses
   to recognize your app):
   - Go to https://discord.com/developers/applications
   - Click **New Application**, name it whatever you want (e.g. "iTunes"),
     since that name is what will appear after "Listening to".
   - On the **General Information** page, copy the **Application ID** -
     that's your Client ID.

2. **(Optional) Upload a static large image asset** as a fallback:
   - In your application, go to **Rich Presence -> Art Assets**.
   - Upload an image and name its asset key `itunes_logo` (or pick your own
     key and update `LargeImageKey` in `config.json` to match).
   - This is only used when Apple's iTunes Search API can't find a match for
     the current track (rare, but happens for obscure/mistagged tracks).

No bot, OAuth2 client secret, or any other credential is needed - just the
Client ID from step 1.

## Install (recommended - no coding required)

1. Do step 1 above (create a Discord application, copy the Client ID).
2. Download the latest `iTunes-Sync.zip` from the
   [Releases page](../../releases/latest) and extract it anywhere.
3. Run `install.bat` inside the extracted folder. It will:
   - Ask you to paste the Client ID from step 1.
   - Install the app to `%LOCALAPPDATA%\iTunes-Sync`.
   - Set it to start automatically at login (see "Running automatically at
     login" below for what that does and doesn't do).
   - Start it immediately.
4. To remove it completely later, run `uninstall.bat` from the same
   extracted folder - it stops the app, removes the autostart shortcut, and
   deletes the installed files. Nothing else on your machine is touched.

Re-running `install.bat` later (e.g. after downloading a newer release)
upgrades the installed files in place and keeps your existing Client ID.

## Build from source (for developers)

```
cd src\iTunesSync
dotnet run
```

Or build a standalone exe:

```
dotnet publish src\iTunesSync -c Release -r win-x64 --self-contained false
```

The published exe will be under
`src\iTunesSync\bin\Release\net8.0-windows\win-x64\publish\`.

Requirements: iTunes must be installed (for the COM API) and Discord must be
running (for the IPC pipe) - both are checked for at runtime, not build time,
so the app just won't show anything until both are up.

**Note on album art disappearing after ~2 minutes:** a directly-passed
external image URL (see above) only stays valid in Discord's client for a
couple of minutes before silently reverting to no image, if the activity
isn't touched again. To keep it alive, the app re-sends the (otherwise
unchanged) activity every 60 seconds even when nothing about the track has
changed - text stays identical, but this resets whatever short-lived
validation Discord applies to the image.

## Running automatically at login

`install.bat` (see above) sets this up automatically by placing a shortcut
to the exe in the Startup folder (`shell:startup`, i.e.
`%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup`). Building from
source instead? Create that shortcut yourself pointing at the published
`iTunesSync.exe`.

It's safe to enable even if you don't always use iTunes: the app checks
whether `iTunes.exe` is actually already running before touching its COM
API, so it won't silently launch iTunes on its own - it just sits idle until
you open iTunes yourself, then picks up playback automatically. It also
builds as a windowed (not console) app, so no window appears at login.

To remove autostart, run `uninstall.bat`, or manually delete the shortcut
from the Startup folder if you set it up by hand.

## Known limitations

- Per-track album art depends on Apple's iTunes Search API finding a match
  for the artist/track name - obscure or mistagged tracks fall back to the
  static `LargeImageKey` logo instead.
- The compact member-list tag shows the artist name only, no "Listening to"
  prefix (see note above) - a deliberate tradeoff to avoid a doubled-up
  header on the full profile card.
