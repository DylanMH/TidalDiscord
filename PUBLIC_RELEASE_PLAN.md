# Public Release Plan

## The problem

TidalDiscord currently authenticates to the TIDAL API with a developer
Client ID + Client Secret stored in .NET User Secrets on the developer's
machine. That works for development, but a public release cannot ship the
secret:

- Any secret embedded in a desktop binary can be extracted.
- Asking every user to register their own TIDAL developer app is too much
  friction.
- Shipping without credentials degrades the experience (no album art, no
  track links).

## The plan: a tiny metadata proxy

Move the client-secret usage behind a small backend service. The desktop
app then talks only to our service — the TIDAL secret never leaves the
server.

```
TidalDiscord (desktop)                TidalDiscord Proxy
----------------------                ------------------
Windows media session API  ──────►    POST /track-lookup
  title, artist, album                { title, artist }
                                      │
                                      ▼
                                   TIDAL API (client_credentials,
                                   secret stays server-side)
                                      │
                                      ▼
                                      { trackId, trackUrl, artworkUrl }
```

### API surface (one endpoint)

`POST /track-lookup`

```json
// Request
{ "title": "Glass Houses", "artist": "Bad Omens" }

// Response (200)
{ "trackId": "313392331",
  "trackUrl": "https://tidal.com/browse/track/313392331",
  "artworkUrl": "https://resources.tidal.com/images/.../1280x1280.jpg" }

// Response (404) — no confident match
{ }
```

That's the whole contract. No user accounts, no auth needed for MVP.

### Server responsibilities

- Hold `TIDAL_CLIENT_ID` / `TIDAL_CLIENT_SECRET` as environment variables.
- Own the OAuth `client_credentials` flow and cache the bearer token.
- Reuse the existing matching logic in `TidalApiService.GetTrackInfoAsync`
  — it's already careful about title/artist scoring and picking the best
  artwork file.
- **Cache aggressively**: key on normalized `title|artist`, TTL of hours.
  Most lookups will be repeats.
- **Rate limit** per client IP to keep abuse (and our TIDAL quota) bounded.
- Return only `trackId`, `trackUrl`, `artworkUrl` — never expose tokens,
  raw TIDAL payloads, or other user's queries.

### Hosting

Trivially small — a single endpoint, mostly cache hits. Options:

- A free-tier container (Fly.io / Railway / Render) running a minimal
  ASP.NET or Node service
- A Cloudflare Worker (cache API fits perfectly, KV for longer caching)
- A Supabase Edge Function

### Desktop changes when this lands

- `TidalApiService` is replaced (or gains a mode) that calls
  `POST {proxy}/track-lookup` and deserializes the response into the
  existing `TidalTrackInfo` — the rest of the app doesn't care where the
  data comes from.
- Add an optional `TidalDiscord:ProxyUrl` setting; default it to the
  hosted service.
- Keep the existing User-Secrets path as a developer fallback so
  self-hosters can still use their own credentials.

### Failure behavior stays the same

If the proxy is unreachable or returns 404, the app falls back to
Windows media metadata + the static TIDAL logo — exactly what happens
today when a lookup fails.

## Status

The proxy described here now exists under `server/TidalDiscord.Api`
(endpoint: `POST /v1/resolve`, health: `GET /health`). See
[server/README.md](server/README.md).

## Out of scope for now

- User authentication on the proxy
- Scrobbling/playback stats — we deliberately do not collect listening
  history
