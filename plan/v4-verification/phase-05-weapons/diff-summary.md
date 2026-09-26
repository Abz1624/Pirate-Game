# M5 diff summary (since f8edb04e)

```
 .../LiberationSans SDF - Fallback.asset            | 1332 +++++++++++++++++++-
 .../Assets/_Game/Scenes/V4/V4ModuleLab.unity       |   24 +-
 .../Assets/_Game/Scenes/V4/V4SaltwindRun.unity     |   24 +-
 .../_Game/Tests/V4/EditMode/V4EnemyM2Tests.cs      |    7 +
 .../_Game/Tests/V4/EditMode/V4StructureTests.cs    |    3 +-
 .../_Game/Tests/V4/PlayMode/V4RunPlayModeTests.cs  |  123 +-
 .../Assets/_Game/V4/Config/Core/ConfigEnums.cs     |    4 +
 .../_Game/V4/Config/Schemas/PoolCatalogConfig.cs   |    6 +
 .../Assets/_Game/V4/Config/Schemas/WeaponConfig.cs |  176 ++-
 .../_Game/V4/Data/ConfigSets/SaltwindDefault.asset |    7 +
 .../_Game/V4/Data/Weapons/broadside_battery.asset  |   91 ++
 .../_Game/V4/Data/Weapons/deck_cannons.asset       |   91 ++
 .../_Game/V4/Data/Weapons/flamethrower.asset       |   91 ++
 .../_Game/V4/Data/Weapons/flying_cutlasses.asset   |   91 ++
 .../Assets/_Game/V4/Data/Weapons/harpoon_gun.asset |   91 ++
 .../_Game/V4/Data/Weapons/lightning_mast.asset     |   91 ++
 .../Assets/_Game/V4/Data/Weapons/long_nine.asset   |   91 ++
 .../Assets/_Game/V4/Data/Weapons/mortar.asset      |   91 ++
 .../Assets/_Game/V4/Data/Weapons/powder_kegs.asset |   91 ++
 .../Assets/_Game/V4/Data/Weapons/swivel_guns.asset |   91 ++
 .../_Game/V4/DevTools/Console/TuningConsole.cs     |    1 +
 .../Assets/_Game/V4/Editor/V3ConfigMigrator.cs     |  179 +++
 .../Assets/_Game/V4/Editor/V4SceneSetup.cs         |   56 +
 .../_Game/V4/Presentation/Pooling/PoolCatalog.cs   |   27 +
 .../_Game/V4/Presentation/V4PresentationRoot.cs    |    4 +
 .../Assets/_Game/V4/Runtime/Core/V4Game.cs         |    7 +
 .../Assets/_Game/V4/Runtime/Core/V4Phase.cs        |    2 +-
 .../_Game/V4/Runtime/Legacy/LegacyAdapters.cs      |    6 +-
 .../_Game/V4/Runtime/Legacy/LegacyRunShell.cs      |    8 +-
 .../_Game/V4/Runtime/Legacy/V4LegacyBridge.cs      |   12 +
 docs/V4_IMPLEMENTATION_PLAN.md                     |    2 +-
 plan/v4-verification/V3_DEFECT_REGISTER.md         |    2 +
 32 files changed, 2877 insertions(+), 45 deletions(-)
Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4WeaponM5Tests.cs
Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4WeaponM5Tests.cs.meta
Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WeaponEvolutionConfig.cs
Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WeaponEvolutionConfig.cs.meta
Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WeaponRulesConfig.cs
Pirate Game Unity/Assets/_Game/V4/Config/Schemas/WeaponRulesConfig.cs.meta
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions.meta
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/bladed_tide.asset
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/bladed_tide.asset.meta
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/devils_wake.asset
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/devils_wake.asset.meta
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/hellfire_nozzle.asset
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/hellfire_nozzle.asset.meta
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/kings_broadside.asset
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/kings_broadside.asset.meta
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/stormshot_cannon.asset
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/stormshot_cannon.asset.meta
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/tempest_mast.asset
Pirate Game Unity/Assets/_Game/V4/Data/Evolutions/tempest_mast.asset.meta
Pirate Game Unity/Assets/_Game/V4/Data/Weapons/WeaponRules.asset
Pirate Game Unity/Assets/_Game/V4/Data/Weapons/WeaponRules.asset.meta
Pirate Game Unity/Assets/_Game/V4/DevTools/Console/WeaponsSection.cs
Pirate Game Unity/Assets/_Game/V4/DevTools/Console/WeaponsSection.cs.meta
Pirate Game Unity/Assets/_Game/V4/Presentation/Weapons.meta
Pirate Game Unity/Assets/_Game/V4/Presentation/Weapons/ProjectileViewPresenter.cs
Pirate Game Unity/Assets/_Game/V4/Presentation/Weapons/ProjectileViewPresenter.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyOrder50Slot.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyOrder50Slot.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyWeaponAdapters.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyWeaponAdapters.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyWeaponLab.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyWeaponLab.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyWeaponWiring.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyWeaponWiring.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/DamageCalculator.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/DamageCalculator.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/PlayerProjectiles.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/PlayerProjectiles.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponBehaviors.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponBehaviors.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponCombat.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponCombat.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponContracts.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponContracts.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponManager.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponManager.cs.meta
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponTargetingService.cs
Pirate Game Unity/Assets/_Game/V4/Runtime/Weapons/WeaponTargetingService.cs.meta
plan/v4-verification/phase-05-weapons/CHECKLIST.md
plan/v4-verification/phase-05-weapons/config-hash.txt
plan/v4-verification/phase-05-weapons/diff-summary.md
plan/v4-verification/phase-05-weapons/editmode-results.json
plan/v4-verification/phase-05-weapons/playable-check/console-errors.txt
plan/v4-verification/phase-05-weapons/playable-check/steps.log
plan/v4-verification/phase-05-weapons/playable-check/weapon-broadside_battery.png
plan/v4-verification/phase-05-weapons/playable-check/weapon-flying_cutlasses.png
plan/v4-verification/phase-05-weapons/playable-check/weapon-lightning_mast.png
plan/v4-verification/phase-05-weapons/playable-check/weapon-long_nine.png
plan/v4-verification/phase-05-weapons/playable-check/weapon-mortar.png
plan/v4-verification/phase-05-weapons/playable-check/weapon-powder_kegs.png
plan/v4-verification/phase-05-weapons/playable-check/weapon-stormshot.png
plan/v4-verification/phase-05-weapons/player/v4-trace-seed-101-a.jsonl
plan/v4-verification/phase-05-weapons/player/v4-trace-seed-101-parity-b.jsonl
plan/v4-verification/phase-05-weapons/player/v4-trace-seed-101-parity.jsonl
plan/v4-verification/phase-05-weapons/player/v4-trace-seed-202-a.jsonl
plan/v4-verification/phase-05-weapons/player/v4-trace-seed-202-parity.jsonl
plan/v4-verification/phase-05-weapons/player/v4-trace-seed-303-a.jsonl
plan/v4-verification/phase-05-weapons/player/v4-trace-seed-303-parity.jsonl
plan/v4-verification/phase-05-weapons/playmode-results.json
plan/v4-verification/phase-05-weapons/smoke/01-weapons-firing.png
plan/v4-verification/phase-05-weapons/smoke/console-errors.txt
plan/v4-verification/phase-05-weapons/smoke/steps.log
plan/v4-verification/phase-05-weapons/trace-comparison.txt
plan/v4-verification/tools/playable-check-m5.ps1
```

Write set: Assets/_Game/V4, Assets/_Game/Tests/V4, Scenes/V4, plan/v4-verification, docs/V4_IMPLEMENTATION_PLAN.md. Outside: TMP font atlas (reverted) and the pre-existing untracked 'Weapon UI /' files (untouched).
