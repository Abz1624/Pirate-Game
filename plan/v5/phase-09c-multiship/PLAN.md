# M9c — Multi-ship runtime and the go/no-go: implementation plan (11 Oct)

Drafted by a read-only planning agent on HEAD `4c050833`; owner away, so the product decisions below use the
recommended defaults until the owner answers (logged in `plan/v5/OPEN-QUESTIONS.md`, 11 Oct). Paths are relative to
`Pirate Game Unity/Assets/_Game/`.

## Today's code (what blocks several ships)

- `RunManager` (Foundation/Core) is a static `Instance` mixing world state (clock, state, pause, `RunRng`, seed, stage,
  tier, objective) and ship state (`Stats`, gold, kills). About 55 uses in shipped Runtime/Foundation, 25 in DevTools,
  130 in tests.
- `V4Game` wires one of everything (one `ShipPort`, `RunPort`, `BuildModule`, `WeaponModule`, `PlayerDamageService`,
  `RunLifecycle`); the ship is found with `FindFirstObjectByType<PlayerShipController>`.
- Modules already sit behind small contracts (`IProgressionRun/Ship`, `IPickupRewards`, `IShipState`, `IEnemyTarget`,
  `IHostileShotTarget`, `IPlayerHull`, `IStageRun/Ship`, `IRunStatus`, `IRunRandom`): the refactor is mostly rewiring
  adapters.
- Enemies take one target (`EnemySimulation.Bind(..., IEnemyTarget)`); hostile shots, hazards and barrels take one
  `ShipPort`; spawn placement and the leash use one simulation view.
- Gameplay steps on Unity's frame (`V4Game.Update`, `V4FrameDriver`, `RunManager.Update`); ship code reads Unity time;
  the server holds runs with `Time.timeScale` (`NetServerHost.cs:89`, `NetServer.cs:421/440/597`).
- One session per process (`NetServer.Current`, `LoadSceneMode.Single`, process statics: `PlayBounds`,
  `IslandObstacles`, `StageBalanceProfileContext`, `ActiveRun`, `RunRequest` flags, `RunCombatSignals.Instance`,
  `CombatExperimentProfile`, `WeaponFeatureFlags`; about 14 `Physics.*` scene queries).
- Chests pause the run (`InteractionModel`, `POIChoice`); the server skips the choice today.
- Presentation sinks for mirroring already exist (`IWeaponEffects`, `IProjectileViews`, `IHostileShotViews`,
  `IHazardView`, `IEnemyEffects`, `IPickupViews`, `IPowerupViews`, `IBurningPatchView`); M9b's `NetEnemyViews` is the
  pattern.

## Target architecture (no new singleton)

- Foundation: `SessionClock` (UnityFrame mode = today; Driven mode stepped by the server, so a hold is "not stepped");
  `RunSession` (plain C#, the world run); `RunManager` per ship (stats, gold, kills, hold/choice state, run id, its
  session), injected into the ship components.
- Runtime: `IRunSession`, `IShipRun`, `ShipRoster`/`ShipSlot` (seat, ports, build, weapons, damage, lifecycle, run log),
  `IEnemyTargets`, `IShipsView`. Ship 0's random streams alias the run's streams, so one ship stays bit-identical.
- Config: `CrewConfig` (player-count scaling, XP share, away policy, card mode, chest rule, end rule; no effect with one
  ship); `NetConfig` in V5/Net outside the voyage ConfigSet (protocol rates, radii, caps, byte budget).
- Net: `NetServer` (listener, admission, routing by ticket `mid`), `NetMatch` (one session), `NetSeat` (one player);
  client `RemoteShipViews` and `NetViewReplayers`.

## Ordered sub-steps (one agent each; each ends with zero console errors, its tests, the voyage fingerprint, the
weapon goldens and the M9a determinism test; from S2 the full suites)

