# M0 — Composition root, V4 scenes, determinism harness

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `8a8fbd1c` (docs) |
| Editor | Unity 6000.3.11f1, Editor platform |
| Status | Passed (playtested 2026-09-26) |

## What M0 delivers

- **Assemblies** per plan §4.1: `RoguePirates.V4.Config`, `RoguePirates.V4`, `RoguePirates.V4.Presentation`, `RoguePirates.V4.DevTools` (define constraint `UNITY_EDITOR || DEVELOPMENT_BUILD`), `RoguePirates.V4.Editor`, and two test assemblies under `Tests/V4/`.
- **Core:** `V4Game` (composition root, `[DefaultExecutionOrder(-100)]` like `V3RunBootstrap`), `RunContext` (interface-only contracts), `ModuleRegistry` (stable tick order), `RunClock` (60 Hz accumulator, max 5 steps, pause-reason set), `CombatEvents` (per-tick buffer, publish order, no publishing while dispatching), `RunRequest`, `V4Phase`, `DeletedInPhaseAttribute`, `V4SceneNames`.
- **Legacy shell:** `LegacyRunShell` runs the `V3RunBootstrap` sequence in the same order through named adapters. The adapters and their deletion phases:

  | Adapter | Deleted in |
  |---|---|
  | `LegacyStageAdapter` | M7 |
  | `LegacyEnemyWorldPort` | M8 |
  | `V4LegacyBridge`, `LegacyDatabaseAdapter`, `LegacyBuildAdapter` | M9 |
  | `LegacyRewardsAdapter` | M12 |
  | `LegacyProfileAdapter`, `LegacyWorldAdapter`, `LegacyPresentationAdapter`, `LegacyEventFeed`, `LegacyRunShell` | M14 |

  `V4LegacyBridge` is the only class that names `EncounterSpawner` or `WeaponController` (a test checks this).
- **Scenes** (copied with fresh GUIDs by `RoguePirates/V4/Create V4 Scenes`, `V4SceneSetup.cs`):
  - `V4Boot`: `V3BootLoader` swapped for `V4BootLoader`.
  - `V4MainMenu`: `V3MainMenuController` swapped for `V4MainMenuController`; the Captain carousel is reused.
  - `V4SaltwindRun`: `V3RunBootstrap` swapped for `V4Game` at the same component index on `/Systems`, with the same serialized references.
  - `V4ModuleLab`: a copy of `Test_WeaponEnemy`.

  All four are appended to the build list after the V3 scenes.
- **Restart and navigation:** Retry reloads `V4SaltwindRun` with a retry flag (random seed, as V3 `RestartRun` does), Home loads `V4MainMenu`, and a console restart uses `RunRequest` with a fixed seed. Both legacy results screens get their placeholder buttons rebound after the run ends.
- **Tuning Console** (F1, gamepad Select or ⚙): header, search, "pause while open", and a **Run** section (restart on seed, retry, time scale, god mode, jump to time, force death). It lives on its own `V4DevTools` object (see R-72).
- **Trace harness:** `TraceBoot`, `RunTraceRecorder`, `PlayerPilot` (`player-v1`, default: kites enemies, dodges hostile shots and hazard circles, collects pickups, stays inside the storm edge, steers around islands, backs out only when an island is ahead; private legacy pools are read by reflection so V3 code stays untouched), `ScriptedPilot` (`wander-v1`, kept only for reference), `TraceComparer` (exact and parity), `V4TraceSession` (prefs snapshot/restore, pinned Captain, 1080p Game view), plus `tools/run-trace.ps1`, `tools/playable-check-m0.ps1` and `tools/check-write-set.ps1`.
- **Docs:** `docs/V4_IMPLEMENTATION_PLAN.md` §5.0 (adopted order M0–M15 with deviations), §6 (order) and §7 (status) rewritten.

## Legacy reader audit (component removed from the V4 scene: `V3RunBootstrap`)

| Reader | Where | Classification |
|---|---|---|
| `V3RunResultsScreen.OnRestartClicked` → `V3RunBootstrap.TryRequestRestart()`, else load V3 menu | `V3RunResultsScreen.cs` L109–125 | **Adapted**: `V4Game` rebinds both results screens' Retry/Home buttons to V4 restart and `V4MainMenu` once the run ends |
| `WeaponEnemyPlaytestBootstrap` / `Director` disable any `V3RunBootstrap` | `Playtest/*.cs` | Null-safe absent (only in the lab scene) |
| `MapGeneratorWorkbench` | `World/MapGeneratorWorkbench.cs` L83 | Not in V4 scenes |
| Editor scene-setup tools | `Editor/HighSeas*.cs` | Editor only, target V3 scenes |

