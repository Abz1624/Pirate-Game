# M8 diff summary (switch-over, since 6ff1d88c)

```
 .../_Game/Tests/V4/EditMode/V4EnemyWorldM8Tests.cs |  59 +++
 .../_Game/Tests/V4/EditMode/V4SpawnM3Tests.cs      |  45 +-
 .../_Game/Tests/V4/EditMode/V4StructureTests.cs    |   2 +-
 .../V4/PlayMode/V4EnemyLockstepPlayModeTests.cs    |  19 +-
 .../Tests/V4/PlayMode/V4EnemyPlayModeTests.cs      |  93 +++-
 .../_Game/Tests/V4/PlayMode/V4HudPlayModeTests.cs  |   4 +-
 .../_Game/Tests/V4/PlayMode/V4RunPlayModeTests.cs  |  20 +-
 .../Tests/V4/PlayMode/V4StagePlayModeTests.cs      |   4 +-
 .../_Game/Tests/V4/PlayMode/V4TestEnemies.cs       |  15 +
 .../_Game/Tests/V4/PlayMode/V4TestEnemies.cs.meta  |   2 +
 .../V4/Config/Schemas/EnemyArchetypeConfig.cs      |   2 +-
 .../_Game/V4/Config/Schemas/EnemyVariantConfig.cs  |   2 +-
 .../Assets/_Game/V4/Editor/V4SceneSetup.cs         |  20 +
 .../Assets/_Game/V4/Runtime/Core/V4Game.cs         |  29 +-
 .../Assets/_Game/V4/Runtime/Core/V4Phase.cs        |   2 +-
 .../_Game/V4/Runtime/Enemies/EnemyContracts.cs     |   3 +
 .../_Game/V4/Runtime/Enemies/EnemyHazards.cs       |   6 +-
 .../_Game/V4/Runtime/Legacy/LegacyAdapters.cs      |  62 +--
 .../V4/Runtime/Legacy/LegacyAreaDenialRelease.cs   |  46 --
 .../Runtime/Legacy/LegacyAreaDenialRelease.cs.meta |   2 -
 .../_Game/V4/Runtime/Legacy/LegacyBossAdapters.cs  |  55 +--
 .../_Game/V4/Runtime/Legacy/LegacyEnemyViews.cs    |   4 +-
 .../_Game/V4/Runtime/Legacy/LegacyEnemyWiring.cs   | 202 ++++++++
 .../V4/Runtime/Legacy/LegacyEnemyWiring.cs.meta    |   2 +
 .../V4/Runtime/Legacy/LegacyEnemyWorldAdapters.cs  |  28 +-
 .../V4/Runtime/Legacy/LegacyHazardTelegraphView.cs |  42 +-
 .../_Game/V4/Runtime/Legacy/LegacyOrder50Slot.cs   |   2 +-
 .../_Game/V4/Runtime/Legacy/LegacyRunShell.cs      |  18 +-
 .../_Game/V4/Runtime/Legacy/LegacySpawnAdapters.cs | 107 +----
 .../_Game/V4/Runtime/Legacy/LegacyStageAdapters.cs |   4 +-
 .../_Game/V4/Runtime/Legacy/LegacyStageWiring.cs   |  13 +-
 .../_Game/V4/Runtime/Legacy/LegacyUpdateSlot.cs    |   2 +-
 .../Assets/_Game/V4/Runtime/Legacy/LegacyViews.cs  |  37 +-
 .../V4/Runtime/Legacy/LegacyWeaponAdapters.cs      |  45 +-
 .../_Game/V4/Runtime/Legacy/LegacyWeaponWiring.cs  |   7 +-
 .../_Game/V4/Runtime/Legacy/V4RuntimeDatabase.cs   |   2 +-
 docs/V4_IMPLEMENTATION_PLAN.md                     |   2 +-
 plan/v4-verification/phase-08-enemies/CHECKLIST.md |  51 +-
 plan/v4-verification/phase-08-enemies/DESIGN.md    |   2 +-
 .../phase-08-enemies/config-hash.txt               |  64 +++
 .../playable-check/beacon-hold-telegraph.png       | Bin 0 -> 1304676 bytes
 .../playable-check/boss-defeated.png               | Bin 0 -> 1317158 bytes
 .../phase-08-enemies/playable-check/boss-fight.png | Bin 0 -> 1316035 bytes
 .../playable-check/buoy-charging.png               | Bin 0 -> 1330757 bytes
 .../playable-check/buoy-reward.png                 | Bin 0 -> 1339956 bytes
 .../playable-check/chest-opened.png                | Bin 0 -> 1211294 bytes
 .../playable-check/chest-prompt.png                | Bin 0 -> 1187008 bytes
 .../playable-check/console-errors.txt              |   3 +
 .../playable-check/enemies-v4-world.png            | Bin 0 -> 1311538 bytes
 .../playable-check/powder-cache-ambush.png         | Bin 0 -> 1257982 bytes
 .../playable-check/repair-wreck.png                | Bin 0 -> 1256963 bytes
 .../phase-08-enemies/playable-check/results.png    | Bin 0 -> 1279538 bytes
 .../playable-check/stage-start.png                 | Bin 0 -> 1307155 bytes
 .../phase-08-enemies/playable-check/steps.log      |  72 +++
 .../player/v4-trace-seed-101-parity.jsonl          | 438 +++++++++++++++++
 .../player/v4-trace-seed-202-parity.jsonl          | 410 ++++++++++++++++
 .../player/v4-trace-seed-303-parity.jsonl          | 524 +++++++++++++++++++++
 .../phase-08-enemies/trace-comparison.txt          |   7 +
 plan/v4-verification/tools/playable-check-m8.ps1   | 119 +++++
 62 files changed, 2243 insertions(+), 560 deletions(-)
```

Write set: only Assets/_Game/V4, Scenes/V4, Tests/V4, plan/v4-verification and docs/V4_IMPLEMENTATION_PLAN.md. The TMP fallback font asset (touched by the Editor) was reverted.