| Step | Goal | Est. h |
|---|---|---|
| S0 | Byte-exact single-player voyage fingerprint (seeds 8282, 4242; per-minute log, final-state hash, enemy-position hash at 60/120/180 s) committed as a golden; baseline perf and server tick | 3-4 |
| S1 | `SessionClock`; `V4Game.StepFrame(dt)`; the server drives sessions at 30 Hz with 2 substeps and never writes `Time.timeScale`; structure test banning frame time in gameplay | 8-10 |
| S2 | Split `RunSession` from a per-ship `RunManager` behind `IRunSession`/`IShipRun`; `RunManager.Instance` banned in shipped code; behaviour-neutral | 10-14 |
| S3 | `ShipRoster`: N ships in one run, per-ship build/weapons/damage; enemies choose the nearest ship (deterministic schedule, hysteresis); leash and spawns against every ship's view; kill credit by owner seat; Scenario Lab `extraShips`; isolation tests | 18-24 |
| S4 | Shared-run rules via `CrewConfig` (cards without pause, collector pickups, per-ship chest prompts, stow on hidden tab, sinking and spectating, player-count scaling); solo server chest uses the cards' hold (removes the M9b skip); run checks regenerated and API deployed | 12-16 |
| S5 | Protocol v6: seats, other ships (interpolated), per-seat choices, spectate, compact card strip; 2 clients in-process | 14-18 |
| S6 | Mirroring of shots, effects, pickups, chests and threats; delta-encoded enemies; per-client byte budget with priorities; per-category byte metrics | 18-24 |
| S7 | Several sessions per process (additive scenes with their own physics; process statics become per-session) | 16-22 |
| S8 | Ram jolt smoothing (option A: impact-flagged corrections spread over 0.25 s) | 3-5 |
| S9 | Load-test harness: 2 then 4 headless clients in Docker, `load200` test preset (never in release), sessions-per-process variant | 8-10 |
| S10 | Railway runs (server only; clients from this PC), two-browser WebGL check, `REPORT.md` with D9c.1 and go/no-go, clean-up proof | 6-8 |
| S11 | Gate: suites twice, `dotnet test`, goldens, fingerprint, determinism, playable check, v5-reviewer, commit `V5 M9c: multi-ship runtime` | 6-10 |

Total about 122-165 agent-hours. Order S0 → S11; S8 optional.

## Mirroring and bandwidth design (S5, S6)

- Named messages, one coalesced unreliable frame per snapshot where it fits (about 1,100 B), quantized relative to the
  receiver's own (or spectated) ship.
- `snap` own ship (as v5, 15 Hz, about 2.2 KB/s); `ships` other ships (11 B each, 15 Hz near, 5 Hz far); `enemies`
  delta-encoded against the client's acked baseline (about 4-5 B per moving enemy); `fx` events from server-side view
  recorders (salvos as one event, dead reckoning for projectiles, state-bearing events repeated twice and de-duplicated);
  `stage` reliable on change; `choice` per seat; `threats` at 2 Hz for the minimap; `defs` once.