Scene-load paths reachable in `V4SaltwindRun`:

| Path | Status |
|---|---|
| Results Retry / Home | Rebound to V4 |
| Console restart | `RunRequest` + `V4SaltwindRun` |
| `AppRoot.LoadScene` | Not called by V4 |
| `MenuScreens` ("Run" / "MainMenu") on `/RunCanvas` | No reachable caller during a Saltwind run |

The profile context still ends when leaving for the menu, because `V4MainMenuController.Start` calls `AppRoot.EndActiveSaltwindRunContext()` and `V4Game.OnDestroy` ends it through `LegacyProfileAdapter`.

## Results

| Check | Result | Evidence |
|---|---|---|
| V3 baseline, EditMode | 187 tests, 184 pass, 3 known failures | `baseline-editmode-results.json`, `baseline-failures.txt` |
| V3 baseline, PlayMode | 29 tests, 28 pass, 1 known failure | `baseline-playmode-results.json` |
| V4 EditMode (`RoguePirates.V4`) | 18 / 18 pass | `editmode-results.json` |
| V4 PlayMode (`RoguePirates.V4`) | 3 / 3 pass: boot → menu → run; stale miniboss arena cleared on reload; boss kill → death → settles once, no double pay | `playmode-results.json` |
| V3 regression, PlayMode | 31 tests, 30 pass, only the known storm-arena failure | `v3-regression-playmode-results.json` |
| V3 regression, EditMode | 205 tests (187 V3 + 18 V4), 202 pass, only the 3 known failures | `v3-regression-editmode-results.json` |
| Traces: V4 == V3, seeds 101 / 202 / 303 | **Exact match**: key header fields plus every event line, including frame numbers. Covered path: first run, Captain Rook, actives off, chests skipped, boss summoned at 4:00 and killed by the harness at 5:00, then play to death and settlement. Not covered by exact traces: retry, captain actives, chest purchase, non-default starting weapons, same-tick boss death (boss kill → death → settle-once is covered by a PlayMode test) | `v3-trace-*.jsonl`, `v4-trace-*.jsonl`, `trace-comparison.txt` |
| V4 replay: seed 101 twice | Exact match | `v4-trace-seed-101-a/b.jsonl` |
| V3 run-to-run determinism: seed 101 twice | Exact match (D5 exact mode holds), shown with both pilots before the boss kill was added; V4 replay plus V4 == V3 covers it since | (earlier runs) |
| Automated playable check | 8 / 8 steps OK, 0 console errors in the final run | `playable-check/` |
| Write set | Clean, except a pre-existing checkout artifact (below) | `diff-summary.md` |

## Independent review (2026-09-26)

A read-only reviewer checked commit `1811ffe9` against V3 and the plan. No blockers: the V4 run matches V3 call for call, the traces genuinely match (non-trivial, V4 scene really runs V4Game), and nothing outside the write set changed. Findings and what was done:

