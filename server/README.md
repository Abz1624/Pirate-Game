# Rogue Pirates playtest API

ASP.NET Core (.NET 10 LTS) API for the playtest: guest profiles, the weekly kills leaderboard, and 1v1 Arena lobbies
(invites, seat status, drafts, real-time state, results and Elo). For 1v1 matches it is also the **relay and
referee**: both players run the game locally (each simulates its own ship and weapons), the API forwards their messages
over the lobby WebSocket and decides everything shared (countdown, spawns, barrels, storm clock, hits tally, the end and
the result). See [1v1 match protocol](#1v1-match-protocol-relay). With `MATCH_SERVER_URL` set, a dedicated match server
runs the match instead and the API only handles the lobby around it.

- `RoguePirates.Api/` the API (`Program.cs`, `Endpoints/PlaytestEndpoints.cs`, `Playtest/`, `Data/`)
- `RoguePirates.Api.Tests/` xUnit tests (in-memory test server, throwaway SQLite file, injected clock)
- `Dockerfile` deployment (build context and Railway root directory: `server`); the Railway service settings are in
  [`.railway/railway.ts`](../.railway/railway.ts) at the repository root (see [Service settings](#service-settings-railwayrailwayts))

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
| `RATE_LIMIT_PER_MINUTE` | `60` | Writes (POST/PUT/PATCH) allowed per client IP per minute. Reads and WebSockets are not limited, except the RPC reads below. |
| `RPC_READ_RATE_LIMIT_PER_MINUTE` | `30` | V5 M9h: `GET /api/wallet/balance` calls per client IP per minute (each is a Solana RPC call; `429 rate_limited` over it). |
| `SWEEP_SECONDS` | `30` | How often idle lobbies expire and single-report matches are decided in the background (both also happen on read). |
| `ARENA_TICK_MS` | `100` | How often the 1v1 relay advances its clock (countdown, barrels, end on time, disconnect forfeits). |
| `RP_MATCH_TICKET_SECRET` | unset | V5 M9b: the join ticket secret shared with the Netcode match server (32+ characters). Unset: `POST /api/lobbies/voyage/ticket` answers 503. Never logged. |
| `VOYAGE_SERVER_URL` | unset | V5 M9b: the match server a voyage ticket is for (`wss://...` on Railway), returned as `matchServerUrl`. |
| `VOYAGE_MATCH_ID` | unset | V5 M9b: the match server's `RP_MATCH_ID`, put in the ticket as `mid`; unset: a fresh id per ticket. |
| `MATCH_TICKET_LIFETIME_SECONDS` | `60` | V5 M9b: voyage ticket lifetime, clamped to 10-120 s. |
| `SOLANA_RPC_URL` | `https://api.devnet.solana.com` | Hackathon: Solana JSON-RPC endpoint (devnet). |
| `SOLANA_CLUSTER` | `devnet` | Hackathon: cluster named in the sign-in message and explorer links. |
| `TREASURY_SECRET_KEY` | unset | Hackathon: treasury/escrow keypair, base58 64-byte secret or a solana-keygen JSON array. Never logged or returned. Unset: endpoints that need it answer 503 `treasury_off`. |
| `SIWS_DOMAIN` | `roguepirates.fun` | Hackathon: domain in the sign-in message. |
| `ADMIN_KEY` | unset | Hackathon: owner key for `/api/admin/*` (header `X-Admin-Key`). Unset: 503 `admin_off`. |
| `WAGER_MAX_LAMPORTS` | `500000000` | Hackathon: the 1v1 stake cap (0.5 SOL); the minimum is 0.001 SOL. |
| `WEEKLY_REWARD_SOL` | `0.5,0.3,0.2` | Hackathon: weekly kills board rewards in SOL, 1st place first (at most 10 places). |
| `WEEKLY_REWARD_CHECKED_ONLY` | `true` | Hackathon: only runs that passed the server's run checks win weekly SOL (13 Oct hotfix; was `false`). `false` pays on client-reported totals, which a modified client can fake. |
| `ALLOW_MAINNET` | `false` | 13 Oct hotfix: a `SOLANA_RPC_URL` or `SOLANA_CLUSTER` containing `mainnet` keeps the treasury off (`503 treasury_off`, an error in the log) unless this is `true`. |
| `ADMIN_PAYOUT_MAX_LAMPORTS` | `1000000000` | 13 Oct hotfix: the largest owner payout (`POST /api/admin/payouts`), 1 SOL. |
| `RUM_RUN_CAP`, `RUM_RUN_HALF` | `40`, `20` | 13 Oct hotfix: voyage RUM per run goes through the soft cap `cap * (1 - 0.5^(raw / half))` (raw = kills / 10), never more than raw: 105 kills -> 10, 300 -> 25, any amount -> at most 39-40. |
| `RUM_DAY_CAP`, `RUM_DAY_HALF` | `150`, `75` | 13 Oct hotfix: the same curve over a player's capped runs of one UTC day; a run credits what it adds to the day's total, so a day never credits more than 150 RUM from voyages (an island store makes 120+ RUM an hour). |
| `WEEKLY_REWARD_MAP` | `saltwind` | Hackathon: the board the automatic weekly payout pays. |
| `WEEKLY_REWARDS_AUTO` | `true` | Hackathon: `false` turns off the background job that pays the previous week. |
| `WEEKLY_REWARDS_INTERVAL_MINUTES` | `60` | Hackathon: how often that job runs (first run one interval after startup). |
| `DAILY_RACE_SALT` | fixed default | Hackathon: salt for the daily race seed (HMAC-SHA256 of the UTC date). |
| `DAILY_RACE_FIXED_SALT` | `true` | V5 M9h switch (switch values: true/false, 1/0, on/off, yes/no; anything else keeps the default and logs an error at startup) (kept on by the owner, H2): with `DAILY_RACE_SALT` unset the seed uses the public default salt in the code, so future seeds can be computed. `false`: a secret salt derived from the treasury key (one-way HMAC; random per process without a key). |
| `WAGERS_HUMAN_ENABLED` | `true` | V5 M9h switch (kept on): SOL stakes between human players. `false`: new stakes and new deposits answer `409 wagers_off` (a transfer already sent is recorded and refunded); open wagers still settle. |
| `RAID_LOOT_ENABLED` | `true` | V5 M9h switch (kept on): a won raid takes RUM. `false`: raids still win, shield and count, but loot 0 (`loot.enabled:false`). |
| `ISLAND_SALE_CARRIES_RUM` | `true` | V5 M9h switch (kept on): a market sale hands the island over as it is. `false` (I26.2): the island's store and vault RUM go to the seller's balance and its buildings go back to their start levels when it changes hands. |
| `SESSION_LIFETIME_DAYS` | `30` | V5 M9h: wallet sessions expire this long after their last use (sliding). |
| `MINT_DAILY_BUDGET` | `20` | V5 M9h: Island NFT mints the treasury pays per UTC day (each about 0.018 SOL); over it claims and mint retries answer `429 mint_budget`. `0` stops mints. |
| `TREASURY_MIN_LAMPORTS` | `2000000000` | V5 M9h: the treasury float (2 SOL). Below it `/api/treasury` reports `low:true` and an error `TREASURY LOW` is logged (when it goes low, then hourly). |
| `DAILY_RACE_MAP` | `saltwind` | Hackathon: the daily race map. |
| `DAILY_RACE_TARGET_KILLS` | `100` | Hackathon: kills to reach in the daily race. |

No secrets live in code. The schema is managed with EF Core migrations, applied on startup (an empty database gets
every table; the database the old `EnsureCreated` built is baselined first, keeping its data). How to add a migration
and the deploy steps: [MIGRATIONS.md](MIGRATIONS.md).

The API is meant to run as **one instance** (lobby and match writes are serialized in-process and WebSocket pushes
come from that process). Keep Railway replicas at 1.

## Deploy on Railway (owner steps; nothing is deployed by the agent)

1. In Railway, **New Project**.
2. **Add a database: PostgreSQL**. The plugin creates `DATABASE_URL`.
3. **New service: GitHub repo** (this repository). In the service settings set **Root Directory** to `server`.
   Then run `railway config apply` from the repository root to set the build and deploy settings from
   `.railway/railway.ts` (it builds `server/Dockerfile`).
4. In the service **Variables**, add a reference to the database: `DATABASE_URL = ${{Postgres.DATABASE_URL}}`
   (the internal URL; no SSL needed inside Railway), and set `ALLOWED_ORIGINS`, for example
   `https://www.roguepirates.fun,https://roguepirates.fun` (add `http://localhost:*` while testing local builds).
   Optionally `MATCH_SERVER_URL` once a match server exists.
5. **Settings, Networking: Generate Domain** to get a public `*.up.railway.app` URL. Check `https://<domain>/health`
   shows `{"ok":true,"db":"postgres"}`.
6. Optional custom domain: **Custom Domain** `api.roguepirates.fun`, then add the CNAME record Railway shows at your DNS
   provider.
7. Point the game's API base URL at that domain (`https://...` for HTTP, `wss://...` for the lobby WebSocket).

### Service settings (`.railway/railway.ts`)

The api service's build and deploy settings are Railway Infrastructure as Code in `.railway/railway.ts` (Config as Code,
the old `server/railway.json`, stops being read on 2026-12-01): Dockerfile builder, `Dockerfile`, health check `/health`
with a 60 s timeout, one replica; the restart policy is left at Railway's default, which is railway.json's ON_FAILURE
with 10 retries (Railway reads a declared default back as null, so declaring it makes every apply redeploy). It manages only the `api` service (partial `api`);
Postgres is untouched. `server/deploy.ps1` runs `railway config apply --yes` before `railway up` when `railway config plan` shows changes
(an apply that changes anything redeploys the service), without
`--confirm-destructive`, so an apply that would delete anything stops the deploy.

- **Variables:** a variable the file does not list is deleted on apply. Existing ones are listed with `preserve()`,
  which keeps the value Railway holds, so no value lives in the repo. Before adding a variable in the Railway UI, add
  `NAME: preserve()` to `env` in `railway.ts`.
- **Check without changing anything:** from the repository root, `railway config plan` (needs `npm ci --prefix .railway`
  once; on Windows set `$env:_` to `<npm>\node_modules\@railway\cli\bin\railway.exe` first, as `deploy.ps1` does,
  or the SDK's CLI version check fails).

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
kept) and stored in the `RunLogs` table with its run. Structural checks only: `log_too_large` (over 64 KB),
`malformed_log`, `unsupported_log_version`, `log_missing_seed`, `log_missing_config_hash`, `log_mismatch` (the minutes'
kills must sum to `kills`, the last minute's level must equal `level`, and the log's seconds must match `runSeconds`
within 1 s). In shadow mode (owner decision of 8 Oct) a log that fails them does **not** refuse the run: the run is
accepted and ranks, the reply has `"checked":false,"verdict":"log_rejected","logRejected":"<code>"`, and the raw log is
kept (cut to 64 KB, with its code and message in `RejectCode`/`RejectReason`) for diagnosis. Only a request body over
96 KB is refused: `413 {"error":"body_too_large"}`, nothing stored.

Retention: run logs older than `RUNLOG_RETENTION_DAYS` (default 56, 8 weeks) are deleted by a daily background sweep
(`RUNLOG_RETENTION_SWEEP_HOURS`, default 24), except the logs of `flagged` and `log_rejected` runs. The runs and their
verdicts stay.

Run checks (V5 M9a, I9a.3), in **shadow mode** (D23): every run gets a verdict in the `RunVerdicts` table: `pass`,
`flagged`, `outdated_client` (no log, another log version, or a config the rules were not made for) or `log_rejected`
(the log failed the structural checks, rule `PR-LOG`), with its reason
codes, rule ids, the details (minute, value, bound), the rules version and the client build. Nothing is rejected or
withheld because of it: a flagged run still counts on the board and still pays out (D24); flags are logged as warnings
for the owner's review. The reply's `checked` is `true` on a pass; `verdict` names the status. The rules are
`RoguePirates.Api/Plausibility/plausibility-rules.json` (embedded), generated by
`plan/playtest/montecarlo/plausibility.mjs` from the game's config and the refit simulator (upper bounds on output
only: per minute and per run kills, XP, gold, damage dealt, chests and points of interest; the level; the level against
the XP; kills against spawns; the hull against damage and heals; time survived in overtime). When the rules version
changes, the stored runs are re-scored in the background at startup.

Rules v2 (8 Oct): the per-minute bounds on kills, XP, gold and damage dealt never fall as the run goes on (a running max;
v1's minute bounds followed the spawn schedule's dips, and the owner's Cursed Doubloons voyage S6 was flagged at minute 4
with 200 kills against 194 after 186 at minute 1); each simulated voyage draws its build's reach (the share of the wave
enemies it fights, between the fitted 0.9 and 1); the simulated spawns count the scripted fights and the boss. D25: the
owner's voyages on the current build (S5 as today's config, S6) must pass the shipped rules.

