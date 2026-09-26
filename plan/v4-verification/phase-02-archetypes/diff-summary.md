# M2 diff summary

Phase start: `72709f45` (M1). Committed as "V4 M2: Enemy archetypes and spawners".

`tools/check-write-set.ps1 -Since 72709f45`: all changes are inside the V4 write set. The only reported paths are the 12 files of the pre-existing `Assets/UI/Weapons/Weapon UI /` checkout artifact (folder name ends in a space; see M0). Test side effects (the TMP fallback font atlas, `plan/saltwind-verification/balance-lab/*`) were reverted before committing.

Changed or added (without .meta files):
```
 M "Pirate Game Unity/Assets/_Game/Scenes/V4/V4ModuleLab.unity"
 M "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/RoguePirates.V4.Tests.EditMode.asmdef"
 M "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4ConfigM1Tests.cs"
 M "Pirate Game Unity/Assets/_Game/Tests/V4/PlayMode/V4RunPlayModeTests.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Core/V4ConfigAsset.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/EnemyArchetypeConfig.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WaveTimelineConfig.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset"
 M "Pirate Game Unity/Assets/_Game/V4/Data/Timelines/SaltwindTimeline.asset"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/ConfigSection.cs"
 M "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/TuningConsole.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Editor/V3ConfigMigrator.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Editor/V4SceneSetup.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/V4Game.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/V4Phase.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyAdapters.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyConfigMapping.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyConfigRuntime.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyRunShell.cs"
 M "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/V4LegacySpawnBridge.cs"
 M "Pirate Game Unity/ProjectSettings/EditorBuildSettings.asset"
 M docs/V4_IMPLEMENTATION_PLAN.md
 M plan/v4-verification/V3_DEFECT_REGISTER.md
 M plan/v4-verification/presets/v3-parity.json
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4EnemyM2Tests.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/Schemas/EnemyVariantConfig.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/drowned_harrier.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Enemies/drowned_pursuer.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/bomb_ketch.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/elite_hammerhead.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/elite_kraken_spawn.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/needle_sloop.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/net_cutter.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/powder_barge.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/ram_raider.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/repair_tender.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/shield_frigate.asset"
?? "Pirate Game Unity/Assets/_Game/V4/Data/Variants/treasure_raider.asset"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/ModuleLabSection.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Enemies/EnemyStatResolver.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/V4RuntimeDatabase.cs"
?? plan/v4-verification/phase-02-archetypes/CHECKLIST.md
?? plan/v4-verification/phase-02-archetypes/config-hash.txt
?? plan/v4-verification/phase-02-archetypes/diff-summary.md
?? plan/v4-verification/phase-02-archetypes/editmode-results.json
?? plan/v4-verification/phase-02-archetypes/playable-check.md
?? plan/v4-verification/phase-02-archetypes/playable-check/01-all-enemies.png
?? plan/v4-verification/phase-02-archetypes/playable-check/02-lab-inspector.png
?? plan/v4-verification/phase-02-archetypes/playable-check/console-errors.txt
?? plan/v4-verification/phase-02-archetypes/playable-check/steps.log
?? plan/v4-verification/phase-02-archetypes/player/v4-trace-seed-101-a.jsonl
?? plan/v4-verification/phase-02-archetypes/player/v4-trace-seed-101-parity-b.jsonl
?? plan/v4-verification/phase-02-archetypes/player/v4-trace-seed-101-parity.jsonl
?? plan/v4-verification/phase-02-archetypes/player/v4-trace-seed-202-a.jsonl
?? plan/v4-verification/phase-02-archetypes/player/v4-trace-seed-202-parity.jsonl
?? plan/v4-verification/phase-02-archetypes/player/v4-trace-seed-303-a.jsonl
?? plan/v4-verification/phase-02-archetypes/player/v4-trace-seed-303-parity.jsonl
?? plan/v4-verification/phase-02-archetypes/playmode-results.json
?? plan/v4-verification/phase-02-archetypes/trace-comparison.txt
?? plan/v4-verification/phase-02-archetypes/v3-regression-editmode-results.json
?? plan/v4-verification/phase-02-archetypes/v3-regression-playmode-results.json
?? plan/v4-verification/tools/playable-check-m2.ps1
```
