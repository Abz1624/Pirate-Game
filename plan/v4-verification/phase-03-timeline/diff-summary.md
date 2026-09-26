# M3 diff summary

Phase start: `eba369a6` (M2). Committed as "V4 M3: Wave timeline and spawn controller".

`tools/check-write-set.ps1 -Since eba369a6`: all changes are inside the V4 write set; the only reported paths are the 12 files of the pre-existing `Assets/UI/Weapons/Weapon UI /` checkout artifact. Test side effects (TMP fallback font atlas, `plan/saltwind-verification/balance-lab/*`) were reverted before committing. The two V4 scenes carry the ambient slot with file ID 658786993 (set by `V4SceneSetup.ApplyM3`).

Changed or added (without .meta files):
```
 M "Pirate Game Unity/Assets/_Game/Scenes/V4/V4ModuleLab.unity"
 M "Pirate Game Unity/Assets/_Game/Scenes/V4/V4SaltwindRun.unity"
 M "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4StructureTests.cs"
 M "Pirate Game Unity/Assets/_Game/Tests/V4/PlayMode/V4RunPlayModeTests.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Core/ConfigSetRuntime.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Core/PresetCodec.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/DifficultyCurveConfig.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/EnemyArchetypeConfig.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/EnemyVariantConfig.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/ExperimentConfig.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WaveTimelineConfig.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Difficulty/SaltwindDifficulty.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/barnacle_king.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/boarding_barge.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/bomb_boat.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/cannonboat.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/cutthroat_skiff.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/deckhand_dinghy.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/drowned_harrier.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/drowned_pursuer.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/gunner_cog.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/hammerhead.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/kraken_spawn.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/manta_ray.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/rammer_raft.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/reef_crab_miniboss.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/shark.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/swordfish.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Timelines/SaltwindTimeline.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/bomb_ketch.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/elite_hammerhead.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/elite_kraken_spawn.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/needle_sloop.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/net_cutter.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/powder_barge.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/ram_raider.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/repair_tender.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/shield_frigate.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Variants/treasure_raider.asset"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/ConfigSection.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/TuningConsole.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/RunTraceRecorder.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceComparer.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceRequest.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Editor/V3ConfigMigrator.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Editor/V4SceneSetup.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/V4Game.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/V4Phase.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyAdapters.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyConfigRuntime.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyRunShell.cs"
 M docs/V4_IMPLEMENTATION_PLAN.md
 M plan/v4-verification/V3_DEFECT_REGISTER.md
 M plan/v4-verification/tools/compare-traces.py
 M plan/v4-verification/tools/run-traces-parallel.ps1
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4SpawnM3Tests.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Legacy/V4EncounterOnlySchedule.asset"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/TimelineSection.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/AssemblyInfo.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacySpawnAdapters.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyUpdateSlot.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/AmbientSpawner.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/OvertimeAndStorm.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/PhaseKinds.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/ScriptedPhases.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/SpawnComposition.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/SpawnContracts.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/SpawnController.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/SpawnExecutor.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/SpawnServices.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Spawning/TimelineTriggers.cs"
?? plan/v4-verification/phase-03-timeline/CHECKLIST.md
?? plan/v4-verification/phase-03-timeline/config-hash.txt
?? plan/v4-verification/phase-03-timeline/diff-summary.md
?? plan/v4-verification/phase-03-timeline/editmode-results.json
?? plan/v4-verification/phase-03-timeline/playable-check/01-wave-timeline.png
?? plan/v4-verification/phase-03-timeline/playable-check/02-reef-crab-arena.png
?? plan/v4-verification/phase-03-timeline/playable-check/03-after-edits.png
?? plan/v4-verification/phase-03-timeline/playable-check/console-errors.txt
?? plan/v4-verification/phase-03-timeline/playable-check/steps.log
?? plan/v4-verification/phase-03-timeline/player/v3-trace-seed-101-long.jsonl
?? plan/v4-verification/phase-03-timeline/player/v3-trace-seed-202-long.jsonl
?? plan/v4-verification/phase-03-timeline/player/v3-trace-seed-303-long.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-101-a.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-101-long.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-101-parity-b.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-101-parity.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-202-a.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-202-long.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-202-parity.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-303-a.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-303-long.jsonl
?? plan/v4-verification/phase-03-timeline/player/v4-trace-seed-303-parity.jsonl
?? plan/v4-verification/phase-03-timeline/playmode-results.json
?? plan/v4-verification/phase-03-timeline/trace-comparison.txt
?? plan/v4-verification/phase-03-timeline/v3-regression-editmode-results.json
?? plan/v4-verification/phase-03-timeline/v3-regression-playmode-results.json
?? plan/v4-verification/tools/playable-check-m3.ps1
D  "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/V4LegacySpawnBridge.cs"
```
