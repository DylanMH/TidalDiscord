# TidalDiscord.Api

Tiny metadata proxy that lets the public TidalDiscord desktop app resolve
the exact TIDAL track + album artwork **without shipping the TIDAL client
secret in the desktop binary**.

The desktop sends only `{ title, artist, album? }` — no accounts, no
history. The server performs `client_credentials` OAuth server-side,
searches TIDAL, and returns just `{ trackId, trackUrl, artworkUrl, ... }`.

## Endpoints

### `GET /health`

```json
{ "status": "ok" }
```

### `POST /v1/resolve`

```json
// request
{ "title": "Glass Houses", "artist": "Bad Omens",
  "album": "THE DEATH OF PEACE OF MIND" }

// 200
{ "trackId": "123456789",
  "trackUrl": "https://tidal.com/browse/track/123456789",
  "artworkUrl": "https://resources.tidal.com/images/.../1280x1280.jpg",
  "matchedTitle": "Glass Houses",
  "matchedArtist": "Bad Omens",
  "matchedAlbum": "THE DEATH OF PEACE OF MIND" }
```

| Code | Meaning |
| --- | --- |
| 200 | resolved |
| 400 | malformed request (missing/oversized fields) |
| 404 | no reasonable match |
| 429 | rate limited |
| 502 | upstream TIDAL failure |
| 503 | server missing TIDAL credentials |

Tokens, client IDs/secrets, and raw TIDAL payloads are never returned.

## Configuration (environment variables only)

| Variable | Required | Purpose |
| --- | --- | --- |
| `TIDAL_CLIENT_ID` | yes | TIDAL developer app client id |
| `TIDAL_CLIENT_SECRET` | yes | TIDAL developer app secret — **server-side only** |
| `ASPNETCORE_ENVIRONMENT` | no | `Development` enables verbose request logging |
| `PORT` | no | honored automatically by most hosts |
| `RATE_LIMIT_PERMIT` | no | requests per window per IP (default `60`) |
| `RATE_LIMIT_WINDOW_SECONDS` | no | window length (default `60`) |

.NET-style config keys `Tidal:ClientId` / `Tidal:ClientSecret` also work
(user secrets, appsettings) — but never commit them.

## Local development

```bat
set TIDAL_CLIENT_ID=<id>
set TIDAL_CLIENT_SECRET=<secret>
dotnet run --project server/TidalDiscord.Api
# or from this folder: dotnet run --project TidalDiscord.Api
```

Then:

```bash
curl http://localhost:<port>/health
curl -X POST http://localhost:<port>/v1/resolve \
     -H "Content-Type: application/json" \
     -d '{"title":"Glass Houses","artist":"Bad Omens"}'
```

`server/.env.example` documents the variables; copy it to `.env` if your
tooling auto-loads dotenv files (the API itself reads real env vars).

## Caching & rate limiting

- Successful lookups cached 24h per normalized `title|artist|album`;
  misses cached 10 min.
- OAuth token cached until ~5 min before expiry.
- Per-IP fixed-window limit (default 60/min) + global concurrency cap.
- `X-Forwarded-For` honored so per-IP limits work behind a proxy.

## Deployment

`server/Dockerfile` produces a self-contained image:

```bash
docker build -t tidaldiscord-api server/
docker run -p 8080:8080 \
  -e TIDAL_CLIENT_ID=... -e TIDAL_CLIENT_SECRET=... \
  tidaldiscord-api
```

Inject secrets via the hosting provider's environment settings — never
into the image or repository.

## Privacy notes

Normal logs record status, latency, and cache hit/miss only — not track
metadata. Track titles are logged only at `Debug` level when
`ASPNETCORE_ENVIRONMENT=Development`.
