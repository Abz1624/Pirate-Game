# Rogue Pirates playtest API

ASP.NET Core (.NET 10 LTS) API for the playtest: guest profiles, the weekly kills leaderboard, and 1v1 Arena lobbies
(invites, seat status, drafts, real-time state, results and Elo). For 1v1 matches it is also the **relay and
referee**: both players run the game locally (each simulates its own ship and weapons), the API forwards their messages
over the lobby WebSocket and decides everything shared (countdown, spawns, barrels, storm clock, hits tally, the end and
the result). See [1v1 match protocol](#1v1-match-protocol-relay). With `MATCH_SERVER_URL` set, a dedicated match server
runs the match instead and the API only handles the lobby around it.

- `RoguePirates.Api/` the API (`Program.cs`, `Endpoints/PlaytestEndpoints.cs`, `Playtest/`, `Data/`)
- `RoguePirates.Api.Tests/` xUnit tests (in-memory test server, throwaway SQLite file, injected clock)
- `Dockerfile`, `railway.json` deployment (build context and Railway root directory: `server`)

## Run locally

Requires the .NET 10 SDK. No Docker: locally the API uses SQLite (`roguepirates.db`, created and migrated on first start).

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
| `MATCH_SERVER_URL` | empty | Match server address returned in the lobby when a match starts. Empty: this API relays and referees the match (`hostMode: "relay"`). |
| `PLAY_URL` | `https://www.roguepirates.fun/play/` | Base of invite links (`?join=CODE` is appended). |
| `RATE_LIMIT_PER_MINUTE` | `60` | Writes (POST/PUT/PATCH) allowed per client IP per minute. Reads and WebSockets are not limited. |
| `SWEEP_SECONDS` | `30` | How often idle lobbies expire and single-report matches are decided in the background (both also happen on read). |
| `ARENA_TICK_MS` | `100` | How often the 1v1 relay advances its clock (countdown, barrels, end on time, disconnect forfeits). |

No secrets live in code. The schema is managed with EF Core migrations, applied on startup (an empty database gets
every table; the database the old `EnsureCreated` built is baselined first, keeping its data). How to add a migration
and the deploy steps: [MIGRATIONS.md](MIGRATIONS.md).

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
 "isNewWeeklyBest":true,"logStored":true,"checked":true,"weeklyBest":{"kills":640,"runSeconds":900,"level":34,"captain":"anne"}}
```
`mapId` defaults to `saltwind` (1-32 of `a-z 0-9 _ -`); `captain` up to 32 characters, `buildHash` 128,
`clientVersion` 32. Sanity rules (otherwise `422 {"error":"implausible_run"}`, logged): `kills >= 0`,
`runSeconds` 10..7200, `level` 1..200, at most 600 kills per minute. Each player keeps one best per week and map
(more kills wins; equal kills, the shorter run wins).

Run log (V5 M9a, I9a.1): the game also sends `"log":{...}`, the voyage's compact run log (format `rp-runlog` v1,
documented in `RoguePirates.Api/Playtest/RunLogRules.cs`: seed, seed source, config hash, balance version, build id,
per-minute spawns, kills and kills per weapon, gold, XP, level, damage taken and dealt, pickups, chests, points of
interest, and input samples). It is optional (older clients send none; the reply's `logStored` says whether one was
kept) and stored in the `RunLogs` table with its run. Structural checks only, each a `400` with its code, nothing
stored: `log_too_large` (over 64 KB), `malformed_log`, `unsupported_log_version`, `log_missing_seed`,
`log_missing_config_hash`, `log_mismatch` (the minutes' kills must sum to `kills`, the last minute's level must equal
`level`, and the log's seconds must match `runSeconds` within 1 s).

Run checks (V5 M9a, I9a.3), in **shadow mode** (D23): every run gets a verdict in the `RunVerdicts` table: `pass`,
`flagged` or `outdated_client` (no log, another log version, or a config the rules were not made for), with its reason
codes, rule ids, the details (minute, value, bound), the rules version and the client build. Nothing is rejected or
withheld because of it: a flagged run still counts on the board and still pays out (D24); flags are logged as warnings
for the owner's review. The reply's `checked` is `true` on a pass. The rules are
`RoguePirates.Api/Plausibility/plausibility-rules.json` (embedded), generated by
`plan/playtest/montecarlo/plausibility.mjs` from the game's config and the refit simulator (upper bounds on output
only: per minute and per run kills, XP, gold, damage dealt, chests and points of interest; the level; the level against
the XP; kills against spawns; the hull against damage and heals; time survived in overtime). When the rules version
changes, the stored runs are re-scored in the background at startup.

`GET /api/leaderboard/weekly?mapId=saltwind&limit=50` (auth optional; `limit` 1..100, default 50) -> `200`
```json
{"mapId":"saltwind","weekStart":"2026-09-28T00:00:00Z","weekEnd":"2026-10-05T00:00:00Z",
 "entries":[{"rank":1,"playerId":"8b0c...","name":"Anne Bonny","tag":"#4821","kills":640,"runSeconds":900,"level":34,"captain":"anne","checked":true}],
 "me":{"rank":7,"kills":120,"runSeconds":600,"level":15,"checked":false}}
