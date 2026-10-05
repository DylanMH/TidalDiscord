# TidalDiscord

A lightweight Windows tray app that shows the currently playing TIDAL track as Discord Rich Presence.

<!-- screenshot placeholder -->
<!-- ![Status window](docs/screenshot-status.png) -->
<!-- ![Discord presence](docs/screenshot-presence.png) -->

> TidalDiscord is an independent community project and is not affiliated
> with or endorsed by TIDAL or Discord.

## Features

- Shows your currently playing TIDAL track in Discord as a *Listening* activity
- Dynamic album artwork as the large image
- "Listen on TIDAL" button linking to the exact track
- Elapsed-time indicator that survives seeks
- Optionally hides presence while paused
- Runs in the system tray — no console window
- Status window with live connection state, now-playing info, and settings
- Launch with Windows support
- Single-instance (relaunching just opens the status window)

## Requirements

- Windows 10 (19041+) or Windows 11
- The TIDAL desktop app
- The Discord desktop app
- .NET 10 SDK — development only; published builds are self-contained

## Installation

Prebuilt binaries are not published yet. Build from source (below) and run
`src\TidalDiscord\publish.ps1` to produce a standalone `TidalDiscord.exe`.

## Settings

Settings persist to `%AppData%\TidalDiscord\settings.json`:

| Setting | Default |
|---|---|
| Enable Discord presence | On |
| Launch with Windows | Off |
| Hide presence while paused | On |
| Show album name | On |
| Show elapsed time | On |
| Show album artwork | On |
| Show "Listen on TIDAL" button | On |

Logs are written to `%AppData%\TidalDiscord\logs\` and kept for 7 days.

## Repository layout

```
src/TidalDiscord/        Windows tray app (WinForms, .NET 10)
server/TidalDiscord.Api/ Metadata proxy backend (ASP.NET Core)
tests/                   Backend tests
```

## Development setup

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Clone the repo.
3. (Optional — developer/direct-API mode only) configure TIDAL API
   credentials via .NET User Secrets:

   ```
   cd src/TidalDiscord
   dotnet user-secrets set "Tidal:ClientId" "<your client id>"
   dotnet user-secrets set "Tidal:ClientSecret" "<your client secret>"
   dotnet user-secrets set "Tidal:UseDirectApi" "true"
   ```

   The credentials come from a
   [TIDAL developer](https://developer.tidal.com/) application and are used
   only to look up the public track ID and album artwork for the currently
   playing song.

   **Never commit real credentials.** Without them, the app simply uses the
   public metadata proxy instead of calling the TIDAL API directly.

4. Build and run:

   ```
   dotnet build src/TidalDiscord
   dotnet run --project src/TidalDiscord
   ```

## Publishing the desktop app

```
src\TidalDiscord\publish.ps1
```

Produces a self-contained single-file `TidalDiscord.exe` (win-x64) in
`src\TidalDiscord\publish\TidalDiscord\`.

## Running the backend locally

```
set TIDAL_CLIENT_ID=<your client id>
set TIDAL_CLIENT_SECRET=<your client secret>
dotnet run --project server/TidalDiscord.Api
```

See [server/README.md](server/README.md) and `server/.env.example`.

## Privacy

- Reads the currently playing track from the Windows media session API
  (local only).
- To resolve album art and the exact track link, the app sends the current
  track title/artist to the metadata proxy (or directly to TIDAL in
  developer mode). Nothing else is sent — no accounts, no history, no
  analytics, no telemetry.
- The backend sees only the lookup request and caches results briefly; it
  does not store listening history.
- Credentials never leave your machine (they stay in .NET User Secrets).

## Architecture

- `TidalMediaService` reads playback state from the Windows
  `GlobalSystemMediaTransportControls` API.
- `DiscordPresenceService` talks to the local Discord IPC/RPC pipe.
- `ITrackMetadataProvider` resolves the exact track/artwork — either via
  the hosted `server/` proxy (public/default) or the TIDAL API directly
  (developer mode).
- The backend performs TIDAL `client_credentials` OAuth server-side so the
  client secret never ships in the desktop binary.

## Public backend status

The metadata proxy lives in `server/`. See
[PUBLIC_RELEASE_PLAN.md](PUBLIC_RELEASE_PLAN.md) for the design and
remaining distribution considerations (including TIDAL developer-terms
items that need review before broad distribution).

## License

No license selected yet — all rights reserved for now.

<!-- TODO: choose a license before public distribution -->
