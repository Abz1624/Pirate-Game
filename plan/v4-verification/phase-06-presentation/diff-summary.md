# M6 diff summary (since c39cb892)

```
 .../_Game/V4/Data/Weapons/flamethrower.asset       |  12 +
 .../_Game/V4/Data/Weapons/flying_cutlasses.asset   |  12 +
 .../Assets/_Game/V4/Data/Weapons/harpoon_gun.asset |  12 +
 .../_Game/V4/Data/Weapons/lightning_mast.asset     |  12 +
 .../Assets/_Game/V4/Data/Weapons/long_nine.asset   |  12 +
 .../Assets/_Game/V4/Data/Weapons/mortar.asset      |  12 +
 .../Assets/_Game/V4/Data/Weapons/powder_kegs.asset |  12 +
 .../Assets/_Game/V4/Data/Weapons/swivel_guns.asset |  12 +
 .../_Game/V4/DevTools/Console/EventsSection.cs     | 177 ++++
 .../V4/DevTools/Console/EventsSection.cs.meta      |   2 +
 .../_Game/V4/DevTools/Console/TuningConsole.cs     |   1 +
 .../Assets/_Game/V4/Editor/V3ConfigMigrator.cs     |  56 ++
 .../Assets/_Game/V4/Editor/V4IconCatalogBuilder.cs |  80 ++
 .../_Game/V4/Editor/V4IconCatalogBuilder.cs.meta   |   2 +
 .../Assets/_Game/V4/Editor/V4SceneSetup.cs         |  33 +
 .../Assets/_Game/V4/Presentation/Hud.meta          |   8 +
 .../V4/Presentation/Hud/AuthoredHudPresenter.cs    | 413 ++++++++++
 .../Presentation/Hud/AuthoredHudPresenter.cs.meta  |   2 +
 .../V4/Presentation/Hud/BeaconGuidePresenter.cs    | 151 ++++
 .../Presentation/Hud/BeaconGuidePresenter.cs.meta  |   2 +
 .../_Game/V4/Presentation/Hud/IconCatalog.cs       |  55 ++
 .../_Game/V4/Presentation/Hud/IconCatalog.cs.meta  |   2 +
 .../V4/Presentation/Hud/LoadoutHudPresenter.cs     | 352 ++++++++
 .../Presentation/Hud/LoadoutHudPresenter.cs.meta   |   2 +
 .../_Game/V4/Presentation/Hud/MinimapPresenter.cs  | 607 ++++++++++++++
 .../V4/Presentation/Hud/MinimapPresenter.cs.meta   |   2 +
 .../_Game/V4/Presentation/Hud/ResultsPresenter.cs  | 203 +++++
 .../V4/Presentation/Hud/ResultsPresenter.cs.meta   |   2 +
 .../V4/Presentation/Hud/SpawnWarningPresenter.cs   | 166 ++++
 .../Presentation/Hud/SpawnWarningPresenter.cs.meta |   2 +
 .../RoguePirates.V4.Presentation.asmdef            |   3 +-
 .../_Game/V4/Presentation/V4PresentationRoot.cs    | 111 ++-
 .../Presentation/Weapons/FlameStreamPresenter.cs   |  15 +-
 .../V4/Presentation/Weapons/WeaponAimOverlay.cs    | 545 ++++++++++++
 .../Presentation/Weapons/WeaponAimOverlay.cs.meta  |   2 +
 .../V4/Presentation/Weapons/WeaponVfxPresenter.cs  | 909 +++++++++++++++++++++
 .../Weapons/WeaponVfxPresenter.cs.meta             |   2 +
 Pirate Game Unity/Assets/_Game/V4/Resources.meta   |   8 +
 .../Assets/_Game/V4/Resources/V4IconCatalog.asset  |  74 ++
 .../_Game/V4/Resources/V4IconCatalog.asset.meta    |   8 +
 .../Assets/_Game/V4/Runtime/Core/V4Game.cs         |   8 +-
 .../Assets/_Game/V4/Runtime/Core/V4Phase.cs        |   2 +-
 .../Assets/_Game/V4/Runtime/Events/CombatEvents.cs |  11 +-
 .../_Game/V4/Runtime/Legacy/LegacyAdapters.cs      | 146 +++-
 .../_Game/V4/Runtime/Legacy/LegacyEventFeed.cs     |  72 +-
 .../V4/Runtime/Legacy/LegacyResultsRebinder.cs     |  45 -
 .../Runtime/Legacy/LegacyResultsRebinder.cs.meta   |   2 -
 .../_Game/V4/Runtime/Legacy/LegacyRunShell.cs      |   2 +-
 .../_Game/V4/Runtime/Legacy/LegacySpawnAdapters.cs |  12 +-
 .../Assets/_Game/V4/Runtime/Legacy/LegacyViews.cs  | 418 ++++++++++
 .../_Game/V4/Runtime/Legacy/LegacyViews.cs.meta    |   2 +
 .../V4/Runtime/Legacy/LegacyWeaponAdapters.cs      | 109 ---
 .../_Game/V4/Runtime/Legacy/LegacyWeaponWiring.cs  |  10 +-
 .../_Game/V4/Runtime/Spawning/SpawnContracts.cs    |   4 +-
 .../_Game/V4/Runtime/Spawning/SpawnExecutor.cs     |   5 +-
 .../_Game/V4/Runtime/Spawning/SpawnServices.cs     |   4 +-
 .../Assets/_Game/V4/Runtime/Views.meta             |   8 +
 .../Assets/_Game/V4/Runtime/Views/RunViews.cs      | 210 +++++
 .../Assets/_Game/V4/Runtime/Views/RunViews.cs.meta |   2 +
 .../_Game/V4/Runtime/Weapons/WeaponAimView.cs      |  46 ++
 .../_Game/V4/Runtime/Weapons/WeaponAimView.cs.meta |   2 +
 .../V4/Runtime/Weapons/WeaponEffectsForwarder.cs   |  65 ++
 .../Runtime/Weapons/WeaponEffectsForwarder.cs.meta |   2 +
 .../_Game/V4/Runtime/Weapons/WeaponManager.cs      |   3 +
 docs/V4_IMPLEMENTATION_PLAN.md                     |   2 +-
 plan/v4-verification/V3_DEFECT_REGISTER.md         |   7 +-
 .../phase-06-presentation/CHECKLIST.md             |  97 +++
 .../phase-06-presentation/config-hash.txt          |  62 ++
 .../playable-check/aim-overlay.png                 | Bin 0 -> 1172674 bytes
 .../playable-check/beacon-guide.png                | Bin 0 -> 1136093 bytes
 .../playable-check/console-errors.txt              |   3 +
 .../playable-check/hud-loadout-and-effects.png     | Bin 0 -> 1341811 bytes
 .../playable-check/hud-start.png                   | Bin 0 -> 1324893 bytes
 .../playable-check/juice-blast-and-oil.png         | Bin 0 -> 1356013 bytes
 .../playable-check/minimap-miniboss.png            | Bin 0 -> 1351087 bytes
 .../playable-check/results.png                     | Bin 0 -> 1323251 bytes
 .../playable-check/spawn-warning.png               | Bin 0 -> 1134562 bytes
 .../phase-06-presentation/playable-check/steps.log |  44 +
 plan/v4-verification/tools/playable-check-m6.ps1   | 112 +++
 96 files changed, 6628 insertions(+), 364 deletions(-)
```

Write set: every file is under Assets/_Game/V4, Scenes/V4, Tests/V4, plan/ or docs/. Not included: the Editor's runtime font-atlas change (reverted) and the 'Assets/UI/Weapons/Weapon UI /' files, which Windows cannot open (trailing space in the folder name; untouched, not staged).
