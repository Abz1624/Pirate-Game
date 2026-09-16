# Combat Redesign — Repository Mapping (Step 1)

Engineering note mapping existing systems to `Rogue_Pirates_Cursor_Combat_Redesign_v0_2.md`.

## Arsenal capacity

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Core/GameConstants.cs` | `DefaultWeaponSlots = 6` |
| `Assets/_Game/Scripts/Weapons/WeaponController.cs` | Runtime equip cap (`maxSlots`, `EquipWeapon`) |
| `Assets/_Game/Scripts/UI/LevelSystem.cs` | Owned weapons list, offer gating by `weaponSlots` |
| `Assets/_Game/Scripts/UI/RunHud.cs` | 6-slot weapon strip (legacy run HUD) |
| `Assets/_Game/Scripts/V3/V3RunLoadoutHud.cs` | V3 bottom loadout bar |

## Offer generation

| File | Role |
|---|---|
| `Assets/_Game/Scripts/UI/LevelOfferGenerator.cs` | 3-card rolls, first-level scripting, fallback cards |
| `Assets/_Game/Scripts/UI/LevelSystem.cs` | V3 simplified offers vs legacy generator |
| `Assets/_Game/Scripts/Data/LevelUpSettings.cs` | Cards per level, rarity line counts |

## Weapon definitions

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Data/WeaponDefinition.cs` | ScriptableObject schema (pattern, targeting, proc) |
| `Assets/_Game/Data/Weapons/*.asset` | Six baseline weapons |
| `Assets/_Game/Scripts/Core/GameDatabaseLoader.cs` | Runtime fallback database |
| `Assets/_Game/Scripts/Combat/CombatWeaponCatalog.cs` | Experimental 12-weapon pool (added by redesign) |

## Targeting

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Core/GameEnums.cs` | `TargetingMode` enum |
| `Assets/_Game/Scripts/Combat/WeaponTargeting.cs` | Reusable arc/cluster queries (added) |
| `Assets/_Game/Scripts/Enemies/EnemyWorld.cs` | Spatial hash + nearest queries |
| `Assets/_Game/Scripts/Weapons/WeaponController.cs` | Pattern dispatch + fire methods |

## Spawn director

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Enemies/SpawnDirector.cs` | Threat budget spawning, V3 pacing |
| `Assets/_Game/Scripts/Enemies/SpawnDirectorSettings.cs` | Wave script + enemy tuning |
| `Assets/_Game/Scripts/V3/V3PressureBeatDirector.cs` | Minute 3/4/7/8 beats |
| `Assets/_Game/Scripts/V3/V3OvertimeDirector.cs` | Post-10:00 overtime spawns |

## Captain config

| File | Role |
|---|---|
| `Assets/_Game/Scripts/V3/V3CaptainProfile.cs` | Rook/Hank/Richie/Sparky/Ember/Roxie stats |
| `Assets/_Game/Scripts/V3/V3CaptainController.cs` | Passive rule application |
| `Assets/_Game/Scripts/V3/V3CaptainSelection.cs` | PlayerPrefs captain pick |

## Enemy definitions

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Data/EnemyDefinition.cs` | HP, behavior, `threatCost` |
| `Assets/_Game/Data/Enemies/*.asset` | Seven baseline families + extras |
| `Assets/_Game/Scripts/Enemies/EnemyWorld.cs` | Runtime agents, movement, damage |

## Damage / proc

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Combat/DamageSystem.cs` | Roll damage + proc coefficient |
| `Assets/_Game/Scripts/Combat/DamageProcKind.cs` | Crit/proc eligibility |
| `Assets/_Game/Scripts/Combat/WeaponDamageLedger.cs` | Per-weapon contribution (added) |

## VFX / caps

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Platform/QualityProfile.cs` | Enemy/projectile caps by quality tier |
| `Assets/_Game/Scripts/Weapons/ProjectileWorld.cs` | Projectile pool |
| `Assets/_Game/Scripts/VFX/VfxPool.cs` | Particle pooling |

## Serialization

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Save/SaveService.cs` | Meta-only PlayerPrefs save |
| `Assets/_Game/Scripts/Core/RunManager.cs` | In-memory run state (not snapshotted) |

## Telemetry / post-run stats

| File | Role |
|---|---|
| `Assets/_Game/Presentation/Telemetry/GameplayTelemetry.cs` | Generic event buffer |
| `Assets/_Game/Scripts/Combat/RunCombatTelemetry.cs` | Combat redesign metrics (added) |
| `Assets/_Game/Scripts/V3/V3RunResultsScreen.cs` | End-of-run summary UI |

## Experiment framework (added)

| File | Role |
|---|---|
| `Assets/_Game/Scripts/Combat/CombatExperimentProfile.cs` | Profiles A/B/C/D + runtime context |
| `Assets/_Game/Resources/CombatExperiment/*.asset` | Optional authored profile assets |
