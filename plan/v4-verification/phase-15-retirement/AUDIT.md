# M15 dependency audit: V4 vs HighSeas (V3) code

Read-only audit of `Pirate Game Unity/Assets` on branch `v4-modular-architecture` (HEAD `ba0aead6 V4 M14: Switch-over`), 2026-09-29.

**How it was measured.** A script parsed every `.cs` under `Assets/_Game/Scripts/**` (312 files) for top-level type declarations and namespaces. It then scanned each V4 file (`V4/**`, `Tests/V4/**`, 265 files) with comments and strings stripped. A bare identifier counts as a use only if the file can see that namespace (a `using HighSeas.X`, an alias, or a qualified `HighSeas.X.T`). Names shadowed by a V4 type in an enclosing or imported namespace are skipped, and nested types (for example `ReefCrabMinibossBehavior.Phase`) are not matched by their bare name. The same scan built the internal Scripts dependency graph. Asset references come from a GUID crawler over every YAML asset (`.unity/.prefab/.asset/.mat/.controller/...`), resolved against every `.meta` in Assets and in `Library/PackageCache` plus the built-in packages. I spot-checked the results by hand (for example, `EnemySimulation.cs` uses `SpatialHash`, which is declared inside `EnemyWorld.cs`). Treat it as close to exact, not compiler-exact. Reflection- or string-based uses are listed separately where found.

## 0. Headline numbers

| | files | lines |
|---|---|---|
| `Scripts/**` total (HighSeas.Runtime + HighSeas.Editor) | 312 | 53,406 |
| Referenced from any V4 code or test | 168 | |
| Referenced from V4 runtime assemblies (Runtime incl. Legacy, Presentation, DevTools) | 104 | |
| Referenced from V4 prod code outside `Runtime/Legacy` | 64 | |
| **Proposed keep set** (section 3; move into V4) | **119** | **13,930** |
| of which under `Scripts/V3/` (move and rename) | 21 | 2,429 |
| Delete: rest of `Scripts/V3/**` | 81 | 13,022 |
| Delete: non-V3 Scripts outside the keep set | 86 | 21,146 |
| Delete or trim: `Scripts/Editor` (HighSeas.Editor; V4 uses none of its types) | 26 | 5,718 |
| Scenes outside V4: `Scenes/v3` (8, 8.8 MB), `Scenes/Tests` (4), `Scenes/Promo` (1), `Scenes/*.unity` root (5), `Assets/Scenes/Main.unity` | 19 | |
| Old tests: `Tests/EditMode` (HighSeas.Tests.EditMode) + `Tests/PlayMode` | 22 + 13 | 2,790 + 1,326 |
| V4 files that break when V3 goes (Legacy ports, trace golden paths, V3-side editor tools, V4 tests that reference V3) | ~7 + 5 + 6 + 20 | |

Asmdef facts (checked):
- `RoguePirates.V4`, `.V4.Presentation`, `.V4.DevTools`, `.V4.Editor`, `V4.Tests.EditMode` and `V4.Tests.PlayMode` all reference `HighSeas.Runtime`.
- `RoguePirates.V4.Editor` also references `HighSeas.Editor`, but no V4 file uses a HighSeas.Editor type, so that reference is dead.
- `RoguePirates.V4.Config` does **not** reference HighSeas, and `V4StructureTests.AssemblyReferences_FollowTheLayering` forbids it. It also forbids `HighSeas.Runtime` from referencing `RoguePirates.V4`.
- `RoguePirates.Presentation` (`Assets/_Game/Presentation`, 4 files) references HighSeas.Runtime through `V3SimulationAdapter.cs`, which no scene or prefab uses.
- `RoguePirates.Simulation`, `.Content`, `.AccountClient`, `.PlatformAdapters` and `RoguePirates.Tests.EditMode` do not depend on HighSeas. They are unaffected, except that the Simulation assembly is also used by HighSeas `Balance/*` and by V4 (`RunClock`, `V4Game`).

## 1. V4 -> HighSeas usage

Real namespaces in `Scripts/`, with file counts: HighSeas.V3 (84), HighSeas.Combat (30), HighSeas.Editor (26), HighSeas.Balance (24), HighSeas.World (23), HighSeas.Enemies (21), HighSeas.V3.MultiShip (19), HighSeas.VFX (16), HighSeas.Data (14), HighSeas.UI (14), HighSeas.Core (9), HighSeas.Pickups (4), HighSeas.Platform (4), HighSeas.POI (3), HighSeas.Player (3), HighSeas.Debugging (3), HighSeas.Playtest (3), HighSeas.Weapons (3), HighSeas.PlayerSystems (2), HighSeas.Promo (2), HighSeas.Save (2), HighSeas.Audio (1), HighSeas.Encounters (1), HighSeas.Meta (1). Note that `Player/ShipBoostController.cs` and `Player/BoostCameraZoom.cs` live in namespace `HighSeas.PlayerSystems`.

`using HighSeas.*` directives in V4 code and tests, counted per directive: Core 95, Enemies 46, Data 44, Balance 34, Combat 29, V3 27, Player 25, World 22, POI 10, VFX 10, Encounters 9, UI 9, Pickups 6, Platform 5, Weapons 4, Audio 2, V3.MultiShip 2, Debugging 1, Meta 1, PlayerSystems 1, Save 1.

### 1c. The `[DeletedInPhase(15)]` "tag 15 ports" in `V4/Runtime/Legacy/` (7 files, 1,263 lines)