```
Order: kills (high first), then run time (short first), then who got there first. `me` is `null` without a token or
without a run this week. `checked` (I9a.3): the entry's run passed the run checks; unchecked runs still rank (shadow mode).

`GET /api/leaderboard/weekly/previous?mapId=saltwind&limit=50` -> same shape for last week (final board).

### 1v1 lobbies

Lobby `status`: `waiting` (host alone), `full`, `starting` (both ready; match assigned; 6-second countdown),
`in_match` (the countdown ended, or both seats reported `in_match`), `finished` (result decided), `closed` (host left
before the match), `expired` (15 minutes without activity while `waiting` or `full`). Seat `status`: `joined`,
`drafting`, `ready`, `in_match`, `left`.

Lobby state (returned by every lobby endpoint and pushed on the WebSocket):
```json
{"lobbyId":"5f1e...","code":"K7PQ2M","inviteUrl":"https://www.roguepirates.fun/play/?join=K7PQ2M","public":true,
 "status":"starting","hostPlayerId":"8b0c...","guestPlayerId":"c2d4...",
 "createdAt":"...","updatedAt":"...","expiresAt":null,
 "seats":[
  {"seat":"host","playerId":"8b0c...","name":"Anne Bonny","tag":"#4821","rating":1000,"bot":false,"status":"ready","ready":true,
   "hasDraft":true,"draft":{"weapons":["w1","w2","w3","w4","w5","w6"],"logbooks":["l1","l2","l3","l4","l5","l6"]}},
  {"seat":"guest","playerId":"c2d4...","name":"Calico_Jack","tag":"#0193","rating":1000,"bot":false,"status":"ready","ready":true,
   "hasDraft":true,"draft":{"weapons":["..."],"logbooks":["..."]}}],
 "match":{"matchId":"91aa...","seed":1234567,"matchServer":null,"hostMode":"relay","hostPlayerId":"8b0c...",
  "serverNow":1790856000000,"countdownMs":3000,"startsAt":1790856003000,"startedAt":null,"endsAt":1790856303000,
  "durationMs":300000,"arena":{"centerX":0,"centerZ":0,"radius":120},
  "spawns":[{"seat":0,"playerId":"8b0c...","x":-45,"z":0,"yaw":90},{"seat":1,"playerId":"c2d4...","x":45,"z":0,"yaw":270}],
  "storm":{"shrinkStartMs":1790856153000,"shrinkEndMs":1790856303000,"startRadius":120,"endRadius":25,
   "damagePerSecond":8,"damageGrowthPerSecond":2},
  "botSeat":null}}
