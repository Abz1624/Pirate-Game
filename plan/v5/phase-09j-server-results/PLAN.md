# M9j — The server decides or checks game results (design and step 1)

Owner request (20 Oct): make the server decide (or verify) game results instead of trusting the game client. Phase
start commit: `41561be9` (branch `v5-live-game`). Server suite at the start: 260/262 (2 devnet skips).

## 1. What the client reports today, and what it moves

| Mode | Client-reported | Value it moves (devnet) | Today's server checks |
|---|---|---|---|
| 1v1 relay (`MatchRelay.cs`, `HitReferee.cs`, `MatchService.cs`) | each ship's own hull, max hull and `alive`; hits (refereed: drafted weapon, range, a DPS budget at 1.5x the worst legit build) | staked human matches pay the winner 2x (`WAGERS_HUMAN_ENABLED`, D31); Elo | hits refereed; sinking is the victim's own report; at a time-out with exactly equal accepted damage the reported hull decides; rooms are in memory, so after a restart one client report decides after 60 s (`SingleReportTimeout`) |
| Conquest raids (`RaidService.cs` 193-269) | `guardianSunk`, `minuteReached`, `seconds` (and the old `won`/`partial` flags) | 25% (12.5% partial) of the exposed island RUM, a 4 h shield on the defender, the island's defence record (an NFT trait, so a sale price) | `seconds <= elapsed + 5`; a guardian sink not before its minute **only when `minuteReached` is sent**; the old `won` flag path has no timing check at all; the `runLog` field is accepted and ignored (the client sends none) |
| Daily race (`DailyRaceService.cs`) | time to 100 kills, kills, level; the run log is optional (the client sends none) | nothing (a board; no prize) | seed match, a 10 s floor (600 kills/min), the seed is public (`DAILY_RACE_FIXED_SALT`) |
| Weekly board and SOL (`RewardService.cs`, run checks v9) | a run log per voyage | 0.5/0.3/0.2 devnet SOL a week, paid only for checked runs (`WEEKLY_REWARD_CHECKED_ONLY`) | plausibility rules v9 in shadow mode (D23, D24); voyage RUM soft-capped (40/run, 150/day) |

The hackathon review's numbered findings (server #3, #7, #8, #25; client #9; gameplay #2) are not in the repo
(`plan/hackathon/review/` holds only the test run); this design works from the code above and the M9i "Remaining"
list, which names the same three: client-reported PvP hull, raid results and race times.

## 2. Options, cheapest to strongest

Railway rates: $20 per vCPU-month, $10 per GB-month, $0.05 per GB out (`ANSWERS.md`); measured: one server-run voyage
costs about **$0.007 per hour** (0.11 vCPU, 0.22 GB, 14 MB out; `phase-09b-netcode/REPORT.md`), server tick p95
0.4-2 ms. The workspace has a $15 hard limit and a $10 alert against a $6-9 baseline (D23).