| Type (file:line) | Wraps (HighSeas types) | Constructed/used by (V4) |
|---|---|---|
| `LegacyDatabaseAdapter` (LegacyAdapters.cs:27) | `GameDatabase`, `GameDatabaseLoader.Load/IsValid`, `CombatExperimentContext.Active`, `CombatWeaponCatalog.ApplyToDatabase`, `CombatSpecialistEnemyCatalog.ApplyToDatabase` | RunBootstrapSequence |
| `LegacyBuildMapping` (LegacyBuildMapping.cs:14) | `V3CaptainId`, `V3RunItemEffects`, `V3FloorPowerupKind`, `StatId`: V3-id to V4-config mapping for items, captains and logbooks | V4BuildConfigGenerator (editor, 12 uses), V4StructureTests |
| `LegacyEnemyWorldView` (LegacyEnemyViews.cs:16) | V3 `EnemyWorld` (FindFirstObjectByType) for V3-scene golden recordings | DevTools Trace (PlayerPilot, RunTraceRecorder) |
| `LegacyHostileProjectileView` (LegacyEnemyViews.cs:88) | `HostileProjectileWorld`, `HostileProjectile` | PlayerPilot |
| `LegacyHazardView` (LegacyEnemyViews.cs:108) | `SaltwindHazardTelegraph` | PlayerPilot, V4EnemyTickM8Tests, V4EnemyLockstepPlayModeTests |
| `LegacyRunPort` (LegacyPorts.cs:25) | `RunManager.Instance` (state, pause, gold, kills, elapsed, overtime) + its `RunRng` streams (Director/Loot/Combat/Map) | V4Game, RunBootstrapSequence, StageModule, WeaponModule, EnemyWorldModule, 3 tests |
| `LegacyShipPort` (LegacyPorts.cs:140) | `PlayerShipController`, `DamageReceiver`, `ShipBoostController`, `StatModifier`/`StatId` | V4Game, StageModule, WeaponModule, EnemyWorldModule, 2 tests |
| `LegacyPlayBoundsPort` (LegacyPorts.cs:255) | `PlayBounds` statics (storm region, bounds) | V4Game |
| `LegacySpawnPlacementPort` (LegacyPorts.cs:280) | `SpawnPlacementService.TryPickSpawnPosition/OffsetGroupPosition/IsFormationPositionValid` (+`RunManager.Instance.Rng`) | V4Game |
| `LegacyStageProfilePort` (LegacyStageProfilePort.cs:20) | Builds a `StageBalanceProfile` from V4 configs and begins or ends `StageBalanceProfileContext` (read by RunManager.IsOvertime, V3ContentUnlocks and StageBuilder's POI placement). Also `SaltwindStageEconomyLogic.ApplyGoldGrant`, `SaltwindRunResultsLogic.FormatProfileFooter`, `V3RunSession.IsLocalMultiplayer`, `AppRoot.EndActiveSaltwindRunContext` | BuildModule, RunBootstrapSequence, V3ConfigMapping (editor), V4SwitchoverM14Tests |
| `LegacyWorldPort` (LegacyWorldPort.cs:18) | `StageBuilder` (`ConfigureV3Layout`, `Build`), `IslandLayoutDefinition` (incl. `Resources.Load("V3SaltwindIslandLayout")`), `ProceduralIslandSettings`, `V3SpawnAnchorValidator.ResolveRuntimeSpawn`, `IslandObstacles`, `V3HomeSeed`, `V3RunContext`, `V3StormEdgeVisual.DisableWorldRings`, `PlayBounds.ResetStormSafeRegion`, `V3ExplosiveBarrelWorld.Ensure` (disabled marker), `EnvironmentalHazardsDisabled`/`WindGustDirector`/`EnvironmentHazardVisual`/`OceanCurrentZone`/`WhirlpoolHazard` (destroyed), `PlayerShipVisualBootstrap`, `DayNightDirector`, `ShipBoostController`, `BoostCameraZoom`, `SaltwindRunResetLogic.NotifyRetryStarting`, `RunSnapshotService.Clear` | RunBootstrapSequence (11), StageModule |
| `LegacyContentPort` (LegacyWorldPort.cs:165) | `V3ContentUnlocks`, `V3PlaytestFlags`, `V3CaptainSelection`, `V3RunContext.IsActive`, `WeaponFeatureFlags`, `AppRoot.EnsureInitialized/EndActiveSaltwindRunContext`, `CombatExperimentContext/Profile` | BuildModule, MenuServices, StageModule, CaptainChoice |
| `V4RuntimeDatabase` (V4RuntimeDatabase.cs:21) | Per-run copy of `GameDatabase` whose `EnemyDefinition[]` is rewritten from V4 archetype configs | RunBootstrapSequence, 5 test files |

`V4StructureTests.NoAdapter_OutlivesItsDeletionPhase` (Tests/V4/EditMode/V4StructureTests.cs:90) fails once `V4Phase.Current >= 15` while any of these types exists. `V4HudM6Tests.cs:73` also requires every Legacy type to carry the attribute.


### 1a. Table: every Scripts file referenced from V4 code or V4 tests

Columns: prod = V4 Runtime/Presentation/Config outside Legacy; legacy = `Runtime/Legacy/`; dev = DevTools; ed = V4 Editor; test = Tests/V4. "keep" = in the proposed keep set (section 3). Counts are distinct V4 files.

| Scripts file | types used | prod | legacy | dev | ed | test | total | keep | V4 files (prod/legacy/dev first; tests abbreviated) |
|---|---|---|---|---|---|---|---|---|---|
| `Core/GameEnums.cs` | EliteAffix, EnemyCategory, EnemyTier, POIType, PauseReason, PickupType, QualityTier, Rarity, RunState, StatId, StatusType | 31 | 2 | 5 | 1 | 20 | 59 | yes | PickupPresenter, MinimapPresenter, OfferCards, ResultsPresenter, SpawnWarningPresenter, PoiMarkerPresenter, BuildModule, RunBootstrapSequence, RunLifecycle, SlotPool ...(+49) |
| `Player/PlayerShipController.cs` | PlayerShipController | 8 | 2 | 9 | 0 | 9 | 28 | yes | BuildModule, EnemyWorldModule, RunBootstrapSequence, StageModule, V4Game, EnemyCombatBehaviors, EnemySimulation.Tick, EnemySpecialistCombat, LegacyPorts, LegacyWorldPort ...(+18) |
| `Core/RunManager.cs` | RunManager | 4 | 1 | 10 | 0 | 12 | 27 | yes | BuildModule, EnemyWorldModule, RunBootstrapSequence, V4Game, LegacyPorts, BuildSection, ConfigSection, RunSection, StageSection, TimelineSection ...(+17) |
| `Data/EnemyDefinition.cs` | EnemyDefinition | 15 | 1 | 0 | 1 | 8 | 25 | yes | EnemyViewPresenter, EnemyCombatBehaviors, EnemyCombatProfiles, EnemyCombatServices, EnemyReefCrabCombat, EnemySimulation.Tick, EnemySimulation, EnemySimulationContracts, EnemySpecialBehaviors, EnemySpecialistCombat ...(+15) |
| `Enemies/SpawnProfile.cs` | SpawnProfile | 11 | 0 | 0 | 2 | 6 | 19 | yes | EnemyCombatProfiles, EnemyCombatServices, EnemySimulation, EnemySimulationContracts, EnemyUnit, EnemyWorldContracts, AmbientSpawner, EncounterExecutor, SpawnContracts, SpawnExecutor ...(+9) |
| `Data/DemoJuiceSettings.cs` | DemoJuiceSettings | 11 | 0 | 0 | 1 | 5 | 17 | yes | CombatFeedbackPresenter, EnemyViewPresenter, HostileShotPresenter, PickupPresenter, PoolCatalog, PoiMarkerPresenter, V4PresentationRoot, FlameStreamPresenter, ProjectileViewPresenter, WeaponVfxPresenter ...(+7) |
| `Balance/StageBalanceProfile.cs` | StageBalanceProfile | 0 | 1 | 0 | 5 | 10 | 16 | yes | LegacyStageProfilePort, V3ConfigMapping, V3ConfigMigrator, V4BuildConfigGenerator, V4RewardConfigGenerator, V4SceneSetup, V4BuildM9Tests, V4ConfigM1Tests, V4EnemyTickM8Tests, V4EnemyWorldM8Tests ...(+6) |
| `Balance/StageBalanceProfileContext.cs` | StageBalanceProfileContext | 0 | 1 | 1 | 0 | 13 | 15 | yes | LegacyStageProfilePort, RunTraceRecorder, V4BuildM9Tests, V4EnemyTickM8Tests, V4EnemyWorldM8Tests, V4PickupM9Tests, V4ProgressionM9Tests, V4SwitchoverM14Tests, V4AdaptivePlayModeTests, V4EnemyLockstepPlayModeTests ...(+5) |
| `Core/GameConstants.cs` | GameConstants, GameMath, StatModifier | 4 | 1 | 0 | 2 | 6 | 13 | yes | EnemySimulation.Tick, EnemySimulation, PickupModule, StatResolver, LegacyPorts, V3ConfigMigrator, V4BuildConfigGenerator, V4BuildM9Tests, V4ConfigM1Tests, V4DifficultyM10Tests ...(+3) |
| `Data/GameDatabase.cs` | GameDatabase | 4 | 2 | 0 | 2 | 4 | 12 | yes | BuildModule, EnemyWorldModule, RunBootstrapSequence, V4Game, LegacyAdapters, V4RuntimeDatabase, V3ConfigMigrator, V4BuildConfigGenerator, V4EnemyM2Tests, V4EnemyTickM8Tests ...(+2) |
| `Combat/DamageReceiver.cs` | DamageReceiver | 7 | 1 | 0 | 0 | 3 | 11 | yes | BuildModule, EnemyCombatBehaviors, EnemyCombatServices, EnemyReefCrabCombat, EnemySimulation.Tick, EnemySpecialBehaviors, EnemySpecialistCombat, LegacyPorts, V4EnemyTickM8Tests, V4PickupM9Tests ...(+1) |
| `Combat/EnemyCombatProfile.cs` | EnemyCombatPhase, EnemyCombatProfile, SaltwindCombatArchetype | 6 | 0 | 0 | 0 | 4 | 10 | yes | EnemyCombatBehaviors, EnemyCombatProfiles, EnemyReefCrabCombat, EnemySimulationContracts, EnemySpecialistCombat, EnemyUnit, V3RegistryFromConfig, V4EnemyM2Tests, V4EnemyTickM8Tests, V3RegistryFromConfig |
| `World/IslandObstacles.cs` | IslandObstacles | 6 | 1 | 2 | 0 | 1 | 10 | yes | BuildModule, EnemyCombatBehaviors, EnemySimulation.Tick, EnemySimulation, EnemySpecialBehaviors, EnemyStatusesAndShots, LegacyWorldPort, HumanPilot, PlayerPilot, V4EnemyLockstepPlayModeTests |
| `Combat/CombatExperimentProfile.cs` | CombatExperimentContext, CombatExperimentProfile | 0 | 1 | 0 | 1 | 7 | 9 | yes | LegacyAdapters, V3ConfigMigrator, V4ConfigM1Tests, V4EnemyM2Tests, V4EnemyTickM8Tests, V4EnemyWorldM8Tests, V4HudM6Tests, V4ProgressionM9Tests, V4WeaponVfxM6Tests |
| `Core/AppRoot.cs` | AppRoot | 5 | 2 | 1 | 0 | 1 | 9 | yes | CameraShakePresenter, MenuServices, RunBootstrapSequence, V4Game, V4BootLoader, LegacyStageProfilePort, LegacyWorldPort, TraceBoot, V4RunPlayModeTests |
| `Balance/SaltwindRunSettlement.cs` | SaltwindRunSettlement | 0 | 0 | 1 | 0 | 7 | 8 | **no** | RunTraceRecorder, V4AdaptivePlayModeTests, V4EnemyLockstepPlayModeTests, V4EnemyPlayModeTests, V4HudPlayModeTests, V4NarrativePlayModeTests, V4RunPlayModeTests, V4StagePlayModeTests |
| `Combat/CombatWeaponCatalog.cs` | CombatWeaponCatalog | 0 | 1 | 0 | 1 | 6 | 8 | yes | LegacyAdapters, V3ConfigMigrator, V4ConfigM1Tests, V4EnemyM2Tests, V4EnemyTickM8Tests, V4EnemyWorldM8Tests, V4HudM6Tests, V4ProgressionM9Tests |
| `Enemies/EnemySpawnRuntimeParams.cs` | EnemySpawnRuntimeParams | 5 | 0 | 0 | 0 | 3 | 8 | yes | EnemyCombatProfiles, EnemyPatternMovement, EnemySimulationContracts, EnemySpecialBehaviors, EnemyUnit, V4EnemyTickM8Tests, V4EnemyWorldM8Tests, V4EnemyLockstepPlayModeTests |
| `V3/V3CaptainId.cs` | V3CaptainId | 3 | 2 | 0 | 1 | 2 | 8 | yes | AuthoredHudPresenter, CaptainSelectView, V4MainMenuController, LegacyBuildMapping, LegacyWorldPort, V4BuildConfigGenerator, V4BuildM9Tests, V4ProgressionM9Tests |
| `World/PlayBounds.cs` | PlayBounds | 4 | 2 | 2 | 0 | 0 | 8 | yes | MinimapPresenter, BuildModule, EnemyCombatBehaviors, EnemySimulation, LegacyPorts, LegacyWorldPort, HumanPilot, PlayerPilot |
| `World/StageGenerator.cs` | POISlot, StageLayout | 6 | 1 | 0 | 0 | 1 | 8 | yes | EnemyWorldModule, RunBootstrapSequence, StageModule, BeaconDiscovery, StageContentModule, StageView, LegacyWorldPort, V4StageM7Tests |
| `Balance/RunMarksTracker.cs` | RunMarksTracker | 0 | 0 | 0 | 0 | 7 | 7 | **no** | V4AdaptivePlayModeTests, V4EnemyLockstepPlayModeTests, V4EnemyPlayModeTests, V4HudPlayModeTests, V4NarrativePlayModeTests, V4RunPlayModeTests, V4StagePlayModeTests |
| `Encounters/RunEncounterTracker.cs` | RunEncounterTracker | 0 | 0 | 0 | 0 | 7 | 7 | **no** | V4AdaptivePlayModeTests, V4EnemyLockstepPlayModeTests, V4EnemyPlayModeTests, V4HudPlayModeTests, V4NarrativePlayModeTests, V4RunPlayModeTests, V4StagePlayModeTests |
| `Enemies/EnemyWorld.cs` | EnemyAgent, EnemyWorld, SpatialHash | 1 | 1 | 0 | 1 | 4 | 7 | **no** | EnemySimulation, LegacyEnemyViews, V4SceneSetup, V4EnemyTickM8Tests, V4EnemyWorldM8Tests, V4EnemyLockstepPlayModeTests, V4EnemyPlayModeTests |
| `Save/SaveService.cs` | MemorySaveBackend, SaveService | 4 | 0 | 1 | 0 | 2 | 7 | yes | MenuServices, CaptainLocks, UnlockService, ChestEconomy, TraceBoot, V4RewardsM12Tests, V4RunPlayModeTests |
| `Combat/CombatSpecialistEnemyCatalog.cs` | CombatSpecialistEnemyCatalog | 0 | 1 | 0 | 1 | 4 | 6 | yes | LegacyAdapters, V3ConfigMigrator, V4ConfigM1Tests, V4EnemyM2Tests, V4EnemyTickM8Tests, V4EnemyWorldM8Tests |
| `Combat/EnemyCombatProfileRegistry.cs` | EnemyCombatProfileRegistry | 0 | 0 | 0 | 1 | 5 | 6 | yes | V3ConfigMigrator, V3RegistryFromConfig, V4ConfigM1Tests, V4EnemyM2Tests, V4EnemyTickM8Tests, V3RegistryFromConfig |
| `Core/PlayerStats.cs` | PlayerStats | 2 | 0 | 0 | 0 | 4 | 6 | yes | BuildWeaponModifiers, StatResolver, V4BuildM9Tests, V4M9GateFixTests, V4ProgressionM9Tests, V4WeaponM5Tests |
| `Data/DemoPacingSettings.cs` | DemoPacingSettings | 0 | 0 | 0 | 2 | 4 | 6 | **no** | V3ConfigMigrator, V4BuildConfigGenerator, V4BuildM9Tests, V4ConfigM1Tests, V4ProgressionM9Tests, V4SpawnM3Tests |
| `Enemies/EncounterSpawner.cs` | EncounterSpawner | 0 | 0 | 0 | 1 | 5 | 6 | **no** | V4SceneSetup, V4ProgressionM9Tests, V4SpawnM3Tests, V4HudPlayModeTests, V4RunPlayModeTests, V4StagePlayModeTests |
| `Enemies/SpawnProfileModules.cs` | FormationBehaviorSettings, SpawnAttackOverrides, SpawnEliteSettings, SpawnMovementOverrides, SpawnPlacementSettings | 4 | 1 | 0 | 0 | 1 | 6 | yes | EnemyCombatProfiles, EnemySpecialBehaviors, SpawnContracts, SpawnerProfiles, LegacyPorts, V4SpawnM3Tests |
| `Combat/HostileProjectileWorld.cs` | HostileProjectile, HostileProjectileWorld | 0 | 1 | 0 | 1 | 3 | 5 | **no** | LegacyEnemyViews, V4SceneSetup, V4EnemyTickM8Tests, V4EnemyLockstepPlayModeTests, V4EnemyPlayModeTests |
| `Combat/WeaponEvolutionCatalog.cs` | WeaponEvolutionCatalog, WeaponEvolutionRuntime | 0 | 0 | 0 | 1 | 4 | 5 | yes | V3ConfigMigrator, V4HudM6Tests, V4ProgressionM9Tests, V4WeaponM5Tests, V4EnemyPlayModeTests |
| `Core/RunRng.cs` | RunRng | 0 | 0 | 0 | 0 | 5 | 5 | yes | V4PickupM9Tests, V4ProgressionM9Tests, V4SpawnM3Tests, V4SwitchoverM14Tests, V4WeaponM5Tests |
| `Enemies/EncounterSpawnSchedule.cs` | EncounterSpawnSchedule, ScheduledSpawnEntry | 0 | 0 | 0 | 3 | 2 | 5 | yes | V3ConfigMapping, V3ConfigMigrator, V4SceneSetup, V4ConfigM1Tests, V4SwitchoverM14Tests |
| `Balance/SaltwindStageEconomyLogic.cs` | SaltwindStageEconomyLogic | 0 | 1 | 0 | 0 | 3 | 4 | yes | LegacyStageProfilePort, V4PickupM9Tests, V4StageM7Tests, V4SwitchoverM14Tests |
| `Combat/StatusController.cs` | StatusController | 0 | 0 | 0 | 1 | 3 | 4 | **no** | V4SceneSetup, V4EnemyTickM8Tests, V4EnemyLockstepPlayModeTests, V4EnemyPlayModeTests |
| `Core/GameDatabaseLoader.cs` | GameDatabaseLoader | 0 | 1 | 0 | 1 | 2 | 4 | yes | LegacyAdapters, V3ConfigMigrator, V4EnemyM2Tests, V4WorldViewsM14Tests |
| `Enemies/EnemyVisualFallbackResolver.cs` | EnemyVisualFallbackResolver | 2 | 0 | 0 | 0 | 2 | 4 | yes | EnemyViewPresenter, EnemyViewForwarder, V3EnemyViewSink, V3EnemyViewSink |
| `POI/POIManager.cs` | POIInteractable, POIManager | 0 | 0 | 1 | 1 | 2 | 4 | **no** | RunTraceRecorder, V4SceneSetup, V4RunPlayModeTests, V4StagePlayModeTests |
| `Pickups/PickupWorld.cs` | PickupAgent, PickupWorld | 0 | 0 | 1 | 1 | 2 | 4 | **no** | PlayerPilot, V4SceneSetup, V4PickupM9Tests, V4HudPlayModeTests |
| `Platform/MoveInput.cs` | IShipSteerInput | 0 | 0 | 4 | 0 | 0 | 4 | yes | HumanPilot, PlayerPilot, ScriptedPilot, TraceBoot |
| `UI/LevelOffer.cs` | LevelOffer, LevelOfferKind, WeaponUpgradeLine | 2 | 0 | 1 | 0 | 1 | 4 | yes | LevelUpPresenter, OfferCards, RunTraceRecorder, V4ProgressionM9Tests |
| `UI/LevelSystem.cs` | LevelSystem | 0 | 0 | 1 | 1 | 2 | 4 | **no** | RunTraceRecorder, V4SceneSetup, V4ProgressionM9Tests, V4HudPlayModeTests |
| `V3/V3FloorPowerupWorld.cs` | V3FloorPowerupWorld | 0 | 0 | 0 | 1 | 3 | 4 | **no** | V4BuildConfigGenerator, V4BuildM9Tests, V4ProgressionM9Tests, V4HudPlayModeTests |
| `V3/V3ItemCatalog.cs` | V3ItemCatalog | 0 | 0 | 0 | 2 | 2 | 4 | **no** | V4BuildConfigGenerator, V4IconCatalogBuilder, V4BuildM9Tests, V4HudM6Tests |
| `V3/V3PlaceholderFactory.cs` | V3PlaceholderFactory | 4 | 0 | 0 | 0 | 0 | 4 | yes | PowerupPresenter, ResultsPresenter, InteractionPromptPresenter, PoiMarkerPresenter |
| `VFX/VfxParticleUtil.cs` | VfxParticleUtil | 4 | 0 | 0 | 0 | 0 | 4 | yes | HostileShotPresenter, FlameStreamPresenter, ProjectileViewPresenter, WeaponVfxPresenter |
| `VFX/WorldHealthBar.cs` | WorldHealthBar | 2 | 0 | 0 | 0 | 2 | 4 | yes | EnemyViewPresenter, ShipHealthBarPresenter, V3EnemyViewSink, V3EnemyViewSink |
| `Weapons/WeaponController.cs` | WeaponController | 0 | 0 | 0 | 1 | 3 | 4 | **no** | V4SceneSetup, V4ProgressionM9Tests, V4HudPlayModeTests, V4RunPlayModeTests |
| `Balance/SaltwindAmbientCompositionLogic.cs` | SaltwindAmbientCompositionLogic | 0 | 0 | 0 | 1 | 2 | 3 | **no** | V3ConfigMigrator, V4ConfigM1Tests, V4SpawnM3Tests |
| `Balance/SaltwindFinaleLogic.cs` | BarnacleKingAttackKind, BarnacleKingAttackState, BarnacleKingBossAttackLogic, BarnacleKingBossPhase, SaltwindFinaleLogic | 0 | 0 | 0 | 1 | 2 | 3 | yes | V4BossConfigGenerator, V4BossM7Tests, V4SpawnM3Tests |
| `Balance/SaltwindRunResultsLogic.cs` | SaltwindRunResultsLogic | 1 | 1 | 0 | 0 | 1 | 3 | yes | ResultsPresenter, LegacyStageProfilePort, V4HudM6Tests |
| `Combat/SaltwindCombatAgentStateStore.cs` | SaltwindCombatAgentStateStore | 0 | 0 | 0 | 0 | 3 | 3 | **no** | V4EnemyTickM8Tests, V4EnemyWorldM8Tests, V4EnemyLockstepPlayModeTests |
| `Combat/SaltwindCombatFeature.cs` | SaltwindCombatFeature | 0 | 0 | 0 | 0 | 3 | 3 | yes | V4EnemyTickM8Tests, V4EnemyWorldM8Tests, V4EnemyLockstepPlayModeTests |
| `Combat/SaltwindHazardTelegraph.cs` | SaltwindHazardTelegraph | 0 | 1 | 0 | 0 | 2 | 3 | **no** | LegacyEnemyViews, V4EnemyTickM8Tests, V4EnemyLockstepPlayModeTests |
| `Data/IslandLayoutDefinition.cs` | IslandLayoutDefinition | 2 | 1 | 0 | 0 | 0 | 3 | yes | RunBootstrapSequence, V4Game, LegacyWorldPort |
| `Enemies/EnemyShipWakeJuice.cs` | EnemyShipWakeJuice | 1 | 0 | 0 | 0 | 2 | 3 | yes | EnemyViewPresenter, V3EnemyViewSink, V3EnemyViewSink |
| `Enemies/EnemyViewAnimation.cs` | EnemyViewAnimation | 1 | 0 | 0 | 0 | 2 | 3 | yes | EnemyViewPresenter, V3EnemyViewSink, V3EnemyViewSink |
| `Enemies/SpawnProfileEnums.cs` | AdaptiveSignal, EnemyMovementPattern, EnemySpecialBehavior, FormationRole, FormationShape, PhaseDashAxis, TerrainAmbushTrigger | 3 | 0 | 0 | 0 | 0 | 3 | yes | EnemyPatternMovement, EnemySimulationContracts, EnemySpecialBehaviors |
| `Meta/MetaSystems.cs` | LootService, QuestService, TriggerBus | 0 | 0 | 1 | 1 | 1 | 3 | **no** | RunTraceRecorder, V4SceneSetup, V4HudPlayModeTests |
| `UI/LevelUpModal.cs` | LevelUpModal | 0 | 0 | 1 | 1 | 1 | 3 | **no** | RunTraceRecorder, V4SceneSetup, V4RunPlayModeTests |
| `V3/V3AuthoredGameplayHud.cs` | V3AuthoredGameplayHud | 0 | 0 | 0 | 1 | 2 | 3 | **no** | V4SceneSetup, V4HudM6Tests, V4HudPlayModeTests |
| `V3/V3CaptainProfile.cs` | V3CaptainProfile | 0 | 0 | 0 | 1 | 2 | 3 | **no** | V4BuildConfigGenerator, V4BuildM9Tests, V4ProgressionM9Tests |
| `V3/V3ExplosiveBarrelWorld.cs` | V3ExplosiveBarrelWorld | 0 | 1 | 0 | 0 | 2 | 3 | **no** | LegacyWorldPort, V4EnemyPlayModeTests, V4StagePlayModeTests |
| `V3/V3FloorPowerupKind.cs` | V3FloorPowerupKind | 1 | 1 | 0 | 0 | 1 | 3 | yes | PowerupPresenter, LegacyBuildMapping, V4BuildM9Tests |
| `V3/V3RunBootstrap.cs` | V3RunBootstrap | 0 | 0 | 1 | 1 | 1 | 3 | **no** | TraceBoot, V4SceneSetup, V4RunPlayModeTests |
| `V3/V3RunItemInventory.cs` | V3RunItemInventory | 0 | 0 | 0 | 0 | 3 | 3 | **no** | V4BuildM9Tests, V4ProgressionM9Tests, V4HudPlayModeTests |
| `VFX/EnemyBurnFlash.cs` | EnemyBurnFlash | 1 | 0 | 0 | 0 | 2 | 3 | yes | EnemyViewPresenter, V3EnemyViewSink, V3EnemyViewSink |
| `Weapons/ProjectileWorld.cs` | ProjectileWorld | 0 | 0 | 0 | 1 | 2 | 3 | **no** | V4SceneSetup, V4HudPlayModeTests, V4RunPlayModeTests |
| `World/StageBuilder.cs` | StageBuilder | 2 | 1 | 0 | 0 | 0 | 3 | yes | RunBootstrapSequence, V4Game, LegacyWorldPort |
| `Balance/EnemyTierLogic.cs` | EnemyTierLogic | 1 | 0 | 0 | 1 | 0 | 2 | yes | EnemyWorldContracts, V3ConfigMigrator |
| `Balance/SaltwindPressureBeatLogic.cs` | SaltwindPressureBeatLogic | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V3ConfigMigrator, V4SpawnM3Tests |
| `Combat/CombatDebugOverlays.cs` | CombatDebugOverlays | 1 | 0 | 0 | 0 | 1 | 2 | yes | WeaponAimOverlay, V4WeaponVfxM6Tests |
| `Combat/DamageSystem.cs` | DamageSystem | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4EnemyTickM8Tests, V4WeaponM5Tests |
| `Combat/HellfirePatchWorld.cs` | HellfirePatchWorld | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4EnemyTickM8Tests, V4EnemyPlayModeTests |
| `Combat/RunCombatTelemetry.cs` | RunCombatTelemetry | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4SwitchoverM14Tests, V4HudPlayModeTests |
| `Combat/SaltwindCombatEncounterBudget.cs` | SaltwindCombatEncounterBudget | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4EnemyTickM8Tests, V4EnemyLockstepPlayModeTests |
| `Data/LevelUpSettings.cs` | LevelUpSettings | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4BuildConfigGenerator, V4ProgressionM9Tests |
| `Data/WeaponDefinition.cs` | WeaponDefinition | 0 | 0 | 0 | 1 | 1 | 2 | yes | V3ConfigMigrator, V4ConfigM1Tests |
| `Data/WeaponUpgradeAxisConfig.cs` | WeaponUpgradeAxisConfig | 1 | 0 | 0 | 1 | 0 | 2 | yes | OfferCards, V3ConfigMigrator |
| `Enemies/EnemyViewPool.cs` | EnemyViewPool | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V3EnemyViewSink, V3EnemyViewSink |
| `Enemies/SpawnModifierStack.cs` | SpawnModifierStack | 2 | 0 | 0 | 0 | 0 | 2 | yes | ScriptedPhases, SpawnController |
| `Enemies/SpawnPlacementService.cs` | SpawnPlacementService | 1 | 1 | 0 | 0 | 0 | 2 | yes | EnemySpecialBehaviors, LegacyPorts |
| `POI/SalvageChestWaterFloat.cs` | SalvageChestWaterFloat | 1 | 0 | 0 | 0 | 1 | 2 | yes | PoiMarkerPresenter, V4StagePlayModeTests |
| `Player/ShipBoostController.cs` | ShipBoostController | 0 | 2 | 0 | 0 | 0 | 2 | yes | LegacyPorts, LegacyWorldPort |
| `Save/RunSnapshot.cs` | RunSnapshotData | 0 | 0 | 1 | 0 | 1 | 2 | **no** | RunSection, V4RunPlayModeTests |
| `UI/LevelOfferGenerator.cs` | LevelOfferGenerator | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4BuildM9Tests, V4ProgressionM9Tests |
| `UI/RunHud.cs` | RunHud, WeaponLevelStripHud | 1 | 0 | 0 | 1 | 0 | 2 | **no** | RunCanvasSetup, V4SceneSetup |
| `UI/RunHudUiFactory.cs` | RunHudTheme, RunHudUiFactory | 2 | 0 | 0 | 0 | 0 | 2 | yes | AuthoredHudPresenter, RunCanvasSetup |
| `V3/BarnacleKingBossAttackDirector.cs` | BarnacleKingBossAttackDirector | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4BossConfigGenerator, V4StagePlayModeTests |
| `V3/V3CaptainActiveController.cs` | V3CaptainActiveController | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4ProgressionM9Tests, V4HudPlayModeTests |
| `V3/V3CaptainCatalog.cs` | V3CaptainCatalog | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4BuildConfigGenerator, V4BuildM9Tests |
| `V3/V3CaptainController.cs` | V3CaptainController | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4ProgressionM9Tests, V4HudPlayModeTests |
| `V3/V3CaptainSelection.cs` | V3CaptainSelection | 0 | 1 | 1 | 0 | 0 | 2 | yes | LegacyWorldPort, RunTraceRecorder |
| `V3/V3ContentUnlocks.cs` | V3ContentUnlocks | 0 | 1 | 0 | 0 | 1 | 2 | yes | LegacyWorldPort, V4ProgressionM9Tests |
| `V3/V3DiscoverySystem.cs` | BeaconRevealState, V3DiscoverySystem | 0 | 0 | 0 | 0 | 2 | 2 | **no** | V4HudM6Tests, V4StagePlayModeTests |
| `V3/V3InteractionController.cs` | V3InteractionController | 0 | 0 | 1 | 0 | 1 | 2 | **no** | RunTraceRecorder, V4StagePlayModeTests |
| `V3/V3LogbookCatalog.cs` | V3LogbookCatalog | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4BuildConfigGenerator, V4BuildM9Tests |
| `V3/V3MinibossArena.cs` | V3MinibossArena | 0 | 0 | 1 | 0 | 1 | 2 | **no** | RunTraceRecorder, V4RunPlayModeTests |
| `V3/V3MinimapUiSprites.cs` | V3MinimapUiSprites | 2 | 0 | 0 | 0 | 0 | 2 | yes | BeaconGuidePresenter, MinimapPresenter |
| `V3/V3PlayBoundsWallVisual.cs` | V3PlayBoundsWallVisual | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `V3/V3PlayerInfoHudArtSettings.cs` | V3PlayerInfoHudArtSettings | 2 | 0 | 0 | 0 | 0 | 2 | yes | AuthoredHudPresenter, RunCanvasSetup |
| `V3/V3RunContext.cs` | V3RunContext | 0 | 1 | 0 | 0 | 1 | 2 | **no** | LegacyWorldPort, V4ProgressionM9Tests |
| `V3/V3RunHudIconResolver.cs` | V3RunHudIconResolver | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4IconCatalogBuilder, V4HudM6Tests |
| `V3/V3RunItemEffects.cs` | V3RunItemEffects | 0 | 1 | 0 | 0 | 1 | 2 | **no** | LegacyBuildMapping, V4ProgressionM9Tests |
| `V3/V3RunResultsScreen.cs` | V3RunResultsScreen | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `V3/V3SaltwindStormRingVisual.cs` | V3SaltwindStormRingVisual | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `VFX/CombatVfxController.cs` | CombatVfxController | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `VFX/JuiceVfxBroker.cs` | JuiceVfxBroker | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `VFX/PlayerHealthBarBinder.cs` | PlayerHealthBarBinder | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `VFX/PlayerJuiceController.cs` | PlayerJuiceController | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `VFX/WeaponJuiceController.cs` | WeaponJuiceController | 0 | 0 | 0 | 1 | 1 | 2 | **no** | V4SceneSetup, V4HudPlayModeTests |
| `Balance/SaltwindMarksUnlockLogic.cs` | SaltwindMarksUnlockLogic | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4RewardsM12Tests |
| `Balance/SaltwindStageSettlementLogic.cs` | SaltwindStageSettlementLogic | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4RewardsM12Tests |
| `Balance/StageBalanceProfileHost.cs` | StageBalanceProfileHost | 0 | 0 | 0 | 1 | 0 | 1 | yes | V4SceneSetup |
| `Combat/CombatTelemetryDriver.cs` | CombatTelemetryDriver | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4EnemyPlayModeTests |
| `Combat/DamageProcKind.cs` | DamageProcKind | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4WeaponM5Tests |
| `Combat/PlayerHullSync.cs` | PlayerHullSync | 1 | 0 | 0 | 0 | 0 | 1 | yes | BuildModule |
| `Combat/WeaponDebugPalette.cs` | WeaponDebugPalette | 1 | 0 | 0 | 0 | 0 | 1 | yes | WeaponAimOverlay |
| `Combat/WeaponTargetingVisualizer.cs` | WeaponTargetingVisualizer | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4HudPlayModeTests |
| `Enemies/RunCombatSignals.cs` | RunCombatSignals | 1 | 0 | 0 | 0 | 0 | 1 | yes | EnemyCombatServices |
| `POI/SalvageChestTreasureGlow.cs` | SalvageChestTreasureGlow | 1 | 0 | 0 | 0 | 0 | 1 | yes | PoiMarkerPresenter |
| `Pickups/FlyingHealthBillboard.cs` | FlyingHealthBillboard | 1 | 0 | 0 | 0 | 0 | 1 | yes | PickupPresenter |
| `Platform/QualityProfile.cs` | QualityProfile | 1 | 0 | 0 | 0 | 0 | 1 | yes | SlotPool |
| `Player/BoostCameraZoom.cs` | BoostCameraZoom | 0 | 1 | 0 | 0 | 0 | 1 | yes | LegacyWorldPort |
| `Player/PlayerShipVisualBootstrap.cs` | PlayerShipVisualBootstrap | 0 | 1 | 0 | 0 | 0 | 1 | yes | LegacyWorldPort |
| `UI/LevelUpUiFactory.cs` | LevelUpUiFactory | 1 | 0 | 0 | 0 | 0 | 1 | yes | LevelUpPresenter |
| `UI/MenuScreens.cs` | MainMenuController, RunEndScreen | 0 | 0 | 0 | 1 | 0 | 1 | **no** | V4SceneSetup |
| `UI/StatDisplay.cs` | StatDisplay | 1 | 0 | 0 | 0 | 0 | 1 | yes | OfferCards |
| `UI/UiCanvasConfigurator.cs` | UiCanvasConfigurator | 1 | 0 | 0 | 0 | 0 | 1 | yes | RunCanvasSetup |
| `V3/MultiShip/V3RunSession.cs` | V3RunSession | 0 | 1 | 0 | 0 | 0 | 1 | **no** | LegacyStageProfilePort |
| `V3/V3AuthoredBottomLoadoutHud.cs` | V3AuthoredBottomLoadoutHud | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4HudPlayModeTests |
| `V3/V3BootLoader.cs` | V3BootLoader | 0 | 0 | 0 | 1 | 0 | 1 | **no** | V4SceneSetup |
| `V3/V3BossBeaconGuide.cs` | V3BossBeaconGuide | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4HudPlayModeTests |
| `V3/V3BossSummonHotkey.cs` | V3BossSummonHotkey | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4StagePlayModeTests |
| `V3/V3CharacterCardArtSettings.cs` | V3CharacterCardArtSettings | 1 | 0 | 0 | 0 | 0 | 1 | yes | CaptainSelectView |
| `V3/V3HomeSeed.cs` | V3HomeSeed | 0 | 1 | 0 | 0 | 0 | 1 | yes | LegacyWorldPort |
| `V3/V3MainMenuCharacterSelect.cs` | V3MainMenuCharacterSelect | 0 | 0 | 0 | 1 | 0 | 1 | **no** | V4SceneSetup |
| `V3/V3MainMenuController.cs` | V3MainMenuController | 0 | 0 | 0 | 1 | 0 | 1 | **no** | V4SceneSetup |
| `V3/V3MegabonkQuestDisplay.cs` | V3MegabonkQuestDisplay | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4HudM6Tests |
| `V3/V3MinimapArtSettings.cs` | V3MinimapArtSettings | 1 | 0 | 0 | 0 | 0 | 1 | yes | MinimapPresenter |
| `V3/V3MinimapHud.cs` | V3MinimapHud | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4HudPlayModeTests |
| `V3/V3MinimapHudSettings.cs` | V3MinimapHudSettings | 1 | 0 | 0 | 0 | 0 | 1 | yes | MinimapPresenter |
| `V3/V3MinimapMarkerGraphics.cs` | V3MinimapMarkerGraphics | 1 | 0 | 0 | 0 | 0 | 1 | yes | MinimapPresenter |
| `V3/V3ObjectiveHud.cs` | V3ObjectiveHud | 1 | 0 | 0 | 0 | 0 | 1 | **no** | RunCanvasSetup |
| `V3/V3OnboardingDirector.cs` | V3OnboardingDirector | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4NarrativePlayModeTests |
| `V3/V3OvertimeDirector.cs` | V3OvertimeDirector | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4RunPlayModeTests |
| `V3/V3PlayBoundsWallSegment.cs` | V3PlayBoundsWallSegment | 1 | 0 | 0 | 0 | 0 | 1 | yes | BoundsWallPresenter |
| `V3/V3PlayerInfoHud.cs` | V3PlayerInfoHud | 1 | 0 | 0 | 0 | 0 | 1 | **no** | RunCanvasSetup |
| `V3/V3PlaytestFlags.cs` | V3PlaytestFlags | 0 | 1 | 0 | 0 | 0 | 1 | yes | LegacyWorldPort |
| `V3/V3PressureBeatDirector.cs` | V3PressureBeatDirector | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4RunPlayModeTests |
| `V3/V3RunCaptainActiveHud.cs` | V3RunCaptainActiveHud | 1 | 0 | 0 | 0 | 0 | 1 | **no** | RunCanvasSetup |
| `V3/V3RunLoadoutHud.cs` | V3RunLoadoutHud | 1 | 0 | 0 | 0 | 0 | 1 | **no** | RunCanvasSetup |
| `V3/V3RunStatsHud.cs` | V3RunStatsHud | 1 | 0 | 0 | 0 | 0 | 1 | **no** | RunCanvasSetup |
| `V3/V3RunTimerHud.cs` | V3RunTimerHud | 1 | 0 | 0 | 0 | 0 | 1 | **no** | RunCanvasSetup |
| `V3/V3SpawnAnchorValidator.cs` | V3SpawnAnchorValidator | 0 | 1 | 0 | 0 | 0 | 1 | yes | LegacyWorldPort |
| `V3/V3SpawnEdgeIndicator.cs` | V3SpawnEdgeIndicator | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4HudPlayModeTests |
| `V3/V3SpawnWarningHud.cs` | V3SpawnWarningHud | 0 | 0 | 0 | 0 | 1 | 1 | **no** | V4HudPlayModeTests |
| `V3/V3StormEdgeVisual.cs` | V3StormEdgeVisual | 0 | 1 | 0 | 0 | 0 | 1 | **no** | LegacyWorldPort |
| `VFX/LongNineBeamVisual.cs` | LongNineBeamVisual | 1 | 0 | 0 | 0 | 0 | 1 | yes | WeaponVfxPresenter |
| `World/DayNightDirector.cs` | DayNightDirector | 0 | 1 | 0 | 0 | 0 | 1 | yes | LegacyWorldPort |
| `World/EnvironmentHazardVisual.cs` | EnvironmentHazardVisual | 0 | 1 | 0 | 0 | 0 | 1 | **no** | LegacyWorldPort |
| `World/EnvironmentalHazardsDisabled.cs` | EnvironmentalHazardsDisabled | 0 | 1 | 0 | 0 | 0 | 1 | **no** | LegacyWorldPort |
| `World/OceanCurrentZone.cs` | OceanCurrentZone | 0 | 1 | 0 | 0 | 0 | 1 | **no** | LegacyWorldPort |
| `World/ProceduralIslandSettings.cs` | ProceduralIslandSettings | 0 | 1 | 0 | 0 | 0 | 1 | yes | LegacyWorldPort |
| `World/WhirlpoolHazard.cs` | WhirlpoolHazard | 0 | 1 | 0 | 0 | 0 | 1 | **no** | LegacyWorldPort |
| `World/WindGustDirector.cs` | WindGustDirector | 0 | 1 | 0 | 0 | 0 | 1 | **no** | LegacyWorldPort |

Total Scripts files referenced by V4 (any): 168; by V4 runtime assemblies (prod+legacy+dev): 104; by prod only (non-Legacy runtime/presentation): 64.

### 1b. Per V4 file: HighSeas files it references

| V4 file | category | # Scripts files | Scripts files |
|---|---|---|---|
| `Tests/V4/EditMode/V3EnemyViewSink.cs` | test | 9 | Core/GameEnums.cs, Data/DemoJuiceSettings.cs, Data/EnemyDefinition.cs, Enemies/EnemyShipWakeJuice.cs, Enemies/EnemyViewAnimation.cs, Enemies/EnemyViewPool.cs, Enemies/EnemyVisualFallbackResolver.cs, VFX/EnemyBurnFlash.cs, VFX/WorldHealthBar.cs |
| `Tests/V4/EditMode/V3RegistryFromConfig.cs` | test | 2 | Combat/EnemyCombatProfile.cs, Combat/EnemyCombatProfileRegistry.cs |
| `Tests/V4/EditMode/V4AdaptiveM11Tests.cs` | test | 1 | Core/GameEnums.cs |
| `Tests/V4/EditMode/V4BossM7Tests.cs` | test | 1 | Balance/SaltwindFinaleLogic.cs |
| `Tests/V4/EditMode/V4BuildM9Tests.cs` | test | 16 | Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileContext.cs, Core/GameConstants.cs, Core/GameEnums.cs, Core/PlayerStats.cs, Core/RunManager.cs, Data/DemoPacingSettings.cs, UI/LevelOfferGenerator.cs, V3/V3CaptainCatalog.cs, V3/V3CaptainId.cs, V3/V3CaptainProfile.cs, V3/V3FloorPowerupKind.cs, V3/V3FloorPowerupWorld.cs, V3/V3ItemCatalog.cs, V3/V3LogbookCatalog.cs, V3/V3RunItemInventory.cs |
| `Tests/V4/EditMode/V4ConfigM1Tests.cs` | test | 11 | Balance/SaltwindAmbientCompositionLogic.cs, Balance/StageBalanceProfile.cs, Combat/CombatExperimentProfile.cs, Combat/CombatSpecialistEnemyCatalog.cs, Combat/CombatWeaponCatalog.cs, Combat/EnemyCombatProfileRegistry.cs, Core/GameConstants.cs, Data/DemoPacingSettings.cs, Data/WeaponDefinition.cs, Enemies/EncounterSpawnSchedule.cs, Enemies/SpawnProfile.cs |
| `Tests/V4/EditMode/V4DifficultyM10Tests.cs` | test | 1 | Core/GameConstants.cs |
| `Tests/V4/EditMode/V4EnemyM2Tests.cs` | test | 8 | Combat/CombatExperimentProfile.cs, Combat/CombatSpecialistEnemyCatalog.cs, Combat/CombatWeaponCatalog.cs, Combat/EnemyCombatProfile.cs, Combat/EnemyCombatProfileRegistry.cs, Core/GameDatabaseLoader.cs, Data/EnemyDefinition.cs, Data/GameDatabase.cs |
| `Tests/V4/EditMode/V4EnemyTickM8Tests.cs` | test | 24 | Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileContext.cs, Combat/CombatExperimentProfile.cs, Combat/CombatSpecialistEnemyCatalog.cs, Combat/CombatWeaponCatalog.cs, Combat/DamageReceiver.cs, Combat/DamageSystem.cs, Combat/EnemyCombatProfile.cs, Combat/EnemyCombatProfileRegistry.cs, Combat/HellfirePatchWorld.cs, Combat/HostileProjectileWorld.cs, Combat/SaltwindCombatAgentStateStore.cs, Combat/SaltwindCombatEncounterBudget.cs, Combat/SaltwindCombatFeature.cs, Combat/SaltwindHazardTelegraph.cs, Combat/StatusController.cs, Core/GameEnums.cs, Core/RunManager.cs, Data/EnemyDefinition.cs, Data/GameDatabase.cs, Enemies/EnemySpawnRuntimeParams.cs, Enemies/EnemyWorld.cs, Enemies/SpawnProfile.cs, Player/PlayerShipController.cs |
| `Tests/V4/EditMode/V4EnemyWorldM8Tests.cs` | test | 12 | Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileContext.cs, Combat/CombatExperimentProfile.cs, Combat/CombatSpecialistEnemyCatalog.cs, Combat/CombatWeaponCatalog.cs, Combat/SaltwindCombatAgentStateStore.cs, Combat/SaltwindCombatFeature.cs, Data/EnemyDefinition.cs, Data/GameDatabase.cs, Enemies/EnemySpawnRuntimeParams.cs, Enemies/EnemyWorld.cs, Enemies/SpawnProfile.cs |
| `Tests/V4/EditMode/V4HudM6Tests.cs` | test | 10 | Balance/SaltwindRunResultsLogic.cs, Combat/CombatExperimentProfile.cs, Combat/CombatWeaponCatalog.cs, Combat/WeaponEvolutionCatalog.cs, Core/GameEnums.cs, V3/V3AuthoredGameplayHud.cs, V3/V3DiscoverySystem.cs, V3/V3ItemCatalog.cs, V3/V3MegabonkQuestDisplay.cs, V3/V3RunHudIconResolver.cs |
| `Tests/V4/EditMode/V4M9GateFixTests.cs` | test | 3 | Core/GameEnums.cs, Core/PlayerStats.cs, Core/RunManager.cs |
| `Tests/V4/EditMode/V4PickupM9Tests.cs` | test | 11 | Balance/SaltwindStageEconomyLogic.cs, Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileContext.cs, Combat/DamageReceiver.cs, Core/GameConstants.cs, Core/GameEnums.cs, Core/RunManager.cs, Core/RunRng.cs, Data/DemoJuiceSettings.cs, Pickups/PickupWorld.cs, Player/PlayerShipController.cs |
| `Tests/V4/EditMode/V4PoolM4Tests.cs` | test | 1 | Core/GameEnums.cs |
| `Tests/V4/EditMode/V4ProgressionM9Tests.cs` | test | 29 | Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileContext.cs, Combat/CombatExperimentProfile.cs, Combat/CombatWeaponCatalog.cs, Combat/DamageReceiver.cs, Combat/WeaponEvolutionCatalog.cs, Core/GameConstants.cs, Core/GameEnums.cs, Core/PlayerStats.cs, Core/RunManager.cs, Core/RunRng.cs, Data/DemoPacingSettings.cs, Data/GameDatabase.cs, Data/LevelUpSettings.cs, Enemies/EncounterSpawner.cs, Player/PlayerShipController.cs, UI/LevelOffer.cs, UI/LevelOfferGenerator.cs, UI/LevelSystem.cs, V3/V3CaptainActiveController.cs, V3/V3CaptainController.cs, V3/V3CaptainId.cs, V3/V3CaptainProfile.cs, V3/V3ContentUnlocks.cs, V3/V3FloorPowerupWorld.cs, V3/V3RunContext.cs, V3/V3RunItemEffects.cs, V3/V3RunItemInventory.cs, Weapons/WeaponController.cs |
| `Tests/V4/EditMode/V4RewardsM12Tests.cs` | test | 4 | Balance/SaltwindMarksUnlockLogic.cs, Balance/SaltwindStageSettlementLogic.cs, Balance/StageBalanceProfile.cs, Save/SaveService.cs |
| `Tests/V4/EditMode/V4SpawnM3Tests.cs` | test | 10 | Balance/SaltwindAmbientCompositionLogic.cs, Balance/SaltwindFinaleLogic.cs, Balance/SaltwindPressureBeatLogic.cs, Balance/StageBalanceProfile.cs, Core/RunRng.cs, Data/DemoPacingSettings.cs, Data/EnemyDefinition.cs, Enemies/EncounterSpawner.cs, Enemies/SpawnProfile.cs, Enemies/SpawnProfileModules.cs |
| `Tests/V4/EditMode/V4StageM7Tests.cs` | test | 4 | Balance/SaltwindStageEconomyLogic.cs, Balance/StageBalanceProfile.cs, Core/GameEnums.cs, World/StageGenerator.cs |
| `Tests/V4/EditMode/V4SwitchoverM14Tests.cs` | test | 8 | Balance/SaltwindStageEconomyLogic.cs, Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileContext.cs, Combat/RunCombatTelemetry.cs, Core/GameEnums.cs, Core/RunRng.cs, Enemies/EncounterSpawnSchedule.cs, Enemies/SpawnProfile.cs |
| `Tests/V4/EditMode/V4WeaponM5Tests.cs` | test | 5 | Combat/DamageProcKind.cs, Combat/DamageSystem.cs, Combat/WeaponEvolutionCatalog.cs, Core/PlayerStats.cs, Core/RunRng.cs |
| `Tests/V4/EditMode/V4WeaponVfxM6Tests.cs` | test | 3 | Combat/CombatDebugOverlays.cs, Combat/CombatExperimentProfile.cs, Data/DemoJuiceSettings.cs |
| `Tests/V4/EditMode/V4WorldViewsM14Tests.cs` | test | 4 | Core/GameDatabaseLoader.cs, Core/GameEnums.cs, Data/DemoJuiceSettings.cs, Data/EnemyDefinition.cs |
| `Tests/V4/PlayMode/V3EnemyViewSink.cs` | test | 9 | Core/GameEnums.cs, Data/DemoJuiceSettings.cs, Data/EnemyDefinition.cs, Enemies/EnemyShipWakeJuice.cs, Enemies/EnemyViewAnimation.cs, Enemies/EnemyViewPool.cs, Enemies/EnemyVisualFallbackResolver.cs, VFX/EnemyBurnFlash.cs, VFX/WorldHealthBar.cs |
| `Tests/V4/PlayMode/V3RegistryFromConfig.cs` | test | 2 | Combat/EnemyCombatProfile.cs, Combat/EnemyCombatProfileRegistry.cs |
| `Tests/V4/PlayMode/V4AdaptivePlayModeTests.cs` | test | 7 | Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Core/GameEnums.cs, Core/RunManager.cs, Encounters/RunEncounterTracker.cs, Player/PlayerShipController.cs |
| `Tests/V4/PlayMode/V4EnemyLockstepPlayModeTests.cs` | test | 18 | Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Combat/HostileProjectileWorld.cs, Combat/SaltwindCombatAgentStateStore.cs, Combat/SaltwindCombatEncounterBudget.cs, Combat/SaltwindCombatFeature.cs, Combat/SaltwindHazardTelegraph.cs, Combat/StatusController.cs, Core/GameEnums.cs, Core/RunManager.cs, Data/EnemyDefinition.cs, Encounters/RunEncounterTracker.cs, Enemies/EnemySpawnRuntimeParams.cs, Enemies/EnemyWorld.cs, Enemies/SpawnProfile.cs, Player/PlayerShipController.cs, World/IslandObstacles.cs |
| `Tests/V4/PlayMode/V4EnemyPlayModeTests.cs` | test | 14 | Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Combat/CombatTelemetryDriver.cs, Combat/HellfirePatchWorld.cs, Combat/HostileProjectileWorld.cs, Combat/StatusController.cs, Combat/WeaponEvolutionCatalog.cs, Core/GameEnums.cs, Core/RunManager.cs, Encounters/RunEncounterTracker.cs, Enemies/EnemyWorld.cs, Player/PlayerShipController.cs, V3/V3ExplosiveBarrelWorld.cs |
| `Tests/V4/PlayMode/V4HudPlayModeTests.cs` | test | 33 | Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Combat/RunCombatTelemetry.cs, Combat/WeaponTargetingVisualizer.cs, Core/GameEnums.cs, Core/RunManager.cs, Encounters/RunEncounterTracker.cs, Enemies/EncounterSpawner.cs, Meta/MetaSystems.cs, Pickups/PickupWorld.cs, Player/PlayerShipController.cs, UI/LevelSystem.cs, V3/V3AuthoredBottomLoadoutHud.cs, V3/V3AuthoredGameplayHud.cs, V3/V3BossBeaconGuide.cs, V3/V3CaptainActiveController.cs, V3/V3CaptainController.cs, V3/V3FloorPowerupWorld.cs, V3/V3MinimapHud.cs, V3/V3PlayBoundsWallVisual.cs, V3/V3RunItemInventory.cs, V3/V3RunResultsScreen.cs, V3/V3SaltwindStormRingVisual.cs, V3/V3SpawnEdgeIndicator.cs, V3/V3SpawnWarningHud.cs, VFX/CombatVfxController.cs, VFX/JuiceVfxBroker.cs, VFX/PlayerHealthBarBinder.cs, VFX/PlayerJuiceController.cs, VFX/WeaponJuiceController.cs, Weapons/ProjectileWorld.cs, Weapons/WeaponController.cs |
| `Tests/V4/PlayMode/V4NarrativePlayModeTests.cs` | test | 7 | Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Core/GameEnums.cs, Core/RunManager.cs, Encounters/RunEncounterTracker.cs, V3/V3OnboardingDirector.cs |
| `Tests/V4/PlayMode/V4RunPlayModeTests.cs` | test | 20 | Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Core/AppRoot.cs, Core/GameConstants.cs, Core/GameEnums.cs, Core/RunManager.cs, Encounters/RunEncounterTracker.cs, Enemies/EncounterSpawner.cs, POI/POIManager.cs, Player/PlayerShipController.cs, Save/RunSnapshot.cs, Save/SaveService.cs, UI/LevelUpModal.cs, V3/V3MinibossArena.cs, V3/V3OvertimeDirector.cs, V3/V3PressureBeatDirector.cs, V3/V3RunBootstrap.cs, Weapons/ProjectileWorld.cs, Weapons/WeaponController.cs |
| `Tests/V4/PlayMode/V4StagePlayModeTests.cs` | test | 15 | Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Core/GameEnums.cs, Core/RunManager.cs, Encounters/RunEncounterTracker.cs, Enemies/EncounterSpawner.cs, POI/POIManager.cs, POI/SalvageChestWaterFloat.cs, Player/PlayerShipController.cs, V3/BarnacleKingBossAttackDirector.cs, V3/V3BossSummonHotkey.cs, V3/V3DiscoverySystem.cs, V3/V3ExplosiveBarrelWorld.cs, V3/V3InteractionController.cs |
| `V4/DevTools/Console/BuildSection.cs` | devtools | 2 | Core/RunManager.cs, Player/PlayerShipController.cs |
| `V4/DevTools/Console/ConfigSection.cs` | devtools | 1 | Core/RunManager.cs |
| `V4/DevTools/Console/ModuleLabSection.cs` | devtools | 1 | Player/PlayerShipController.cs |
| `V4/DevTools/Console/PoolsSection.cs` | devtools | 1 | Player/PlayerShipController.cs |
| `V4/DevTools/Console/RunSection.cs` | devtools | 3 | Core/RunManager.cs, Player/PlayerShipController.cs, Save/RunSnapshot.cs |
| `V4/DevTools/Console/StageSection.cs` | devtools | 3 | Core/GameEnums.cs, Core/RunManager.cs, Player/PlayerShipController.cs |
| `V4/DevTools/Console/TimelineSection.cs` | devtools | 1 | Core/RunManager.cs |
| `V4/DevTools/Console/TuningConsole.cs` | devtools | 2 | Core/GameEnums.cs, Core/RunManager.cs |
| `V4/DevTools/Trace/HumanPilot.cs` | devtools | 6 | Core/GameEnums.cs, Core/RunManager.cs, Platform/MoveInput.cs, Player/PlayerShipController.cs, World/IslandObstacles.cs, World/PlayBounds.cs |
| `V4/DevTools/Trace/PlayerPilot.cs` | devtools | 7 | Core/GameEnums.cs, Core/RunManager.cs, Pickups/PickupWorld.cs, Platform/MoveInput.cs, Player/PlayerShipController.cs, World/IslandObstacles.cs, World/PlayBounds.cs |
| `V4/DevTools/Trace/RunTraceRecorder.cs` | devtools | 13 | Balance/SaltwindRunSettlement.cs, Balance/StageBalanceProfileContext.cs, Core/GameEnums.cs, Core/RunManager.cs, Meta/MetaSystems.cs, POI/POIManager.cs, Player/PlayerShipController.cs, UI/LevelOffer.cs, UI/LevelSystem.cs, UI/LevelUpModal.cs, V3/V3CaptainSelection.cs, V3/V3InteractionController.cs, V3/V3MinibossArena.cs |
| `V4/DevTools/Trace/ScriptedPilot.cs` | devtools | 2 | Core/RunManager.cs, Platform/MoveInput.cs |
| `V4/DevTools/Trace/TraceBoot.cs` | devtools | 5 | Core/AppRoot.cs, Platform/MoveInput.cs, Player/PlayerShipController.cs, Save/SaveService.cs, V3/V3RunBootstrap.cs |
| `V4/Editor/V3ConfigMapping.cs` | editor | 3 | Balance/StageBalanceProfile.cs, Enemies/EncounterSpawnSchedule.cs, Enemies/SpawnProfile.cs |
| `V4/Editor/V3ConfigMigrator.cs` | editor | 20 | Balance/EnemyTierLogic.cs, Balance/SaltwindAmbientCompositionLogic.cs, Balance/SaltwindPressureBeatLogic.cs, Balance/StageBalanceProfile.cs, Combat/CombatExperimentProfile.cs, Combat/CombatSpecialistEnemyCatalog.cs, Combat/CombatWeaponCatalog.cs, Combat/EnemyCombatProfileRegistry.cs, Combat/WeaponEvolutionCatalog.cs, Core/GameConstants.cs, Core/GameDatabaseLoader.cs, Core/GameEnums.cs, Data/DemoJuiceSettings.cs, Data/DemoPacingSettings.cs, Data/EnemyDefinition.cs, Data/GameDatabase.cs, Data/WeaponDefinition.cs, Data/WeaponUpgradeAxisConfig.cs, Enemies/EncounterSpawnSchedule.cs, Enemies/SpawnProfile.cs |
| `V4/Editor/V4BossConfigGenerator.cs` | editor | 2 | Balance/SaltwindFinaleLogic.cs, V3/BarnacleKingBossAttackDirector.cs |
| `V4/Editor/V4BuildConfigGenerator.cs` | editor | 11 | Balance/StageBalanceProfile.cs, Core/GameConstants.cs, Data/DemoPacingSettings.cs, Data/GameDatabase.cs, Data/LevelUpSettings.cs, V3/V3CaptainCatalog.cs, V3/V3CaptainId.cs, V3/V3CaptainProfile.cs, V3/V3FloorPowerupWorld.cs, V3/V3ItemCatalog.cs, V3/V3LogbookCatalog.cs |
| `V4/Editor/V4IconCatalogBuilder.cs` | editor | 2 | V3/V3ItemCatalog.cs, V3/V3RunHudIconResolver.cs |
| `V4/Editor/V4RewardConfigGenerator.cs` | editor | 1 | Balance/StageBalanceProfile.cs |
| `V4/Editor/V4SceneSetup.cs` | editor | 29 | Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileHost.cs, Combat/HostileProjectileWorld.cs, Combat/StatusController.cs, Enemies/EncounterSpawnSchedule.cs, Enemies/EncounterSpawner.cs, Enemies/EnemyWorld.cs, Meta/MetaSystems.cs, POI/POIManager.cs, Pickups/PickupWorld.cs, UI/LevelSystem.cs, UI/LevelUpModal.cs, UI/MenuScreens.cs, UI/RunHud.cs, V3/V3AuthoredGameplayHud.cs, V3/V3BootLoader.cs, V3/V3MainMenuCharacterSelect.cs, V3/V3MainMenuController.cs, V3/V3PlayBoundsWallVisual.cs, V3/V3RunBootstrap.cs, V3/V3RunResultsScreen.cs, V3/V3SaltwindStormRingVisual.cs, VFX/CombatVfxController.cs, VFX/JuiceVfxBroker.cs, VFX/PlayerHealthBarBinder.cs, VFX/PlayerJuiceController.cs, VFX/WeaponJuiceController.cs, Weapons/ProjectileWorld.cs, Weapons/WeaponController.cs |
| `V4/Presentation/Effects/CameraShakePresenter.cs` | prod | 1 | Core/AppRoot.cs |
| `V4/Presentation/Effects/CombatFeedbackPresenter.cs` | prod | 1 | Data/DemoJuiceSettings.cs |
| `V4/Presentation/Effects/EnemyViewPresenter.cs` | prod | 7 | Data/DemoJuiceSettings.cs, Data/EnemyDefinition.cs, Enemies/EnemyShipWakeJuice.cs, Enemies/EnemyViewAnimation.cs, Enemies/EnemyVisualFallbackResolver.cs, VFX/EnemyBurnFlash.cs, VFX/WorldHealthBar.cs |
| `V4/Presentation/Effects/HostileShotPresenter.cs` | prod | 2 | Data/DemoJuiceSettings.cs, VFX/VfxParticleUtil.cs |
| `V4/Presentation/Effects/PickupPresenter.cs` | prod | 3 | Core/GameEnums.cs, Data/DemoJuiceSettings.cs, Pickups/FlyingHealthBillboard.cs |
| `V4/Presentation/Effects/PowerupPresenter.cs` | prod | 2 | V3/V3FloorPowerupKind.cs, V3/V3PlaceholderFactory.cs |
| `V4/Presentation/Hud/AuthoredHudPresenter.cs` | prod | 3 | UI/RunHudUiFactory.cs, V3/V3CaptainId.cs, V3/V3PlayerInfoHudArtSettings.cs |
| `V4/Presentation/Hud/BeaconGuidePresenter.cs` | prod | 1 | V3/V3MinimapUiSprites.cs |
| `V4/Presentation/Hud/LevelUpPresenter.cs` | prod | 2 | UI/LevelOffer.cs, UI/LevelUpUiFactory.cs |
| `V4/Presentation/Hud/MinimapPresenter.cs` | prod | 6 | Core/GameEnums.cs, V3/V3MinimapArtSettings.cs, V3/V3MinimapHudSettings.cs, V3/V3MinimapMarkerGraphics.cs, V3/V3MinimapUiSprites.cs, World/PlayBounds.cs |
| `V4/Presentation/Hud/OfferCards.cs` | prod | 4 | Core/GameEnums.cs, Data/WeaponUpgradeAxisConfig.cs, UI/LevelOffer.cs, UI/StatDisplay.cs |
| `V4/Presentation/Hud/ResultsPresenter.cs` | prod | 3 | Balance/SaltwindRunResultsLogic.cs, Core/GameEnums.cs, V3/V3PlaceholderFactory.cs |
| `V4/Presentation/Hud/RunCanvasSetup.cs` | prod | 10 | UI/RunHud.cs, UI/RunHudUiFactory.cs, UI/UiCanvasConfigurator.cs, V3/V3ObjectiveHud.cs, V3/V3PlayerInfoHud.cs, V3/V3PlayerInfoHudArtSettings.cs, V3/V3RunCaptainActiveHud.cs, V3/V3RunLoadoutHud.cs, V3/V3RunStatsHud.cs, V3/V3RunTimerHud.cs |
| `V4/Presentation/Hud/SpawnWarningPresenter.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Presentation/Menu/CaptainSelectView.cs` | prod | 2 | V3/V3CaptainId.cs, V3/V3CharacterCardArtSettings.cs |
| `V4/Presentation/Menu/V4MainMenuController.cs` | prod | 1 | V3/V3CaptainId.cs |
| `V4/Presentation/Pooling/PoolCatalog.cs` | prod | 1 | Data/DemoJuiceSettings.cs |
| `V4/Presentation/Stage/InteractionPromptPresenter.cs` | prod | 1 | V3/V3PlaceholderFactory.cs |
| `V4/Presentation/Stage/PoiMarkerPresenter.cs` | prod | 5 | Core/GameEnums.cs, Data/DemoJuiceSettings.cs, POI/SalvageChestTreasureGlow.cs, POI/SalvageChestWaterFloat.cs, V3/V3PlaceholderFactory.cs |
| `V4/Presentation/V4PresentationRoot.cs` | prod | 1 | Data/DemoJuiceSettings.cs |
| `V4/Presentation/Weapons/FlameStreamPresenter.cs` | prod | 2 | Data/DemoJuiceSettings.cs, VFX/VfxParticleUtil.cs |
| `V4/Presentation/Weapons/ProjectileViewPresenter.cs` | prod | 2 | Data/DemoJuiceSettings.cs, VFX/VfxParticleUtil.cs |
| `V4/Presentation/Weapons/WeaponAimOverlay.cs` | prod | 2 | Combat/CombatDebugOverlays.cs, Combat/WeaponDebugPalette.cs |
| `V4/Presentation/Weapons/WeaponVfxPresenter.cs` | prod | 3 | Data/DemoJuiceSettings.cs, VFX/LongNineBeamVisual.cs, VFX/VfxParticleUtil.cs |
| `V4/Presentation/World/BoundsWallPresenter.cs` | prod | 1 | V3/V3PlayBoundsWallSegment.cs |
| `V4/Presentation/World/ShipHealthBarPresenter.cs` | prod | 2 | Data/DemoJuiceSettings.cs, VFX/WorldHealthBar.cs |
| `V4/Runtime/Composition/BuildModule.cs` | prod | 8 | Combat/DamageReceiver.cs, Combat/PlayerHullSync.cs, Core/GameEnums.cs, Core/RunManager.cs, Data/GameDatabase.cs, Player/PlayerShipController.cs, World/IslandObstacles.cs, World/PlayBounds.cs |
| `V4/Runtime/Composition/EnemyWorldModule.cs` | prod | 4 | Core/RunManager.cs, Data/GameDatabase.cs, Player/PlayerShipController.cs, World/StageGenerator.cs |
| `V4/Runtime/Composition/MenuServices.cs` | prod | 2 | Core/AppRoot.cs, Save/SaveService.cs |
| `V4/Runtime/Composition/RunBootstrapSequence.cs` | prod | 8 | Core/AppRoot.cs, Core/GameEnums.cs, Core/RunManager.cs, Data/GameDatabase.cs, Data/IslandLayoutDefinition.cs, Player/PlayerShipController.cs, World/StageBuilder.cs, World/StageGenerator.cs |
| `V4/Runtime/Composition/StageModule.cs` | prod | 2 | Player/PlayerShipController.cs, World/StageGenerator.cs |
| `V4/Runtime/Core/RunLifecycle.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Core/SlotPool.cs` | prod | 2 | Core/GameEnums.cs, Platform/QualityProfile.cs |
| `V4/Runtime/Core/V4Game.cs` | prod | 7 | Core/AppRoot.cs, Core/GameEnums.cs, Core/RunManager.cs, Data/GameDatabase.cs, Data/IslandLayoutDefinition.cs, Player/PlayerShipController.cs, World/StageBuilder.cs |
| `V4/Runtime/Enemies/EnemyCombatBehaviors.cs` | prod | 6 | Combat/DamageReceiver.cs, Combat/EnemyCombatProfile.cs, Data/EnemyDefinition.cs, Player/PlayerShipController.cs, World/IslandObstacles.cs, World/PlayBounds.cs |
| `V4/Runtime/Enemies/EnemyCombatProfiles.cs` | prod | 5 | Combat/EnemyCombatProfile.cs, Data/EnemyDefinition.cs, Enemies/EnemySpawnRuntimeParams.cs, Enemies/SpawnProfile.cs, Enemies/SpawnProfileModules.cs |
| `V4/Runtime/Enemies/EnemyCombatServices.cs` | prod | 4 | Combat/DamageReceiver.cs, Data/EnemyDefinition.cs, Enemies/RunCombatSignals.cs, Enemies/SpawnProfile.cs |
| `V4/Runtime/Enemies/EnemyPatternMovement.cs` | prod | 2 | Enemies/EnemySpawnRuntimeParams.cs, Enemies/SpawnProfileEnums.cs |
| `V4/Runtime/Enemies/EnemyReefCrabCombat.cs` | prod | 3 | Combat/DamageReceiver.cs, Combat/EnemyCombatProfile.cs, Data/EnemyDefinition.cs |
| `V4/Runtime/Enemies/EnemySimulation.Tick.cs` | prod | 5 | Combat/DamageReceiver.cs, Core/GameConstants.cs, Data/EnemyDefinition.cs, Player/PlayerShipController.cs, World/IslandObstacles.cs |
| `V4/Runtime/Enemies/EnemySimulation.cs` | prod | 6 | Core/GameConstants.cs, Data/EnemyDefinition.cs, Enemies/EnemyWorld.cs, Enemies/SpawnProfile.cs, World/IslandObstacles.cs, World/PlayBounds.cs |
| `V4/Runtime/Enemies/EnemySimulationContracts.cs` | prod | 6 | Combat/EnemyCombatProfile.cs, Core/GameEnums.cs, Data/EnemyDefinition.cs, Enemies/EnemySpawnRuntimeParams.cs, Enemies/SpawnProfile.cs, Enemies/SpawnProfileEnums.cs |
| `V4/Runtime/Enemies/EnemySpecialBehaviors.cs` | prod | 7 | Combat/DamageReceiver.cs, Data/EnemyDefinition.cs, Enemies/EnemySpawnRuntimeParams.cs, Enemies/SpawnPlacementService.cs, Enemies/SpawnProfileEnums.cs, Enemies/SpawnProfileModules.cs, World/IslandObstacles.cs |
| `V4/Runtime/Enemies/EnemySpecialistCombat.cs` | prod | 4 | Combat/DamageReceiver.cs, Combat/EnemyCombatProfile.cs, Data/EnemyDefinition.cs, Player/PlayerShipController.cs |
| `V4/Runtime/Enemies/EnemyStatusesAndShots.cs` | prod | 2 | Core/GameEnums.cs, World/IslandObstacles.cs |
| `V4/Runtime/Enemies/EnemyUnit.cs` | prod | 4 | Combat/EnemyCombatProfile.cs, Core/GameEnums.cs, Enemies/EnemySpawnRuntimeParams.cs, Enemies/SpawnProfile.cs |
| `V4/Runtime/Enemies/EnemyViewForwarder.cs` | prod | 3 | Core/GameEnums.cs, Data/EnemyDefinition.cs, Enemies/EnemyVisualFallbackResolver.cs |
| `V4/Runtime/Enemies/EnemyWorldContracts.cs` | prod | 4 | Balance/EnemyTierLogic.cs, Core/GameEnums.cs, Data/EnemyDefinition.cs, Enemies/SpawnProfile.cs |
| `V4/Runtime/Enemies/EnemyWorldHazards.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Enemies/KillFanout.cs` | prod | 1 | Data/EnemyDefinition.cs |
| `V4/Runtime/Legacy/LegacyAdapters.cs` | legacy | 5 | Combat/CombatExperimentProfile.cs, Combat/CombatSpecialistEnemyCatalog.cs, Combat/CombatWeaponCatalog.cs, Core/GameDatabaseLoader.cs, Data/GameDatabase.cs |
| `V4/Runtime/Legacy/LegacyBuildMapping.cs` | legacy | 4 | Core/GameEnums.cs, V3/V3CaptainId.cs, V3/V3FloorPowerupKind.cs, V3/V3RunItemEffects.cs |
| `V4/Runtime/Legacy/LegacyEnemyViews.cs` | legacy | 3 | Combat/HostileProjectileWorld.cs, Combat/SaltwindHazardTelegraph.cs, Enemies/EnemyWorld.cs |
| `V4/Runtime/Legacy/LegacyPorts.cs` | legacy | 9 | Combat/DamageReceiver.cs, Core/GameConstants.cs, Core/GameEnums.cs, Core/RunManager.cs, Enemies/SpawnPlacementService.cs, Enemies/SpawnProfileModules.cs, Player/PlayerShipController.cs, Player/ShipBoostController.cs, World/PlayBounds.cs |
| `V4/Runtime/Legacy/LegacyStageProfilePort.cs` | legacy | 6 | Balance/SaltwindRunResultsLogic.cs, Balance/SaltwindStageEconomyLogic.cs, Balance/StageBalanceProfile.cs, Balance/StageBalanceProfileContext.cs, Core/AppRoot.cs, V3/MultiShip/V3RunSession.cs |
| `V4/Runtime/Legacy/LegacyWorldPort.cs` | legacy | 26 | Core/AppRoot.cs, Data/IslandLayoutDefinition.cs, Player/BoostCameraZoom.cs, Player/PlayerShipController.cs, Player/PlayerShipVisualBootstrap.cs, Player/ShipBoostController.cs, V3/V3CaptainId.cs, V3/V3CaptainSelection.cs, V3/V3ContentUnlocks.cs, V3/V3ExplosiveBarrelWorld.cs, V3/V3HomeSeed.cs, V3/V3PlaytestFlags.cs, V3/V3RunContext.cs, V3/V3SpawnAnchorValidator.cs, V3/V3StormEdgeVisual.cs, World/DayNightDirector.cs, World/EnvironmentHazardVisual.cs, World/EnvironmentalHazardsDisabled.cs, World/IslandObstacles.cs, World/OceanCurrentZone.cs, World/PlayBounds.cs, World/ProceduralIslandSettings.cs, World/StageBuilder.cs, World/StageGenerator.cs, World/WhirlpoolHazard.cs, World/WindGustDirector.cs |
| `V4/Runtime/Legacy/V4RuntimeDatabase.cs` | legacy | 2 | Data/EnemyDefinition.cs, Data/GameDatabase.cs |
| `V4/Runtime/Progression/BuildWeaponModifiers.cs` | prod | 2 | Core/GameEnums.cs, Core/PlayerStats.cs |
| `V4/Runtime/Progression/CaptainModule.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Progression/PickupModule.cs` | prod | 2 | Core/GameConstants.cs, Core/GameEnums.cs |
| `V4/Runtime/Progression/PowerupModule.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Progression/ProgressionModule.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Progression/StatResolver.cs` | prod | 3 | Core/GameConstants.cs, Core/GameEnums.cs, Core/PlayerStats.cs |
| `V4/Runtime/Rewards/CaptainLocks.cs` | prod | 1 | Save/SaveService.cs |
| `V4/Runtime/Rewards/UnlockService.cs` | prod | 1 | Save/SaveService.cs |
| `V4/Runtime/Scenes/V4BootLoader.cs` | prod | 1 | Core/AppRoot.cs |
| `V4/Runtime/Spawning/AmbientSpawner.cs` | prod | 1 | Enemies/SpawnProfile.cs |
| `V4/Runtime/Spawning/EncounterExecutor.cs` | prod | 1 | Enemies/SpawnProfile.cs |
| `V4/Runtime/Spawning/ScriptedPhases.cs` | prod | 1 | Enemies/SpawnModifierStack.cs |
| `V4/Runtime/Spawning/SpawnContracts.cs` | prod | 3 | Data/EnemyDefinition.cs, Enemies/SpawnProfile.cs, Enemies/SpawnProfileModules.cs |
| `V4/Runtime/Spawning/SpawnController.cs` | prod | 1 | Enemies/SpawnModifierStack.cs |
| `V4/Runtime/Spawning/SpawnExecutor.cs` | prod | 1 | Enemies/SpawnProfile.cs |
| `V4/Runtime/Spawning/SpawnerProfiles.cs` | prod | 3 | Data/EnemyDefinition.cs, Enemies/SpawnProfile.cs, Enemies/SpawnProfileModules.cs |
| `V4/Runtime/Stage/BeaconDiscovery.cs` | prod | 2 | Core/GameEnums.cs, World/StageGenerator.cs |
| `V4/Runtime/Stage/ChestEconomy.cs` | prod | 1 | Save/SaveService.cs |
| `V4/Runtime/Stage/InteractionModel.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Stage/StageContentModule.cs` | prod | 2 | Core/GameEnums.cs, World/StageGenerator.cs |
| `V4/Runtime/Stage/StageContracts.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Stage/StageRewards.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Stage/StageView.cs` | prod | 1 | World/StageGenerator.cs |
| `V4/Runtime/Telemetry/ResultsTelemetry.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Views/PresentationForwarders.cs` | prod | 1 | Core/GameEnums.cs |
| `V4/Runtime/Views/RunViews.cs` | prod | 1 | Core/GameEnums.cs |




## 2. Scene and asset references

### 2a. MonoBehaviour scripts placed directly in each V4 scene (m_Script GUIDs in the .unity file)

- `_Game/Scenes/V4/V4Boot.unity` (1): _Game/V4/Runtime/Scenes/V4BootLoader.cs
- `_Game/Scenes/V4/V4MainMenu.unity` (1): _Game/V4/Presentation/Menu/V4MainMenuController.cs
- `_Game/Scenes/V4/V4ModuleLab.unity` (23): Stylized Water For URP/Scripts/Runtime/Water/StylizedWaterURP.cs; pkg:com.unity.inputsystem/InputSystemUIInputModule.cs; pkg:com.unity.ugui/CanvasScaler.cs; pkg:com.unity.render-pipelines.core/Volume.cs; pkg:com.unity.ugui/HorizontalLayoutGroup.cs; pkg:com.unity.ugui/LayoutElement.cs; pkg:com.unity.render-pipelines.universal/UniversalAdditionalLightData.cs; pkg:com.unity.ugui/Button.cs; pkg:com.unity.ugui/Slider.cs; pkg:com.unity.ugui/EventSystem.cs; pkg:com.unity.render-pipelines.universal/UniversalAdditionalCameraData.cs; pkg:com.unity.ugui/GraphicRaycaster.cs; pkg:com.unity.ugui/TextMeshProUGUI.cs; pkg:com.unity.ugui/Image.cs; **_Game/Scripts/Combat/DamageReceiver.cs**; **_Game/Scripts/Core/RunManager.cs**; **_Game/Scripts/Platform/VirtualJoystick.cs**; **_Game/Scripts/Player/FollowCamera.cs**; **_Game/Scripts/Player/PlayerShipController.cs**; **_Game/Scripts/UI/LevelUpCardView.cs**; **_Game/Scripts/World/DemoIslandMarker.cs**; **_Game/Scripts/World/StageBuilder.cs**; _Game/V4/Runtime/Core/V4Game.cs
- `_Game/Scenes/V4/V4SaltwindRun.unity` (23): Stylized Water For URP/Scripts/Runtime/Water/StylizedWaterURP.cs; pkg:com.unity.inputsystem/InputSystemUIInputModule.cs; pkg:com.unity.ugui/CanvasScaler.cs; pkg:com.unity.render-pipelines.core/Volume.cs; pkg:com.unity.ugui/HorizontalLayoutGroup.cs; pkg:com.unity.ugui/LayoutElement.cs; pkg:com.unity.render-pipelines.universal/UniversalAdditionalLightData.cs; pkg:com.unity.ugui/Button.cs; pkg:com.unity.ugui/Slider.cs; pkg:com.unity.ugui/EventSystem.cs; pkg:com.unity.render-pipelines.universal/UniversalAdditionalCameraData.cs; pkg:com.unity.ugui/GraphicRaycaster.cs; pkg:com.unity.ugui/TextMeshProUGUI.cs; pkg:com.unity.ugui/Image.cs; **_Game/Scripts/Combat/DamageReceiver.cs**; **_Game/Scripts/Core/RunManager.cs**; **_Game/Scripts/Platform/VirtualJoystick.cs**; **_Game/Scripts/Player/FollowCamera.cs**; **_Game/Scripts/Player/PlayerShipController.cs**; **_Game/Scripts/UI/LevelUpCardView.cs**; **_Game/Scripts/World/DemoIslandMarker.cs**; **_Game/Scripts/World/StageBuilder.cs**; _Game/V4/Runtime/Core/V4Game.cs
- `_Game/Scenes/V4/V4TraceLauncher.unity` (0): (none)

### 2b. Full reference closure (scene -> prefabs -> assets -> scripts), project scripts only

**_Game/Scenes/V4/V4SaltwindRun.unity** - 378 assets reached, 64 distinct script GUIDs:
- Stylized Water For URP/Scripts/Runtime/Water/StylizedWaterURP.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/Combat/DamageReceiver.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/Core/RunManager.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/Data/IslandLayoutDefinition.cs  <- _Game/Data/V3/V3SaltwindIslandLayout.asset
- **[Scripts]** _Game/Scripts/Data/IslandVisualLibrary.cs  <- _Game/Resources/IslandVisualLibrary.asset
- **[Scripts]** _Game/Scripts/Platform/VirtualJoystick.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/Player/FollowCamera.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/Player/PlayerShipController.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/UI/LevelUpCardView.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/V3/V3AuthoredGameplayHud.cs  <- UI/Prefabs/Canvas.prefab
- **[Scripts]** _Game/Scripts/World/DemoIslandMarker.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- **[Scripts]** _Game/Scripts/World/IslandCollisionAuthoring.cs  <- GameObject/Islands/Island_Tropical_1.prefab, GameObject/Islands/Island_Tropical_10.prefab, GameObject/Islands/Island_Tropical_2.prefab ...(+7)
- **[Scripts]** _Game/Scripts/World/StageBuilder.cs  <- _Game/Scenes/V4/V4SaltwindRun.unity
- V4 scripts: ConfigSet.cs, BossConfig.cs, CaptainConfig.cs, DifficultyCurveConfig.cs, DirectorConfig.cs, EnemyArchetypeConfig.cs, EnemySpawnerConfig.cs, EnemyVariantConfig.cs, ExperimentConfig.cs, ItemConfig.cs, LogbookConfig.cs, NarrativeConfig.cs, OfferWeightsConfig.cs, PickupConfig.cs, PoolCatalogConfig.cs, PowerupConfig.cs, ProgressionConfig.cs, RewardConfig.cs, RunConfig.cs, StageContentConfig.cs, UnlockConfig.cs, WaveTimelineConfig.cs, WeaponConfig.cs, WeaponEvolutionConfig.cs, WeaponRulesConfig.cs, V4Game.cs

**_Game/Scenes/V4/V4ModuleLab.unity** - 378 assets reached, 64 distinct script GUIDs:
- Stylized Water For URP/Scripts/Runtime/Water/StylizedWaterURP.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/Combat/DamageReceiver.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/Core/RunManager.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/Data/IslandLayoutDefinition.cs  <- _Game/Data/V3/V3SaltwindIslandLayout.asset
- **[Scripts]** _Game/Scripts/Data/IslandVisualLibrary.cs  <- _Game/Resources/IslandVisualLibrary.asset
- **[Scripts]** _Game/Scripts/Platform/VirtualJoystick.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/Player/FollowCamera.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/Player/PlayerShipController.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/UI/LevelUpCardView.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/V3/V3AuthoredGameplayHud.cs  <- UI/Prefabs/Canvas.prefab
- **[Scripts]** _Game/Scripts/World/DemoIslandMarker.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- **[Scripts]** _Game/Scripts/World/IslandCollisionAuthoring.cs  <- GameObject/Islands/Island_Tropical_1.prefab, GameObject/Islands/Island_Tropical_10.prefab, GameObject/Islands/Island_Tropical_2.prefab ...(+7)
- **[Scripts]** _Game/Scripts/World/StageBuilder.cs  <- _Game/Scenes/V4/V4ModuleLab.unity
- V4 scripts: ConfigSet.cs, BossConfig.cs, CaptainConfig.cs, DifficultyCurveConfig.cs, DirectorConfig.cs, EnemyArchetypeConfig.cs, EnemySpawnerConfig.cs, EnemyVariantConfig.cs, ExperimentConfig.cs, ItemConfig.cs, LogbookConfig.cs, NarrativeConfig.cs, OfferWeightsConfig.cs, PickupConfig.cs, PoolCatalogConfig.cs, PowerupConfig.cs, ProgressionConfig.cs, RewardConfig.cs, RunConfig.cs, StageContentConfig.cs, UnlockConfig.cs, WaveTimelineConfig.cs, WeaponConfig.cs, WeaponEvolutionConfig.cs, WeaponRulesConfig.cs, V4Game.cs

**_Game/Scenes/V4/V4MainMenu.unity** - 242 assets reached, 27 distinct script GUIDs:
- V4 scripts: ConfigSet.cs, BossConfig.cs, CaptainConfig.cs, DifficultyCurveConfig.cs, DirectorConfig.cs, EnemyArchetypeConfig.cs, EnemySpawnerConfig.cs, EnemyVariantConfig.cs, ExperimentConfig.cs, ItemConfig.cs, LogbookConfig.cs, NarrativeConfig.cs, OfferWeightsConfig.cs, PickupConfig.cs, PoolCatalogConfig.cs, PowerupConfig.cs, ProgressionConfig.cs, RewardConfig.cs, RunConfig.cs, StageContentConfig.cs, UnlockConfig.cs, WaveTimelineConfig.cs, WeaponConfig.cs, WeaponEvolutionConfig.cs, WeaponRulesConfig.cs, V4MainMenuController.cs

**_Game/Scenes/V4/V4Boot.unity** - 1 assets reached, 1 distinct script GUIDs:
- V4 scripts: V4BootLoader.cs

**_Game/Scenes/V4/V4TraceLauncher.unity** - 1 assets reached, 0 distinct script GUIDs:
- V4 scripts: (none)

**V4 Data+Resources** - 285 assets reached, 30 distinct script GUIDs:
- **[Scripts]** _Game/Scripts/Data/EnemyDefinition.cs  <- _Game/Data/Enemies/barnacle_king.asset, _Game/Data/Enemies/cannonboat.asset, _Game/Data/Enemies/cutthroat_skiff.asset ...(+3)
- **[Scripts]** _Game/Scripts/Enemies/EncounterSpawnSchedule.cs  <- _Game/V4/Data/Legacy/V4EncounterOnlySchedule.asset
- **[Scripts]** _Game/Scripts/Enemies/SpawnProfile.cs  <- _Game/Data/SpawnProfiles/Saltwind/SaltwindBossSummon.asset, _Game/Data/SpawnProfiles/Saltwind/SaltwindDrownedHarrier.asset, _Game/Data/SpawnProfiles/Saltwind/SaltwindDrownedPursuer.asset ...(+4)
- V4 scripts: ConfigSet.cs, BossConfig.cs, CaptainConfig.cs, DifficultyCurveConfig.cs, DirectorConfig.cs, EnemyArchetypeConfig.cs, EnemySpawnerConfig.cs, EnemyVariantConfig.cs, ExperimentConfig.cs, ItemConfig.cs, LogbookConfig.cs, NarrativeConfig.cs, OfferWeightsConfig.cs, PickupConfig.cs, PoolCatalogConfig.cs, PowerupConfig.cs, ProgressionConfig.cs, RewardConfig.cs, RunConfig.cs, StageContentConfig.cs, UnlockConfig.cs, WaveTimelineConfig.cs, WeaponConfig.cs, WeaponEvolutionConfig.cs, WeaponRulesConfig.cs, IconCatalog.cs


### 2c. Resources that V4 or kept code loads by path (not visible to the GUID crawl from scenes)

| Resources asset | Script type (GUID) | Loaded by | Other scripts in its closure | Verdict |
|---|---|---|---|---|
| `_Game/Resources/DemoJuiceSettings.asset` | `Data/DemoJuiceSettings.cs` (6f142fd0...) | V4PresentationRoot (x5), PoolCatalog, PoiMarkerPresenter | CFXR_Effect (JMO pkg); 217 assets (VFX prefabs) | keep |
| `_Game/Resources/GameDatabase.asset` | `Data/GameDatabase.cs` (c059bb5d...) | `GameDatabaseLoader.Load` via LegacyDatabaseAdapter | EnemyDefinition, WeaponDefinition, RelicDefinition, LogbookDefinition, ShipDefinition; 154 assets incl. `_Game/Data/{Enemies,Weapons,Relics,Logbooks,Ships}` and `Prefabs/Player/CutlassSloop.prefab` (via `Data/Ships/cutlass_sloop.asset`) | keep |
| `_Game/Resources/V3SaltwindIslandLayout.asset` | IslandLayoutDefinition | LegacyWorldPort.cs:83 (fallback) | IslandCollisionAuthoring (island prefabs) | keep (the scene itself serializes `_Game/Data/V3/V3SaltwindIslandLayout.asset`, which has a different GUID) |
| `_Game/Resources/DemoIslandLayout.asset` | IslandLayoutDefinition | `IslandLayoutDefinition.LoadAssignedOrDefault` (IslandLayoutDefinition.cs:42), last fallback | IslandCollisionAuthoring | keep (fallback) |
| `_Game/Resources/IslandVisualLibrary.asset` | IslandVisualLibrary | IslandVisualLibrary.cs:22; also serialized in the V4 run scene | none | keep |
| `_Game/Resources/TropicalIslandPrefabCatalog.asset` | TropicalIslandPrefabCatalog | TropicalIslandPrefabCatalog.cs:31 (StageBuilder) | IslandCollisionAuthoring | keep |
| `_Game/Resources/LevelUpCardArtSettings.asset` | UI/LevelUpCardArtSettings | LevelUpCardArtSettings.cs:25 (LevelUpCardView) | none | keep |
| `_Game/Resources/V3PlayerInfoHudArtSettings.asset` | V3/V3PlayerInfoHudArtSettings | RunCanvasSetup.cs:24, AuthoredHudPresenter.cs:126 | none | keep (rename type later) |
| `_Game/Resources/V3CharacterCardArtSettings.asset` | V3/V3CharacterCardArtSettings | CaptainSelectView, InteractionPromptPresenter | none | keep |
| `_Game/Resources/V3MinimapArtSettings.asset`, `V3MinimapHudSettings.asset` | V3/V3MinimapArtSettings, V3MinimapHudSettings | MinimapPresenter | none | keep |
| `_Game/Resources/V3PlayBoundsWallSegment.prefab` | V3/V3PlayBoundsWallSegment (c4e8a91f...) | BoundsWallPresenter.cs:14/38 (`"V3PlayBoundsWallSegment"`) | none | keep (the path string is load-bearing if renamed) |
| `_Game/V4/Resources/V4IconCatalog.asset` | V4 IconCatalog | IconCatalog.cs:32 | none | V4 already |
| `_Game/Resources/DemoPacingSettings.asset`, `LevelUpSettings.asset` | Data/DemoPacingSettings, Data/LevelUpSettings | only V3 systems (LevelSystem, WeaponController, POIManager...), V4 editor generators, V4 tests (V4SpawnM3Tests.cs:918) | none | delete with V3 (after the V4 tests and generators drop them) |
| `_Game/Resources/EncounterSpawnSchedule.asset` | Enemies/EncounterSpawnSchedule | V3 EncounterSpawner, V3 tests, V4SwitchoverM14Tests.cs:25/118 | EnemyDefinition, SpawnProfile (all 13 Saltwind profiles) | delete candidate (V4 runtime never loads it) |
| `_Game/Resources/V3MainMenuTypographySettings.asset` | V3/V3MainMenuTypographySettings | V3 menu only | none | delete |
| `_Game/Resources/CharacterPoses/*.png` (6) | sprites | `V3CharacterSelectPoseResolver` (V3 only; `Resources.Load<Sprite>("CharacterPoses/...")`) | none | delete with V3 (V4 uses `_Game/Art/Character Poses` by GUID) |
| `CombatExperiment/<FullCombatV2Id>` | CombatExperimentProfile | CombatExperimentProfile.cs:86 | none; the asset does not exist, so it falls back to `CreateBuiltIn()` | nothing to move |

`_Game/Prefabs/Player/CutlassSloop.prefab` has **no MonoBehaviours**: 27 assets, all meshes, materials and textures. It is reached from `Data/Ships/cutlass_sloop.asset` (ShipDefinition) and is also placed in the V4 run scene. The V4 run scene's player ship is a scene object carrying PlayerShipController, DamageReceiver and FollowCamera (on the camera).

### 2d. Serialized-script GUIDs that must survive the move (move each `.cs` together with its `.cs.meta`)

Referenced by V4 scenes or V4-loaded assets:
- DamageReceiver `e062f9b19dd56fa4d9357d2663127f42`
- RunManager `7921e778e6ba8874bb03f4ec821a4a63`
- VirtualJoystick `76f96467cf26d4c41bd33f603e6b1833`
- FollowCamera `f7d3024c8abf42747af7b122b9ef82e8`
- PlayerShipController `78c31716260cf6e48a2938ec25047099`
- LevelUpCardView `26a56f1433b762b40ac7e4990f7b76dd`
- DemoIslandMarker `8883a2d4ce2b12241ae199037d861ce4` (27 island prefabs)
- StageBuilder `61306dca8d63a514a8f7190774ca866c`
- IslandLayoutDefinition `b161c8dd2d3dc0e47b211db6c68b12bb`
- IslandVisualLibrary `12ca31f6931596c4a8da2a4051215214`
- IslandCollisionAuthoring `67cae99f2e3b9854ab04af0431ece1c4` (74 island prefabs under `Assets/GameObject/Islands` and elsewhere)
- EnemyDefinition `d165e405d987a6b4a9eef8b11a2f7a8e` (14 assets)
- SpawnProfile `22e2c9f784c76794eaee4a4b9fe8f00a` (13)
- EncounterSpawnSchedule `91c3caae0823bfc42bbbb84cd666da92`
- GameDatabase `c059bb5d29c51604a94937b4cf2565de`
- DemoJuiceSettings `6f142fd00426a4943b384970062a3fe7`
- WeaponDefinition `0042c8fbd3f13d7498562ae346e23b5d` (6)
- RelicDefinition `51a7f53d676a01a46b61cae4b3655a9a` (12)
- LogbookDefinition `e45593a37dc2dbf44a60f7383ff33976` (10)
- ShipDefinition `ff6f0fa963ac2ee4699efccfab549435`
- TropicalIslandPrefabCatalog `e8c4a92b1d3f4056982a1b0c9d7e6f5a`
- LevelUpCardArtSettings `8cca2bc9747929d47b0a5da566f07ffe`
- V3CharacterCardArtSettings `f8e7d6c5b4a3928170695a4b3c2d1e0f`
- V3MinimapArtSettings `0de511003357b6640bf9d32031816e0b`
- V3MinimapHudSettings `8167e84a16de2d1458571f5809065ab7`
- V3PlayerInfoHudArtSettings `943d8d43e5682504bad13d4bd8a52154`
- V3PlayBoundsWallSegment `c4e8a91f2b3d4e6580a1b2c3d4e5f678`
- StageBalanceProfile `ced4ae99c4b419d44b2fcbcc8175de21` (only `_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset`, used by V3 scenes; at runtime V4 creates its instance in memory)

Some of these GUIDs are hand-written (`c4e8a91f2b3d4e6580a1b2c3d4e5f678`, `f8e7d6c5b4a3928170695a4b3c2d1e0f`, `e8c4a92b1d3f...`). Keep the existing `.meta` files; never let Unity regenerate them.

### 2e. Missing-script hazards found in V4-reachable assets

- **`Assets/UI/Prefabs/Canvas.prefab` carries `V3/V3AuthoredGameplayHud` (component fileID 4724500206099722037, script GUID `4afe51fdfe6d73e4fac90ef1b87473a6`).** Both V4 run scenes instance this prefab and list that component in `m_RemovedComponents` (V4SaltwindRun.unity:2493 and V4ModuleLab.unity:2493), so it never loads at runtime. Once the script is deleted, the prefab asset itself will show a Missing Script, and the scene's removed-component override points at a missing object. Strip the component from `Canvas.prefab` (an Editor operation) in the same step, then re-save both V4 scenes.
- No other script outside the keep set is reachable from any V4 scene, V4 data or the Resources assets V4 loads. The earlier "unresolved" GUIDs are all package scripts (uGUI, TMP, URP, Input System) or Unity built-ins (`0000000000000000e000...`/`f000...`), except two in `Assets/UI/Prefabs/Weapon_powerup.prefab` (`3da599d688f5a483f8f011b97fa6d392`, `689c492f97e4b4cf3bd78710e37ef6cd`). Those resolve nowhere and are pre-existing missing references, not caused by M15.
- V4 data references outside `V4/`: only `V4/Data/Legacy/V4EncounterOnlySchedule.asset` (an `EncounterSpawnSchedule`) points at 7 `_Game/Data/SpawnProfiles/Saltwind/*.asset` and 6 `_Game/Data/Enemies/*.asset`. It is created and read only by `V4/Editor/V3ConfigMigrator.cs:367-417` and `V4SceneSetup.cs:137`, so it is a delete candidate together with `V4/Data/Legacy/`.

## 3. Transitive closure and proposed keep set

### 3a. Confirmed V3 references in kept files (file:line, comment lines excluded)

Known examples, confirmed:
- `Scripts/Combat/DamageReceiver.cs:74-75` reads `V3CaptainActiveController.Instance.IncomingDamageMultiplier`; `:77-78` reads `V3RunItemEffects.Instance.GetIncomingDamageMultiplier(player)`; `:81` calls `V3RunItemEffects.Instance?.NotifyPlayerTookDamage()`. V4 never creates those singletons (its captain and items apply through BuildModule and LegacyShipPort), so all three are always null. Cut them.
- `Scripts/World/StageBuilder.cs:136` calls `V3StagePoiPlacer.Place(layout, new RunRng(seed + 404), demoLayout)` when `_useV3PoiLayout`, which is set by `ConfigureV3Layout`, which V4 calls in LegacyWorldPort.cs:91. **This is live V4 gameplay.** It places the salvage chests, POIs, barrels and spawn anchors on the map stream, and StageContentConfig.cs:61 documents it. `V3StagePoiPlacer` (552 lines) and `V3StagePoiPlacementOptions` (58) are therefore in the keep set (move and rename), not cut. Their dependencies (SaltwindPoiLayoutLogic, V3SpawnAnchorValidator, PlayBounds, StageBalanceProfileContext) are all kept.
- `Scripts/Core/RunManager.cs:87` `RunMarksTracker.Tick`, `:132` `RunMarksTracker.NotifyKill`, `:167` `SaltwindRunSettlement.TrySettleCurrentRun(AppRoot.Instance?.Save)` (doubloons and Marks settlement), `:113/:123` `RunCombatTelemetry.Instance?.RecordGold*`, `:146` `RestoreSuspendSnapshot(RunSnapshotData)`. `:37` also uses `SaltwindFinaleLogic.ResolveStageDeadlineSeconds` (kept). V4 tests already assert "the V3 settlement never runs" (V4RunPlayModeTests.cs:784).

Every cut edge found by the closure (a kept file pointing to a file that will be deleted):

```
== Balance/SaltwindRunResetLogic.cs -> Encounters/RunEncounterTracker.cs, V3/MultiShip/V3RunSession.cs
   32 [RunEncounterTracker] if (RunEncounterTracker.ActiveBossEncounter != null)
   37 [V3RunSession] !V3RunSession.IsLocalMultiplayer;
== Balance/SaltwindRunResultsLogic.cs -> Balance/SaltwindStageSettlementLogic.cs
   67 [RunMarksBreakdown] public static bool MarksUiMatchesLedger(RunMarksBreakdown breakdown, int marksAwarded) =>
   70 [RunMarksBreakdown] public static string FormatMarksBreakdownForPlayer(RunMarksBreakdown breakdown, int marksAwarded, int treasuryMarks)
== Balance/SaltwindStageEconomyLogic.cs -> V3/V3ChestLoot.cs, V3/V3Economy.cs
   49 [V3Economy] return V3Economy.GetChestPrice(successfulPurchases);
   71 [V3ChestLoot] var itemId = V3ChestLoot.RollSalvageItem(rng, luck);
== Balance/StageBalanceProfileContext.cs -> V3/MultiShip/V3RunSession.cs
   57 [V3RunSession] !V3RunSession.IsLocalMultiplayer && StageBalanceProfileContext.IsActive;
   66 [V3RunSession] if (V3RunSession.IsLocalMultiplayer)
== Combat/DamageReceiver.cs -> V3/V3CaptainActiveController.cs, V3/V3RunItemEffects.cs
   74 [V3CaptainActiveController] if (V3CaptainActiveController.Instance != null)
   75 [V3CaptainActiveController] amount *= V3CaptainActiveController.Instance.IncomingDamageMultiplier;
   77 [V3RunItemEffects] if (V3RunItemEffects.Instance != null && player != null)
   78 [V3RunItemEffects] amount *= V3RunItemEffects.Instance.GetIncomingDamageMultiplier(player);
   81 [V3RunItemEffects] V3RunItemEffects.Instance?.NotifyPlayerTookDamage();
== Combat/WeaponEvolutionCatalog.cs -> V3/V3RunItemInventory.cs
   85 [V3RunItemInventory] return V3RunItemInventory.Instance != null && V3RunItemInventory.Instance.HasItem(def.requiredItemId);
== Core/AppRoot.cs -> V3/V3RunContext.cs
   65 [V3RunContext] V3RunContext.IsActive = false;
   66 [V3RunContext] V3RunContext.Layout = null;
== Core/RunManager.cs -> Balance/RunMarksTracker.cs, Balance/SaltwindRunSettlement.cs, Combat/RunCombatTelemetry.cs, Save/RunSnapshot.cs
   87 [RunMarksTracker] RunMarksTracker.Tick(ElapsedSeconds);
   113 [RunCombatTelemetry] RunCombatTelemetry.Instance?.RecordGoldEarned(amount);
   123 [RunCombatTelemetry] RunCombatTelemetry.Instance?.RecordGoldSpent(amount);
   132 [RunMarksTracker] RunMarksTracker.NotifyKill();
   146 [RunSnapshotData] public void RestoreSuspendSnapshot(RunSnapshotData data)
   167 [SaltwindRunSettlement] SaltwindRunSettlement.TrySettleCurrentRun(AppRoot.Instance?.Save);
== Player/PlayerShipController.cs -> V3/MultiShip/V3MultiShipBotBounds.cs, V3/MultiShip/V3RunSession.cs, V3/MultiShip/V3ShipCombatant.cs
   260 [V3RunSession] if (V3RunSession.IsMultiShip)
   262 [V3ShipCombatant] var combatant = GetComponent<V3ShipCombatant>();
   264 [V3MultiShipBotBounds] return V3MultiShipBotBounds.BotPlayMargin(hullRadius);
== UI/RunHudUiFactory.cs -> UI/RunHud.cs
   51 [WeaponLevelStripHud] public WeaponLevelStripHud weaponStrip;
   60 [WeaponLevelStripHud] var strips = canvas.GetComponentsInChildren<WeaponLevelStripHud>(true);
   133 [WeaponLevelStripHud] static WeaponLevelStripHud BuildWeaponStrip(Transform root)
   154 [WeaponLevelStripHud] var strip = stripGo.AddComponent<WeaponLevelStripHud>();
== V3/V3CharacterCardArtSettings.cs -> V3/V3CaptainCatalog.cs
   89 [V3CaptainCatalog] return V3CaptainCatalog.GetMenuThemeColor(captain);
== V3/V3HomeSeed.cs -> V3/V3RunContext.cs
   29 [V3RunContext] if (!V3RunContext.IsActive || HasLockedHomeSeed || runSeed == 0)
== V3/V3MinimapHudSettings.cs -> V3/V3MinimapHud.cs
   44 [V3MinimapHud] public void ApplyTo(V3MinimapHud hud)
== V3/V3PlaceholderFactory.cs -> V3/V3FloorPowerupWorld.cs
   62 [V3FloorPowerupWorld] V3WorldBillboardLabel.Attach(marker, V3FloorPowerupWorld.GetDisplayName(kind), hint);
```


Notes on the cut list:
- `SaltwindStageEconomyLogic.cs:49/71` are inside `QuoteChestPrice` and `TryPlanChestPurchase`, which V4 does not call (V4 calls only `ResolveKillGoldDrop` from KillFanout.cs:52 and `ApplyGoldGrant` from LegacyStageProfilePort.cs:25). Delete those two methods and `SaltwindChestPurchasePlan`.
- `SaltwindRunResultsLogic.cs:67/70` (Marks breakdown) is unused by V4. V4 calls `BuildHeadline`, `BuildOutcomeSubline` (ResultsPresenter.cs:83/103) and `FormatProfileFooter`. Delete the two Marks methods; this also drops the dependency on `SaltwindStageSettlementLogic`.
- `StageBalanceProfileContext.cs:57/66`, `SaltwindRunResetLogic.cs:37`, `PlayerShipController.cs:260-264`: `V3RunSession.IsLocalMultiplayer/IsMultiShip` is always false in V4. Replace with false or drop the branch.
- `SaltwindRunResetLogic.cs:32`: `RunEncounterTracker.ActiveBossEncounter` is V3 state that is always null in V4 (the V4 encounter state lives in the V4 spawner).
- `AppRoot.cs:65-66` (`V3RunContext`), and `AppRoot.cs:44-60` (`LoadScene` / `IsLocalMultiplayerRunScene` using `V3SceneNames`): V4 never calls `AppRoot.LoadScene`; it uses `SceneManager.LoadScene` with `V4SceneNames` (V4Game.cs:422-435, V4MainMenuController.cs:138). Delete `LoadScene`, and `V3SceneNames` can go too. It is kept above only because AppRoot pulls it in.
- `V3HomeSeed.cs:29` reads `V3RunContext.IsActive`. LegacyWorldPort.cs:113 sets it true in V4 runs, so replace it with a V4 "run active" flag or drop the check. `V3RunContext` itself (Layout, CaptainId, IsActive) is otherwise read only by V3 HUD and POI code plus `LegacyContentPort.IsWeaponOfferEligible` and `RunContextActive`. Do not keep it; fold `IsActive` into the V4 run.
- `WeaponEvolutionCatalog.cs:85` (`V3RunItemInventory`) is reached only through `SaltwindStageProgressionLogic` <- `SaltwindStageEconomyLogic`; V4 runtime code does not use it (V4 has `Evolutions` configs). Once the chest-purchase methods are deleted, check whether `SaltwindStageProgressionLogic` and `WeaponEvolutionCatalog` drop out of the keep set entirely (probably yes; about 270 lines).
- `RunHudUiFactory.cs` needs `WeaponLevelStripHud`, which lives in `UI/RunHud.cs` (V3 HUD, reads LevelSystem and WeaponController at RunHud.cs:23/42/184-200). V4 uses only `RunHudTheme` colors (AuthoredHudPresenter.cs:164/236) and `RunHudUiFactory.RootName` (RunCanvasSetup.cs:35, to hide the V3 layout). Move `RunHudTheme` into V4 and delete RunHudUiFactory, RunHud and the hide calls. That removes about 630 lines from the keep set.
- `V3PlaceholderFactory.cs:62` uses `V3FloorPowerupWorld.GetDisplayName(kind)`. Copy the name table (V4 needs it for PowerupPresenter.cs:33 and PowerupModule.cs:28).
- `V3CharacterCardArtSettings.cs:89` (`V3CaptainCatalog.GetMenuThemeColor`) and `V3MinimapHudSettings.cs:44` (`ApplyTo(V3MinimapHud)`): inline the colors, and delete `ApplyTo`.
- `EnemyWorld.cs` (1,589 lines, V3) is referenced by V4 prod code only for `SpatialHash` (declared at EnemyWorld.cs:44 and used at EnemySimulation.cs:39). Move `SpatialHash` into V4 and delete EnemyWorld.

### 3b. V4-side edits needed (V4 code that references code being deleted, excluding Legacy)

| V4 file | Reference to cut |
|---|---|
| `V4/Runtime/Enemies/EnemySimulation.cs:39` | `SpatialHash` (from EnemyWorld.cs) |
| `V4/Presentation/Hud/RunCanvasSetup.cs:28-37` | `HidePlaceholders<V3ObjectiveHud/V3RunLoadoutHud/V3RunCaptainActiveHud/V3RunStatsHud/V3RunTimerHud/V3PlayerInfoHud>`, `RunHudUiFactory.RootName`, `WeaponLevelStripHud`. None of these components exists in the V4 scenes (the scene closure has none of them), so delete the lines. |
| `V4/Presentation/Hud/AuthoredHudPresenter.cs:42/126/164/236/265` | `V3PlayerInfoHudArtSettings`, `RunHudTheme`, `V3CaptainId` cast (kept types; retarget after the move) |
| `V4/DevTools/Trace/RunTraceRecorder.cs:37-53,147-177,184,297,345-347,376-412,478-480` | V3-scene golden-recording branches: `POIManager`, `TriggerBus`, `LevelUpModal` (reflection on `_offers`/`panel`/`Pick`), `V3InteractionController` (reflection on `_target`, `_salvageChestDismissedIndex`, `EndSalvageChestChoicePause`), `LevelSystem`, `V3MinibossArena`, `SaltwindRunSettlement`, `V3CaptainSelection` |
| `V4/DevTools/Trace/PlayerPilot.cs:31,221-226` | `PickupWorld._pool` reflection + `LegacyEnemyWorldView`/`LegacyHostileProjectileView`/`LegacyHazardView` |
| `V4/DevTools/Trace/TraceBoot.cs:93-94` | `V3RunBootstrap` seed injection for V3 scenes |
| `V4/DevTools/Console/RunSection.cs:91` | `RunManager.RestoreSuspendSnapshot(new RunSnapshotData{...})` |
| `V4/Editor/V3ConfigMigrator.cs` (1,279 lines), `V3ConfigMapping.cs` (223), `V4BuildConfigGenerator.cs` (369), `V4BossConfigGenerator.cs` (101), `V4IconCatalogBuilder.cs` (80), `V4SceneSetup.cs` (387, the `RemoveAll<V3 type>` steps at :180-250) | One-shot V3-to-V4 migrators and scene cleaners. They read V3 catalogs (`V3CaptainCatalog`, `V3ItemCatalog`, `V3LogbookCatalog`, `V3CaptainProfile`, `V3RunHudIconResolver`, `BarnacleKingBossAttackDirector`, `DemoPacingSettings`, `LevelUpSettings`, `SaltwindAmbientCompositionLogic`, `SaltwindPressureBeatLogic`, `EnemyTierLogic` ...) and `LegacyBuildMapping`. Delete them or strip them to their non-V3 parts. |
| `V4/Runtime/Progression/PowerupModule.cs:28`, `V4/Presentation/Effects/PowerupPresenter.cs:13/33`, `Stage/PoiMarkerPresenter.cs:180`, `Stage/InteractionPromptPresenter.cs:68/80`, `Hud/ToastPresenter.cs:48`, `Hud/ResultsPresenter.cs:172/253` | `V3PlaceholderFactory` + `V3FloorPowerupKind` (kept; rename later) |
| Legacy port consumers: `V4Game.cs`, `RunBootstrapSequence.cs`, `StageModule.cs`, `WeaponModule.cs`, `EnemyWorldModule.cs`, `BuildModule.cs`, `MenuServices.cs`, `CaptainChoice.cs` | Re-point from the Legacy* ports to the moved types (or V4 services), then delete `Runtime/Legacy/` |

### 3c. Proposed keep set (119 files, 13,930 lines)

This set is the closure from three kinds of seeds, not following edges into cut targets:
- (a) Scripts types used by V4 prod code outside Legacy;
- (b) scripts serialized in V4 scenes, V4 data and the Resources assets V4 loads;
- (c) the infrastructure the Legacy ports wrap that V4 still needs at runtime (RunRng, PlayBounds, SpawnPlacementService, the boost and camera playfeel, DayNightDirector, StageBuilder and island pipeline, V3SpawnAnchorValidator, V3StagePoiPlacer, V3HomeSeed, the V3ContentUnlocks/V3PlaytestFlags/V3CaptainSelection progression state, CombatExperimentProfile, GameDatabaseLoader plus the two runtime catalogs, the StageBalanceProfile context, the economy/results/reset logic, WeaponFeatureFlags, MoveInput).

Excluded as cut points: V3AuthoredGameplayHud (scene-removed), EnemyWorld (SpatialHash only), the six V3 HUD placeholder types and RunHud (RunCanvasSetup hide calls). Tags: SEED = used by V4 prod code or a V4 scene/asset; LEGACY-INFRA = wrapped by a Legacy port and still needed; "via X" = pulled in transitively. Candidates to shrink further are listed in 3a notes (RunHudUiFactory, SaltwindStageProgressionLogic, WeaponEvolutionCatalog, V3SceneNames, StageBalanceProfile's export helpers `BossStatExportLogic`/`EncounterScheduleExportLogic`/`WeaponStatExportLogic`, `StageBalanceProfileValidator`, `StageBalanceProfileHost` (a MonoBehaviour used only in V3 scenes and Test_WeaponEnemy), `Playtest/WeaponEnemyPlaytestSession` (pulled by `SaltwindCombatFeature`), `Audio/SfxService` (pulled by ShipBoostController)).


| Scripts file | lines | why kept |
|---|---|---|
| `Audio/SfxService.cs` | 36 lines | via Player/ShipBoostController.cs |
| `Balance/BossStatExportLogic.cs` | 34 lines | via Balance/StageBalanceProfile.cs |
| `Balance/EncounterScheduleExportLogic.cs` | 51 lines | via Balance/StageBalanceProfile.cs |
| `Balance/EnemyTierLogic.cs` | 64 lines | SEED |
| `Balance/SaltwindFinaleLogic.cs` | 316 lines | via Core/RunManager.cs |
| `Balance/SaltwindPoiLayoutLogic.cs` | 196 lines | via Balance/StageBalanceProfile.cs |
| `Balance/SaltwindRunResetLogic.cs` | 39 lines | LEGACY-INFRA |
| `Balance/SaltwindRunResultsLogic.cs` | 97 lines | SEED |
| `Balance/SaltwindStageEconomyLogic.cs` | 149 lines | LEGACY-INFRA |
| `Balance/SaltwindStageProgressionLogic.cs` | 141 lines | via Balance/SaltwindStageEconomyLogic.cs |
| `Balance/StageBalanceProfile.cs` | 329 lines | LEGACY-INFRA |
| `Balance/StageBalanceProfileContext.cs` | 95 lines | LEGACY-INFRA |
| `Balance/StageBalanceProfileHost.cs` | 14 lines | via Balance/StageBalanceProfileContext.cs |
| `Balance/StageBalanceProfileValidator.cs` | 114 lines | via Balance/StageBalanceProfileContext.cs |
| `Balance/WeaponStatExportLogic.cs` | 72 lines | via Balance/StageBalanceProfile.cs |
| `Combat/CombatDebugOverlays.cs` | 21 lines | SEED |
| `Combat/CombatExperimentProfile.cs` | 91 lines | LEGACY-INFRA |
| `Combat/CombatSpecialistEnemyCatalog.cs` | 81 lines | LEGACY-INFRA |
| `Combat/CombatWeaponCatalog.cs` | 160 lines | LEGACY-INFRA |
| `Combat/DamageReceiver.cs` | 91 lines | SEED |
| `Combat/EnemyCombatProfile.cs` | 68 lines | SEED |
| `Combat/EnemyCombatProfileRegistry.cs` | 209 lines | via Enemies/EnemySpawnRuntimeParams.cs |
| `Combat/PlayerHullSync.cs` | 18 lines | SEED |
| `Combat/SaltwindCombatFeature.cs` | 14 lines | via Enemies/EnemySpawnRuntimeParams.cs |
| `Combat/WeaponDamageLedger.cs` | 70 lines | via Balance/SaltwindRunResultsLogic.cs |
| `Combat/WeaponDebugPalette.cs` | 35 lines | SEED |
| `Combat/WeaponEvolutionCatalog.cs` | 130 lines | via Balance/SaltwindStageProgressionLogic.cs |
| `Combat/WeaponFeatureFlags.cs` | 16 lines | LEGACY-INFRA |
| `Core/AppRoot.cs` | 81 lines | SEED |
| `Core/GameConstants.cs` | 152 lines | SEED |
| `Core/GameDatabaseLoader.cs` | 184 lines | LEGACY-INFRA |
| `Core/GameEnums.cs` | 201 lines | SEED |
| `Core/PlayerStats.cs` | 78 lines | SEED |
| `Core/RunManager.cs` | 181 lines | SEED |
| `Core/RunRng.cs` | 83 lines | LEGACY-INFRA |
| `Data/DemoJuiceSettings.cs` | 172 lines | SEED |
| `Data/EnemyDefinition.cs` | 35 lines | SEED |
| `Data/GameDatabase.cs` | 46 lines | SEED |
| `Data/IslandLayoutDefinition.cs` | 69 lines | SEED |
| `Data/IslandVisualLibrary.cs` | 55 lines | SEED |
| `Data/LogbookDefinition.cs` | 17 lines | via Core/GameDatabaseLoader.cs |
| `Data/RelicDefinition.cs` | 22 lines | via Core/GameDatabaseLoader.cs |
| `Data/ShipDefinition.cs` | 20 lines | via Core/GameDatabaseLoader.cs |
| `Data/TropicalIslandPrefabCatalog.cs` | 115 lines | via World/StageBuilder.cs |
| `Data/WeaponDefinition.cs` | 122 lines | via Combat/CombatWeaponCatalog.cs |
| `Data/WeaponJuiceVfxBinding.cs` | 21 lines | via Data/DemoJuiceSettings.cs |
| `Data/WeaponUpgradeAxisConfig.cs` | 39 lines | SEED |
| `Enemies/EncounterSpawnSchedule.cs` | 41 lines | SEED |
| `Enemies/EnemyRangedAttackRules.cs` | 25 lines | via Enemies/EnemySpawnRuntimeParams.cs |
| `Enemies/EnemyShipWakeJuice.cs` | 131 lines | SEED |
| `Enemies/EnemySpawnRuntimeParams.cs` | 234 lines | SEED |
| `Enemies/EnemyViewAnimation.cs` | 129 lines | SEED |
| `Enemies/EnemyVisualFallbackResolver.cs` | 68 lines | SEED |
| `Enemies/RunCombatSignals.cs` | 129 lines | SEED |
| `Enemies/SpawnModifierStack.cs` | 79 lines | SEED |
| `Enemies/SpawnPlacementService.cs` | 312 lines | SEED |
| `Enemies/SpawnProfile.cs` | 42 lines | SEED |
| `Enemies/SpawnProfileEnums.cs` | 108 lines | SEED |
| `Enemies/SpawnProfileModules.cs` | 179 lines | SEED |
| `POI/SalvageChestTreasureGlow.cs` | 80 lines | SEED |
| `POI/SalvageChestWaterFloat.cs` | 92 lines | SEED |
| `Pickups/FlyingHealthBillboard.cs` | 105 lines | SEED |
| `Platform/MoveInput.cs` | 124 lines | LEGACY-INFRA |
| `Platform/QualityProfile.cs` | 24 lines | SEED |
| `Platform/VirtualJoystick.cs` | 43 lines | SEED |
| `Player/BoostCameraZoom.cs` | 28 lines | LEGACY-INFRA |
| `Player/FollowCamera.cs` | 436 lines | SEED |
| `Player/PlayerShipController.cs` | 691 lines | SEED |
| `Player/PlayerShipVisualBootstrap.cs` | 85 lines | LEGACY-INFRA |
| `Player/ShipBoostController.cs` | 57 lines | LEGACY-INFRA |
| `Playtest/WeaponEnemyPlaytestSession.cs` | 8 lines | via Combat/SaltwindCombatFeature.cs |
| `Save/SaveService.cs` | 228 lines | SEED |
| `UI/LevelOffer.cs` | 50 lines | SEED |
| `UI/LevelUpCardArtSettings.cs` | 48 lines | via UI/LevelUpCardView.cs |
| `UI/LevelUpCardView.cs` | 322 lines | SEED |
| `UI/LevelUpRarityStyles.cs` | 74 lines | via UI/LevelUpCardView.cs |
| `UI/LevelUpUiFactory.cs` | 283 lines | SEED |
| `UI/RunHudUiFactory.cs` | 319 lines | SEED |
| `UI/StatDisplay.cs` | 69 lines | SEED |
| `UI/UiCanvasConfigurator.cs` | 74 lines | SEED |
| `V3/PlaceholderMarker.cs` | 29 lines | via V3/V3PlaceholderFactory.cs |
| `V3/V3CaptainId.cs` | 12 lines | SEED |
| `V3/V3CaptainSelection.cs` | 15 lines | LEGACY-INFRA |
| `V3/V3CharacterCardArtSettings.cs` | 92 lines | SEED |
| `V3/V3ContentUnlocks.cs` | 68 lines | LEGACY-INFRA |
| `V3/V3FloorPowerupKind.cs` | 15 lines | SEED |
| `V3/V3HomeSeed.cs` | 42 lines | LEGACY-INFRA |
| `V3/V3MinimapArtSettings.cs` | 20 lines | SEED |
| `V3/V3MinimapHudSettings.cs` | 60 lines | SEED |
| `V3/V3MinimapMarkerGraphics.cs` | 172 lines | SEED |
| `V3/V3MinimapUiSprites.cs` | 114 lines | SEED |
| `V3/V3PlaceholderFactory.cs` | 166 lines | SEED |
| `V3/V3PlayBoundsWallSegment.cs` | 178 lines | SEED |
| `V3/V3PlayerInfoHudArtSettings.cs` | 56 lines | SEED |
| `V3/V3PlaytestFlags.cs` | 47 lines | LEGACY-INFRA |
| `V3/V3SceneNames.cs` | 26 lines | via Core/AppRoot.cs |
| `V3/V3SpawnAnchorValidator.cs` | 612 lines | LEGACY-INFRA |
| `V3/V3StagePoiPlacementOptions.cs` | 58 lines | via V3/V3StagePoiPlacer.cs |
| `V3/V3StagePoiPlacer.cs` | 552 lines | LEGACY-INFRA |
| `V3/V3WeaponDisplayNames.cs` | 24 lines | via Balance/SaltwindRunResultsLogic.cs |
| `V3/V3WorldBillboardLabel.cs` | 71 lines | via V3/V3PlaceholderFactory.cs |
| `VFX/EnemyBurnFlash.cs` | 121 lines | SEED |
| `VFX/LongNineBeamVisual.cs` | 36 lines | SEED |
| `VFX/VfxParticleUtil.cs` | 162 lines | SEED |
| `VFX/WorldHealthBar.cs` | 48 lines | SEED |
| `World/DayNightDirector.cs` | 35 lines | LEGACY-INFRA |
| `World/DemoIslandMarker.cs` | 22 lines | SEED |
| `World/IslandColliderUtility.cs` | 164 lines | via World/StageBuilder.cs |
| `World/IslandCollisionAuthoring.cs` | 18 lines | SEED |
| `World/IslandLayoutSpawner.cs` | 114 lines | via World/StageBuilder.cs |
| `World/IslandObstacles.cs` | 369 lines | SEED |
| `World/IslandSpawnExclusionRegistry.cs` | 102 lines | via V3/V3SpawnAnchorValidator.cs |
| `World/IslandVisualBuilder.cs` | 96 lines | via World/StageBuilder.cs |
| `World/IslandVisualUtility.cs` | 52 lines | via World/IslandLayoutSpawner.cs |
| `World/PlayBounds.cs` | 140 lines | SEED |
| `World/ProceduralIslandSettings.cs` | 54 lines | LEGACY-INFRA |
| `World/StageBuilder.cs` | 381 lines | SEED |
| `World/StageGenerator.cs` | 230 lines | SEED |
| `World/TropicalIslandSpawner.cs` | 196 lines | via World/StageBuilder.cs |



## 4. Delete candidates

### 4a. Code

| Set | Files | Lines | Notes |
|---|---|---|---|
| `Scripts/V3/**` minus the 21 kept | 81 | 13,022 | Includes all of `V3/MultiShip/**` (19 files, 1,577 lines). Kept V3-folder files: V3CaptainId, V3CaptainSelection, V3CharacterCardArtSettings, V3ContentUnlocks, V3FloorPowerupKind, V3HomeSeed, V3MinimapArtSettings, V3MinimapHudSettings, V3MinimapMarkerGraphics, V3MinimapUiSprites, V3PlaceholderFactory, PlaceholderMarker, V3WorldBillboardLabel, V3PlayBoundsWallSegment, V3PlayerInfoHudArtSettings, V3PlaytestFlags, V3SpawnAnchorValidator, V3StagePoiPlacer, V3StagePoiPlacementOptions, V3WeaponDisplayNames, V3SceneNames (the last is droppable, see 3a). |
| Non-V3 `Scripts/**` outside the keep set | 86 | 21,146 | Full list in 4b |
| `Scripts/Editor/**` (asmdef HighSeas.Editor) | 26 | 5,718 | V4 uses none of these types. They include custom inspectors for kept types: `WeaponDefinitionEditor` (`[CustomEditor(typeof(WeaponDefinition))]`) and the island tools `HighSeasIslandColliderEditor` / `HighSeasIslandLayoutEditor` (menu items that bake IslandLayoutDefinition / IslandCollisionAuthoring). Keep or port those three if the island pipeline is still authored. External callers: `tools/build-webgl.bat` (`-executeMethod HighSeas.Editor.HighSeasBuild.BuildWebGL`) and `tools/bootstrap-highseas.bat` (`HighSeas.Editor.HighSeasBootstrap.BootstrapAll`). |
| `Tests/EditMode/*.cs` (HighSeas.Tests.EditMode) | 22 | 2,790 | All V3 (Saltwind gates, EncounterSpawner, LevelOfferGenerator, V3Integration...). Do **not** delete `Tests/EditMode/RoguePirates/` (RoguePirates.Tests.EditMode: 4 files, 285 lines, Simulation/Content/AccountClient only; no HighSeas reference). Its folder is nested inside the V3 test folder, so move it first or delete selectively. |
| `Tests/PlayMode/*.cs` (HighSeas.Tests.PlayMode) | 13 | 1,326 | All V3 scene tests |
| `V4/Runtime/Legacy/*` | 7 | 1,263 | Required by the M15 phase gate (DeletedInPhaseTests) |
| V4 editor migrators (3b) | 6 | ~2,440 | V3ConfigMigrator, V3ConfigMapping, V4BuildConfigGenerator, V4BossConfigGenerator, V4IconCatalogBuilder, parts of V4SceneSetup |
| V4 test code that is V3 parity or golden only | `Tests/V4/EditMode/V3EnemyViewSink.cs`, `V3RegistryFromConfig.cs`, `Tests/V4/PlayMode/V3EnemyViewSink.cs`, `V3RegistryFromConfig.cs`, plus V3-comparison cases inside 20 V4 test files (list in 4c) | | |
| `Assets/_Game/Presentation/` (RoguePirates.Presentation) | 4 | 125 | `V3SimulationAdapter.cs` uses `RunManager`/`GameConstants`; no scene or prefab uses it (GUID search empty). Delete it or drop the HighSeas reference. |

### 4b. Non-V3 Scripts files outside the keep set (86)

Balance: RunMarksTracker, SaltwindAmbientCompositionLogic, SaltwindExplosiveBarrelLogic, SaltwindLabTraceParityLogic, SaltwindMarksUnlockLogic, SaltwindPressureBeatLogic, SaltwindRunSettlement, SaltwindSpawnTelemetryLogic, SaltwindStageSettlementLogic, SaltwindStageTimelineLogic.
Combat: CombatProcRuntime, CombatTelemetryDriver, DamageProcKind, DamageSystem, HellfirePatchWorld, HostileProjectileWorld, RunCombatTelemetry, SaltwindCombatAgentStateStore, SaltwindCombatBehaviorSystem (975), SaltwindCombatEffects, SaltwindCombatEncounterBudget, SaltwindHazardTelegraph, SaltwindReefCrabCombat, SaltwindSpecialistCombat, StatusController, WeaponBeamTargeting, WeaponFireOutcome, WeaponTargeting (478), WeaponTargetingVisualizer (519).
Core: RunBootstrap. Data: DemoPacingSettings, LevelUpSettings. Debug: CollisionDebugOverlay, EnemyIdentityDebugLabels, SpawnDirectorDebugUI. Encounters: RunEncounterTracker.
Enemies: AmbientSpawnBudget, EncounterScheduleRunLogic, EncounterSpawner (974), EnemyPatternMovement, EnemySpecialBehaviorSystem, EnemyViewPool, EnemyWorld (1,589; move `SpatialHash` out first), ReefCrabMinibossBehavior.
Meta: MetaSystems (TriggerBus/LootService/QuestService). POI: POIManager (1,126). Pickups: PickupViewPool, PickupViewVfxCache, PickupWorld (605). Platform: BoundShipKeyboardInput. Playtest: WeaponEnemyPlaytestBootstrap, WeaponEnemyPlaytestDirector. Promo: PromoTeaserDirector (963), PromoTeaserOverwhelmJuice. Save: RunSnapshot (RunSnapshotData + RunSnapshotService; captures POIManager/RunMarksTracker/RunEncounterTracker state), RunSnapshotRestoreLogic.
UI: LevelOfferGenerator (947), LevelSystem (721), LevelUpModal, MenuScreens, RunHud, WeaponCardAnimationDemo.
VFX: CameraShakeService, CannonImpactVfx, CombatVfxController, DamageHighlightPulse, FloatingDamageNumbers, JuiceVfxBroker, PlayerDamageFlash, PlayerHealthBarBinder, PlayerJuiceController, VfxAutoReturn, VfxPool, WeaponJuiceController (785).
Weapons: ProjectileViewPool, ProjectileWorld, WeaponController (1,731).
World: EnvironmentHazardVisual, EnvironmentalHazardsDisabled, ExternalForceReceiver, IslandCompoundColliderBuilder, MapGeneratorWorkbench, MinimapController, OceanCurrentZone, WhirlpoolHazard, WindGustDirector.

Check before deleting: `World/IslandCompoundColliderBuilder` and `ExternalForceReceiver` are not referenced by any kept file per the graph, but they sit in the island and physics area. `Save/RunSnapshot` is referenced by RunManager.cs:146 and RunSection.cs:91 (the suspend/restore dev command). Either drop suspend/restore or move just `RunSnapshotData` and rewrite the service. `Data/DemoPacingSettings` and `LevelUpSettings` are still read by V4 tests (V4SpawnM3Tests.cs:918, V4BuildM9Tests, V4ProgressionM9Tests, V4ConfigM1Tests) and generators.

### 4c. V4 tests that reference deleted code (must be edited in the same commit)

- **EditMode:**
  - V4BuildM9Tests: V3CaptainCatalog, V3CaptainProfile, V3ItemCatalog, V3LogbookCatalog, V3FloorPowerupWorld, V3RunItemInventory, LevelOfferGenerator, DemoPacingSettings
  - V4ProgressionM9Tests: 12 V3 types incl. LevelSystem, V3CaptainActiveController/Controller, V3RunItemEffects/Inventory, EncounterSpawner
  - V4EnemyTickM8Tests and V4EnemyWorldM8Tests: V3 EnemyWorld, StatusController, HostileProjectileWorld, DamageSystem, HellfirePatchWorld, SaltwindCombat* (V3-vs-V4 parity)
  - V4HudM6Tests: V3AuthoredGameplayHud, V3DiscoverySystem, V3ItemCatalog, V3MegabonkQuestDisplay, V3RunHudIconResolver
  - V4SpawnM3Tests: EncounterSpawner, SaltwindAmbientCompositionLogic, SaltwindPressureBeatLogic
  - V4ConfigM1Tests
  - V4RewardsM12Tests: SaltwindMarksUnlockLogic, SaltwindStageSettlementLogic
  - V4WeaponM5Tests: DamageSystem, DamageProcKind
  - V4PickupM9Tests: PickupWorld
  - V4SwitchoverM14Tests: RunCombatTelemetry, EncounterSpawnSchedule resource
  - V3EnemyViewSink: EnemyViewPool
  - V4StructureTests: its layering rules name HighSeas.Runtime and HighSeas.Editor at lines 109-113, and the DeletedInPhase check sits at :90
- **PlayMode:**
  - V4HudPlayModeTests (29 cut refs)
  - V4RunPlayModeTests (13, incl. `RunMarksTracker.Reset()` :93 and `SaltwindRunSettlement.ClearLastResults()` :95)
  - V4StagePlayModeTests (10)
  - V4EnemyLockstepPlayModeTests (9), V4EnemyPlayModeTests (9)
  - V4NarrativePlayModeTests, V4AdaptivePlayModeTests: RunMarksTracker, SaltwindRunSettlement, RunEncounterTracker static resets
  - V3EnemyViewSink

Many of these are static-state resets in SetUp, which become deletions. The V3-vs-V4 lockstep and parity tests must go with V3.

### 4d. Scenes

- `Assets/_Game/Scenes/v3/`: V3Boot, V3MainMenu, V3SaltwindRun, "V3SaltwindRun UI STuff", V3PvPBattle, V3CoOpRun, V3SplitScreenCoOp, V3MapGenerator (5.2 MB). 8.8 MB in total.
- Other `_Game` scenes (V3 era): `Scenes/Boot.unity`, `MainMenu.unity`, `Run.unity` (708 KB), `Run_ENV.unity` (952 KB), `CursedMap.unity` (560 KB), `Scenes/Promo/Teaser_Surrounded.unity`, and `Scenes/Tests/Test_Combat`, `Test_Performance`, `Test_Tuning`, `Test_WeaponEnemy`.
- `Assets/Scenes/Main.unity`: legacy, references CutlassSloop.
- `Assets/InitTestScene78ed01b2-....unity`: a stray untracked test-runner scene.
- Third-party demo scenes under `FlexUnit/`, `RainMaker/`, `Stylish Cannon Pack/`, `Stylized Water For URP/Demos`, `StylizedCharacterPack/Demo`, `JMO Assets`, `LineRendererShader`, `IgniteCoders`, `GabrielAguiarProductions`, `Space`, `StormOcean`: out of scope, not V3.
- `ProjectSettings/EditorBuildSettings.asset` lists **14 non-V4 scenes** (v3 x7, Boot, MainMenu, Run, Test_* x4) after the 5 V4 ones. Remove them in the same commit, or the build breaks.
- The non-V4 scenes use 52 distinct Scripts files, 6 of them under `Scripts/V3`.

### 4e. Things outside Scripts that reference V3

- `Assets/_Game/Simulation/` (RoguePirates.Simulation): **keep**. It has no HighSeas reference and is used by V4 (`V4/Runtime/Core/RunClock.cs`, `V4Game.cs`) and by kept Balance files (StageBalanceProfile, SaltwindFinaleLogic...). The deleted V3 Balance files (RunMarksTracker, SaltwindLabTraceParityLogic, SaltwindMarksUnlockLogic, SaltwindStageSettlementLogic, SaltwindStageTimelineLogic, SaltwindAmbientCompositionLogic) also use it. Check afterwards whether Simulation types become orphaned; RoguePirates.Tests.EditMode (BalanceGateTests, BalanceLabRunnerTests, ProfileBalanceMatrixTests) still covers them.
- `plan/saltwind-verification/*.eval.cs` (author-step04, check-db, editmode-errors, export-profile, logic-check, type-check) and `RERUN_INDEPENDENT_LAB.ps1`/`balance-lab/`: V3 balance-lab tooling that references HighSeas types. It becomes dead historical evidence.
- `plan/v4-verification/tools/run-trace.ps1` (usage example `-Scene Assets/_Game/Scenes/v3/V3SaltwindRun.unity`), `playable-check-m0..m8.ps1`, `playable-check-playtest-m1-m3.ps1`, and the V3 regression result JSONs in phase-00..03: these reference V3 scenes. **The V3-comparison trace set becomes impossible after M15.** The golden V3 traces already stored in `plan/v4-verification/phase-*/v3-trace-*.jsonl` stay usable only if TraceComparer and the V4 trace recorder keep their V4-side format.
- `tools/build-webgl.bat`, `tools/bootstrap-highseas.bat`: call HighSeas.Editor methods (see 4a).
- `Packages/manifest.json:3` `"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main"`: optional per AGENTS.md; unrelated to V3 code. Removal is a separate decision.

### 4f. Data, prefab and art folders

How I judged: a full GUID closure from (V4 scenes + `V4/Data` + `V4/Resources` + the 12 `_Game/Resources` assets that kept code loads by path) = 722 assets, compared with the closure of all non-V4 scenes plus the V3-only Resources loads. Code paths (`AssetDatabase` paths in editor tools, `Resources.Load` strings) were checked by grep.

| Folder | V4-reached | only non-V4 | unreferenced | Verdict |
|---|---|---|---|---|
| `_Game/Data/Enemies` | 14 | 0 | 0 | keep (via GameDatabase) |
| `_Game/Data/Weapons`, `Relics`, `Logbooks`, `Ships` | 6 / 12 / 10 / 1 | 0 | 0 | keep (via GameDatabase; V4 weapon behaviour comes from V4 configs, but the definitions are still loaded) |
| `_Game/Data/SpawnProfiles` | 7 (via V4EncounterOnlySchedule only) | 5 | 1 (`Shark Spawn Profile.asset`) | V3 plus the migrator; delete with V4EncounterOnlySchedule once V4 runtime is confirmed not to read SpawnProfile assets (it reads the `SpawnProfile` *type* in code) |
| `_Game/Data/V3/V3SaltwindIslandLayout.asset` | 1 (serialized in V4 run scene) | | | keep (rename folder later; it is a *different* GUID from the Resources copy) |
| `_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset` | 0 | 1 | | V3-only |
| `_Game/Data/*.asset` root (DemoJuiceSettings, GameDatabase, DemoIslandLayout, EncounterSpawnSchedule, LevelUpCardArtSettings, V3*Settings ...) | 0 | 11 | 7 | **Duplicates of the `_Game/Resources` copies** with different GUIDs. V4 uses the Resources copies. Delete candidates, after checking that no editor tool loads them by path (HighSeas.Editor setups do). |
| `_Game/Resources` | 12 | 4 (DemoPacingSettings, EncounterSpawnSchedule, LevelUpSettings, V3MainMenuTypographySettings) | 6 (`CharacterPoses/*.png`: V3 path-loaded) | keep the 12, delete the 10 |
| `_Game/Prefabs/Environment` | 29 | 0 | 33 (`Island_10/12/14/15...`) | keep the 29; the 33 are unreferenced by any scene (may be editor-bake inputs; `HighSeasIslandSetup` / MapGeneratorWorkbench) |
| `_Game/Prefabs/Player` | 1 | | | keep |
| `_Game/Prefabs/V3/PlayBoundsWallSegment.prefab` | 0 | 0 | 1 | delete (V4 loads the `_Game/Resources` copy) |
| `_Game/Prefabs/Enemies`, `POI`, `Pickups`, `VFX` | contain no assets | | | empty folders |
| `_Game/Art/Captain icon, HP & XP`, `Character Poses`, `Minimap`, `Upgrade Cards` | 13 / 6 / 2 / 10 | | | keep |
| `_Game/Art/Character Card` | 13 | | 5 | keep; 5 unused variants |
| `_Game/Art/Items` (19), `Weapons` (18), `Level detail` (8), `Sails` (5), `Stats and quest` (9) | 0 | 0 | all | Not referenced by any scene or asset. Check `V4IconCatalog.asset`: it reaches 30 assets, none of them in these folders. Possibly future art; not V3 code. |
| `_Game/Materials/V3` | 1 | | | keep (rename) |
| `Assets/UI/Prefabs/*` (Canvas, Weapon_Slot, Quest, Health_Slider, Coin_Count, Profile, TImer, Weapon_powerup) | reached by the V4 run scene | | | keep; strip V3AuthoredGameplayHud from Canvas.prefab |

## 5. Risks and ordering notes

1. **Asmdef cycle, so the order matters.** V4 assemblies reference `HighSeas.Runtime`, and `V4StructureTests` forbids the reverse. Kept files cannot move into `RoguePirates.V4*` while V3 code in HighSeas.Runtime still references them: HighSeas.Runtime would then need V4, which is a cycle. Safe orders:
   - (a) First delete all V3-only consumers, the Legacy ports, the V3 parity tests, the V3 editor tools and the scenes, while everything still sits in HighSeas.Runtime. Strip the cut lines from 3a and compile green. Then move or rename the surviving 119 files into V4 assemblies in a second commit.
   - (b) Rename `HighSeas.Runtime` in place (for example to `RoguePirates.V4.Shared`, keeping the folder), then fold it in.

   Either way, the 6 V4 asmdefs that list `HighSeas.Runtime` (Runtime, Presentation, DevTools, Editor, V4.Tests.EditMode, V4.Tests.PlayMode; Editor also lists `HighSeas.Editor`) and `RoguePirates.Presentation` must change in the same step. The V4 structure test also asserts on `Scripts/HighSeas.Runtime.asmdef` by path (V4StructureTests.cs:113), so update it with the move.
2. **Target assembly for kept ScriptableObject types.** `EnemyDefinition`, `SpawnProfile`, `GameDatabase` and the rest cannot go into `RoguePirates.V4.Config`, which is forbidden to reference HighSeas and holds only V4 configs. Put them in `RoguePirates.V4` (Runtime), or in a new shared data assembly under Config if Config types ever need them. MonoBehaviours in scenes (RunManager, PlayerShipController, DamageReceiver, StageBuilder, FollowCamera, VirtualJoystick, LevelUpCardView, DemoIslandMarker, IslandCollisionAuthoring) must go into an assembly the Player build includes: Runtime or Presentation, not DevTools or Editor.
3. **GUIDs.** Move each `.cs` with its `.meta` (git mv or the Unity Editor) so the `m_Script` GUIDs in 2d stay valid. Assembly and namespace changes do not break `m_Script` references, and there is **no `[SerializeReference]`** in Scripts or V4 and no `asm: HighSeas` managed-reference entry in any asset (checked). `m_EditorClassIdentifier: HighSeas.Runtime::HighSeas.Enemies.EncounterSpawnSchedule` (for example in V4EncounterOnlySchedule.asset:14) is informational only and harmless. No `Type.GetType("HighSeas...")` strings exist in runtime code. The only string type names are in V4EnemyWorldM8Tests.cs:249-250 (scene-text asserts) and V4StructureTests.
4. **File-name/class-name mismatch.** `UI/UiCanvasConfigurator.cs` declares the MonoBehaviour `UiScaleToFitParent`, whose name does not match the file. It is added only at runtime via AddComponent, so it works, but it cannot be serialized. If it is moved, split it into its own `UiScaleToFitParent.cs` (new GUID; nothing serializes it today, so that is safe). If `SpatialHash` is split out of EnemyWorld.cs, give it its own file (not a MonoBehaviour, so safe).
5. **Duplicate type names.** Kept `GameEnums` (EnemyTier, EnemyBehavior, EnemyCategory, EliteAffix, TargetingMode, UpgradeAxis, WeaponPattern), `SpawnProfileEnums` (11 enums) and `EnemyCombatProfile.SaltwindCombatArchetype` share names with enums nested in `RoguePirates.V4.Config.ConfigEnums`. That is safe today because the V4 ones are nested, but a namespace merge or `using static ConfigEnums` in the same file would make them ambiguous. V4 code casts between them by int (for example V3ConfigMigrator.cs:323), so the numeric values must stay identical.
6. **Static singletons and static state in kept code:**
   - `AppRoot` (`[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` AutoBootstrap, AppRoot.cs:20; it creates the SaveService, ConfigService and AudioService)
   - `RunManager.Instance`, `SfxService.Instance`, `RunCombatSignals.Instance` (never created in V4)
   - static fields in `PlayBounds` (13), `StageBalanceProfileContext` (7), `V3PlaytestFlags` (7), `IslandObstacles` (4), `V3MinimapUiSprites` (10 caches), `CombatExperimentProfile` (5), `V3ContentUnlocks`, `V3HomeSeed`, `EnemyVisualFallbackResolver`, `IslandSpawnExclusionRegistry`

   Enter Play Mode Options are enabled with flags 0 (EditorSettings.asset:27-28), so domain reload still runs. There is no reset risk today, but keep it in mind if domain reload is ever disabled. The deleted V3 singletons (V3CaptainActiveController, V3RunItemEffects, V3RunItemInventory, V3RunContext, RunEncounterTracker, RunMarksTracker, TriggerBus) are always null or inactive in V4 runs. V4 tests reset several of them in SetUp, so removing them changes nothing at runtime.
7. **PlayerPrefs keys.** `V3ContentUnlocks.FirstConquestKey`, `V3CaptainSelection` (the saved Captain) and `V3HomeSeed` store player progress under V3 key names. If these classes are renamed, keep the key strings, or players lose their unlocks, saved Captain and home seed.
8. **Live gameplay hidden in "V3" files.** `V3StagePoiPlacer`/`V3StagePoiPlacementOptions`/`V3SpawnAnchorValidator` (POI, barrel and spawn placement on the map RNG stream) and `V3PlaceholderFactory` (POI and powerup markers, UI panels and buttons) run in every V4 run. Deleting `Scripts/V3/**` wholesale would break stage generation. Also, `SaltwindStageEconomyLogic.ResolveKillGoldDrop` is V4's kill-gold rule (KillFanout.cs:52).
9. **Behavioural parity when cutting always-null branches.** The DamageReceiver multipliers, RunManager's Marks/settlement/telemetry calls and PlayerShipController's multiship branch are null or false in V4 runs, so removing them is behaviour-neutral. Confirm with targeted tests, not full traces. `SaltwindRunSettlement.TrySettleCurrentRun` (RunManager.cs:167) writes to the save when active; V4RunPlayModeTests.cs:784 already asserts it never applies.
10. **Trace tooling.** Per the phase-gating memory, M15 is infrastructure deletion, so no trace set is required. The V3-scene golden paths in RunTraceRecorder, PlayerPilot and TraceBoot, and `run-trace.ps1`'s V3 example, lose their target. The stored `v3-trace-*.jsonl` files stay readable by TraceComparer.
11. **Missing-script cleanup.** Strip `V3AuthoredGameplayHud` from `Assets/UI/Prefabs/Canvas.prefab` in the Editor when deleting it (2e), and remove the 14 non-V4 scenes from EditorBuildSettings.
12. **Nested test assembly.** `Tests/EditMode/RoguePirates/` (non-V3) sits inside the V3 test folder `Tests/EditMode/`, so a blanket folder delete would remove it.