| # | Finding | Resolution |
|---|---|---|
| 1 | Major: CombatEvents delivered one batch per frame, not per tick; legacy events stamped with legacy time | **Fixed:** `EndTick()` after every module tick; legacy events published outside a tick are delivered at end of frame with the current tick stamp; the feed stamps V4 clock time. Reader contract documented |
| 2 | Major: modules got `RunEnd` only on scene unload, possibly with `SimTime=0`/objective lost | **Fixed:** `RunEnd` is raised on the run-ended edge with values captured then; `OnDestroy` is only a fallback. PlayMode test asserts exactly one `RunEnd` with the objective |
| 3 | Trace evidence narrower than claimed; header drift not compared | **Fixed:** the harness now kills the boss at 5:00 (victory, objective and boss Marks in every trace); `CompareExact` also compares seed, pilot, dt, captain, platform, boss timings and config hash (new test); claim narrowed above |
| 4 | Retry toast/log said "locked seed" for the random retry seed | **Fixed:** toast and log follow the retry flag like V3 |
| 5 | Stale trace request file could hijack a later manual session | **Fixed:** requests older than 2 minutes are ignored |
| 6 | Trace prefs snapshot could be overwritten or lost on an Editor crash | **Fixed:** pending snapshots are restored before a new one; snapshot persisted to `Temp/` and restored on the next Editor load. Game view resolution is still left at 1080p (not restored) |
| 7 | V4 clock pause not mapped to the legacy pause | **Fixed:** the clock's pause set mirrors `RunManager` every frame (UserPause → Manual, other reasons → Choice) |
| 8 | Unrecorded deviations (RunRequest fields, menu, commit, legacy statics in V4Game, static event) | **Fixed/recorded:** seed resolution and results rebinding moved to `Legacy/` (`LegacyRunShell.ResolveSeed`, `LegacyResultsRebinder`); commit hash now in trace headers; the rest recorded below |
| 9 | PlayMode teardown restored the real save before leaving the run scene | **Fixed:** teardown leaves the scene first; snapshot covers captain, actives and run snapshot keys too |
| 10 | Console: time scale carried across restarts; typing in the console reached gameplay (B summons the boss) | **Fixed:** time scale resets when the run scene unloads; the game keyboard is muted while a console text field has focus. "Pause while open" only clears a pause it set |
| 11 | V3 retry-only calls missing | **Fixed:** Retry calls `SaltwindRunResetLogic.NotifyRetryStarting()` and `RunSnapshotService.Clear()` like V3 |
| 12 | Error path: V3 still ticks worlds when the profile fails to resolve | Recorded (V4 refuses to run instead; only reachable with a broken profile) |
| 13 | Core edges: missed first RunState event; registering during a tick; buffer lifetime; GUID asmdef refs; bridge tag 9 cannot enforce the M3 spawn removal | **Fixed:** feed publishes the current state on attach; registry throws on registration during a call (new test); lifetime documented; layering test forbids GUID refs and Editor assemblies; spawn path split into `V4LegacySpawnBridge` tagged M3 |
| 14 | Docs: R-55/R-71 marked applied too early; R-68 wording; statuses; stale diff summary | **Fixed** in the register and summary |

Speed-up investigation (your question): runs are CPU-bound in simulation. Not rendering the world diverged the run (new defect R-74, simulation reads renderer bounds) and gave no speed-up; suppressing log output kept exact parity but gave no speed-up either. Both were reverted. See the reply for the options that do save time.

Determinism follow-up: after adding the boss kill, a V4 seed-101 replay diverged by one gold at 8:30 (positions and kills identical). Cause: R-74, since skinned-mesh bounds only update while some camera draws them, so results could depend on Editor window visibility. Trace runs now pin visual state (skinned meshes update off-screen, animators always animate; header field `visualPins`, compared by `CompareExact`), and all goldens and V4 traces were re-recorded with the pin.

## Deviations from the plan, recorded

1. **Catalogs on the loaded database:** M0 applies the weapon and specialist catalogs to the loaded `GameDatabase` exactly as V3 does, with no deep clone. The deep clone moves to M2, where V4 builds its own runtime database. Reason: a clone changes nothing observable in M0 and would be a second copy of V3's catalog logic.
2. **Per-stream `RunRng` draw counts** are not in the trace. `RunRng` exposes no counters, and adding them means editing shared code (D1). Exact event matching already covers RNG divergence in M0–M2.
3. **Boss at 240 s, as planned (changed after playtest feedback).** The first pilot (`wander-v1`) steered blind and pinned itself against the first island it met (0 m moved for 100+ s in every seed), so it died at 150–225 s and the goldens used 120 s. At your request the pilot was rebuilt to play like a person (`player-v1`); it survives 620 s (seed 101), 414 s (202) and 633 s (303) with zero stuck samples, and reaches level 9–12. The boss is summoned at 240 s in every run; the pilot kites it rather than killing it. All V3 goldens and V4 traces were re-recorded with it; V4 == V3 exactly for all three seeds, and V3 and V4 each replay seed 101 identically.
4. **`V4ModuleLab`** keeps its legacy playtest bootstrap until M2, when archetypes exist to spawn.
5. **`V4DeterminismTests` [V4Slow]** is replaced by the trace harness replay (seed 101 a/b above). A trace needs a fresh Play Mode session, which a PlayMode test cannot provide.
6. **Goldens were recorded in `V3SaltwindRun`** instead of a V4 copy that still ran `V3RunBootstrap`. The V4 scene is a straight copy, so this is the same content. The header `scene` field differs and is ignored by the comparison.
7. **`RunRequest` carries seed and retry only.** Captain, preset and pilot are added when a module needs them (preset M1, captain M9); the menu keeps V3's `V3CaptainSelection` hand-off.
8. **`V4Game.RunStarted` is a static event** (a hook for DevTools, holds no state) besides `RunRequest`.
9. **Traces kill the boss at 5:00 through the weapon-hit path**, because the autopilot kites the boss instead of fighting it.