Rules v3 (9 Oct, D27): regenerated for the Cuttlefish Bone config (the Iron Figurehead renamed; same rarity, chest pool and
+9% damage). `configHashes` also lists the config before it (`sameBoundsConfigs`, with the reason): the generator lists an
earlier hash only when that config's committed Monte Carlo export equals today's once the named renames are applied, so
the simulator's input, and with it every bound, is the same. Voyages from builds before D27 (S6) still pass instead of
turning `outdated_client`.

Rules v8 (12 Oct, hackathon round 2): enemy damage x2 and the Anchor Flail's chained mace (level-1 DPS then matched to the
other starters) are balance changes, so the bounds are new. The two earlier builds of that day stay accepted
(`MERGED_CONFIGS` in plausibility.mjs, listed in `sameBoundsConfigs` with `"bounds": "merged"`): 83779c (rules v7, from its
committed export) and 841839 (round 2 before the flail fix, export kept in `plan/playtest/montecarlo/exports/`) are
simulated the same way and every bound is the higher of theirs and today's, so their runs meet at least their own bounds.
The older same-bounds hashes are checked against 83779c's export.

Rules v9 (13 Oct, after the hackathon): the four switched-off weapons and the Cursed Chest Key are back in the pools,
the Art Guide's four designed items and the Chaos Logbook are in, the chained mace has its director coverage and the
Signal Buoy is off as config (config f88859f9). The rules v8 config (8ea655e3, from commit 72918c94's export) joins
`MERGED_CONFIGS` with 83779c and 841839, so builds from 12 Oct keep passing. The hackathon builds of 13 Oct (the
switch-offs and renames, never exported) are not accepted: their runs score `outdated_client`.

Regenerating the rules (after any balance change; `V5PlausibilityRulesTests` fails until then), in
`plan/playtest/montecarlo`, after exporting `data.json` from the Editor (RoguePirates > V5 > Balance: export the Monte
Carlo data):
1. `node plausibility.mjs`: the rules (the version goes up by one when their content changes);
2. `node plausibility.mjs --check`: the false-positive budget (at most 0.1% of 10,000 strongest-personality voyages);
3. `node plausibility.mjs --sessions`: the recorded voyages as run logs, their expected verdicts and the D25 check;
4. `node plausibility.mjs --edge-cases`: the shared edge-case fixture (the three checkers agree).

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

### Voyage join ticket (V5 M9b, Netcode match server)

`POST /api/lobbies/voyage/ticket` (bearer token, no body): a signed one-time join ticket for a server-run voyage, `{ticket, matchServerUrl, matchId, mode: "voyage", expiresAt}`. The ticket is `"v1." + b64url(payload) + "." + b64url(HMAC-SHA256(RP_MATCH_TICKET_SECRET, "v1." + b64url(payload)))` with payload `{"v":1,"tid","sub":<player id>,"mid","mode":"voyage","aud":"rp-match","iat","exp"}` (a fresh `tid` each call, lifetime at most 120 s); the match server takes each `tid` once. 401 without a valid token, 503 `match_tickets_off` without the secret. Contract and tests: `plan/v5/phase-09b-netcode/README.md`, `MatchTicketTests`.

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

### Hackathon: wallet sign-in, treasury, deposits (Solana devnet)

Lamports are integers (1 SOL = 1,000,000,000). Solana RPC failures answer `502 solana_rpc_error` with the node's message.

`POST /api/auth/wallet/nonce` `{"wallet":"<base58>"}` -> `200`
`{"wallet","nonce","message","issuedAt","expiresAt"}`. Sign `message` exactly (UTF-8 bytes, ed25519, the wallet's
`signMessage`); valid 5 minutes, usable once. `400 invalid_wallet`.

`POST /api/auth/wallet/verify` `{"wallet","signature":"<base58 64 bytes>","message","name":null}` (optional bearer of a
guest profile: the wallet is linked to that profile) -> `200`
`{"playerId","token","name","tag","wallet","email","isNew"}` (the same `token` use as `POST /api/profiles`: send it as
`Authorization: Bearer`). Each sign-in issues its own session; earlier sessions stay valid until they expire.
**Sessions (V5 M9h):** a wallet session expires `SESSION_LIFETIME_DAYS` (30) days after its last use (each use moves the
expiry, written at most once an hour; sessions from before M9h count from their creation); an expired token is `401
unauthorized`, sign in again. Linking a wallet to a guest profile (a bearer sent with the verify) rotates the guest token:
the old guest token stops working and the profile is reached through the returned wallet session. A guest profile's own
token does not expire (a guest has no other way back in). Expired sessions are deleted by the sweep.
`POST /api/auth/sessions/revoke-all` (auth) signs out everywhere -> `{"playerId","revokedSessions","guestTokenRotated","token","signedOut"}`:
every wallet session of the profile, the caller's included, is deleted (`signedOut:true`; sign in with the wallet again);
a guest profile instead gets a new token in `token` (shown once) and the old one stops working. Errors: `401`
`unknown_nonce`, `nonce_used`, `nonce_expired`, `bad_signature`; `400 invalid_message`, `invalid_request`.
New wallet profiles are named `Captain XXXX` (first 4 characters of the wallet) unless a valid `name` is sent.

`PUT /api/profile/email` (auth) `{"email":"a@b.co"}` (empty clears) -> `200 {"playerId","wallet","email","emailVerified":false}`; `400 invalid_email`.

`GET /api/profiles/me` now also returns `wallet`, `email` and `rum`.

`GET /api/wallet/balance` (auth; or `?wallet=<address>` without auth) -> `200 {"wallet","lamports","sol","cluster"}`;
`400 wallet_required` for a guest profile. V5 M9h: at most `RPC_READ_RATE_LIMIT_PER_MINUTE` (30) per client IP per
minute (`429 rate_limited`), counted per bearer token when one is sent, else per client IP. The client IP is the
last `X-Forwarded-For` hop; if Railway's edge ever puts its own address there, unauthenticated callers share one bucket.

`GET /api/treasury` (public) -> `200 {"enabled":true,"address","lamports","sol","cluster","floatLamports","low","readAt"}`
(`enabled:false` and nulls without a treasury key). V5 M9h: the balance is read from the chain at most every 30 s
(`readAt`); `low` is true below `TREASURY_MIN_LAMPORTS`.

`GET /api/switches` (public, V5 M9h) -> the live-API switches as this server runs them:
`{"wagersHumanEnabled","raidLootEnabled","islandSaleCarriesRum","dailyRaceFixedSalt","weeklyRewardCheckedOnly","weeklyRewardsAuto","botStakes":false,"raidStakes":false,"mintDailyBudget","sessionLifetimeDays"}`.
The owner kept every switch on (H2, 13 Oct: "don't switch off anything for now"); each one is a variable (table above), so
flipping one is a Railway variable change and a restart, no code change.

`POST /api/deposits/verify` (auth, wallet profile) `{"signature","purpose":"generic","refId":null,"minLamports":1,"memo":null}`
-> `200 {"depositId","signature","wallet","purpose","refId","lamports","sol","memo","status":"verified","slot","createdAt","alreadyVerified"}`.
Checks on chain (confirmed commitment): the transaction succeeded, your wallet signed it, its system transfers from
your wallet to the treasury add up to at least `minLamports`, and its memo contains `memo` when given. Each signature
is credited once: the same player, purpose and `refId` again returns the stored deposit with `alreadyVerified:true`;
any other reuse is `409 signature_used`. Errors: `409 tx_not_confirmed` (retry in a few seconds), `400 tx_failed`,
`wrong_sender`, `wrong_recipient`, `amount_too_low`, `memo_mismatch`, `invalid_signature`, `invalid_purpose`,
`wallet_required`; `503 treasury_off`. `purpose`: 1-32 of `a-z 0-9 _`.
**Reserved purposes (13 Oct hotfix):** `wager*`, `raid*`, `market*`, `island*`, `buy*`, `refund*`, `payout*` answer
`400 purpose_reserved`: those deposits are recorded only by their own endpoints (`POST /api/wagers/{lobbyId}/deposit`,
`POST /api/market/{id}/buy`), which check amount, memo, wallet and seat. (Before, a 1-lamport transfer registered here
as `{"purpose":"wager","refId":<wagerId>,"minLamports":1}` was then accepted as a full stake; same for market buys.)
Wager and market endpoints also re-check a stored deposit against the current state under their lock (player and
wallet, purpose and refId, memo, at least the current stake or price, not used or refunded before).

`GET /api/deposits/mine`, `GET /api/payouts/mine` (auth) -> the last 50 (`{"deposits":[...]}` / `{"payouts":[{"payoutId","wallet","lamports","sol","reason","refId","status","signature","error","explorerUrl","createdAt","sentAt"}]}`).
Deposit `status`: `verified`, `consumed` (used by a seat or a purchase), `refunding` (a refund was decided: it is never
attached again), `refunded`.

**Payouts (13 Oct hotfix).** Payout `status`: `pending` (recorded; in flight once it has a `signature`), `sent`
(confirmed on chain), `unconfirmed` (sent or maybe sent, result unknown), `failed` (proven not to have moved SOL: never
sent, rejected by the node's preflight, failed on chain, or expired). How a payout is sent:
1. The row is inserted first. A unique index on `(Reason, RefId)` over the rows that are not `failed`
   (`IX_Payouts_Reason_RefId_Live`, migration `AddPayoutSafety`) allows one live payout per obligation (wager win, one
   deposit's refund, one weekly place, one market sale): a concurrent or repeated pay gets the existing row back.
2. The transaction is signed and its `signature` and `LastValidBlockHeight` are saved **before** it is sent.
3. Only a preflight rejection (`-32002`/`-32003`: not forwarded) marks it `failed`. Any other send error (timeout,
   HTTP error, unreadable answer) and any error or timeout (45 s) while polling leaves it `unconfirmed`; no lock is held
   while polling.
4. The reconciler (`LedgerService.ReconcileAsync`, every `SWEEP_SECONDS` on the wager sweep timer) resolves `pending`
   and `unconfirmed` payouts older than 3 minutes by signature: confirmed -> `sent`; failed on chain -> `failed`;
   unknown **after** the chain passed its `LastValidBlockHeight` -> `failed` (it can never land). A `pending` row with
   no signature was never sent -> `failed`. Only `failed` payouts are retried (once a minute, at most 10 times).
5. The migration turns pending payouts from before the hotfix that have no signature into `unconfirmed` (the old code
   saved the signature only after confirming, so they may have been sent): **check them on the explorer by hand**; the
   reconciler leaves them alone. It also marks any existing duplicate live payout of one obligation (a past double
   payment) with `RefId` `<refId>#dup<id>` so the index can be built; the oldest keeps the key.

RPC errors reach clients as `502 solana_rpc_error` with a generic message; the node's raw answer is only logged.

Owner tools (header `X-Admin-Key: <ADMIN_KEY>`):
- `GET /api/admin/treasury` (V5 M9h) -> a fresh balance, `floatLamports`, `low`, `lowSince`, `mintsToday`,
  `mintDailyBudget`, `pendingMints`, `payoutsNotSent` (count and lamports per status) and `topUpRule`.
- `GET /api/admin/report?since=2026-10-12` (V5 M9h; default 12 Oct 2026) -> every payout since that date (with explorer
  links), every Island NFT mint (claimed, sent, minted, pending or failed, with the owner's wallet), and totals:
  `payoutTotals` per reason and status, `payoutsSentLamports`, `mintsMinted`, `depositTotals` per purpose and status.
- **Treasury top-up rule** (C9h.2, until M11 splits the keys): keep the treasury at or above `TREASURY_MIN_LAMPORTS`
  (2 SOL); when `/api/treasury` reports `low` (or the logs show `TREASURY LOW`), the owner tops it up to at least twice the
  float the same day (`POST /api/admin/airdrop` on devnet, or a transfer from the owner's wallet).

`POST /api/admin/payouts` `{"wallet","lamports","reason"}` with an
`Idempotency-Key: <1-64 chars>` header (required, `400 idempotency_key_required`; the same key returns the first
payout and never pays twice) and at most `ADMIN_PAYOUT_MAX_LAMPORTS` (`400 amount_too_high`) -> payout view;
`POST /api/admin/airdrop` `{"wallet":null,"lamports":1000000000}` (devnet faucet; default the treasury).

### Hackathon: 1v1 SOL wagers (devnet)

Code: `Hackathon/WagerService.cs`, `Endpoints/WagerEndpoints.cs`. Both players need a wallet profile; stakes are
0.001..0.5 SOL (`WAGER_MAX_LAMPORTS`). Errors are the usual `{"error","message"}`.

1. **Stake.** `POST /api/lobbies` `{"public":false,"stakeLamports":10000000}` creates a staked lobby (`400 invalid_stake`,
   `400 wallet_required`, `503 treasury_off`). Or, host only, before any deposit: `POST /api/wagers/{lobbyId}`
   `{"stakeLamports":N}` (0 removes the stake) -> lobby JSON (`409 wager_locked` after a deposit, `403 not_host`).
2. **Deposit.** Each human seat sends a SOL transfer of at least `stakeLamports` from its wallet to `wager.treasury` with
   the memo `wager.memo` (`wager:<lobbyId>`), then `POST /api/wagers/{lobbyId}/deposit` `{"signature":"<base58>"}`
   -> lobby JSON. Same checks and errors as `/api/deposits/verify` (`409 tx_not_confirmed`: retry in a few seconds;
   `memo_mismatch`, `amount_too_low`, ...) plus `404 no_wager`. The same signature again is idempotent.
   **V5 M9h:** a transfer from your wallet to the treasury that cannot pay the stake (less than the stake, another memo
   or none) is no longer left in the treasury with `400 amount_too_low` / `memo_mismatch`: it is recorded and refunded
   once (payout `wager_refund`, refId `dep:<depositId>`), and the call answers `409 deposit_refunded` with the reason
   and "Your deposit is being refunded to your wallet" (or "was refunded"). A transfer not signed by your wallet
   (`wrong_sender`) or not to the treasury (`wrong_recipient`) is still refused without a record (nothing of yours to
   return). With `WAGERS_HUMAN_ENABLED=false` every new deposit is refunded this way.
   Since 12 Oct a valid transfer is always verified and recorded **before** the seat and wager checks: when the seat
   is no longer valid (not seated or left, the wager closed or cancelled, the match started, or this seat already paid
   with another transfer) the transfer is refunded once (one payout per deposit) and the call returns
   `409 deposit_refunded` with the reason in `message`. Sending the same signature again returns `deposit_refunded`
   without a second refund. (Replaces the old `not_in_lobby`, `already_deposited`, `already_started` and
   `wager_closed` errors here, which left the SOL in the treasury.)
3. **Start.** The match starts only when both seats are ready **and** the stake is funded (the second deposit or the
   last "ready" starts it, as before). **Bot matches are free play (13 Oct hotfix):** a bot match is decided by the
   host's client, so the house no longer matches stakes. `POST /api/lobbies/{id}/bot` on a lobby with an open stake,
   and `POST /api/wagers/{lobbyId}` with a stake on a lobby that seats the bot, answer `409 bot_stakes_off`; a staked
   lobby never starts against the bot. (The game client's Vs Captain Marrow stake chips should offer only "No stake".)
4. **Result.** When the match is decided (relay referee: sunk, time, forfeit; or the two reports) the winner is paid
   `potLamports` = 2x stake from the treasury. Draw, disputed or abandoned match: every deposit is refunded. A vs-house
   wager staked before the hotfix is refunded whatever the result (`house_won` only appears on older rows). A deposit
   is checked again when it is attached (at least the **current** stake, the lobby's memo, your wallet), and a deposit
   with a refund decided is never attached again (`409 deposit_refunded`). A guest leaving before the start gets
   their deposit back (the wager returns to `awaiting_deposits`); a lobby closed or expired before the start refunds
   everything. Settlement runs right after the match ends and every `SWEEP_SECONDS`; failed payouts (e.g. an empty
   treasury) are retried each minute, up to 10 times, never paid twice.

Every lobby JSON (HTTP and WebSocket, `GET /api/lobbies/{id}`) now has `wager` (null without a stake):

```json
"wager": {
  "wagerId": "...", "kind": "pvp", "lobbyId": "...", "matchId": null,
  "stakeLamports": 10000000, "stakeSol": 0.01, "potLamports": 20000000, "vsHouse": false,
  "status": "awaiting_deposits | funded | settled | refunded | cancelled", "funded": false,
  "result": null, "memo": "wager:<lobbyId>", "treasury": "<treasury address>", "cluster": "devnet",
  "host":  { "playerId": "...", "deposited": true,  "signature": "..." },
  "guest": { "playerId": "...", "house": false, "deposited": false, "signature": null },
  "winnerPlayerId": null, "payoutSignature": null,
  "payouts": [ { "payoutId", "wallet", "lamports", "sol", "reason": "wager_win | wager_refund", "refId", "status", "signature", "explorerUrl", "createdAt", "sentAt" } ],
  "createdAt": "...", "settledAt": null
}
```

`result`: null while open, `paid_winner`, `house_won`, `refunded`, `cancelled`. `GET /api/lobbies/public` entries gain
`stakeLamports` (null without a stake). `GET /api/wagers/{lobbyId}` (public) -> the same `wager` object (`404 no_wager`);
`GET /api/wagers/mine` (auth) -> `{"wagers":[...]}` (last 20). Owner: `POST /api/admin/wagers/sweep` runs the settlement
pass now -> `{"wagers":N}`.

### Hackathon: weekly kill leaderboard SOL rewards

Code: `Hackathon/RewardService.cs`. The top places of a week's kills board (map `saltwind`) win devnet SOL
(`WEEKLY_REWARD_SOL`, default 0.5 / 0.3 / 0.2). Only wallet profiles are eligible: the places go to the best wallet
players in board order (a guest above them is skipped); since 13 Oct only runs that passed the run checks count
(`WEEKLY_REWARD_CHECKED_ONLY`, default `true`). A week is recorded once (`WeeklyRewards`) and each place is paid once:
the payout key is `(week, map, rank)` (`wr:<yyyyMMdd>:<map>:<rank>`), so rows deleted and recorded again never pay a
place twice (payouts keyed by row id before the hotfix are still counted). Failed payouts are retried. A background job
pays the previous week (hourly, `WEEKLY_REWARDS_AUTO`).

`POST /api/admin/rewards/weekly` (X-Admin-Key) `{"weekStart":"2026-10-05","dryRun":false,"mapId":null}` (all optional;
default the previous week; any date is snapped to its Monday) -> the rewards view; `dryRun:true` shows the winners
without writing or paying. A real run pays only a week that has **ended** (`409 week_not_ended` for the current week,
13 Oct hotfix); a future week is `400 future_week`.

`GET /api/rewards/weekly?weekStart=&mapId=` (public; default the previous week) ->
`{"weekStart","weekEnd","mapId","dryRun","alreadyRecorded","checkedOnly","table":[{"rank","lamports","sol"}],
"winners":[{"rank","playerId","name","tag","wallet","walletShort","kills","runSeconds","checked","lamports","sol","payout":<payout view or null>}]}`.

`GET /api/leaderboard/weekly` and `/previous` entries gain `wallet` (shortened `AbCd...WxYz`, null for guests) and
`reward` (`{"lamports","sol","status","signature"}` or null).

### Hackathon: daily island race

Code: `Hackathon/DailyRaceService.cs`. One race per UTC day: everyone plays the same seed on the same map and races to
`targetKills` (100) kills; fastest time wins. The seed is HMAC-SHA256(`DAILY_RACE_SALT`, `yyyy-MM-dd`); unset, the public
default salt while `DAILY_RACE_FIXED_SALT` is true (the owner kept it on), else a secret salt (V5 M9h). A day's seed is
stored with its race, so a restart never changes it.

`GET /api/race/daily` (optional auth) -> `{"race":{"raceId","date":"2026-10-12","seed","mapId","targetKills","goal",
"opensAt","closesAt","serverNow"},"entries":N,"me":null | {"rank","timeSeconds","timeMs","attempts","verdict","submittedAt"}}`.
Start the run with `race.seed` on `race.mapId`; stop the clock at `targetKills` kills.

`POST /api/race/daily/entry` (auth) `{"date":"2026-10-12","seed":123,"timeSeconds":118.4,"kills":100,"level":12,"clientVersion":null,"log":null}`
-> `{"accepted":true,"improved","race":{...},"verdict","checked","best":{"rank","timeSeconds","timeMs","attempts","verdict","submittedAt"}}`.
One best time per player and day (a slower time only counts an attempt). `date` defaults to today; yesterday's race
takes entries for 30 minutes after midnight. `log` (optional, the same run log as `POST /api/runs`; send `level` with
it) is scored by the run checks in shadow mode: `verdict` is `pass`, `flagged`, `log_rejected`, `seed_mismatch`, ...
or null without a log; it never refuses the entry. Errors: `400 seed_mismatch`, `400 target_not_reached`,
`422 implausible_time` (under 10 s for 100 kills or over 7200 s), `400 race_not_open`, `409 race_closed`, `400 invalid_date`.

`GET /api/race/daily/board?date=&limit=50` (optional auth) -> `{"race":{...},"total","entries":[{"rank","playerId","name",
"tag","wallet","timeSeconds","timeMs","attempts","verdict","checked","submittedAt"}],"me":{...}|null}`.

### Hackathon: islands, RUM, raids, island NFT market (Solana devnet)

Code: `Endpoints/IslandEndpoints.cs`, `Hackathon/IslandRules.cs` (every number), `IslandService.cs`, `RaidService.cs`,
`MarketService.cs`; migration `AddHackathonIslands`. Every write needs a **wallet** profile (`403 wallet_required` for
guests). Errors are `{"error","message"}` as everywhere. Optional env `PUBLIC_API_URL` (e.g.
`https://api.roguepirates.fun`) is the base of the NFT metadata/image URLs; without it the request's host is used.

**Economy** (V5 D19(1) as changed by D24): an island brews RUM into its own storage; **60% goes to the vault** (never
raidable), 40% is **exposed**. A won raid takes **25% of the exposed RUM** (12.5% on a partial win) and gives the island
a **4-hour shield**; attacking never ends your own shield. Raid fee **25 RUM**. An island is raidable by one attacker at a time for up to 20 minutes. Voyages: `POST /api/runs` adds
`kills / 10` RUM (rounded down) for wallet players and returns `rumEarned`, soft-capped per run and per UTC day since 13 Oct
(`RUM_RUN_CAP`/`RUM_RUN_HALF`, `RUM_DAY_CAP`/`RUM_DAY_HALF`; kills are client-reported).

| Building (`kind`) | Levels | Upgrade cost (RUM) to level 1/2/3/4/5 | Effect |
|---|---|---|---|
| `rum_store` | 1-5 | -, 300, 700, 1500, 3000 | 120/180/260/360/500 RUM per hour; storage cap 480/900/1600/2600/4000 |
| `vault` | 1-3 | -, 400, 1000 | cap x1 / x1.5 / x2 |
| `cannon_tower` | 0-3 | 250, 600, 1400 | defender: +1 extra wave per level, weapon level 1/3/5/7 |
| `walls` | 0-3 | 200, 500, 1200 | defender hull x1.0 / 1.25 / 1.5 / 1.75 |

**Island view** (returned by most island calls):
`{"islandId","name","slot","slotName","region","x","y","level","owner":{"playerId","name","wallet","walletShort"},"isMine",
"nft":{"status":"pending|minted","mint","mintSignature","error","explorerUrl","metadataUrl","imageUrl"},
"rum":{"exposed","vault","total","cap","ratePerHour","vaultPercent":60,"lastAccrualAt"},
"buildings":[{"kind","level","maxLevel","nextCost"}],"defence":{"hullMultiplier","extraWaves","weaponLevel","difficulty","cannonTower","walls"},
"shieldUntil","shielded","listed","listingId","priceLamports","raidInProgress","raidable","createdAt",
"defenceRecord":{"raidsAgainst","defended","fallen","successPercent":0-100|null,"label"}}`.
`x`,`y` are on a 1000x1000 world map; `raidable` = not yours, not shielded, not listed, nobody raiding it.
`defenceRecord` (12 Oct): finished raids against the island (`won`, `partial`, `lost`, `abandoned`; started raids do not
count); `defended` = the raider lost or abandoned; `successPercent` = defended * 100 / raidsAgainst rounded down, `null`
and `label:"No raids yet"` with none. The record belongs to the island (it survives a sale).

`POST /api/islands/claim` `{"name":"Tortuga"}` (3-16 chars) -> island view. One island per player (`409
already_has_island`; buying more on the market is allowed); a random free slot of the 24 (`409 world_full`). Mints the
Island NFT to your wallet (name `Rogue Island: <name>`, symbol `RPISL`, uri = the metadata URL). If the treasury is off
or the mint fails, the island is still created with `nft.status:"pending"` and `nft.error`.
**V5 M9h treasury bounds:** each wallet claims **one free island ever** (`409 island_claim_used` once it claimed one, even
after selling it; profiles that owned or sold an island before M9h count as having claimed); at most `MINT_DAILY_BUDGET`
(20) mints a UTC day (`429 mint_budget`, nothing claimed; claims racing at the limit can still be claimed and wait for the next day's mint).
A wager deposit must be a transfer made after the wager was created (`400 tx_too_old`, nothing recorded), so an older
transfer to the treasury is never refunded through a wager.
`POST /api/islands/{id}/mint` (owner) retries -> island view; `502 mint_failed`, `409 already_minted`, `503 treasury_off`,
`429 mint_budget`, `409 mint_in_flight`.
**No double mint (V5 M9h):** the mint's address (`nft.pendingMint`), signature and last valid block height are stored
**before** the transaction is sent. A retry first checks that attempt on chain: the mint and its metadata exist, or the
signature confirmed: minted (no new transaction); failed on chain, or the chain is 150 blocks past its last valid block height and the
mint account does not exist: cleared, and only then a new mint (a new address) is sent; otherwise `409 mint_in_flight`.
A send or confirmation whose result is unknown leaves the attempt pending; the sweep (every `SWEEP_SECONDS`) resolves
pending attempts older than 2 minutes the same way (it never sends a mint).

`GET /api/islands/mine` (auth) -> `{"rum":<balance>,"islands":[view]}`. Island reads (this, `GET /api/islands/{id}`,
the world map) show the RUM accrued up to now but write nothing and take no lock (V5 M9h); every write accrues first.
`GET /api/islands` (auth optional, sets `isMine`) -> `{"mapWidth":1000,"mapHeight":1000,"slots":[{"slot","name","region","x","y","islandId|null"}],"freeSlots","islands":[view]}`.
`GET /api/islands/{id}` -> island view. `GET /api/islands/{id}/metadata.json` -> Metaplex JSON
`{"name","symbol","description","image","external_url","attributes":[{"trait_type","value"}],"properties":{"category","files"}}`.
Attributes: Location, Region, Slot, Level, Rum Store, Vault, Cannon Tower, Walls, `Rum Per Hour` (number),
`Raids Defended` ("3 of 4") and `Defence Success` ("75%" or "No raids yet").
`GET /static/islands/{slot}.png` -> a generated 256x256 placeholder picture.

`POST /api/islands/{id}/collect` (owner) -> `{"collected","fromStore","fromVault","rum","island"}` (all of the island's
RUM, vault included, to your balance). `POST /api/islands/{id}/build` `{"kind":"walls"}` (owner) -> `{"kind","level","cost","rum","island"}`.
Errors: `403 not_your_island`, `409 island_listed`, `400 invalid_kind`, `409 max_level`, `400 insufficient_rum`.

**Raids.** Raids have **no SOL stake** (owner, 12 Oct: SOL wagers are only in 1v1 PvP); the only cost is the RUM fee.
`POST /api/raids/start` `{"islandId"}` ->
`{"raidId","islandId","islandName","startedAt","expiresAt","feePaid":25,"rum","defender":{...},"defence":{...},"loot":{"exposedRum","ifWon","ifPartial","lootPercent":25},"wager":null}`.
`defence` is the island's level (see **Island defence designs** below):
`{"isCustom","seed","timeLimitSeconds":960,"windowSeconds":60,"design":{"windows":[...15],"guardian":{...}}}`.
The **defender config** for the AI-captain fight:
`{"captainId":"ai_captain","captainName":"Captain Marrow","seed","hullMultiplier","extraWaves","weaponLevel","cannonTower","walls","difficulty":1-7,"timeLimitSeconds":960}` (kept for older clients).
Errors: `400 own_island`, `409 island_shielded`, `409 island_listed`, `409 raid_in_progress`, `400 insufficient_rum`,
`400 raid_stakes_off` (any `stakeLamports` > 0), `400 invalid_stake` (negative). Starting a new raid abandons your own
unfinished one. The start JSON also has `"practice":false`.

`POST /api/raids/practice` `{"islandId"}` (owner only, 12 Oct: "test your island's defences") -> the same JSON as a start
with `"practice":true`, `"raidId":null`, `"expiresAt":null`, `"feePaid":0` and zero loot; `defence` is the island's saved
design with a fresh seed. Nothing is charged or stored (no RUM fee, no raid row, no shield, no loot, no record), so there
is no result to report. Allowed while the island is listed or shielded. Errors: `403 not_your_island`, `404 island_not_found`,
`403 wallet_required`.

`POST /api/raids/{id}/result` `{"won":true,"guardianSunk":true,"minuteReached":9,"seconds":520,"runLog":null}` (within
**20 minutes** of the start; `seconds` 1..1020 (the 16-minute fight + 60 s grace) and not more than the time since the
start; `minuteReached` 0..16, the last one-minute window reached) ->
`{"raidId","result":"won|partial|lost","rumLooted","rum","seconds","guardianMinute","minuteReached","guardianSunk","defenderShieldUntil","wager"}`.
The attacker must **conquer** the island: **won** = `guardianSunk:true`; **partial** = reached the guardian's minute
(`minuteReached >= guardianMinute`, the minute in the design the raid started with) without sinking it; **lost**
otherwise. Older clients that send neither `guardianSunk` nor `minuteReached` keep the old rule (`won`, `partial` flags).
Errors: `409 raid_expired` (marked `abandoned`), `409 raid_finished`, `400 invalid_seconds`, `400 invalid_minute`,
`400 invalid_result` (`guardianSunk` with `minuteReached` before the guardian's minute), `403 not_your_raid`.
`runLog` is accepted and not checked yet. `GET /api/raids/mine` rows also carry `guardianMinute`, `minuteReached`, `guardianSunk`.

`RAID_LOOT_ENABLED=false` (V5 M9h switch, on by default): a won or partial raid loots 0 RUM; the start JSON's `loot` has
`enabled:false` and zero `ifWon`/`ifPartial`. The shield and the defence record are unchanged.

`POST /api/raids/{id}/wager` (the old raid stake deposit) -> `410 raid_stakes_off`. Raids staked before stakes were
switched off still settle as before, so no deposit is lost: their `wager` is
`{"wagerId","stakeLamports","status":"awaiting_deposits|funded|settled|cancelled","memo":"raid:<raidId>","treasury","payoutIfWon","depositSignature","payout"}`;
a full win pays **2x** (`raid_win`), a partial win refunds the stake (`raid_refund`), a loss or expiry keeps it, and an
unfunded one is cancelled at the result. If the payout fails the wager stays `funded` with `payout.status:"failed"`:
`POST /api/raids/{id}/settle` retries (never pays twice). New raids always have `wager: null`.

`GET /api/raids/mine` -> `{"attacks":[row],"defences":[row]}` (a started raid past its 20 minutes shows `abandoned`; the
sweep stores it), row
`{"raidId","islandId","islandName","attacker":{"playerId","name","wallet"},"defender":{...},"result","rumLooted","seconds","startedAt","finishedAt","wager"}`.

**Island defence designs** (migration `AddIslandDefence`; code `Hackathon/DefenceService.cs`, numbers in
`IslandRules`). Each island is a roguelike level its owner designs: a **15-minute timeline of 15 one-minute windows**
(window `m` starts at `(m-1)*60` s), each a list of `{kind, count}`, plus a **guardian boss** and the minute it arrives
(2..15, default 5; it arrives at the start of that window). A raid conquers the island by surviving the waves and sinking
the guardian. Islands without a saved design use the **default design** (a ramp from fodder to shooters to elites, the
Barnacle King at minute 5), so every raid has one; a saved design that no longer validates also falls back to it.

Budget per window: `floor((20 + 6*minute) * (100 + 15*(cannonTower + walls)) / 100)` points (26, 32, ... 110 on a bare
island; x1.9 with both buildings at 3). Validation: every window's cost <= its budget, counts 0..60, known kinds only,
each kind once per window, minutes 1..15 once each (missing windows are empty), a known guardian, guardian minute 2..15.

| Cost | Kinds (ids as in the game's `V4/Data` Enemies and Variants) |
|---|---|
| 1 | `deckhand_dinghy`, `swordfish` (fodder) |
| 2 | `rammer_raft`, `cutthroat_skiff`, `hammerhead`, `shark` (melee) |
| 3 | `cannonboat`, `needle_sloop` (shooters), `bomb_boat` (bomber), `manta_ray` (melee) |
| 4 | `gunner_cog` (shooter), `bomb_ketch` (bomber), `boarding_barge` (heavy) |
| 5 | `kraken_spawn` (heavy) |
| 12 | `elite_hammerhead`, `elite_kraken_spawn` (elites) |
| 30 | `reef_crab_miniboss` (miniboss) |

Guardian bosses: `barnacle_king` (the Saltwind boss; the only boss in the game data today, the default).

Design JSON (PUT body, and `design` in every view, where each window also has `startSecond`, `cost`, `budget` and the
guardian `displayName`, `arrivesAtSecond`; those extra fields are ignored on PUT):
`{"windows":[{"minute":1,"enemies":[{"kind":"deckhand_dinghy","count":12}]},...],"guardian":{"kind":"barnacle_king","minute":5}}`.

- `GET /api/defence/catalog` -> `{"windows":15,"windowSeconds":60,"maxCount":60,"raidTimeLimitSeconds":960,
  "budget":{"base":20,"perMinute":6,"percentPerBuildingLevel":15,"buildings":["cannon_tower","walls"],"formula","baseBudgets":[15]},
  "guardianMinute":{"min":2,"max":15,"default":5},"kinds":[{"kind","displayName","role","cost"}],
  "bosses":[{"kind","displayName","isDefault"}],"defaultGuardian","defaultDesign":design,
  "presets":[{"id":"swarm|gunline|elite_guard","name":"Swarm|Gunline|Elite guard","description","design"}]}`.
  Presets and the default fit the base budgets, so they are valid on every island (PUT a preset's `design` as is).
- `GET /api/islands/{id}/defence` (anyone; auth optional) -> `{"islandId","islandName","isMine","isCustom","updatedAt",
  "cannonTower","walls","budgetBonusPercent","budgets":[15],"design","costs":{"<kind>":cost},"kinds","bosses","maxCount","guardianMinute"}`.
- `PUT /api/islands/{id}/defence` (owner, wallet) with the design -> the same view. Errors: `403 not_your_island`,
  `409 island_listed`, `404 island_not_found`, and `400 invalid_defence` with every problem:
  `{"error":"invalid_defence","message","errors":[{"minute":1|null,"field":"windows[0].enemies[1].count","code","message"}]}`,
  codes `over_budget`, `unknown_kind`, `invalid_count`, `duplicate_kind`, `duplicate_minute`, `invalid_minute`,
  `too_many_windows`, `invalid_window`, `unknown_guardian`, `invalid_guardian_minute`, `missing_design` (`minute` is null
  for problems outside a window).

**Market** (treasury = escrow). Listing view:
`{"listingId","status":"active|paid|sold|cancelling|cancelled","priceLamports","priceSol","nftMint","memo":"buy:<listingId>","treasury",
"island":{"islandId","name","slot","slotName","region","level","levels","ratePerHour","defenceRecord"},"seller":{"playerId","name","wallet","walletShort"},"buyer",
"escrowSignature","paymentSignature","transferSignature","payoutSignature","transferExplorerUrl","payoutExplorerUrl","error","createdAt","closedAt"}`.

1. Sell: the client sends the island NFT (SPL transfer, amount 1) from the seller's wallet to `treasury` (from
   `GET /api/treasury`), then `POST /api/market/list` `{"islandId","priceLamports","signature"}` (price
   1,000,000..100,000,000,000) -> listing (`active`). The island is locked (no raids, collect or build). Errors:
   `403 not_your_island`, `409 nft_not_minted`, `409 already_listed`, `400 nft_not_received`, `409 signature_used`, `400 invalid_price`.
2. `GET /api/market` -> `{"treasury","cluster","listings":[listing]}` (active); `GET /api/market/{id}`; `GET /api/market/mine` (as seller or buyer).
3. Buy: send `priceLamports` to `treasury` with memo `buy:<listingId>`, then `POST /api/market/{id}/buy`
   `{"signature"}` -> listing. The server moves the NFT to the buyer, the island to the buyer, and pays the seller.
   `status:"sold"` when all is done; `status:"paid"` with `error` when a step failed: `POST /api/market/{id}/complete`
   (buyer or seller) finishes it (re-sending the same buy also works). Errors: `409 listing_not_active` (do not pay),
   `400 own_listing`, deposit errors (`amount_too_low`, `memo_mismatch`, `tx_not_confirmed`, ...).
   **13 Oct hotfix:** the payment (memo `buy:<listingId>`, any amount) is verified and recorded first, then checked
   against the listing as it is now under the market lock. A payment that cannot buy it (listing sold or cancelled
   meanwhile, too little, not used before) is **refunded in full** (payout reason `market_refund`, refId
   `dep:<depositId>`) and the error message says so; paying more than the price buys the island and refunds the
   difference (`market_refund`, `over:<depositId>`). The sweep (every `SWEEP_SECONDS`) retries those refunds and
   refunds a payment recorded but never used after 2 minutes.
4. `POST /api/market/{id}/cancel` (seller, `active` only) returns the NFT -> `cancelled`; on failure `cancelling` with
   `error` (call cancel again).
5. `ISLAND_SALE_CARRIES_RUM` (V5 M9h switch, on by default: the island changes hands as it is). `false` (I26.2): when the
   island moves to the buyer its store and vault RUM are credited to the seller and its buildings go back to their start
   levels (in one transaction, once).

### Hackathon: demo reset (owner tool)

Code: `Hackathon/DemoResetService.cs`. Removes test wallets' data from a live API before a demo (for example the devnet
smoke test's wallets A and B, whose 4000- and 1500-kill runs top the week's board).

`POST /api/admin/demo/reset` (X-Admin-Key; `503 admin_off` without `ADMIN_KEY`, `403 not_admin` with a wrong key)
`{"wallets":["DcUm...","3cm6..."],"clearWeeklyRewards":true,"weekStart":"2026-10-12","dryRun":false}`.
`dryRun` defaults to **true** (counts only, nothing written); send `"dryRun":false` to delete. At most 50 wallets;
`400 nothing_to_reset` with no wallets and no `clearWeeklyRewards`. For the players signed in with the listed wallets
it deletes, in one transaction:
- their `Runs` with their `RunLogs` and `RunVerdicts`, all their `WeeklyBests` (every week), and their `DailyRaceEntries`;
- `Raids` where they are the attacker or defender, or on an island being released;
- their islands (`Islands` + `IslandBuildings` rows; the world slots are free again). The Island NFTs are **not**
  touched on chain: they stay in the wallets. An island with an open market listing (`active`, `paid`, `cancelling`:
  NFT in escrow or a trade in progress) is skipped and listed in `skippedIslands`;
- closed (`sold` / `cancelled`) market listings they sold or bought, or of a released island.

With `clearWeeklyRewards:true` it also deletes every `WeeklyRewards` row of `weekStart` (any date, snapped to its Monday;
default the current week). **13 Oct hotfix:** a week with any reward payout sent or in flight is refused
(`409 rewards_paid`, nothing deleted; the dry run reports `weeklyRewards.paidPayouts`), since deleting a paid week let
it be recorded and paid again. A week with no payout can be cleared and is then paid once it has ended. Players,
sessions, deposits, wagers and payouts are never deleted, and RUM balances are not reset.

-> `{"dryRun","wallets":[{"wallet","found","playerId","name"}],"deleted":{"runs","runLogs","runVerdicts","weeklyBests",
"dailyRaceEntries","raids","islands","islandBuildings","marketListings","weeklyRewards"},"releasedIslands":[{"id","name",
"slot","nftMint"}],"skippedIslands":[{"id","name","slot","reason":"listing_open","listingStatus"}],
"weeklyRewards":null | {"weekStart","rows","payoutsKept","paidPayouts"},"notes":[...]}`.

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
and hits are client-reported within the caps above, so a modified client can still lie about them.

**Value moves on devnet (since the hackathon, 12 Oct).** Devnet SOL is paid from the treasury on client-reported results:
1v1 wagers between humans (relay-decided sinkings), weekly prizes (checked runs only), market sales; RUM moves on raids
and island sales. The owner kept every flow on (H2, 13 Oct); each has a switch (`/api/switches`). One key is treasury,
escrow, payout signer and mint authority until M11 splits it: the bounds are one free island claim per wallet, the daily
mint budget, the treasury float with its alarm, idempotent payouts and the owner's report. Mainnet stays off
(`ALLOW_MAINNET`) until the authoritative match server (M13b) and the legal review (D8).
