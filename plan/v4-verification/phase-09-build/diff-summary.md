# M9 diff summary (phase start 33f8b95e)

```
 .../_Game/V4/Runtime/Legacy/LegacyEventFeed.cs     |  11 +-
 .../_Game/V4/Runtime/Legacy/LegacyMagneticTide.cs  |  82 ---
 .../V4/Runtime/Legacy/LegacyMagneticTide.cs.meta   |   2 -
 .../_Game/V4/Runtime/Legacy/LegacyPickupViews.cs   | 113 +++
 .../V4/Runtime/Legacy/LegacyPickupViews.cs.meta    |   2 +
 .../_Game/V4/Runtime/Legacy/LegacyRunShell.cs      |  58 +-
 .../_Game/V4/Runtime/Legacy/LegacySpawnAdapters.cs |  61 +-
 .../_Game/V4/Runtime/Legacy/LegacyStageAdapters.cs |   9 +-
 .../_Game/V4/Runtime/Legacy/LegacyStageWiring.cs   |   6 +-
 .../Assets/_Game/V4/Runtime/Legacy/LegacyViews.cs  |  68 +-
 .../V4/Runtime/Legacy/LegacyWeaponAdapters.cs      | 176 -----
 .../_Game/V4/Runtime/Legacy/LegacyWeaponLab.cs     |  43 +-
 .../_Game/V4/Runtime/Legacy/LegacyWeaponWiring.cs  |  21 +-
 .../_Game/V4/Runtime/Legacy/V4LegacyBridge.cs      |  51 --
 .../_Game/V4/Runtime/Legacy/V4LegacyBridge.cs.meta |   2 -
 .../_Game/V4/Runtime/Legacy/V4RuntimeDatabase.cs   |   2 +-
 .../Assets/_Game/V4/Runtime/Progression.meta       |   8 +
 .../V4/Runtime/Progression/BuildWeaponModifiers.cs |  45 ++
 .../Progression/BuildWeaponModifiers.cs.meta       |   2 +
 .../_Game/V4/Runtime/Progression/CaptainModule.cs  | 180 +++++
 .../V4/Runtime/Progression/CaptainModule.cs.meta   |   2 +
 .../_Game/V4/Runtime/Progression/ItemModule.cs     | 217 ++++++
 .../V4/Runtime/Progression/ItemModule.cs.meta      |   2 +
 .../_Game/V4/Runtime/Progression/OfferGenerator.cs | 508 +++++++++++++
 .../V4/Runtime/Progression/OfferGenerator.cs.meta  |   2 +
 .../_Game/V4/Runtime/Progression/PickupModule.cs   | 384 ++++++++++
 .../V4/Runtime/Progression/PickupModule.cs.meta    |   2 +
 .../_Game/V4/Runtime/Progression/PowerupModule.cs  | 344 +++++++++
 .../V4/Runtime/Progression/PowerupModule.cs.meta   |   2 +
 .../V4/Runtime/Progression/ProgressionLoadout.cs   | 133 ++++
 .../Runtime/Progression/ProgressionLoadout.cs.meta |   2 +
 .../V4/Runtime/Progression/ProgressionMath.cs      |  19 +
 .../V4/Runtime/Progression/ProgressionMath.cs.meta |   2 +
 .../V4/Runtime/Progression/ProgressionModule.cs    | 371 ++++++++++
 .../Runtime/Progression/ProgressionModule.cs.meta  |   2 +
 .../V4/Runtime/Progression/ProgressionView.cs      |  35 +
 .../V4/Runtime/Progression/ProgressionView.cs.meta |   2 +
 .../_Game/V4/Runtime/Progression/StatResolver.cs   |  81 +++
 .../V4/Runtime/Progression/StatResolver.cs.meta    |   2 +
 docs/V4_IMPLEMENTATION_PLAN.md                     |   2 +-
 plan/v4-verification/phase-09-build/CHECKLIST.md   |  48 ++
 plan/v4-verification/phase-09-build/DESIGN.md      |  84 +++
 .../phase-09-build/playable-check.md               |  14 +
 .../playable-check/01-run-start-hud.png            | Bin 0 -> 1311744 bytes
 .../playable-check/02-level-up-cards.png           | Bin 0 -> 1117991 bytes
 .../playable-check/03-drops-bursting.png           | Bin 0 -> 1122813 bytes
 .../playable-check/04-after-magnetic-tide.png      | Bin 0 -> 1316052 bytes
 .../playable-check/05-captain-active-toast.png     | Bin 0 -> 1319575 bytes
 .../playable-check/06-item-toast.png               | Bin 0 -> 1319000 bytes
 .../playable-check/07-console-build-section.png    | Bin 0 -> 1226912 bytes
 .../phase-09-build/playable-check/log.txt          |  23 +
 .../player/v4-trace-seed-101-parity.jsonl          | 485 +++++++++++++
 .../player/v4-trace-seed-202-parity.jsonl          | 440 ++++++++++++
 .../player/v4-trace-seed-303-parity.jsonl          | 782 +++++++++++++++++++++
 .../phase-09-build/research-items-captains.md      |  94 +++
 .../phase-09-build/research-pickups-powerups.md    |  65 ++
 .../phase-09-build/research-progression.md         |  57 ++
 .../phase-09-build/trace-comparison.txt            |  26 +
 plan/v4-verification/presets/v3-parity.json        |  60 ++
 128 files changed, 7988 insertions(+), 850 deletions(-)
```
