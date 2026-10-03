# Rogue Pirates playtest API

ASP.NET Core (.NET 8) API for the playtest: guest profiles, the weekly kills leaderboard, and 1v1 Arena lobbies
(invites, seat status, drafts, real-time state, results and Elo). The match itself runs elsewhere (a Unity headless
server, or the host's client); this API only handles the lobby around it.

- `RoguePirates.Api/` the API (`Program.cs`, `Endpoints/PlaytestEndpoints.cs`, `Playtest/`, `Data/`)
- `RoguePirates.Api.Tests/` xUnit tests (in-memory test server, throwaway SQLite file, injected clock)
- `Dockerfile`, `railway.json` deployment (build context and Railway root directory: `server`)

## Run locally

Requires the .NET 8 SDK. No Docker: locally the API uses SQLite (`roguepirates.db`, created on first start).

```powershell
cd server/RoguePirates.Api
dotnet run
# -> http://localhost:8080/health   {"ok":true,"db":"sqlite"}
```

Tests:

```powershell
dotnet test server/RoguePirates.Api.Tests
```

## Environment variables

| Variable | Default | Meaning |
|---|---|---|
| `PORT` | `8080` | Port to listen on (Railway sets it). |
| `DATABASE_URL` | unset | Postgres URL (`postgresql://user:pass@host:port/db`, optional `?sslmode=require`). Set: Postgres. Unset: SQLite. |
| `SQLITE_PATH` | `roguepirates.db` | SQLite file when `DATABASE_URL` is unset (`/tmp/roguepirates.db` in the Docker image). |
| `ALLOWED_ORIGINS` | `https://www.roguepirates.fun,https://roguepirates.fun,http://localhost:*,http://127.0.0.1:*` | Comma-separated CORS and WebSocket origins. `scheme://host:*` allows any port. |
| `MATCH_SERVER_URL` | empty | Match server address returned in the lobby when a match starts. Empty: the host's client hosts the match (`hostMode: "client_host"`). |
| `PLAY_URL` | `https://www.roguepirates.fun/play/` | Base of invite links (`?join=CODE` is appended). |
| `RATE_LIMIT_PER_MINUTE` | `60` | Writes (POST/PUT/PATCH) allowed per client IP per minute. Reads and WebSockets are not limited. |
| `SWEEP_SECONDS` | `30` | How often idle lobbies expire and single-report matches are decided in the background (both also happen on read). |

No secrets live in code. The schema is created on startup with EF Core `EnsureCreated` (all tables on an empty
database, nothing otherwise). A later schema change needs a migration or a fresh database; there is no data worth
keeping across such a change during the playtest, but say so before wiping it.

The API is meant to run as **one instance** (lobby and match writes are serialized in-process and WebSocket pushes
come from that process). Keep Railway replicas at 1.

## Deploy on Railway (owner steps; nothing is deployed by the agent)

1. In Railway, **New Project**.
2. **Add a database: PostgreSQL**. The plugin creates `DATABASE_URL`.
3. **New service: GitHub repo** (this repository). In the service settings set **Root Directory** to `server`.
   Railway picks up `server/railway.json` and builds `server/Dockerfile`.
4. In the service **Variables**, add a reference to the database: `DATABASE_URL = ${{Postgres.DATABASE_URL}}`
   (the internal URL; no SSL needed inside Railway), and set `ALLOWED_ORIGINS`, for example
   `https://www.roguepirates.fun,https://roguepirates.fun` (add `http://localhost:*` while testing local builds).
   Optionally `MATCH_SERVER_URL` once a match server exists.
5. **Settings, Networking: Generate Domain** to get a public `*.up.railway.app` URL. Check `https://<domain>/health`
   shows `{"ok":true,"db":"postgres"}`.
6. Optional custom domain: **Custom Domain** `api.roguepirates.fun`, then add the CNAME record Railway shows at your DNS
   provider.
7. Point the game's API base URL at that domain (`https://...` for HTTP, `wss://...` for the lobby WebSocket).

Note: the repository's root `.gitignore` ignores `*.csproj` (Unity); `server/.gitignore` re-includes them. Make sure
`server/RoguePirates.Api/RoguePirates.Api.csproj` is committed, or the Railway build cannot restore the project.

## API reference

All bodies are JSON (camelCase). Times are ISO 8601 UTC (`2026-09-28T00:00:00Z`). Ids are UUIDs.
Authenticated endpoints take `Authorization: Bearer <token>`. Errors look like
`{"error":"code","message":"text"}` with 400 (bad input), 401 (no/unknown token), 403 (not your lobby/match),
404, 409 (conflict), 422 (implausible run) or 429 (rate limited).

### Health

`GET /health` -> `200 {"ok":true,"db":"sqlite"}` (`"postgres"` on Railway; 503 with `ok:false` if the database is unreachable).

### Profiles (guest, no sign-in)

`POST /api/profiles` `{"name":"Anne Bonny"}` -> `201`
```json
{"playerId":"8b0c...","token":"oAi0wQBV5fyIrnWUDTaVvYton2LZDi6y_HBFzQ1yNsY","name":"Anne Bonny","tag":"#4821"}
```
The token is shown once; the server stores only its SHA-256 hash. Keep one token per local profile; one device can hold
several profiles. Names: trimmed, 3-16 characters of letters, digits, space, `_`, `-`; a small profanity block list;
not unique (show `name` with `tag`).

`GET /api/profiles/me` (auth) -> `200`
```json
{"playerId":"8b0c...","name":"Anne Bonny","tag":"#4821","rating":1000,"wins":0,"losses":0,"createdAt":"2026-10-01T12:00:00Z"}
```

`PATCH /api/profiles/me` (auth) `{"name":"Calico_Jack"}` -> `200` same shape as `GET /api/profiles/me`.

`GET /api/profiles/me/stats` (auth) -> `200`
```json
{"playerId":"8b0c...","bestKillsAllTime":812,"bestKillsThisWeek":640,"runsPlayed":17,
 "weekStart":"2026-09-28T00:00:00Z","weekEnd":"2026-10-05T00:00:00Z"}
```

### Runs and the weekly leaderboard

Week = ISO week, Monday 00:00 UTC to the next Monday 00:00 UTC (`weekEnd` is exclusive).

`POST /api/runs` (auth)
```json
{"kills":640,"runSeconds":900,"level":34,"captain":"anne","mapId":"saltwind","buildHash":"a1b2c3","clientVersion":"0.5.0"}
```
-> `200`
```json
{"accepted":true,"runId":42,"weekStart":"2026-09-28T00:00:00Z","weekEnd":"2026-10-05T00:00:00Z",
 "isNewWeeklyBest":true,"weeklyBest":{"kills":640,"runSeconds":900,"level":34,"captain":"anne"}}
```
`mapId` defaults to `saltwind` (1-32 of `a-z 0-9 _ -`); `captain` up to 32 characters, `buildHash` 128,
`clientVersion` 32. Sanity rules (otherwise `422 {"error":"implausible_run"}`, logged): `kills >= 0`,
`runSeconds` 10..7200, `level` 1..200, at most 600 kills per minute. Each player keeps one best per week and map
(more kills wins; equal kills, the shorter run wins).

`GET /api/leaderboard/weekly?mapId=saltwind&limit=50` (auth optional; `limit` 1..100, default 50) -> `200`
```json
{"mapId":"saltwind","weekStart":"2026-09-28T00:00:00Z","weekEnd":"2026-10-05T00:00:00Z",
 "entries":[{"rank":1,"playerId":"8b0c...","name":"Anne Bonny","tag":"#4821","kills":640,"runSeconds":900,"level":34,"captain":"anne"}],
 "me":{"rank":7,"kills":120,"runSeconds":600,"level":15}}
```
Order: kills (high first), then run time (short first), then who got there first. `me` is `null` without a token or
without a run this week.

`GET /api/leaderboard/weekly/previous?mapId=saltwind&limit=50` -> same shape for last week (final board).

### 1v1 lobbies

Lobby `status`: `waiting` (host alone), `full`, `starting` (both ready; match assigned), `in_match` (both seats
reported `in_match`), `finished` (result decided), `closed` (host left), `expired` (15 minutes without activity while
`waiting` or `full`). Seat `status`: `joined`, `drafting`, `ready`, `in_match`, `left`.

Lobby state (returned by every lobby endpoint and pushed on the WebSocket):
```json
{"lobbyId":"5f1e...","code":"K7PQ2M","inviteUrl":"https://www.roguepirates.fun/play/?join=K7PQ2M","public":true,
 "status":"starting","hostPlayerId":"8b0c...","guestPlayerId":"c2d4...",
 "createdAt":"...","updatedAt":"...","expiresAt":null,
 "seats":[
  {"seat":"host","playerId":"8b0c...","name":"Anne Bonny","tag":"#4821","rating":1000,"status":"ready","ready":true,
   "hasDraft":true,"draft":{"weapons":["w1","w2","w3","w4","w5","w6"],"logbooks":["l1","l2","l3","l4","l5","l6"]}},
  {"seat":"guest","playerId":"c2d4...","name":"Calico_Jack","tag":"#0193","rating":1000,"status":"ready","ready":true,
   "hasDraft":true,"draft":{"weapons":["..."],"logbooks":["..."]}}],
 "match":{"matchId":"91aa...","seed":1234567,"matchServer":null,"hostMode":"client_host","hostPlayerId":"8b0c..."}}
```
`seats` has one entry until a guest joins. `draft` stays `null` (only `hasDraft`) until the lobby is `starting`, so
neither player sees the other's picks early. `match` is `null` until `starting`. `expiresAt` is set while `waiting`
or `full`. `seed` fits in 31 bits.

`POST /api/lobbies` (auth) `{"public":true}` (body optional, default private) -> `201` lobby state.

`GET /api/lobbies/public` -> `200` open public lobbies waiting for a second player, newest first (max 50):
```json
{"lobbies":[{"lobbyId":"5f1e...","code":"K7PQ2M","inviteUrl":"...","createdAt":"...","expiresAt":"...",
  "host":{"playerId":"8b0c...","name":"Anne Bonny","tag":"#4821","rating":1000}}]}
```

`POST /api/lobbies/join` (auth) `{"code":"k7pq2m"}` (case-insensitive) -> `200` lobby state. `404` unknown code,
`409` with `lobby_full`, `lobby_closed` (closed, expired or finished) or `own_lobby`. Joining again as the same guest
returns the state.

`GET /api/lobbies/{lobbyId}` (auth, seated players only) -> `200` lobby state (for polling).

`PUT /api/lobbies/{lobbyId}/status` (auth, seated players only)
```json
{"status":"ready","draft":{"weapons":["w1","w2","w3","w4","w5","w6"],"logbooks":["l1","l2","l3","l4","l5","l6"]}}
```
-> `200` lobby state. `draft` is optional with any status; when sent it must have exactly 6 distinct, non-empty ids
(max 64 characters) in each list, in priority order (the game validates them against content). `ready` needs a draft
(sent now or earlier). When both seats are `ready` the lobby becomes `starting` with a `matchId`, `seed` and the
`MATCH_SERVER_URL` (if empty, the host's client hosts). `joined`/`drafting`/`ready` are only accepted before
`starting` (`409 already_started`); `in_match` only after it (`409 not_started`). `left`: the host leaving closes the
lobby; the guest leaving before `starting` frees the seat (lobby back to `waiting`), after it marks the seat `left`.
Any change resets the 15-minute idle timer.

`WS /ws/lobbies/{lobbyId}?token=<token>` (seated players only; the browser `Origin` must be allowed). Sends the lobby
state as a text message on connect and after every change. Send any text (for example `ping`) to get the state again.
The server closes the socket after pushing a final `finished`, `closed` or `expired` state. Rejected handshakes:
401 (token), 403 (not seated or origin), 404 (lobby).

### Matches

`POST /api/matches/{matchId}/result` (auth, the two players only), sent by each client at the end:
```json
{"winnerPlayerId":"8b0c...","durationSeconds":180,
 "stats":{
  "8b0c...":{"damageDealt":900,"damageTaken":400,"kills":3,"level":12,"weapons":["w1","w2"],"logbooks":["l1"]},
  "c2d4...":{"damageDealt":420,"damageTaken":900,"level":11,"weapons":["w4"],"logbooks":[]}}}
```
-> `200` match view. Rules: `winnerPlayerId` must be one of the two players; `durationSeconds` 0..7200; `stats` keys
are the players' ids, numbers non-negative, `level` 1..200, `kills` optional, at most 12 ids per list. One report per
player (`409 already_reported`). The match is decided when both reports name the same winner (`decidedBy:
"agreement"`), or with the single report 60 s after it arrived if the other never comes (`"timeout"`). Different
winners: `status: "disputed"`, no rating change. In the final stats each player's numbers come from their own report
when they sent one. A decided match updates Elo (K 32, everyone starts at 1000), wins and losses, and moves the lobby
to `finished`.

`GET /api/matches/{matchId}` -> `200` (no auth; for the end screen)
```json
{"matchId":"91aa...","lobbyId":"5f1e...","status":"decided","winnerPlayerId":"8b0c...","durationSeconds":180,
 "decidedBy":"agreement","decidedAt":"...","reportsReceived":2,"decideAfter":null,
 "players":[
  {"playerId":"8b0c...","name":"Anne Bonny","tag":"#4821","won":true,"ratingBefore":1000,"ratingAfter":1016,"ratingChange":16,
   "stats":{"damageDealt":900,"damageTaken":400,"kills":3,"level":12,"weapons":["w1","w2"],"logbooks":["l1"]}},
  {"playerId":"c2d4...","name":"Calico_Jack","tag":"#0193","won":false,"ratingBefore":1000,"ratingAfter":984,"ratingChange":-16,
   "stats":{"damageDealt":420,"damageTaken":900,"kills":null,"level":11,"weapons":["w4"],"logbooks":[]}}]}
```
`status` is `pending` (no report), `awaiting_opponent` (one report; `decideAfter` says when it is decided alone),
`decided` or `disputed`.

## Limits of this playtest API

Results are client-reported (no server authority yet, see V5 plan D6 and M13b/M18/M22): the run sanity checks and the
two-report agreement only stop obvious mistakes and casual cheating. Nothing here moves value; leaderboard prizes,
RUM rewards and wagers need the authoritative match server first.
