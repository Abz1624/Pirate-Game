# M4 diff summary (since bd4d0f51)

```
 .../LiberationSans SDF - Fallback.asset            | 1335 +++++++++++++++++++-
 .../PlayMode/RoguePirates.V4.Tests.PlayMode.asmdef |    1 +
 .../_Game/Tests/V4/PlayMode/V4RunPlayModeTests.cs  |   65 +
 .../Assets/_Game/V4/Config/Core/ConfigEnums.cs     |    2 +
 .../V4/Config/Schemas/EnemyArchetypeConfig.cs      |    3 +
 .../_Game/V4/Data/ConfigSets/SaltwindDefault.asset |    1 +
 .../_Game/V4/DevTools/Console/TuningConsole.cs     |   31 +-
 .../Assets/_Game/V4/Editor/V3ConfigMigrator.cs     |   54 +
 .../Assets/_Game/V4/Runtime/Core/V4Phase.cs        |    2 +-
 docs/V4_IMPLEMENTATION_PLAN.md                     |    4 +-
 plan/v4-verification/V3_DEFECT_REGISTER.md         |    1 +
 11 files changed, 1485 insertions(+), 14 deletions(-)
Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4PoolM4Tests.cs
Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4PoolM4Tests.cs.meta
Pirate Game Unity/Assets/_Game/V4/Config/Schemas/PoolCatalogConfig.cs
Pirate Game Unity/Assets/_Game/V4/Config/Schemas/PoolCatalogConfig.cs.meta
Pirate Game Unity/Assets/_Game/V4/Data/Pools.meta
Pirate Game Unity/Assets/_Game/V4/Data/Pools/PoolStressMarker.prefab
Pirate Game Unity/Assets/_Game/V4/Data/Pools/PoolStressMarker.prefab.meta
Pirate Game Unity/Assets/_Game/V4/Data/Pools/SaltwindPools.asset
Pirate Game Unity/Assets/_Game/V4/Data/Pools/SaltwindPools.asset.meta
Pirate Game Unity/Assets/_Game/V4/DevTools/Console/PoolsSection.cs
Pirate Game Unity/Assets/_Game/V4/DevTools/Console/PoolsSection.cs.meta
Pirate Game Unity/Assets/_Game/V4/DevTools/Diagnostics.meta
Pirate Game Unity/Assets/_Game/V4/DevTools/Diagnostics/PoolStressScenario.cs
Pirate Game Unity/Assets/_Game/V4/DevTools/Diagnostics/PoolStressScenario.cs.meta
Pirate Game Unity/Assets/_Game/V4/DevTools/Diagnostics/SceneObjectCreationProbe.cs
Pirate Game Unity/Assets/_Game/V4/DevTools/Diagnostics/SceneObjectCreationProbe.cs.meta
Pirate Game Unity/Assets/_Game/V4/Presentation/Pooling.meta
Pirate Game Unity/Assets/_Game/V4/Presentation/Pooling/PoolCatalog.cs
Pirate Game Unity/Assets/_Game/V4/Presentation/Pooling/PoolCatalog.cs.meta
Pirate Game Unity/Assets/_Game/V4/Presentation/Pooling/PoolService.cs
Pirate Game Unity/Assets/_Game/V4/Presentation/Pooling/PoolService.cs.meta
Pirate Game Unity/Assets/_Game/V4/Presentation/V4PresentationRoot.cs
Pirate Game Unity/Assets/_Game/V4/Presentation/V4PresentationRoot.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Core/SlotPool.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Core/SlotPool.cs.meta
plan/v4-verification/phase-04-pools/CHECKLIST.md
plan/v4-verification/phase-04-pools/config-hash.txt
plan/v4-verification/phase-04-pools/diff-summary.md
plan/v4-verification/phase-04-pools/editmode-results.json
plan/v4-verification/phase-04-pools/playable-check/01-module-lab-stress-running.png
plan/v4-verification/phase-04-pools/playable-check/02-module-lab-pools-after.png
plan/v4-verification/phase-04-pools/playable-check/03-run-pools-and-creation-probe.png
plan/v4-verification/phase-04-pools/playable-check/console-errors.txt
plan/v4-verification/phase-04-pools/playable-check/steps.log
plan/v4-verification/phase-04-pools/player/v4-trace-seed-101-a.jsonl
plan/v4-verification/phase-04-pools/player/v4-trace-seed-101-parity-b.jsonl
plan/v4-verification/phase-04-pools/player/v4-trace-seed-101-parity.jsonl
plan/v4-verification/phase-04-pools/player/v4-trace-seed-202-a.jsonl
plan/v4-verification/phase-04-pools/player/v4-trace-seed-202-parity.jsonl
plan/v4-verification/phase-04-pools/player/v4-trace-seed-303-a.jsonl
plan/v4-verification/phase-04-pools/player/v4-trace-seed-303-parity.jsonl
plan/v4-verification/phase-04-pools/playmode-results.json
plan/v4-verification/phase-04-pools/trace-comparison.txt
plan/v4-verification/tools/playable-check-m4.ps1
```

Write set: all changes under Assets/_Game/V4, Assets/_Game/Tests/V4, plan/v4-verification and docs/V4_IMPLEMENTATION_PLAN.md. Outside it: the TMP font atlas (Editor side effect, reverted before commit) and 12 pre-existing untracked files in 'Assets/UI/Weapons/Weapon UI /' (Sep 17, not touched).
