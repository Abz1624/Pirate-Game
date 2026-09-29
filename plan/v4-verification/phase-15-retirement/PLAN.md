# M15 — V3 retirement: plan and progress

Started 29 Sep 2026 at `ba0aead6` (M14 committed after your sign-off, which lifts D1: shared code may now change).
Dependency audit (independent, read-only): `AUDIT.md` — 312 `Scripts/` files; V4 needs a keep set of ~119 (13.9k
lines, 21 of them in `Scripts/V3/`), the rest goes.

User decisions (29 Sep):
- **V4 names:** kept code moves into V4 assemblies and V4 namespaces and loses its "V3"/"Legacy" prefixes
  (`V3StagePoiPlacer` → `StagePoiPlacer`). Saved-progress keys (First Conquest and unlocks, saved Captain, home seed)
  keep their exact strings.
- **Delete scope:** all 19 non-V4 scenes, the V3 test suites, V3-only editor tools and the V3→V4 migrators; the island
  authoring editor tools are kept and ported. Git history keeps everything.
- **Coplay MCP package:** removed (AGENTS.md updated).
- **Verification:** targeted tests per sub-step, full V4 suites, a release build from `V4Boot`, and **one** trace set at
  the end compared with M14's stored traces (expected: identical — M15 moves and deletes code, no gameplay change).

Consequences stated up front:
- The V3-comparison trace set becomes impossible (no V3 scenes). The stored V3 goldens and M14 traces stay as files.
- V4 tests that compare V4 with V3 code side by side (lockstep, parity, V3 static resets) go with V3.
- "Always-null" V3 branches in kept code are cut (the Captain/item damage multipliers in `DamageReceiver`, the
  Marks/settlement/telemetry calls in `RunManager`, the multi-ship branch in `PlayerShipController`): behaviour-neutral
  in V4 runs, proven by targeted tests and the final trace set.

## Order (forced by the assembly graph)
V4 assemblies reference `HighSeas.Runtime`; the reverse is forbidden (and would be a cycle). So:
1. **15.1 Delete** everything outside the keep set while it still sits in `HighSeas.Runtime`; cut the V3 edges in kept
   files; strip V3 parity code from V4 tests, DevTools and Editor; compile green.
2. **15.2 Ports:** the tag-15 ports become ordinary V4 services (no `Legacy` names, out of `Runtime/Legacy/`);
   `V4Phase.Current = 15`, zero `[DeletedInPhase]` types left.
3. **15.3 Move and rename** the keep set into V4 (each `.cs` with its `.meta`, so scene/asset GUIDs survive);
   `HighSeas.Runtime`/`HighSeas.Editor` asmdefs removed; structure tests updated.
4. **15.4 Clean-up:** data duplicates and unreferenced V3 assets, `Canvas.prefab` stripped of its V3 HUD script,
   Coplay package, build scripts (`tools/*.bat`), AGENTS.md, docs.
5. **15.5 Verify:** full V4 suites, release build, the trace set vs M14, independent review.