| # | Option | Stops | Does not stop | CPU / Railway $ per 1,000 | Dev time | Latency / UX |
|---|---|---|---|---|---|---|
| L0 | **Server-side bounds on the report**: the server's own clock (elapsed since a server-recorded start), seeds it issued, per-pair rate limits, settlement only from a server decision | instant or impossible claims (a guardian sunk before it arrives, a race time longer than the time since its start), replays, farming one defender, a lost room settling on one client report | a patient cheater who waits the real time and then lies | ~0 (a few DB reads) / **$0.00** | 0.5-1 day | none; a refused claim answers 400 at once |
| L1 | **Run-log checks per mode** (the M9a/v9 structure and rate rules; mode-specific rules for raids and races) | edited totals, logs that disagree with the claim (boss defeated before its arrival, kills per minute beyond the bounds, a race log that reached the target earlier or later than claimed) | a consistent forgery inside the bounds (M9a's tests show one passes when tuned to the margin) | < 1 ms a run / **< $0.01** | 1-2 days (client sends the log for raids and races) | none; verdict stored with the result |
| L2 | **Relay referee state** for 1v1: the server tracks each ship's hull from the hits it accepted (the client's own PvP hit window: x0.06, 0.5 s pooling, 5% cap), heals from the barrels it awarded, and decides the end | an "immortal" client that ignores hits, a self-reported hull deciding the time-out tie-break, a room lost on restart paying a stake | a cheater who reports a larger max hull than its build has (the ceiling is an assumption until it is derived from config data); a hacked client dealing more damage than its build can (the DPS budget is 1.5x the *worst* legit build, about 8x a typical one); armour, max hull and regeneration live on the client, so the hull is an **estimate** | ~0 (per hit message) / **$0.00** | 1 day | none in shadow; enforced, a wrong estimate could sink an honest ship |
| L3 | **Deterministic replay** of a run on a headless server (all value runs, or sampled, or only prize runs) | any forged voyage, if the replay is exact | nothing more than L4 for single-player runs; useless for live PvP | about 27,000 ticks at 1-2 ms = 30-60 s CPU + 3 s start + 0.25 GB for a minute per 15-min run: **about $0.3-0.5 per 1,000 runs**; prize runs only (3 a week): ~$0 | 2-4 weeks **and a research risk**: the S0 fingerprint is byte-exact on one platform only; WebGL (wasm) vs Linux x64 float results and PhysX Rigidbodies are not shown to match, and the log keeps 2,000 sampled inputs, not every tick's input and card choice (log grows to roughly 40-80 KB). D6/Q29 chose plausibility checks because exact replay needed Photon Quantum | value pending until the replay ends (minutes, queued) |
| L4 | **Server-run sessions** (M9b/M9c Netcode server, hosted in M13b): the client sends inputs only | every client-side result lie, including damage | network feel (270 ms from India measured; the ram knockback correction is open) | 15-min voyage or raid **$1.75-1.90**, a 5-min 1v1 at the M9c estimate (0.25 vCPU, 0.4 GB, $0.018/h) **$1.50**, (the $0.007/h already includes egress); at 3,000 value sessions a month about **$6**, assuming M13b's Serverless sleep and several sessions per process (M9b measured one per process), which with the $6-9 baseline reaches the $15 limit: value sessions only, not every voyage | M13b + M15/M17/M18/M22 as planned (weeks) | the session must be online; a dropped socket resumes in 2 s (M9b) |

## 3. Recommendation

**M9j now (L0 + L1 + L2 shadow, server-first, no paid service):** the cheap checks below, enforced where an honest
client can never fail them and in shadow mode where it could; the owner decides enforcement with the shadow data.
**Later, as planned, not duplicated here:** raids as server sessions in **M15** on **M13b**; the daily race
server-timed in **M13b** (H7) and the one-attempt race in **M17**; prize-eligible weekly runs as ranked server sessions
in **M18**; the server-run 1v1 in **M22** (it retires the relay, `POST /api/matches/{id}/result` and this phase's hull
estimate); the race start (`POST /api/race/daily/start`, `DailyRaceStarts`) is interim too, replaced by M13b's
server-timed race and M17. L3 replay is **not recommended** unless the owner wants the weekly prize runs checked before M13b: then a
one-day spike in M9c (the S0 fingerprint on a Linux server build against a WebGL client) decides whether it is possible
at all (question J3).

Per mode:

