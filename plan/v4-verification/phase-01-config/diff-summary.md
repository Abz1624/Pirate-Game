# M1 diff summary

Phase start: `ef5c7175`. Not committed yet: the M1 commit follows your playtest.

`tools/check-write-set.ps1 -Since ef5c7175`: all changes are inside the V4 write set. The only reported paths are the 12 files of the pre-existing `Assets/UI/Weapons/Weapon UI /` checkout artifact (folder name ends in a space; see M0).

Test side effects reverted before committing: the TMP fallback font atlas (rewritten by play sessions) and `plan/saltwind-verification/balance-lab/*` (rewritten by the V3 matrix test).

Changed or added (without .meta files):
```
 M "Pirate Game Unity/Assets/_Game/Scenes/V4/V4SaltwindRun.unity"
 M "Pirate Game Unity/Assets/_Game/Tests/V4/PlayMode/V4RunPlayModeTests.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/TuningConsole.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/RunTraceRecorder.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceBoot.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceComparer.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceRequest.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Editor/V4SceneSetup.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Editor/V4TraceSession.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/RunContext.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/RunRequest.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/V4Game.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyAdapters.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyRunShell.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/V4LegacySpawnBridge.cs"
 M docs/V4_IMPLEMENTATION_PLAN.md
 M plan/v4-verification/V3_DEFECT_REGISTER.md
 M plan/v4-verification/tools/check-write-set.ps1
 M plan/v4-verification/tools/compare-traces.py
 M plan/v4-verification/tools/run-traces-parallel.ps1
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/ProbeConfig.cs"
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4ConfigCoreTests.cs"
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4ConfigM1Tests.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/CanonicalConfigWriter.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/ConfigEnums.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/ConfigFieldWalker.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/ConfigPath.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/ConfigSet.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/ConfigSetRuntime.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/ConfigValidator.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/PresetCodec.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Core/V4ConfigAsset.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/DifficultyCurveConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/EnemyArchetypeConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/EnemySpawnerConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/ExperimentConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/RunConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WaveTimelineConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WeaponConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Difficulty/SaltwindDifficulty.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/barnacle_king.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/boarding_barge.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/bomb_boat.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/cannonboat.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/cutthroat_skiff.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/deckhand_dinghy.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/gunner_cog.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/hammerhead.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/kraken_spawn.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/manta_ray.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/rammer_raft.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/reef_crab_miniboss.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/shark.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/swordfish.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Run/ExperimentConfig.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Run/RunConfig.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/drowned_harrier.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/drowned_pursuer.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_ambient_band_0.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_ambient_band_1.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_ambient_band_2.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_ambient_band_3.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_ambient_band_4.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_boss.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_elite_hammerhead.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_elite_kraken.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_powder_cache.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Spawners/saltwind_swarm_pulse.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Timelines/SaltwindTimeline.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/broadside_battery.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/deck_cannons.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/flamethrower.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/flying_cutlasses.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/harpoon_gun.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/lightning_mast.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/long_nine.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/mortar.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/powder_kegs.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Weapons/swivel_guns.asset"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/ConfigSection.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Editor/V3ConfigMigrator.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyConfigMapping.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyConfigRuntime.cs"
?? plan/v4-verification/phase-01-config/CHECKLIST.md
?? plan/v4-verification/phase-01-config/config-hash.txt
?? plan/v4-verification/phase-01-config/diff-summary.md
?? plan/v4-verification/phase-01-config/editmode-results.json
?? plan/v4-verification/phase-01-config/playable-check.md
?? plan/v4-verification/phase-01-config/playable-check/01-console-config.png
?? plan/v4-verification/phase-01-config/playable-check/02-after-live-edit.png
?? plan/v4-verification/phase-01-config/playable-check/03-preset-run.png
?? plan/v4-verification/phase-01-config/playable-check/console-errors.txt
?? plan/v4-verification/phase-01-config/playable-check/steps.log
?? plan/v4-verification/phase-01-config/player/README.md
?? plan/v4-verification/phase-01-config/player/v3-trace-seed-101-a.jsonl
?? plan/v4-verification/phase-01-config/player/v3-trace-seed-202-a.jsonl
?? plan/v4-verification/phase-01-config/player/v3-trace-seed-303-a.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-101-a.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-101-b.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-101-band0x.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-101-band2x.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-202-a.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-303-a.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-303-band0x.jsonl
?? plan/v4-verification/phase-01-config/player/v4-trace-seed-303-band2x.jsonl
?? plan/v4-verification/phase-01-config/playmode-results.json
?? plan/v4-verification/phase-01-config/presets/band0x.json
?? plan/v4-verification/phase-01-config/presets/band2x.json
?? plan/v4-verification/phase-01-config/trace-comparison.txt
?? plan/v4-verification/phase-01-config/v3-regression-editmode-results.json
?? plan/v4-verification/phase-01-config/v3-regression-playmode-results.json
?? plan/v4-verification/tools/eval.py
?? plan/v4-verification/tools/playable-check-m1.ps1
```