## Sub-steps
| Step | Content | Status |
|---|---|---|
| 15.1 | Delete V3 code, scenes, tests, migrators; cut V3 edges | **Done**: 188 `Scripts/` files outside the keep set deleted (124 kept: the audit's 119 + `IslandCompoundColliderBuilder` + the island editor tools `HighSeasIslandColliderEditor`, `HighSeasIslandLayoutEditor`, `HighSeasIslandSetup` and `WeaponDefinitionEditor`); the 19 non-V4 scenes (and their 14 build-list entries), the V3 test suites (`Tests/EditMode`, `Tests/PlayMode`; the Simulation tests moved to `Tests/RoguePirates`), `Assets/_Game/Presentation` (unused V3 adapter), the V3→V4 migrators/generators and `V4SceneSetup`, `LegacyEnemyViews`, `LegacyBuildMapping`. V3 edges cut in kept code (always null or false in V4 runs): the Captain/item multipliers in `DamageReceiver`, Marks/settlement/telemetry/Doubloons in `RunManager` (`FinalizeRunOnDeath`, `CompleteRun` gone; the suspend snapshot became `JumpClock` for the console jump), the multi-ship branch in `PlayerShipController`, V3 run context/scene loading in `AppRoot`, V3 chest purchase methods, Marks text; name/colour tables copied where V4 still shows them. `EnemyWorld.SpatialHash` moved to V4. The trace recorder/pilot/boot lost their V3-scene branches (the V4 trace format is unchanged). V4 tests: V3 comparisons removed from 24 files, 3 test files deleted (pickup parity, enemy lockstep, progression lockstep) plus the V3-comparison helpers `V3EnemyViewSink` and `V3RegistryFromConfig` (EditMode and PlayMode copies). **Dropped with V3** (no V4 replacement): V3's F8 playtest hotkeys and weapon-card demo; the barrel *markers* the v3-parity preset drew (the V4 enemy world still runs the barrels). EditMode 265/265 (was 314: 49 V3-comparison tests removed), Simulation 22/22, PlayMode 49/49 (was 51). |
| 15.2 | Ports → V4 services; `V4Phase.Current = 15` | **Done**: `Runtime/Legacy/` → `Runtime/Ports/` (namespace `RoguePirates.V4.Ports`), `[DeletedInPhase(15)]` removed; renamed `RunPort`, `ShipPort`, `PlayBoundsPort`, `SpawnPlacementPort`, `StageProfilePort`, `WorldPort`, `ContentPort`, `RunDatabaseLoader` (files `SharedPorts.cs`, `StageProfilePort.cs`, `WorldPort.cs`, `RunDatabaseLoader.cs`; `V4RuntimeDatabase` kept). They stay as the one boundary between V4 modules and the kept shared MonoBehaviours (run manager, ship, stage builder). `V4Phase.Current = 15`; the tag test now requires zero tagged types and no `RoguePirates.V4.Legacy` namespace; presenters may not use `RoguePirates.V4.Ports`; the content-id scan no longer skips a folder (two phase-id literals listed instead). Tests green. |
| 15.3 | Move + rename the keep set into V4 | **Done**: `Assets/_Game/Scripts/` → `Assets/_Game/V4/Foundation/` (assembly `RoguePirates.V4.Foundation`; the audit's `V3SceneNames` was dropped instead of moved — its one remaining constant, the tropical-islands folder, is inlined in `TropicalIslandPrefabCatalog`; the old `HighSeas.Runtime` asmdef renamed in place so its GUID stays; 119 files moved with their `.meta`, so every scene and asset script reference still resolves — checked: zero missing scripts in the five V4 scenes and all prefabs). Namespaces `HighSeas.*` → `RoguePirates.V4.Foundation.*`; the old `V3/` files spread by topic (`Progression`: CaptainId, CaptainSelection, ContentUnlocks, HomeSeed, PlaytestFlags; `UI`: the art/minimap settings; `World`: PlaceholderFactory/Marker, WorldBillboardLabel, PlayBoundsWallSegment, SpawnAnchorValidator, StagePoiPlacer/Options; `Combat`: WeaponDisplayNames; `Pickups`: FloorPowerupKind) and lost their `V3` prefix, as did a few members (`ConfigureSaltwindLayout`, `ClampPlayPosition`, the chest/jitter constants). The island editor tools moved into `V4/Editor/Islands` (`IslandColliderEditor`, `WeaponDefinitionEditor`; `IslandLayoutEditor` and `IslandSetup` were moved too, then deleted after the review — your decision); `HighSeas.Editor` removed. **Unchanged on purpose:** saved-progress keys (`v3_first_conquest_complete`, `v3_selected_captain`, `v3_home_seed`, `highseas_meta_v1`, the developer-preset key) and Resources asset names (`V3MinimapHudSettings`, `V3SaltwindIslandLayout`, …) — renaming either would lose saved progress or break loading; the v3-parity preset's rule names (`V3Ownership`, …) describe V3 behaviour and stay. Layering test: Foundation references no other V4 assembly; Config does not reference Foundation. **Follow-up debt:** 17 Foundation files still key V3's data tables by content id (tiers, combat profiles, weapon/specialist catalogs, database fallbacks, unlock pools); moving them onto config could change gameplay, so it is outside M15 — the content-id scan lists exactly those files and the list may only shrink. Unused V3 statics removed on the way (`WeaponEvolutionRuntime`, `SaveService.AddDoubloons`). EditMode 265/265, PlayMode 49/49. |
| 15.4 | Data/prefab clean-up, Coplay, build scripts, docs | **Done**: V3-only data deleted (the stage balance profile asset, the 13 V3 spawn-profile assets, V4's encounter-only schedule, the `_Game/Data` root duplicates of Resources assets, V3-only Resources assets incl. `CharacterPoses/`, the unused wall prefab copy) after a GUID and path scan found no reference; kept `Data/GameDatabase.asset` (the Editor loads it by path) and `Data/DemoIslandLayout.asset` (island tool). `Canvas.prefab` stripped of its V3 HUD script. Coplay package removed (manifest + lock). `tools/build-webgl.bat` now calls `V4BuildSettings.BuildWebGLFromCommandLine`; V3's bootstrap and test scripts replaced by `tools/run-v4-tests.bat`. AGENTS.md: code layout section, Coplay note. |
| 15.5 | Suites, release build, trace set vs M14, review | **Done** (suite numbers below). Release build (`BuildRelease`, WebGL, non-development): **succeeded**, 161 MB, scenes V4Boot/V4MainMenu/V4SaltwindRun/V4ModuleLab; its IL2CPP assemblies are the V4 ones (`RoguePirates.V4`, `.Config`, `.Foundation`, `.Presentation`) plus Simulation/Content/AccountClient/PlatformAdapters — no DevTools, no HighSeas. The one trace set (seeds 101/202/303, v3-parity preset, vs M14's stored traces): **not exact** — headers match, the runs follow M14 line for line and then drift by float-sized amounts from 40 s / 185 s / 51 s; seed 202 ends identically, 101 and 303 within 35 s / 5 s; boss times and early spawn counts identical. No simulation code changed; the inferred cause is the parity preset's V3 barrel markers (two cylinders whose colliders lived through the first physics step) being gone, which changes PhysX's actor order. Not re-run (protocol). `traces/trace-comparison.txt`. The trace player build also needed the deleted V3 scene removed from its list. |

## Independent review (29 Sep)

A read-only reviewer checked the phase against this plan, the audit and the code at `ba0aead6` (moved files diffed body by
body). No blockers. Verified clean: every moved script `.meta` is an identical rename (GUIDs kept); none of the 327 deleted GUIDs
is referenced by a remaining scene, prefab, asset, asmdef or project setting; class and file names match; PlayerPrefs keys and
Resources names unchanged; moved bodies identical apart from namespaces/prefixes and the listed cuts; the cut branches are V3-only;
layering correct; Canvas strip, Coplay removal and the new build script correct.

| # | Finding | Fix |
|---|---|---|
| 1 | Major: the ported island editor tools still open the deleted `Scenes/Run.unity` (4 menu items fail) | **Your decision (29 Sep): keep collider prep only.** `IslandSetup` and `IslandLayoutEditor` (spawn/bake/clear/quick setup/setup visuals/repair — V3's demo run-scene workflow) deleted, with the demo layout asset they wrote (`Data/DemoIslandLayout.asset`; the Resources copy stays as V4's fallback). Kept: `IslandColliderEditor` ("Prepare Mesh Collider Islands") and `WeaponDefinitionEditor` |
| 2 | Major: the run-active flag (was `V3RunContext.IsActive`) was no longer cleared when a run's stage profile ended; `HomeSeed.TryLockFromRun` lost its run guard — neutral in game, but EditMode results could depend on a previous Play session | Restored V3's exact semantics: a Foundation flag `ActiveRun` (set at run start, cleared by `AppRoot.EndActiveSaltwindRunContext`, checked by the home seed); test `V4ActiveRunTests` |
| 3 | Major: V4-only checks went with the V3 comparisons (runtime database configures every enemy / V4-only ids / collider-free models; stage-profile field ownership; direct pickup tests) | New `RuntimeDatabase_ConfiguresEveryEnemy_AndAppendsTheV4OnlyOnes`, `StageProfile_UnderTheParityPreset_KeepsTheTracedContentHash` (the hash both trace sets recorded), `V4PickupTests` (drop, wait, pay once; same seed same scatter); live band edits were already covered (`V4SpawnM3Tests`) |
| 4 | Minor: the run scenes kept a removed-component override pointing at the stripped Canvas component | `RemoveUnusedOverrides` on both scenes |
| 5 | Minor: presenter forbidden list used old names; content-id allowlist had a non-id entry and no shrink check | List extended (`CaptainSelection`, `ContentUnlocks`, `HomeSeed`, `PlaytestFlags`, `ActiveRun`); `elite_kraken` removed; stale allowlist entries now fail the test |
| 6 | Minor: dead `RunManager.LastRunEndedByDeath`/`BankedRunGold` and its fallback | Removed |
| 7 | Minor: PLAN/AGENTS wording (V3SceneNames, helper files, "all game code") | Corrected |

Final suites after the fixes: EditMode 293/293 (V4 271 + Simulation 22), PlayMode 49/49.

## Your playtest (29 Sep)

| Check | Result |
|---|---|
| A normal run plays as after M14 | Yes |
| RoguePirates → Islands → "Prepare Mesh Collider Islands" present, HighSeas menus gone | Yes |
| Save intact | Hank showed locked ("100 Rum") although unlocked in an earlier playtest; you pressed "UNLOCK — 100 RUM" again, so the unlock really had been lost before this playtest. Not caused by M15: the save code is unchanged apart from its namespace. Checked and excluded: no standalone save, no prefs-guard backup, no pending trace-session snapshot, every PlayMode test class runs on an in-memory save. The cause is not found; M16 hardens the save against silent loss (see M16). |

Also from this playtest: the "storm" report was the red edge wall — the ship can sail past it and nothing happens before the storm (V3 behaviour, the clamp was a no-op); your decision: steady damage outside the wall at any time, with a warning (M16). Gold: 965 gold pays ~21 Rum on the curve; the 37 on the results screen is the run total (your decision: keep the total only).

M15 passed your playtest (29 Sep) → committed `V4 M15: V3 retirement`.
