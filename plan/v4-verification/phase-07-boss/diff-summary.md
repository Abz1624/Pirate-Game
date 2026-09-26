# M7 diff summary (since 8c9ab707)

```
 .../_Game/V4/Data/ConfigSets/SaltwindDefault.asset |    2 +
 Pirate Game Unity/Assets/_Game/V4/Data/Stage.meta  |    8 +
 .../V4/Data/Stage/BossConfig_BarnacleKing.asset    |   65 +
 .../Data/Stage/BossConfig_BarnacleKing.asset.meta  |    8 +
 .../V4/Data/Stage/StageContent_Saltwind.asset      |   66 +
 .../V4/Data/Stage/StageContent_Saltwind.asset.meta |    8 +
 .../_Game/V4/DevTools/Console/StageSection.cs      |   80 ++
 .../_Game/V4/DevTools/Console/StageSection.cs.meta |    2 +
 .../_Game/V4/DevTools/Console/TuningConsole.cs     |    2 +
 .../_Game/V4/DevTools/Trace/RunTraceRecorder.cs    |   41 +-
 .../Assets/_Game/V4/Editor/V3ConfigMigrator.cs     |   99 ++
 .../_Game/V4/Editor/V4BossConfigGenerator.cs       |  101 ++
 .../_Game/V4/Editor/V4BossConfigGenerator.cs.meta  |    2 +
 .../Assets/_Game/V4/Editor/V4SceneSetup.cs         |   53 +
 .../Assets/_Game/V4/Presentation/Stage.meta        |    8 +
 .../Presentation/Stage/BossTelegraphPresenter.cs   |   88 ++
 .../Stage/BossTelegraphPresenter.cs.meta           |    2 +
 .../Stage/InteractionPromptPresenter.cs            |  216 +++
 .../Stage/InteractionPromptPresenter.cs.meta       |    2 +
 .../_Game/V4/Presentation/V4PresentationRoot.cs    |   36 +
 .../Assets/_Game/V4/Runtime/Core/V4Game.cs         |   11 +-
 .../Assets/_Game/V4/Runtime/Core/V4Phase.cs        |    2 +-
 .../_Game/V4/Runtime/Legacy/LegacyAdapters.cs      |   49 +-
 .../_Game/V4/Runtime/Legacy/LegacyBossAdapters.cs  |   92 ++
 .../V4/Runtime/Legacy/LegacyBossAdapters.cs.meta   |    2 +
 .../_Game/V4/Runtime/Legacy/LegacyConfigMapping.cs |   33 +-
 .../_Game/V4/Runtime/Legacy/LegacyConfigRuntime.cs |   10 +-
 .../_Game/V4/Runtime/Legacy/LegacyEventFeed.cs     |   72 +-
 .../_Game/V4/Runtime/Legacy/LegacyRunShell.cs      |   22 +-
 .../_Game/V4/Runtime/Legacy/LegacyStageAdapters.cs |  450 +++++++
 .../V4/Runtime/Legacy/LegacyStageAdapters.cs.meta  |    2 +
 .../_Game/V4/Runtime/Legacy/LegacyStageWiring.cs   |  150 +++
 .../V4/Runtime/Legacy/LegacyStageWiring.cs.meta    |    2 +
 .../Assets/_Game/V4/Runtime/Legacy/LegacyViews.cs  |   50 -
 .../_Game/V4/Runtime/Spawning/EncounterExecutor.cs |  179 +++
 .../V4/Runtime/Spawning/EncounterExecutor.cs.meta  |    2 +
 .../_Game/V4/Runtime/Spawning/SpawnController.cs   |   15 +-
 .../_Game/V4/Runtime/Spawning/SpawnExecutor.cs     |    7 +-
 .../_Game/V4/Runtime/Spawning/SpawnServices.cs     |    3 +
 .../Assets/_Game/V4/Runtime/Stage.meta             |    8 +
 .../_Game/V4/Runtime/Stage/BeaconDiscovery.cs      |  187 +++
 .../_Game/V4/Runtime/Stage/BeaconDiscovery.cs.meta |    2 +
 .../Assets/_Game/V4/Runtime/Stage/BossContracts.cs |   66 +
 .../_Game/V4/Runtime/Stage/BossContracts.cs.meta   |    2 +
 .../Assets/_Game/V4/Runtime/Stage/BossDirector.cs  |  448 +++++++
 .../_Game/V4/Runtime/Stage/BossDirector.cs.meta    |    2 +
 .../_Game/V4/Runtime/Stage/InteractionModel.cs     |  259 ++++
 .../V4/Runtime/Stage/InteractionModel.cs.meta      |    2 +
 .../_Game/V4/Runtime/Stage/StageContentModule.cs   |  640 +++++++++
 .../V4/Runtime/Stage/StageContentModule.cs.meta    |    2 +
 .../_Game/V4/Runtime/Stage/StageContracts.cs       |  120 ++
 .../_Game/V4/Runtime/Stage/StageContracts.cs.meta  |    2 +
 .../Assets/_Game/V4/Runtime/Stage/StageView.cs     |   82 ++
 .../_Game/V4/Runtime/Stage/StageView.cs.meta       |    2 +
 docs/V4_IMPLEMENTATION_PLAN.md                     |    2 +-
 plan/v4-verification/V3_DEFECT_REGISTER.md         |   12 +-
 plan/v4-verification/phase-07-boss/CHECKLIST.md    |   95 ++
 plan/v4-verification/phase-07-boss/config-hash.txt |   64 +
 .../phase-07-boss/editmode-results.json            | 1396 ++++++++++++++++++++
 .../playable-check/beacon-hold-telegraph.png       |  Bin 0 -> 1303905 bytes
 .../phase-07-boss/playable-check/boss-defeated.png |  Bin 0 -> 1313671 bytes
 .../phase-07-boss/playable-check/boss-fight.png    |  Bin 0 -> 1312637 bytes
 .../phase-07-boss/playable-check/buoy-charging.png |  Bin 0 -> 1274398 bytes
 .../phase-07-boss/playable-check/buoy-reward.png   |  Bin 0 -> 1283601 bytes
 .../phase-07-boss/playable-check/chest-opened.png  |  Bin 0 -> 1220940 bytes
 .../phase-07-boss/playable-check/chest-prompt.png  |  Bin 0 -> 1196438 bytes
 .../playable-check/console-errors.txt              |    3 +
 .../playable-check/powder-cache-ambush.png         |  Bin 0 -> 1235731 bytes
 .../phase-07-boss/playable-check/repair-wreck.png  |  Bin 0 -> 1266969 bytes
 .../phase-07-boss/playable-check/results.png       |  Bin 0 -> 1292725 bytes
 .../phase-07-boss/playable-check/stage-start.png   |  Bin 0 -> 1283363 bytes
 .../phase-07-boss/playable-check/steps.log         |   53 +
 .../player/v4-trace-seed-101-parity.jsonl          |  438 ++++++
 .../player/v4-trace-seed-202-parity.jsonl          |  410 ++++++
 .../player/v4-trace-seed-303-parity.jsonl          |  524 ++++++++
 .../phase-07-boss/playmode-results.json            |  223 ++++
 .../phase-07-boss/trace-comparison.txt             |    5 +
 plan/v4-verification/presets/v3-parity.json        |   14 +
 plan/v4-verification/tools/playable-check-m7.ps1   |  114 ++
 95 files changed, 9126 insertions(+), 235 deletions(-)
```

Write set: only Assets/_Game/V4, Scenes/V4, Tests/V4, plan/v4-verification and docs/V4_IMPLEMENTATION_PLAN.md staged; the TMP fallback font asset (touched by the Editor) was reverted.