## Findings during M0

- **R-72 (new):** `EnemyViewPool` deactivates `/Systems` around every new enemy view, so components on `/Systems` get OnDisable/OnEnable mid-run. It closed the Tuning Console. The console now lives on its own object.
- **R-73 (new), profile hash drift:** the stage profile `ContentHash` snapshots weapon stats from the in-memory `GameDatabase`, which every run mutates with the catalogs. Verified in the Editor: clean load `954db8…`; after one catalog application `9da95d…`; after further applications it keeps changing (`da2393…` was seen in a trace header); after unload it is back to `954db8…`. `V4TraceSession` now unloads the database before each trace or check, and all final traces carry `954db8…`. This is V3 behavior; the M2 deep clone removes it.
- **R-68 fix verified:** `V4LegacyBridge.ResetSpawnDirectors` now calls `V3MinibossArena.ResetForRun()` first, and `SceneReload_ClearsStaleMinibossArena` covers it. The traces were re-recorded after the change and still match exactly.
- **R-67a (new), real PlayerPrefs:** V3's own suites change the Editor's PlayerPrefs. `V3IntegrationTests` deletes `v3_home_seed` and `v3_first_conquest_complete`; PlayMode boss kills lock a home seed. Running the baselines therefore changed these keys in this Editor, and both are now absent. The values from before this session are unknown. `tools/prefs-guard.ps1` backs up and restores the registry key around future suite runs.
- **Test side effects cleaned up:** the V3 suites rewrote `plan/saltwind-verification/balance-lab/*`, the TMP fallback font atlas and the line endings of `Data/EncounterSpawnSchedule.asset`. All three were restored with `git checkout`, and V3 data was not otherwise touched.
- **Tooling:** `capture_game_view --save_path` always writes under `Assets/`, so the playable check decodes the inline image instead. A stray `Assets/Temp/` from one test capture was deleted. The 1200-run matrix baseline failure is a CLI main-thread timeout (the CLI was polled while the test held the main thread), not a gameplay failure.

## Trace runner benchmark (26 Sep, your request)

Option 1 is the Editor, one run at a time. Option 2 is a Windows development player built from a mirror copy of the project (`Builds/V4Trace/`, git-ignored, its own Library, so your Editor stays on WebGL), with runs in parallel.

| Measure | Editor, sequential | Player, parallel (4) |
|---|---|---|
| Same 4 V4 runs (seeds 101, 101 replay, 202, 303) | about 1,645 s (27.5 min) | **547 s (9.1 min)** |
| Game time simulated per wall second | 1.4 | 4.0 |
| Build cost | none | first build 828 s (one-time import); incremental 35 s |
| V3 reference runs per phase | none needed (V3 frozen) | none needed (recorded once, 408 s for 3) |

Decision: the player is about 3x faster per phase, so it is the standard trace platform from M1 on. The Editor harness stays for debugging and the playable check. Player goldens: `player/`. V4 == V3 exactly on all three seeds, and the replay is identical even with four copies running at once.

Harness changes for the player:
- `TraceBoot` reads `-v4trace <request>` in a player and starts from the empty `V4TraceLauncher` scene.
- It pins every PlayerPref a run reads to one state (Rook, actives off, aim visuals off, developer preset off, First Conquest done, tutorial seen, no home seed). This matters because parallel copies share one PlayerPrefs store, and First Conquest is written on victory and read by level-up offers mid-run. The Editor harness pins the same keys now too, closing a gap where Editor traces depended on your save state.
- The recorder writes atomically, quits the player when done, and logs the screen size, which is compared.

The Editor goldens in this folder predate the pins and are kept as M0 history.