| Mode | M9j step 1 (now) | Enforced, shadow or off | Final authority |
|---|---|---|---|
| 1v1 wagers | the server hull estimate per seat and its verdict on every match end; at a time-out with equal accepted damage the larger estimated hull share wins (else a draw); a stake settles only on a server decision (`DecidedBy = server_*`): a match decided by client reports (its room lost on a restart, or never relayed) is refunded | tie-break and refund **enforced**; the server sinking a ship **shadow** (`RELAY_HULL_ENFORCE`, default off; enforcing needs D23(b)'s false-positive budget and a dispute path first, J1) | M22 |
| Raids | the guardian's arrival checked for every win and partial (the old flags too); `minuteReached` not beyond the reported time; the client sends the raid's run log, checked in shadow (structure, seed, time, boss defeated after the arrival, v9 rates) with its verdict on the raid | timing **enforced** (an honest client cannot fail it; boundary tests feed the client's own formulas); log verdict **shadow** (loot still moves, D24/D31) | M15 on M13b |
| Raid limits | at most `RAID_PAIR_LIMIT_PER_DAY` raids by one attacker on one island in 24 h; the defence record counting one entry per attacker, island and UTC day (`RAID_RECORD_ONE_PER_DAY`) | built, **off** by default: both are new gameplay limits against D15 (limits off by default), D19(1) and D24 (no daily loss cap), so the owner decides (J4) | M15 |
| Daily race | a server start (`POST /api/race/daily/start`, keyed by the race date, one row per player and race, the latest start kept); the entry's time checked against the server's elapsed time since that start (30 s slack for a start posted late) and against the run log's minute of reaching the target | **shadow** (`raceCheck`: `ok`, `no_start`, `too_fast`, `time_mismatch`; the entry still ranks; "checked" needs a pass and `ok`; the race pays nothing) | M13b (server-timed), M17 |
| Weekly SOL | nothing new (already checked runs only, v9) | - | M18 |

Cost of step 1: no new Railway service; one extra row read or write per raid, race start or match end: **$0** extra.
Migration `AddServerResultChecks` adds two nullable columns on `Raids`, two on `DailyRaceEntries` and a new table
`DailyRaceStarts`; nothing is overwritten or dropped, so no backup is required (D25 c), though the owner's backup to-do
stands.

### Definitions (I9j.3)

- Guardian arrival (run seconds) = (guardianMinute - 1) x 60, the game's `RaidHud.GuardianSeconds`.
- The client reports `seconds = min(ceil(run), floor(wall) - 1)` and `minuteReached = run >= 960 ? 16 : min(15, floor(run / 60) + 1)`,
  at least the guardian's minute when it was sunk (`RaidHud.Report`, `MinuteReached`).
- Enforced: a win or partial needs `seconds + 5 >= arrival` and the server's `elapsed + 5 >= arrival` (elapsed since the
  raid's start row); `minuteReached <= floor((seconds + 5) / 60) + 1` (capped at 15), and 16 only when `seconds + 5 >= 960`.

### What the hull estimate does not see (why it stays shadow)

Heals and max-hull changes on the client the server cannot see: level-up max-hull syncs with a flat heal
(`BuildModule.SyncMaxHull`), fallback heal offers (`OfferGenerator`), powerup repairs (`PowerupModule`), regeneration
beyond the allowance, armour beyond the assumed x0.5, max hull beyond the assumed ceiling (250 + 50 a server level); hits
relayed while the victim's socket was down (counted by the server, never applied by the client; the relay does not
replay them); pooling at server arrival time rather than the victim's frame time. Each of these makes the estimate
count more damage than the ship took, so a `server_sunk_client_alive` verdict is a lead, not proof. Before any
enforcement (J1): a 0.1% false-positive budget test over honest builds (the `PvpDamageCapTests` builds, armour builds, a
2 s socket drop, level-ups) as D23(b) requires, the seat's away time in the verdict, and a dispute path.

## 4. Step 1 tasks

- I9j.1 Relay hull estimate (`Playtest/HullEstimate.cs`, `MatchRelay.cs`): per seat, accepted hits pooled in the
  client's window (0.5 s, x0.06, 5% of max hull a landing, mirrored from `PvpRules`), max hull from the seat's own
  reports clamped to [50, 250 + 50 x (server level - 1)] keeping the damage taken, health barrels +20%, an assumed
  incoming multiplier of 0.5 and a regeneration allowance of 0.2% of max hull a second between events. End stats and
  `match_end` carry `serverHull`, `serverMinHull` and a per-seat `hullCheck` (`agree`, `server_sunk_client_alive`,
  `client_sunk_first`, `not_tracked` for the bot, `server_sunk` when enforced); `server_sunk_client_alive` is logged
  once as a warning. `RELAY_HULL_ENFORCE=true` (human matches only) sinks a seat once the estimate is below
  `-RELAY_HULL_MARGIN` (0.25), end reason `server_sunk`. The time-out tie-break uses the estimated share.
- I9j.2 Stakes settle only on a server decision (`WagerService.DecideAsync`): a match decided by `agreement` or
  `timeout` refunds the wager (logged). Honest-player effect: a winner whose room was lost on a restart gets the stake
  back instead of 2x (the wager view says `refunded`; J5).
- I9j.3 Raid timing (`RaidChecks.Timing` in `ResultAsync`), as defined above; `400 too_early` / `invalid_minute`.
  The old `IslandApi.ReportRaid` (seconds capped at 360) would now be refused on a late guardian; nothing calls it.
- I9j.4 Raid limits, off by default: `RAID_PAIR_LIMIT_PER_DAY` (0 = off; `429 raid_pair_limit`, no fee charged) and
  `RAID_RECORD_ONE_PER_DAY` (a win if any raid of that attacker's day won, else defended; applies to past raids too).
- I9j.5 Raid run log (`RaidResultRequest.RunLog`, `RaidChecks.ScoreLog`): verdict and reason stored on the raid
  (`Raids.LogVerdict`, `Raids.LogReason`), returned with the result and in `/api/raids/mine`; never a reason to refuse.
- I9j.6 Race start and checks (`DailyRaceService.StartAsync`, `RaceChecks`): `DailyRaceStarts`; `raceCheck` and
  `serverSeconds` stored on the best entry and shown on the board.
- I9j.7 Client (`Assets/_Game/Hackathon`): the raid result and the race entry wait (at most 3 s, real time) for the
  closed run log and send it (`HackathonRules.WithRunLog`, `RunLogWait`); the race entry takes the log's own kills and
  level; the race HUD posts the start when the voyage begins; a raid left mid-voyage reports without waiting.
- T9j.x Server: `M9jServerResultTests.cs` (hull estimate units, the raid timing fed with the client's formulas at the
  boundaries, impossible claims, raid log verdicts, the limits on and off, the race start, slack, late start and
  yesterday's race in the grace), `RelayTests` (immortal client shadow and enforced, an honest exchange never sunk even
  enforced, the tie-break from the estimate), `WagerRewardRaceTests` (a client-decided stake refunded). EditMode:
  `HackathonRulesTests` (the bodies with the log as JsonUtility reads them).
- D9j.1 `server/README.md` (raids, race, 1v1, switches), deploy with `server/deploy.ps1`, live checks.

Automated check: the server suite; live: an instant guardian claim refused (`too_early`), the switches read back, a
race start recorded; honest passes are covered by the boundary tests (the owner's own raid and race logs join the check
once a build sends them).

## 5. Owner questions

- **J1** The server sinking a ship in wagered 1v1 from its hull estimate (this narrows D19(2), "sinking stays
  client-reported until Netcode"): stay in shadow and read the verdicts; enforce for staked matches only after the
  false-positive budget test, the away-time rule and a dispute path (D23(b)) and a run of real matches without a
  `server_sunk_client_alive` on a pair you know is honest (suggested) / never, wait for M22.
- **J2** What happens to a raid or race whose log fails its checks: shadow (value moves, the flag is shown; D24 today) /
  hold the loot as pending until reviewed / refuse. Suggested: shadow until the raid rules have their own bounds from
  real raids, then hold as pending.
- **J3** Cost choice for the strongest step: value sessions on the M13b server (about $1.5-1.9 per 1,000 sessions,
  assuming Serverless sleep and several sessions per process; suggested) / also a one-day replay spike for the weekly
  prize runs (L3, free to run, 2-4 weeks if it works) / neither until M28.
- **J4** Raid limits (built, off): turn on a pair limit (suggested 5 raids by one attacker on one island in 24 hours)
  and the one-entry-per-attacker-and-day defence record (it recounts existing islands' records, an NFT trait)? Both go
  against D15 (limits off by default) and D19(1)/D24 (no daily loss cap). Suggested: the record rule on, the pair limit
  off / both on / both off.
- **J5** D19(8) and D19(2) stopped relay anti-cheat work until M22; this phase adds the hull estimate (shadow) and
  refunds stakes decided by client reports, which also refunds an honest winner whose room was lost on a restart.
  Confirm both (suggested) / pay agreement-decided stakes (both clients named the same winner) and refund only single
  reports / revert.

## 6. Progress

| Task | State | Evidence |
|---|---|---|
| Design (this file), plan rev 20 | done; reviewed (2 blockers, 6 majors, 6 minors: all adopted) | `V5_PLAN.md` M9j, `status.json`, section 7 |
| I9j.1-I9j.6 server | done | `server/`, `server-tests-m9j.trx` (288/290, 2 devnet skips) |
| I9j.7 client | done, compiled in the Editor | `Assets/_Game/Hackathon`, `editmode-m9j.json` (841/841) |
| D9j.1 docs, deploy, live checks | done 20 Oct (`cf59109b`, `1a791034`) | `server/README.md`; live: `/health` ok (postgres); `/api/switches` relayHullEnforce false, relayHullMargin 0.25, raidPairLimitPerDay 0, raidRecordOnePerDay false; a race start then an entry 2 s later: `raceCheck` too_fast (the test entry "M9jCheck" stays on the 20 Oct board, unchecked; no value). The raid timing is covered by the suite (a live raid needs a wallet, an island and RUM; not run) |
| Code review of the diff | done: no blockers, 5 majors, 7 minors (section 8); fixed and redeployed | `live-check-2026-10-20.md`, `server-tests-m9j.trx` (289/291) |

## 7. Plan review (v5-reviewer, 20 Oct)

| # | Severity | Finding | Outcome |
|---|---|---|---|
| 1 | Blocker | The raid pair limit is a new gameplay rule (D15, D19(1), D24), not "an honest client cannot fail it"; defaulted on | Off by default (`RAID_PAIR_LIMIT_PER_DAY=0`), the record rule a switch too, J4 cites the decisions |
| 2 | Blocker | Enforced server sinking has no false-positive test; unseen heals, hits during a dropped socket, arrival-time pooling; D23(b) | Shadow only; "enforce now" dropped from J1; the unseen sources listed; enforcement tied to D23(b) |
| 3 | Major | Max hull clamp: plan said [first, 4x, 20,000], the code a level ceiling; a fake max hull defeats it | Plan matches the code; the gap is stated in L2 and J1; deriving it from config data is left to enforcement |
| 4 | Major | Enforced sinking also narrows D19(2) | J1 and J5 cite D19(2) |
| 5 | Major | Refunding client-decided stakes hits an honest winner after a restart | Stated in I9j.2 and J5 with an alternative |
| 6 | Major | "Arrival" undefined; boundary risk | Definitions above; boundary tests use the client's formulas |
| 7 | Major | Race start posted late; yesterday's race in the grace | 30 s slack, start keyed by the race date, tests for both |
| 8 | Major | The gate tests only refusals | Honest-pass tests (raid boundaries, race, relay exchange) |
| 9 | Minor | Which result counts in the record; retroactive | Defined (a win if any) and stated in J4 |
| 10 | Minor | Tie-break claim overstated; share used the reported max | Reworded; the share uses the clamped max hull |
| 11 | Minor | Egress double-counted; L4 assumptions | Fixed in L4 and J3 |
| 12 | Minor | The race start is also interim | Listed |
| 13 | Minor | The old `ReportRaid` now fails late guardians | Noted in I9j.3 |
| 14 | Minor | Code written before the review | Recorded; defaults left reversible (#1) |

## 8. Code review (v5-reviewer, 20 Oct, `41561be9..db6856cc`)

No blockers. The reviewer found no way an honest client's raid result is refused (the client's guardian time is the
server's own `arrivesAtSecond`, run time never runs ahead of the wall clock, the 5 s slack covers the clamp), no path
that pays a client-decided match, an additive-only migration and no outcome change in shadow mode except the tie-break.

| # | Severity | Finding | Outcome |
|---|---|---|---|
| 1 | Major | A result without a run log gets no verdict, so a cheater leaves it out | Fixed: a raid win or partial without a log is `no_log`; a race entry without one `verdict:no_log`; test |
| 2 | Major | A report posted before any socket reached the relay could decide a staked match by timeout (and now void it by refund) | Fixed: while the room exists, a staked match's reports are kept for the record only; test (early report, then the relay's winner is paid) |
| 3 | Major | The honest raid-log test accepted any verdict; boundary tests copy the client's formulas; no client test for the log wait; no live raid | The honest test now requires none of the raid checks to flag; the copied formulas stay (the client's live inside a MonoBehaviour) and are pinned by the definitions in section 3; no live raid (needs a wallet, island and RUM on the live database: `live-check-2026-10-20.md`) |
| 4 | Major | The game shows none of the verdicts the owner check names | The owner check reads them from the API (`/api/raids/mine`, `/api/race/daily/board`, `/api/matches/{id}`); in-game display left to M15/M22 screens |
| 5 | Major | Evidence thin | `live-check-2026-10-20.md`; the trx re-run after the fixes |
| 6 | Minor | `ScoreLog` had no catch-all | Fixed: `check_failed` stored, the result kept |
| 7 | Minor | The tie-break still uses the reported (clamped) max hull; pooled hits at the end | Comment corrected; kept |
| 8 | Minor | `too_fast` catches a time longer than the time since the start, not shorter | Wording fixed (L0) |
| 9 | Minor | A hull test could not fail | Removed |
| 10 | Minor | `RaidHud.Report` had no try/catch | Fixed |
| 11 | Minor | The record switch loads every finished raid of the listed islands | Kept (off by default; SQL grouping when J4 turns it on) |
| 12 | Minor | `/start` has no own rate limit; older entries now show unchecked | The API-wide rate limit applies; README notes the board change |