```
`seats` has one entry until a guest joins. `draft` stays `null` (only `hasDraft`) until the lobby is `starting`; from
then on both drafts (weapons and logbooks, in priority order) are visible to both players. `match` is `null` until
`starting`. `expiresAt` is set while `waiting` or `full`. `seed` fits in 31 bits. All `*Ms`/`*At` numbers in `match`
are server UTC milliseconds since the Unix epoch; `startedAt` is set once the lobby is `in_match`. `hostMode` is
`relay` (this API referees, see below) or `dedicated` (a `MATCH_SERVER_URL` is configured). `botSeat` is `1` when the
practice bot holds the guest seat.

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
`MATCH_SERVER_URL` (if empty, this API relays the match). `joined`/`drafting`/`ready` are only accepted before
`starting` (`409 already_started`); `in_match` only after it (`409 not_started`). `left`: the host leaving before the
match closes the lobby; the guest leaving before `starting` frees the seat (lobby back to `waiting`), after it marks
the seat `left`. Leaving a refereed match that is running (either seat) forfeits it (`match_end` with
`reason: "forfeit"`; the lobby becomes `finished`). Any change resets the 15-minute idle timer.

`POST /api/lobbies/{lobbyId}/bot` (auth, host only, lobby `waiting`) -> `200` lobby state. Seats the **practice bot**
as the guest: `bot: true`, name `Practice Bot`, rating 1000, already `ready` with a fixed draft (weapons
`deck_cannons, mortar, chainshot, swivel_guns, harpoon_gun, long_nine`; logbooks `gunnery_manual, ironclad, admiral,
carpenters_notes, lookouts_glass, oversized_shot`). The lobby becomes `full` (it leaves the public list); it starts when
the host is `ready`. `403 not_host` / `not_in_lobby`, `409 not_waiting`. The bot's player id is the fixed
`00000000-0000-0000-0000-0000000000b0`; it has no profile row and never connects. See [Bot matches](#bot-matches).

`POST /api/lobbies/{lobbyId}/rematch` (auth, a player of the finished refereed match, within 60 s of its end) -> `200`
`{"status":"requested","by":"8b0c...","waitingFor":"c2d4..."}` or, once both asked (the bot always agrees),
`{"status":"created","lobbyId":"77ab...","code":"P3QX9M"}`. Same as the WebSocket message `{"t":"rematch"}`. The new
lobby is private and `full` with the same two players (drafts kept, seats `joined`; the bot stays `ready`): both
connect to the new lobby's WebSocket and press ready again. `409 match_not_over` / `no_rematch` (window over or no
refereed match), `403 not_in_match`. A rematch is optional: the match is a single round.

`WS /ws/lobbies/{lobbyId}?token=<token>` (seated players only; the browser `Origin` must be allowed). Sends the lobby
state (the JSON above plus `"t":"lobby"`) as a text message on connect and after every change. Any text that is not a
JSON object with a string `"t"` (for example `ping`) asks for the lobby state again. JSON messages with `"t"` are the
match protocol below. The server closes the socket after pushing a final `closed` or `expired` state, and after
`finished` (a refereed match keeps it open 60 s longer for the rematch). Messages over 4 KB are dropped. Rejected
handshakes: 401 (token), 403 (not seated or origin), 404 (lobby).

### Matches

`POST /api/matches/{matchId}/result` (auth, the two players only), sent by each client at the end:
```json
{"winnerPlayerId":"8b0c...","durationSeconds":180,
 "stats":{
  "8b0c...":{"damageDealt":900,"damageTaken":400,"kills":3,"level":12,"weapons":["w1","w2"],"logbooks":["l1"]},
  "c2d4...":{"damageDealt":420,"damageTaken":900,"level":11,"weapons":["w4"],"logbooks":[]}}}
