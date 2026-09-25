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
| Traces: V4 == V3, seeds 101 / 202 / 303 | **Exact match**, byte-identical including frame numbers (header excluded) | `v3-trace-*.jsonl`, `v4-trace-*.jsonl`, `trace-comparison.txt` |
| V4 replay: seed 101 twice | Exact match | `v4-trace-seed-101-a/b.jsonl` |
| V3 run-to-run determinism: seed 101 twice | Exact match (D5 exact mode holds) | `v3-trace-seed-101-a/b.jsonl` |
| Automated playable check | 8 / 8 steps OK, 0 console errors in the final run | `playable-check/` |
| Write set | Clean, except a pre-existing checkout artifact (below) | `diff-summary.md` |

## Deviations from the plan, recorded

1. **Catalogs on the loaded database:** M0 applies the weapon and specialist catalogs to the loaded `GameDatabase` exactly as V3 does, with no deep clone. The deep clone moves to M2, where V4 builds its own runtime database. Reason: a clone changes nothing observable in M0 and would be a second copy of V3's catalog logic.
2. **Per-stream `RunRng` draw counts** are not in the trace. `RunRng` exposes no counters, and adding them means editing shared code (D1). Exact event matching already covers RNG divergence in M0–M2.
3. **Boss at 240 s, as planned (changed after playtest feedback).** The first pilot (`wander-v1`) steered blind and pinned itself against the first island it met (0 m moved for 100+ s in every seed), so it died at 150–225 s and the goldens used 120 s. At your request the pilot was rebuilt to play like a person (`player-v1`); it survives 620 s (seed 101), 414 s (202) and 633 s (303) with zero stuck samples, and reaches level 9–12. The boss is summoned at 240 s in every run; the pilot kites it rather than killing it. All V3 goldens and V4 traces were re-recorded with it; V4 == V3 exactly for all three seeds, and V3 and V4 each replay seed 101 identically.
4. **`V4ModuleLab`** keeps its legacy playtest bootstrap until M2, when archetypes exist to spawn.
5. **`V4DeterminismTests` [V4Slow]** is replaced by the trace harness replay (seed 101 a/b above). A trace needs a fresh Play Mode session, which a PlayMode test cannot provide.
6. **Goldens were recorded in `V3SaltwindRun`** instead of a V4 copy that still ran `V3RunBootstrap`. The V4 scene is a straight copy, so this is the same content. The header `scene` field differs and is ignored by the comparison.

## Findings during M0

- **R-72 (new):** `EnemyViewPool` deactivates `/Systems` around every new enemy view, so components on `/Systems` get OnDisable/OnEnable mid-run. It closed the Tuning Console. The console now lives on its own object.
- **R-73 (new), profile hash drift:** the stage profile `ContentHash` snapshots weapon stats from the in-memory `GameDatabase`, which every run mutates with the catalogs. Verified in the Editor: clean load `954db8…`; after one catalog application `9da95d…`; after further applications it keeps changing (`da2393…` was seen in a trace header); after unload it is back to `954db8…`. `V4TraceSession` now unloads the database before each trace or check, and all final traces carry `954db8…`. This is V3 behavior; the M2 deep clone removes it.
- **R-68 fix verified:** `V4LegacyBridge.ResetSpawnDirectors` now calls `V3MinibossArena.ResetForRun()` first, and `SceneReload_ClearsStaleMinibossArena` covers it. The traces were re-recorded after the change and still match exactly.
- **R-67a (new), real PlayerPrefs:** V3's own suites change the Editor's PlayerPrefs. `V3IntegrationTests` deletes `v3_home_seed` and `v3_first_conquest_complete`; PlayMode boss kills lock a home seed. Running the baselines therefore changed these keys in this Editor, and both are now absent. The values from before this session are unknown. `tools/prefs-guard.ps1` backs up and restores the registry key around future suite runs.
- **Test side effects cleaned up:** the V3 suites rewrote `plan/saltwind-verification/balance-lab/*`, the TMP fallback font atlas and the line endings of `Data/EncounterSpawnSchedule.asset`. All three were restored with `git checkout`, and V3 data was not otherwise touched.
- **Tooling:** `capture_game_view --save_path` always writes under `Assets/`, so the playable check decodes the inline image instead. A stray `Assets/Temp/` from one test capture was deleted. The 1200-run matrix baseline failure is a CLI main-thread timeout (the CLI was polled while the test held the main thread), not a gameplay failure.