- Up: input as v5 plus a 2 B enemy-baseline ack (about 1.0-1.2 KB/s).
- Budget: 1,250 B per snapshot per client (about 18.75 KB/s) filled by priority (own ship, choices/stage, other ships,
  own events, near/mid enemies, others' events, far enemies, threats); starved entities gain priority.
- Expected at 4 ships and 200 enemies: about 9-14 KB/s p50 and 18-24 KB/s p95 per client (target <= 20, p95 <= 30).

## Product decisions (defaults used until the owner answers)

1. Shared voyage type: co-op PvE (no friendly fire, no ship collisions).
2. Cards in a shared run: ship keeps sailing and firing; compact card strip (1/2/3, click, tap); no timeout; sets queue
   per ship. Solo server voyages keep the pause.
3. Hidden tab in a shared run: the ship is stowed (untargetable, inactive, faint marker), returns with a 3 s shield;
   removed after 10 minutes.
4. Player-count scaling (n = ships afloat and present): spawn rate and active cap x (1 + 0.75(n-1)), server cap 200;
   normal HP x1; elites/minibosses HP x (1 + 0.5(n-1)); boss HP x (1 + 0.75(n-1)) at summon; scheduled beats x (1 +
   0.5(n-1)); enemy damage unchanged.
5. XP, gold, health: collector only (config switch `xpShare: Collector | Shared`); kill credit to the killing blow;
   ship-scoped powerups to the collector, world-scoped for everyone.
6. Chests: a prompt per player, no pause; first Take wins (taker pays); Skip closes it for that player.
7. Sinking: a sunk player spectates; each player's results end at their sinking; the voyage ends when all are sunk or
   abandoned, or on the objective; no revives; any ship's Beacon summons the boss.
8. Seats from `RP_MATCH_SEATS` and tickets sharing a match id; start when all seats are in or 20 s after the first; no
   late join.
9. Several sessions per process: in-process additive scenes with own physics (alternative: a supervisor running N
   processes).
10. Ram jolt: option A smoothing.
11. Hard per-client bandwidth cap with priorities.
12. Exploration fog and Beacon discovery shared across the crew.
13. Run checks for shared voyages: shadow verdict per player, marked `shared`, not on the weekly board.
14. `CrewConfig` changes the voyage config hash: re-export, regenerate rules, deploy the API (as in M9b); `NetConfig`
    stays out of the hash.
15. Railway spend for M9c: under $1, test service deleted after each session (D23, D26).
16. Keep NGO 2.13.3 and the patched UTP 2.7.4 unless the load test shows a transport limit.

## Risks

Single-player regression from the refactor (mitigated by the S0 fingerprint, behaviour-neutral one-ship steps,
aliased streams, structure tests); determinism traps in multi-target logic; bandwidth at 4 ships with late-game builds;
server CPU on Mono at 4 ships and 200 enemies (IL2CPP fallback untried); hidden static state with several sessions;
card and chest UX without a pause (two code paths); config-hash ripple into the run checks; heavy in-process tests (keep
2 clients in-process, 4 only in Docker); scope creep into M21-M23 (lobby, matchmaking, PvP stay out).

## Changes from the plan review (11 Oct: approve with changes, 0 blockers)

Adopted (they override the sections above where they differ):

1. **Bandwidth judged on the wire with a quality floor.** The memo reports wire bytes (payload + framing), and the
   budget is set so the wire total stays at or under 20 KB/s; it also reports update age for near enemies and own events
   (p95/max), deferred or dropped fx and starvation counts. A cap that only starves entities does not count as a pass.
2. **Each client's view keeps the device cap (108 enemies, D17 Q21)** through interest management; the server may
   simulate up to 200 for the D9c.1 load.
3. **Two gated commits:** `V5 M9c-1: multi-ship runtime` after S0-S3 (refactor + roster; owner check that solo play is
   unchanged) and `V5 M9c-2: shared voyages and the go/no-go` after S4-S11. Every step is tagged so it can be reverted.
4. **New S0b (4-6 h), before S2:** an early go/no-go probe on today's M9b server: the `load200` preset plus 3 synthetic
   ghost ships emitting fx, measuring enemy delta bytes and tick p95 on Mono, checked against D9c.1.
5. **Process statics move into `RunSession` in S2/S3** (also `SaltwindRunResetLogic.RetryGeneration`,
   `RunRequest.Seed/Mode/PendingIssuedRunId`, `SfxService.Instance`, `FlyingHealthBillboard._cachedCamera`, `DemoMode`,
   `SlotPool.Current`), with a structure test banning new mutable statics. S7 keeps the physics scenes (simulated
   manually; all 17 `Physics.*` query sites routed to the session's scene), measures tick p95 per process frame with N
   sessions, tests that matches never see each other's messages, and keeps the ticket replay cache process-wide.
6. **Stronger S0 fingerprint:** fixed input track and capture frame time; Scenario Lab jumps covering card picks,
   chests, powerups, elites, boss summon, overtime and sinking; a solo server golden and the M9a run-log hash; listed
   per-ship fields (stats, gold, kills, XP, hull, weapons); proven stable twice alone and inside the full suite before
   freezing; Editor-only.
7. **Shared-voyage run checks offline:** the server writes per-seat run logs (with presence and stow events) checked in
   `dotnet test`; no upload, no API deploy, no live board change in M9c (upload and settlement in M13b). `CrewConfig`
   records its own hash in the run log instead of changing the voyage config hash, so solo rules and clients don't change.
8. **PvP hooks without PvP:** owner seat on every damage source, a team id per seat, `CrewConfig` switches for friendly
   fire and ship collisions (tested off). One end rule only (AllSunk); scaling as a single config multiplier, no
   per-class tuning (M23 designs those).
9. **Hidden tab default is "hold, still targetable"** (as V5_PLAN states); stow + shield is a `CrewConfig.awayPolicy`
   option for the owner.
10. Ship 0 shares the run's streams only when the crew is 1; with 2+ ships every ship gets its own stream from the seed
    and seat.
11. State-bearing events go on the reliable channel (WSS is TCP: no repeats); frames stay under 1,400 B; tests include
    bursts of TCP stalls.
12. The compact card strip gets a D9 layout test (48/72 px targets, safe insets, 16:9 and 20:9, no hover; taps don't
    steer).
13. S10 puts one WebGL Chrome client in the 4-ship `load200` session for the 60 fps check.
14. **S8 (ram jolt smoothing) is required** (D9c.1 says prediction "as M9b"; the ram exception is still provisional).
15. Multi-ship gates are driven by ticks through S1's Driven clock; one PlayMode test renders `RemoteShipViews`.
16. Re-estimated: `RunManager.Instance` 77 uses in Foundation/Runtime + 9 in V5/Net, 43 in DevTools, 153 in tests; 66
    `Time.*` reads in Foundation/Runtime (S1 about 10-14 h, S2 about 14-18 h). `CrewConfig` gets an id, schema and
    balance version (D10). S10 writes out the D23 usage check and the D26(d) secret removal.

Order now: S0 → S0b → S1 → S2 → S3 → **M9c-1 gate** → S4 → S5 → S6 → S7 → S8 → S9 → S10 → S11 (**M9c-2 gate**).