```
-> `200` match view. For a match the relay referees (a player's socket was connected during it, or a bot match) the
server's decision replaces this flow: reports are still accepted and stored, but decide nothing (`status` stays
`pending` until the relay ends the match). Otherwise, for clients that never use the relay: `winnerPlayerId` must be one of the two players; `durationSeconds` 0..7200; `stats` keys
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
`decided` or `disputed`. Matches decided by the relay have `decidedBy` `server_sunk`, `server_time`, `server_forfeit`
or `server_draw` and `reason` (`sunk`, `time`, `forfeit`, `draw`; `null` for report-decided matches); their stats come
from the server tallies and add `barrelsTaken` and `finalHp` (`kills` is `null`). A draw (`winnerPlayerId: null`)
scores 0.5 each for Elo (equal ratings do not move) and counts as neither a win nor a loss. Bot matches and matches
both players abandoned are recorded without rating, win or loss changes. Each player entry also has `bot`.

## 1v1 match protocol (relay)

Applies when `hostMode` is `"relay"`. Both clients simulate their own ship, weapons and the PvP damage rules locally;
the server forwards their messages and decides everything shared. All messages are JSON text frames on the lobby
WebSocket with a type field `"t"`. `st` is the server time (UTC ms) when the server handled the message; clients
estimate the clock offset with `ping`/`pong`. Positions are metres on the XZ plane, relative to the arena centre
(0, 0); `yaw` is degrees about +Y (Unity), 0 = facing +Z, 90 = facing +X.

### Arena and timeline (server constants, `Playtest/ArenaRules.cs`)

| Item | Value |
|---|---|
| Arena | centre (0, 0), radius 120 m |
| Spawns | seat 0 (host) (-45, 0) yaw 90; seat 1 (guest or bot) (45, 0) yaw 270; mirrored, both facing the centre |
| Countdown | 3 s from both seats ready (`startsAt` = ready time + 3 s); then the lobby is `in_match` with `startedAt` |
| Match length | 5:00 from `startsAt` (`endsAt`) |
| Storm | radius 120 m until 2:30, then shrinks linearly to 25 m at 5:00 (`shrinkStartMs`, `shrinkEndMs`). Clients render it and apply the outside damage themselves: 8 hp/s, +2 hp/s for every second spent outside without a break |
| XP barrel | every 5 s (first at 0:05); taker gets one guaranteed level-up (client applies it) |
| Health barrel | every 30 s (first at 0:30); taker heals 20% of max hull (client applies it) |
| Barrel placement | on the perpendicular bisector of the two ships' last reported positions (equidistant from both), at least 3 m inside the current storm ring, at least 15 m from both ships when the ring allows (else as far as it allows); the arena centre when the bisector misses the ring. Random along the bisector, seeded by the match seed |
| Barrel lifetime | 20 s unclaimed, then `barrel_gone` |
| Claim radius | 6 m from the claimer's last reported position |
| Disconnect | a player with no open socket for more than 20 s forfeits (both away: the match is abandoned, no winner, unrated) |
| Rematch window | 60 s after `match_end` |

Timeline: both `ready` -> lobby `starting` (lobby state + `match_start` to each player) -> 3 s countdown (states are
relayed, hits and pickups are refused) -> `in_match` (lobby state with `startedAt`) -> barrels at 0:05, 0:10, ...;
health barrels at 0:30, 1:00, ... -> storm shrinks 2:30 to 5:00 -> `match_end` on a sinking, a forfeit, or at 5:00 ->
lobby `finished` -> optional `rematch` within 60 s -> sockets closed.

### Client to server

| `t` | Fields | Rate | Server action |
|---|---|---|---|
| `state` | `x`, `z` (required), `yaw`, `vx`, `vz`, `hp`, `maxHp`, `level`, `alive`, `boosting`, optional `asBot` | send ~15 Hz; cap 30/s per ship, extras dropped silently | Stores the last position, hp, maxHp, level; forwards to the opponent (see below). `alive: false` after the countdown = this ship is sunk (match ends). Positions beyond 10 km are dropped. |
| `fx` | anything (cosmetic: weapon fired, ids, positions) | cap 30/s, max 2 KB | Forwarded to the opponent unchanged (the exact text). Over 2 KB: `reject` `too_large`. |
| `hit` | `dmg` (0..500), `weaponId` (1-64 chars), optional `asBot` | cap 20/s (extras get `reject` `rate`) | Validates: match started and not over, attacker alive, damage range, weapon id. Adds `dmg` to the attacker's damage dealt and the victim's damage taken, then forwards `hit` to the victim. Send the weapon's raw damage; the victim's client applies the PvP rules (scaling, invulnerability frames, no one-shots) and reports its hp in `state`. |
| `pickup` | `id` (barrel id), optional `asBot` | cap 10/s | First valid claim wins: barrel live and unclaimed, claimer alive, within 6 m of the claimer's last reported `state` position (send a fresh `state` first). Broadcasts `barrel_taken`; otherwise `reject` (`gone`, `taken`, `too_far`, `dead`, `not_started`, `rate`). |
| `dead` | optional `asBot` | once | This ship is sunk (same as `state` with `alive: false`). Ignored during the countdown. |
| `leave` | | once | Forfeits the match (also `PUT .../status` `left`). |
| `rematch` | | | See `POST /api/lobbies/{id}/rematch`. |
| `ping` | optional `cs` (client time) | | Answers `pong`. |

Unknown types get `reject` `unknown_type`; any match message without a running refereed match gets `reject` `no_match`;
`hit`/`pickup` after the end get `reject` `match_over`.

### Server to client

| `t` | To | Fields |
|---|---|---|
| `lobby` | both | The lobby state (on connect, after every change). |
| `match_start` | each player | `matchId`, `lobbyId`, `seed`, `you` (your seat 0/1), `youPlayerId`, `serverNow`, `countdownMs`, `startsAt`, `endsAt`, `durationMs`, `arena`, `spawns`, `storm` (as in the lobby state), `barrels` (`xpEveryMs`, `healthEveryMs`, `firstXpMs`, `firstHealthMs`, `ttlMs`, `claimRadius`, `healthHealFraction`), `limits` (rates and caps), `players` (per seat: `seat`, `playerId`, `name`, `tag`, `rating`, `bot`, `draft`), `bot` (`{seat:1, playerId, simulatedBy}` or `null`), `live` (`ended`, unclaimed `barrels` as `barrel_spawn` objects, damage `tallies`). Sent when the match is assigned and again on every (re)connect during it. |
| `state` | opponent | `from` (player id), `st`, `x`, `z`, `yaw`, `vx`, `vz`, `hp`, `maxHp`, `level`, `alive`, `boosting`. |
| `fx` | opponent | The sender's message, unchanged. |
| `hit` | victim (the host for a bot victim) | `dmg`, `weaponId`, `from` (attacker id), `to` (victim id), `st`. |
| `barrel_spawn` | both | `id` (int), `kind` (`xp` or `health`), `x`, `z`, `st` (spawn time), `expiresAt`. |
| `barrel_taken` | both | `id`, `kind`, `by` (player id; the bot's id for a bot claim), `st`. |
| `barrel_gone` | both | `id` (despawned unclaimed). |
| `presence` | both | `playerId`, `connected`, `forfeitAt` (UTC ms when a disconnected player forfeits; `null` on reconnect). |
| `match_end` | both | `matchId`, `winnerPlayerId` (`null` for a draw or an abandoned match), `reason` (`sunk`, `time`, `forfeit`, `draw`), `rated`, `durationSeconds`, `stats` (per player id: `damageDealt`, `damageTaken`, `level`, `barrelsTaken`, `finalHp`, `maxHp`, `bot`), `ratings` (per player id: `before`, `after`, `change`; humans only), `rematchUntil`. |
| `rematch` | both | `status` `requested` (`by`) or `created` (`lobbyId`, `code`). |
| `reject` | sender | `of` (message type), `reason`, `id` (pickups). |
| `pong` | sender | `cs` (echoed), `st`. |

### How the match ends

1. **Sunk**: a ship reports `alive: false` (or `dead`) after the countdown: it loses.
2. **Time**: at 5:00 the higher damage dealt (server tally of accepted hits) wins; equal damage: the higher remaining hp
   share (last reported `hp / maxHp`; no report = full) wins; still equal: `draw`.
3. **Forfeit**: `leave`, `PUT status left`, or no open socket for more than 20 s (a `presence` message announces the
   deadline). Both players away: abandoned (no winner, unrated).

The server then writes the Match record (`decidedBy: "server_<reason>"`) and Elo (K 32; draws 0.5 each; bot matches
unrated), moves the lobby to `finished` and broadcasts `match_end`.

### Bot matches

`POST /api/lobbies/{id}/bot` puts the practice bot in seat 1. The server does not expect messages from the bot seat and
never forfeits it. The **host's client simulates the bot's ship** and speaks for it with `"asBot": true` on `state`,
`hit` (the bot hitting the host; forwarded back to the host like any hit), `pickup` (a barrel claim for the bot, checked
against the bot's last reported position) and `dead` (the bot sank). Hits on the bot are forwarded to the host with
`to` = the bot's id. Only the host of a bot match may use `asBot` (`reject` `not_bot_host`). The server still runs the
barrels, the clock and the end; bot matches are recorded but unrated. A rematch needs only the host.

### Notes

- Rooms live in memory (one API instance). After an API restart a client reconnecting to a running match gets a fresh
  room (same timeline, tallies and barrels reset); matches nobody reconnects to stay `pending`.
- A match where no player ever opened the WebSocket during it is left to the old two-report result flow.

## Limits of this playtest API

Results are client-reported (no server authority yet, see V5 plan D6 and M13b/M18/M22): the run sanity checks and the
two-report agreement only stop obvious mistakes and casual cheating. The 1v1 relay referees the shared parts (clock,
barrels and their distance check, the end, the tallies), but each client still simulates its own ship: positions, hp
and hits are client-reported within the caps above, so a modified client can still lie about them. Nothing here moves
value; leaderboard prizes,
RUM rewards and wagers need the authoritative match server first.
