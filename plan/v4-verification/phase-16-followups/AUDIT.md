# M16 audit: content-id literals in the 17 exempt Foundation files

Read-only audit of branch `v4-modular-architecture` (HEAD 4e74a42a). All paths are relative to `Pirate Game Unity/Assets/_Game/`; Foundation files are under `V4/Foundation/`.

**How callers were found.** `RoguePirates.V4.Foundation` is referenced only by V4 assemblies (Runtime, Presentation, DevTools, Editor) and the V4 test assemblies. No non-V4 asmdef references it, and `Assets/Scripts` and `Assets/Editor` do not use it. So "callers" below is the complete set.

**What the test flags.** The check (`Tests/V4/EditMode/V4EnemyM2Tests.cs:199-232`, regex `"([a-z0-9_]+)"`) flags only literals equal to an enemy, variant or weapon config id. These files also hold content ids the test does not flag:
- weapons with no config: `burning_oil`, `grapeshot`, `boarding_party`
- item ids: `grapeshot_pouch`, and others
- evolution ids: `stormshot_cannon`, and others
- captain ids: `brine`, `flint`

Remove those too in the same edits.

---

## 0. The GameDatabase path (what V4 still reads)

**Loading**
- `Core/GameDatabaseLoader.Load()` loads `Assets/_Game/Data/GameDatabase.asset` in the Editor (AssetDatabase) and `Resources/GameDatabase` in a player build. If the asset is invalid it falls back to `CreateRuntimeFallback()`.
- Both assets exist and pass `IsValid`. `V4SaltwindRun.unity:5782` and `V4ModuleLab.unity:5782` serialize `V4Game.database: {fileID: 0}`, so `RunDatabaseLoader.Load` (`Runtime/Ports/RunDatabaseLoader.cs:26-31`) always calls `GameDatabaseLoader.Load()`.

**The two assets differ**
- `Data/GameDatabase.asset` (Editor): 14 enemies then 64 null slots; 6 weapons then 44 nulls.
- `Resources/GameDatabase.asset` (player): 14 enemies with no nulls, in a different order (swordfish moved from first to last); 6 weapons then 8 nulls.
- So the enemy slot layout already differs between the Editor and a build. V4 gameplay therefore cannot depend on exact definition indices across builds. Editor traces, however, may record indices.

**Run bootstrap order** (`Runtime/Composition/RunBootstrapSequence.cs:136-150`)
1. `_stageProfile.TryBegin` builds the profile export and content hash. `ToExport` reads the GameDatabase through `WeaponStatExportLogic` and `BossStatExportLogic`.
2. `_database.ApplyCatalogs()` runs `CombatWeaponCatalog` and `CombatSpecialistEnemyCatalog` (`RunDatabaseLoader.cs:34-39`). They mutate the loaded asset in memory: they edit its weapon and enemy definitions and replace its arrays.
3. `V4RuntimeDatabase.Build(loaded)` (`Runtime/Ports/V4RuntimeDatabase.cs:75-96`):
   - `Object.Instantiate`s the database.
   - Copies each non-null enemy slot, then `Write(id)` → `Apply` overwrites **every** `EnemyDefinition` field from `EnemyArchetypeConfig` / `EnemyVariantConfig`.
   - Appends the V4-only enemies (`inLegacyDatabase == false` archetypes and registry elites, ordinal id order).

**Which parts V4 still reads**

| Part | Read by | Status |
|---|---|---|
| `enemies` | `V4RuntimeDatabase` (slot layout and the set of ids; every value is overwritten by config) → `EnemyWorldModule.ResetAndDefine` (`Runtime/Composition/EnemyWorldModule.cs:150-154`), `Spawners.BindEnemies` (`RunBootstrapSequence.cs:151`). `BossStatExportLogic` reads the pristine `barnacle_king` definition for the hash. | Live. Only the layout and the id set matter; values come from config. The test asserts that every enemy has a config (`V4EnemyM2Tests.cs:55-73`, 22 non-null = 14 + 8 aliases). |
| `weapons` | Only `WeaponStatExportLogic` (content hash). `BuildModule` stores `Func<GameDatabase> _database` (`BuildModule.cs:36,59`) but never reads it. The V4 weapon systems read `WeaponConfig`. | Hash only. |
| `ships` | `Player/PlayerShipVisualBootstrap.Awake` (`ships[0].visualPrefab`), added by `Runtime/Ports/WorldPort.cs:40-41`. | Live (visual fallback). |
| `logbooks`, `relics` | Nothing in V4 (config replaced them). | Not read. |

**Is the catalogs' output still consumed?**
- **Weapon catalog.** It edits the stats (range, arc, proc, targeting) of the 7 V3 weapons and adds `swivel_guns` (plus `long_nine`, `harpoon_gun` and `mortar` when `weaponSlotCap > 2`, which is the default 6). The only reader of the database weapons is the stage-profile hash. The run's first hash is taken *before* the catalog runs. Later runs in the same session (the asset stays mutated in memory) snapshot the mutated or added weapons, so their hash differs. This is register R-73; `V4/Editor/V4TraceSession.cs:127-135` unloads the asset between traces for this reason.
  - Every stat the catalog sets already equals `WeaponConfig`. Checked: deck 22/360/1.0; broadside 14/50/0.7; kegs 12/0/0.55; cutlasses 6/360/0.35; lightning 16/360/0.5; flame 9.5/52/0.2; long_nine 28/2.4/24/40/1.0 with preferHighestHull; swivel 6/0.6/16/360/0.35; harpoon 16/1.2/16/75/0.8; mortar 22/1.8/18/360/0.8, size 4.25.
  - **Gameplay consumption: none.**
- **Specialist catalog.** It appends the 8 alias definitions after the loaded slots, which is what makes those ids exist in the runtime database; `V4RuntimeDatabase` has no alias-creation code. It also retunes manta_ray and reef_crab_miniboss in the asset. Every value is overwritten by `EnemyVariantConfig` / `EnemyArchetypeConfig` in `Write`.
  - **Consumed: yes, for the existence and slot position of the 8 alias ids. The values are not consumed.**

---

## Files, easiest to hardest

### 1. `Combat/WeaponDisplayNames.cs`: DEAD
- **Literals:** lines 8-20, a display-name switch (10 config weapons plus the unflagged `burning_oil`, `grapeshot`, `boarding_party`).
- **Callers:** only `Balance/SaltwindRunResultsLogic.cs:59`, inside `BuildWeaponContributionLines` (lines 42-64), which has no callers in V4 or the tests. `WeaponContributionLine` (line 9) is used only there. `Runtime/Views/RunViewSet.cs:143` already uses the weapon configs' names (M14.11).
- **Change:** delete `WeaponDisplayNames.cs`, and delete `BuildWeaponContributionLines` and `WeaponContributionLine` from `SaltwindRunResultsLogic.cs`. `WeaponDamageLedger` stays; the Runtime uses it.
- **Risk:** none (compile-checked deletion).

### 2. `Enemies/EnemyRangedAttackRules.cs`: DEAD
- **Literals:** lines 13-17: `cannonboat`, `gunner_cog`, `bomb_boat`, `manta_ray`, `drowned_harrier` → "always ranged".
- **Callers:** only `Enemies/EnemySpawnRuntimeParams.cs:208`, inside `SpawnProfileResolver`, which is dead (file 3). The Runtime has its own port driven by the `alwaysRanged` config flag: `Runtime/Enemies/EnemySimulation.Tick.cs:816-817` and `Runtime/Enemies/EnemyCombatProfiles.cs:226-227`.
- **Change:** delete the file.
- **Risk:** none.

### 3. `Enemies/EnemySpawnRuntimeParams.cs`: partly dead
- **Literal:** line 206: `enemy.id == "cannonboat"` → SingleShot, in `SpawnProfileResolver.ResolveAttack`.
- **Callers:** the `EnemySpawnRuntimeParams` **struct** (lines 8-65) is live. It is used by `Runtime/Enemies/EnemyUnit.cs:35`, `EnemyPatternMovement.cs:11`, `EnemySpecialBehaviors.cs`, and `EnemyCombatProfiles.cs:96-183`. The static class **`SpawnProfileResolver`** (lines 67-233) is called only from `Combat/EnemyCombatProfileRegistry.cs:32,35`, which is dead. The Runtime re-implemented it in `Runtime/Enemies/EnemyCombatProfiles.cs:65-230`; its "cannonboat single shot" rule is data (tooltip at `Config/Schemas/EnemyArchetypeConfig.cs:69`).
- **Change:** delete `SpawnProfileResolver` and keep the struct. This file then has no literal.
- **Risk:** none.

### 4. `Combat/EnemyCombatProfileRegistry.cs`: DEAD
- **Literals:** lines 106-195: the whole V3 combat-profile table (22 ids plus the elite_hammerhead and elite_kraken_spawn derivations).
- **Callers:** `ResolveRuntimeParams` is called only from the dead `SpawnProfileResolver` (`EnemySpawnRuntimeParams.cs:80`). `UsesSaltwindBehavior` has no caller. `TryGet` is used only by the test `Tests/V4/EditMode/V4EnemyM2Tests.cs:123` ("never touch the V3 registry"). The run uses `Runtime/Enemies/EnemyCombatProfiles` built from the config `combat` blocks.
- **Change:** delete the file. Rewrite `V4EnemyM2Tests.cs:120-135` so it no longer compares against the registry, keeping only the "per-run copies and live edits" half. The `EnemyCombatProfile` class (`Combat/EnemyCombatProfile.cs`) stays; the Runtime uses it.
- **Equality proof (optional, before deleting):** a one-off test that, for every id in the registry, `FieldMirror`-compares `Profiles[id]` with `new EnemyCombatProfiles(config).TryGet(id)`. M14 already ported the profiles, so this only archives the proof.
- **Risk:** none for gameplay.

### 5. `Combat/WeaponEvolutionCatalog.cs`: DEAD once ContentUnlocks is cleaned (file 9)
- **Literals:** lines 36-46: evolution → base weapon, required item, name and description (6 rows).
- **Callers:**
  - `Balance/SaltwindStageProgressionLogic.cs:120` (`IsRunItemOfferEligible`), whose only caller is `Progression/ContentUnlocks.cs:61` (`IsItemUnlocked`), which has **no callers**.
  - The test `Tests/V4/EditMode/V4WeaponM5Tests.cs:237` compares the table with `WeaponEvolutionConfig`, and it passes today.
  - The live V4 equivalent is `Runtime/Stage/ChestEconomy.cs:152-172`, which reads `WeaponEvolutionConfig.requiredItemId` / `baseWeapon` and `UnlockConfig.starterWeaponIds`. `Runtime/Progression/OfferGenerator.cs:233` also uses config.
- **Change:**
  1. Delete `ContentUnlocks.IsItemUnlocked` and `IsLogbookUnlocked` (no callers).
  2. Delete `SaltwindStageProgressionLogic.IsRunItemOfferEligible` and `IsWeaponAccessibleForProgression` (only caller: `IsItemUnlocked`).
  3. Delete `WeaponEvolutionCatalog.cs`.
  4. Change `V4WeaponM5Tests.Evolutions_AreConfig_...` to assert the 6 config assets directly: the literals move into the test, which is allowed.
- **Risk:** none.

### 6. `Data/ShipDefinition.cs`: no reader
- **Literal:** line 15: field initializer `startingWeaponId = "swivel_guns"`.
- **Callers:** `ShipDefinition.startingWeaponId` is never read in V4. The grep hits are `CaptainConfig.startingWeaponId`, a different class, and `GameDatabaseLoader.cs:52`. The ship asset's serialized value overrides the initializer anyway.
- **Change:** drop the initializer (or the field). Delete `GameDatabaseLoader.cs:52` together with it.
- **Risk:** none.

### 7. `Core/GameDatabaseLoader.cs`: fallback unreachable with the shipped assets
- **Literals:** lines 48-52 (ship), 60-65 (weapons), 124-146 (enemies). The logbook and relic ids are unflagged but still content ids. All are in `CreateRuntimeFallback`, which only runs when the asset is missing or invalid.
- **Callers of `Load()`:**
  - `Runtime/Ports/RunDatabaseLoader.cs:30`
  - `Balance/BossStatExportLogic.cs:16`, `Balance/WeaponStatExportLogic.cs:18`
  - `Player/PlayerShipVisualBootstrap.cs:19`
  - `V4/Editor/V4TraceSession.cs:133`
  - tests: `V4EnemyM2Tests.cs:42`, `V4EnemyTickM8Tests.cs:56`, `V4EnemyWorldM8Tests.cs:49`, `V4WorldViewsM14Tests.cs:277`
- **Is the fallback ever used?** Never with both assets present and valid.
- **Change:** delete `CreateRuntimeFallback` and its helpers; `Load()` logs an error and returns `null`. Every caller already handles null: `RunBootstrapSequence.cs:97` fails the run with "GameDatabase is missing", `WeaponStatExportLogic` returns an empty list, `BossStatExportLogic` returns an id-only snapshot, and `PlayerShipVisualBootstrap` returns.
- **Proof:** add an EditMode test that `AssetDatabase.LoadAssetAtPath<GameDatabase>(AssetPath)` and `Resources.Load<GameDatabase>("GameDatabase")` both pass `IsValid`.
- **Risk:** a build missing `Resources/GameDatabase` would now fail to start instead of running on stub data. That is acceptable, and the test guards it.

### 8. `Balance/StageBalanceProfile.cs`: initializers are always overwritten
- **Literals:** lines 121-126 (`starterWeaponIds` defaults) and 131-134 (`weaponUnlocks` defaults). Captain ids at 139-140 (`brine`, `flint`) are unflagged.
- **Callers:** V4 creates the profile with `CreateInstance` in `Runtime/Ports/StageProfilePort.cs:52-58`. `Fill` → `FieldMirror.CopyFields(stage.sharedProfile, p)` (`StageProfilePort.cs:66`; `Config/Core/FieldMirror.cs:16-48`) overwrites `starterWeaponIds`, `weaponUnlocks` and `captainUnlocks` with `StageContentConfig.sharedProfile`. The asset (`Data/Stage/StageContent_Saltwind.asset:51-71`) holds exactly the same lists and costs. Readers:
  - `ToExport` (line 258-260 → hash), `StageBalanceProfileValidator.cs:62-65`
  - `WeaponStatExportLogic.cs:22-28`
  - `SaltwindStageProgressionLogic.IsWeaponInOfferPool` (via `export.StarterWeaponIds`, reached from `ContentUnlocks`)
  - `CreateBuiltInSaltwindDefaults` (lines 311-320) has no callers: dead.
- **Change:**
  - Initialize the three arrays to `Array.Empty<…>()`.
  - Delete `CreateBuiltInSaltwindDefaults`.
  - Optionally drop `DefaultSaltwindSoloId` / `profileId` defaults; these are not content ids and are not flagged.
- **Risk:** only a config set with no `StageContentConfig` would differ; the config validator requires the stage config.
- **Proof:** the pinned hash test `Tests/V4/EditMode/V4ConfigM1Tests.cs:52-63` (`07c5c03e…`) must stay green.

### 9. `Progression/ContentUnlocks.cs`: V3 fallback unreachable in V4 runs
- **Literals:** lines 34, 38-45 and 51: a V3 unlock table (first-conquest gating, Flint gets powder kegs, experimental playtest weapons).
- **Callers:**
  - `IsWeaponUnlocked` is called from `Runtime/Ports/WorldPort.cs:146` (`ContentPort.IsWeaponUnlocked`) → `Runtime/Composition/BuildModule.cs:376` → `Runtime/Progression/ProgressionModule.cs:356`, and from `WorldPort.cs:149` (`IsWeaponOfferEligible`, only when `ActiveRun.IsActive`).
  - `HasCompletedFirstConquest` / `FirstConquestKey` are live (`WorldPort.cs:142,144`; `SharedPorts.cs:86`). They are not content ids; keep them.
- **Why the table is unreachable:**
  - In V4, `ActiveRun` is set at `RunBootstrapSequence.cs:105`, just before `StageProfilePort.TryBegin` (`:136`). Both are cleared together (`Foundation/Core/AppRoot.cs:44-48`), and offers exist only inside a run.
  - So `StageBalanceProfileContext.IsActive` is always true when `IsWeaponUnlocked` runs. The result is then the export path: `export.StarterWeaponIds`, which equals `UnlockConfig.starterWeaponIds` exactly (`Data/Build/Unlocks_Saltwind.asset`), or the save owns the weapon.
  - Lines 33-51 (the table and `IsExperimentalPlaytestWeapon`) never execute.
- **Change:**
  1. Replace lines 33-47 with `return false;` (or `return AppRoot.Instance?.Save?.HasEffectiveContentOwned(weaponId) ?? false;`).
  2. Delete `IsExperimentalPlaytestWeapon`, `IsItemUnlocked` and `IsLogbookUnlocked` (see file 5).
  3. Better still: move `IsWeaponUnlocked` into `ContentPort`, reading `UnlockConfig.starterWeaponIds` instead of the profile export. The lists are identical; add a test that `sharedProfile.starterWeaponIds == UnlockConfig.starterWeaponIds`.
- **Risk:** none in V4 runs.
- **Proof:** a PlayMode assertion or test that `StageBalanceProfileContext.IsActive` whenever `ContentPort.RunContextActive` during a run. No test calls the real path with the context inactive; `V4M9GateFixTests.cs:60` uses a stub.

### 10. `Balance/WeaponStatExportLogic.cs`: fallback unreachable
- **Literal:** line 32: `if (ids.Count == 0) ids.Add("deck_cannons")`.
- **Callers:** `StageBalanceProfile.ToExport` (line 263) ← `StageBalanceProfileContext.BeginRun` (`Balance/StageBalanceProfileContext.cs:37`) ← `StageProfilePort.TryBegin` / `OnConfigChanged`, and the tests `V4ConfigM1Tests.cs:62` and `V4SpawnM3Tests.cs:769`.
  - The snapshot feeds only the content hash. It reaches the run header (`StageProfilePort.Identity`) and the results footer (`SaltwindRunResultsLogic.FormatProfileFooter`).
  - Its `WeaponStatSnapshots` are consumed only by Simulation balance-lab code, which V4 does not call.
- **Is the fallback reachable?** The ids are never empty: the shared profile has 6 starters and the validator requires at least one.
- **Change:** delete line 32 and keep reading the pristine GameDatabase weapons, so the hash is unchanged.
  - Longer term, snapshot `WeaponConfig` instead in the Runtime. That changes the hash and needs a deliberate re-pin plus new trace headers, so do not do it in this phase.
- **Risk:** none. The pinned hash must stay the same.

### 11. `Enemies/EnemyVisualFallbackResolver.cs`: live code, the table is never reached with shipped config
- **Literals:** lines 12-19: a proxy prefab and scale for 8 enemies.
- **Callers:**
  - `ResolveVisualScale`: `Runtime/Enemies/EnemyViewForwarder.cs:38`, `Presentation/Effects/EnemyViewPresenter.cs:60`, test `V4EnemyTickM8Tests.cs:107`. Invoked from `Runtime/Enemies/EnemySimulation.cs:223-224` **only when `def.visualPrefab == null`**.
  - `ResolvePrefab`: `EnemyViewPresenter.cs:65`, also only when `visualPrefab == null`.
- **Is the null case reached?** Every archetype asset has a `visualPrefab`. Aliases inherit the base model (`inheritBaseVisual: 1`), registry elites use the base model, and V4-only enemies get a collider-free copy. So neither call ever hits the table.
- **Existing config:** the archetype configs already carry the proxies exactly (prefab GUIDs verified against `Assets/GameObject/*.prefab.meta`):
  - cutthroat_skiff: SmuggleBoat, 0.92
  - deckhand_dinghy: SmuggleBoat, 0.82
  - gunner_cog: CannonboatNavy, 1
  - bomb_boat: FireBoat, 1
  - rammer_raft: RamShip, 1
  - boarding_barge: RamShip, 1.12
  - drowned_pursuer: SmuggleBoat, 1.05
  - drowned_harrier: CannonboatNavy, 0.95
- **Change:** `ResolvePrefab` → `def?.visualPrefab`; `ResolveVisualScale` → `def.visualScale > 0.01f ? def.visualScale : 1f` (or inline both and delete the file).
- **Risk:** a live dev-console edit that clears a model would now show the placeholder cube at scale 1 instead of the proxy. That is dev-only.
- **Proof:** a test that every definition in the runtime database has `visualPrefab != null`, plus a test comparing the old table with the archetype visual prefab and scale per id before deleting.

### 12. `VFX/VfxParticleUtil.cs`: live, the literal is unreachable with the shipped asset
- **Literal:** line 101: `return binding.weaponId != "harpoon_gun";` in `UsesCannonballTrail`.
- **Callers:** `Presentation/Weapons/ProjectileViewPresenter.cs:56` and `Presentation/Effects/HostileShotPresenter.cs:46` (live visuals).
- **Is line 101 reachable for the harpoon?** In `Resources/DemoJuiceSettings.asset`, the `harpoon_gun` binding's `projectilePrefab` (guid `1a44c40e…`, line 118) **is** `harpoonProjectilePrefab` (line 134). Line 90 therefore returns false before the loop, and the harpoon comparison can never decide anything.
- **Change:** line 101 → `return true;`, or `return binding.projectilePrefab != settings.harpoonProjectilePrefab;` for robustness. Add a test that the harpoon binding's prefab equals `harpoonProjectilePrefab`.
- **Risk:** it differs only if someone unassigns `harpoonProjectilePrefab`. Presentation only.

### 13. `Balance/BossStatExportLogic.cs`: live (content hash)
- **Literal:** line 10: `DefaultSaltwindBossId = "barnacle_king"`, used as the default argument at line 14.
- **Callers:** `StageBalanceProfile.ToExport` line 264, which calls `SnapshotSaltwindBoss()` with the default. It is otherwise the same path as file 10: the hash only.
- **Existing config:** `BossConfig` (`Config/Schemas/BossConfig.cs:72+`) has **no enemy reference**. The boss is the only archetype with `isBoss: 1` (`Data/Enemies/barnacle_king.asset`), and the `saltwind_boss` spawner's anchor points to it.
- **Change:** add a `bossEnemyId` string to `StageBalanceProfile` and pass `bossEnemyId` from `ToExport`. Fill it in `StageProfilePort.Fill` from config, in one of two ways:
  - (a) add `bossEnemyId` to `SharedStageProfileValues` and set `barnacle_king` in `StageContent_Saltwind.asset`; `FieldMirror` copies it by name; or
  - (b) take the single `EnemyArchetypeConfig` with `isBoss`, or the boss spawner's enemy.

  Keep reading the pristine database definition so the snapshot values are unchanged. The added profile field is not part of the export, so the hash is unchanged, provided the new field is not copied into the export.
- **Risk:** low.
- **Proof:** the pinned hash test (`V4ConfigM1Tests.cs:62`).

### 14. `Combat/WeaponDebugPalette.cs`: live (debug overlay only)
- **Literals:** lines 13-24: an RGB colour per weapon id (plus unflagged `burning_oil`, `grapeshot`, `boarding_party`), grey default. Alpha is 0.55 by default, 0.9 when locked and 0.32 when idle.
- **Callers:** `Presentation/Weapons/WeaponAimOverlay.cs:146` and `:300`, the combat debug aim overlay. Presentation only, not gameplay.
- **Existing config:** none. `WeaponConfig` has `projectileColor` (line 205) and `visual.orbitRingColor` (line 67), but no aim colour.
- **Change:** add `Color aimOverlayColor` (RGB) to `WeaponConfig`, or inside its `visual` block. Fill the 10 weapon assets with the table values (deck `0.35,0.85,1`; broadside `1,0.55,0.15`; kegs `0.95,0.35,0.25`; cutlasses `0.75,0.95,0.35`; flamethrower `1,0.4,0.1`; lightning `0.55,0.75,1`; long_nine `0.95,0.95,0.85`; swivel `0.65,0.55,1`; harpoon `0.35,0.95,0.75`; mortar `1,0.45,0.15`). `WeaponAimOverlay` reads `def.aimOverlayColor` and applies alpha 0.9 / 0.32. Delete the palette. Presentation can read config directly; no Foundation injection is needed.
- **Proof:** a test comparing `WeaponDebugPalette.Get(id)` with the config colour for every `WeaponConfig`, run before deleting the palette.
- **Risk:** none if equal.

### 15. `Balance/EnemyTierLogic.cs`: live (spawn warning HUD)
- **Literals:** lines 23-36: `ResolveTierFromId`, the V3 id → tier table.
- **Callers:**
  - `ResolveTier` is called from `Runtime/Enemies/EnemyWorldContracts.cs:247-248` (`TierOf`), which is read by `Presentation/Hud/SpawnWarningPresenter.cs:96`, and only for spawns flagged `AmbientSpawn`. It decides the "Captain incoming" and "Heavy contact" toasts and the minimap and edge pings. `EnemyDefinition.tier` has no other reader.
  - `CanSpawnTier` and `CanSpawnEnemyId` have **no callers**: dead. Spawn tier gating uses `SpawnComposition` with config `spawnTier` (`Runtime/Spawning/SpawnComposition.cs:164-192`).
- **Is the id-table fallback reached?** Yes, every time. `def.tier` is `archetype.tier`, which is 0 (Soldier) in all 16 archetype assets, and 0 by default for aliases (`EnemyStatResolver.cs:91`). Only `isBoss` / `isMiniboss` short-circuit.
- **Existing config:** `spawnTier` (`EnemyArchetypeConfig.cs:107`, `EnemyVariantConfig.cs:41`). It equals the id table for every archetype and alias:
  - barnacle_king = Boss
  - reef_crab_miniboss, kraken_spawn, boarding_barge = General
  - shark, cannonboat, gunner_cog, bomb_boat, rammer_raft, manta_ray, needle_sloop, ram_raider = Captain
  - everything else = Soldier

  **One mismatch:** `elite_kraken_spawn` has spawnTier General, but the table gives Soldier (unlisted id). It is spawned only by the scheduled `saltwind_elite_kraken` spawner, never ambient, so the warning never evaluates it and there is no observable difference.
- **Change:**
  1. Delete `CanSpawnTier`, `CanSpawnEnemyId` and `ResolveTierFromId`.
  2. Make the resolved tier carry the warning tier. For archetypes and aliases, set `r.tier` from `spawnTier` in `EnemyStatResolver`. For registry elites, force Soldier to match V3 exactly; the alternative is to accept the unreachable General.
  3. `ResolveTier` becomes `isBoss ? Boss : isMiniboss ? General : def.tier`.
  4. Or move `TierOf` into the Runtime with a per-definition tier array built beside `BuildRules`.
- **Proof:** a test that the old `ResolveTier(def)` equals the new tier for every definition in the runtime database, excluding or explicitly checking `elite_kraken_spawn`.
- **Risk:** low. The DevTools ModuleLab row (`DevTools/Console/ModuleLabSection.cs:98`) will show the new tier.

### 16. `Combat/CombatWeaponCatalog.cs`: live call, output affects only the hash
- **Literals:** lines 19 and 27 (sync ids), 62-68 (the baseline retune of 7 weapons, including unflagged `burning_oil`), 92-117 (factories for long_nine, swivel_guns, grapeshot, harpoon_gun, mortar, boarding_party). `CreateGrapeshot` and `CreateBoardingParty` are never called: dead.
- **Callers:** `Runtime/Ports/RunDatabaseLoader.cs:37` (every run), and the tests `V4EnemyM2Tests.cs:47`, `V4EnemyTickM8Tests.cs:61`, `V4EnemyWorldM8Tests.cs:54` (catalog-applied clones).
- **Output consumed?** Not by gameplay; see §0. `WeaponConfig` already holds identical values (`runtimeCatalogWeapon: 1` marks the 4 created weapons).
- **Change:** delete the file and remove the call from `ApplyCatalogs`. Drop the weapon line from the three test fixtures.
  - Optionally keep a one-off test comparing the catalog's post-apply values with `WeaponConfig` for each id, then delete it.
  - `V4TraceSession.UnloadGameDatabase` becomes unnecessary for weapons.
- **Risk:**
  - The first run's hash is unchanged (it is taken before the catalog).
  - Runs 2+ in the same session will now show the first-run hash instead of the mutated-asset hash. That is a telemetry/footer change and it fixes R-73.
  - Gameplay is identical.
  - If any trace header comparison was made on a second-or-later run in a session, expect its hash to change.

### 17. `Combat/CombatSpecialistEnemyCatalog.cs`: live, structural (hardest)
- **Literals:** lines 17-24: alias id and template id pairs. Lines 26-27: manta_ray and reef_crab_miniboss retune.
- **Callers:** `Runtime/Ports/RunDatabaseLoader.cs:38` (every run), and test fixtures `V4EnemyM2Tests.cs:48`, `V4EnemyTickM8Tests.cs:62`, `V4EnemyWorldM8Tests.cs:55`.
- **What it provides:** the only thing that creates the 8 alias `EnemyDefinition`s. Without it, `V4RuntimeDatabase` has no `needle_sloop` / `ram_raider` / … slots, `Spawners.BindEnemies(id => GetEnemy(id))` would return null, and those spawns would break. It also fixes their **slot positions**: appended after the loaded slots, in catalog order.
- **Existing config:** all values already match exactly.

  | Alias | Template | Behavior | Threat | HP | Speed |
  |---|---|---|---|---|---|
  | needle_sloop | cutthroat_skiff | FlankingChaser | 2 | 14 | 5.5 |
  | ram_raider | rammer_raft | TelegraphedCharge | 3 | 22 | 7 |
  | bomb_ketch | bomb_boat | MaintainRange | 5 | 28 | 3.2 |
  | net_cutter | gunner_cog | FormationRanged | 4 | 18 | 4.5 |
  | repair_tender | boarding_barge | MaintainRange | 6 | 24 | 2.8 |
  | powder_barge | boarding_barge | SuicideApproach | 4 | 40 | 2.2 |
  | shield_frigate | gunner_cog | TankChaser | 7 | 45 | 3 |
  | treasure_raider | deckhand_dinghy | FastSwarm | 4 | 16 | 6.5 |

  The retunes also match: manta_ray StrafeBurst / threat 2, reef_crab_miniboss TelegraphedCharge / threat 3.

  The copied template fields and the `specialist` tag come from `EnemyStatResolver.cs:72-110`, and `Write` → `Apply` overwrites every field. So the catalog's values are never used; only the ids and slots are.
- **Change:** move alias creation into `V4RuntimeDatabase.Build`.
  1. After copying the loaded slots, append one `CreateInstance<EnemyDefinition>` per `EnemyVariantConfig` with `kind == DatabaseAlias` and a present `baseArchetype`, then `Write(id)`.
  2. Then append the V4-only enemies as today.
  3. Delete the file and `RunDatabaseLoader.ApplyCatalogs`. The `ApplyCatalogs` call at `RunBootstrapSequence.cs:146` goes too.
  4. **Order:** the config set lists variants alphabetically (`ConfigSets/SaltwindDefault.asset:103-112`), not in catalog order (needle, ram, bomb_ketch, net, repair, powder, shield, treasure). To keep the Editor slot layout identical, either:
     - add an explicit `databaseSlotOrder` int to `EnemyVariantConfig` (0-7 in catalog order); or
     - prove that slot order has no gameplay effect (the Editor and build layouts already differ, §0) and accept the alphabetical order.

     The ordered field is safest for trace comparability.
  5. Match the old definition `name`: aliases had `name == ""` (copy of a nameless `CreateInstance`). Set it to "" or confirm that nothing keys on it; the V4-only path sets `name = id`.
  6. Update the three test fixtures (`CatalogAppliedDatabase` → plain loaded clone) and `V4EnemyM2Tests.cs:65`: the "22 non-null" count moves to the runtime database.
- **Proof:** before deleting, a test that builds the runtime database both ways (old: catalog + `Build`; new: `Build` alone) and asserts:
  - the same `enemies.Length`
  - the same id at every index
  - `JsonUtility.ToJson` equality of every definition (prefab compared by name)
  - the same `EnemySimulation.BuildRules` output

  Run it once on the Editor database and once on the `Resources` database.
- **Risk:** medium, because it is structural. It is fully provable by that test. Gameplay values are unchanged.

---

## Suggested phase order
1. **Delete the dead code:** files 1-5 (with `SaltwindRunResultsLogic` / `SaltwindStageProgressionLogic` / `ContentUnlocks` item and logbook trims), 6, and the `CreateBuiltInSaltwindDefaults` / `CanSpawn*` bits.
2. **Remove the unreachable fallbacks:** 7, 8, 9, 10, 11, 12. The guard tests are:
   - the database assets are valid
   - every definition has a prefab
   - harpoon binding prefab == `harpoonProjectilePrefab`
   - the profile export context is active during runs
   - the hash pin is unchanged
3. **Move data into config:** 13 (boss id), 14 (aim colour), 15 (tier). Write the old-vs-config equality tests first.
4. **Remove the catalogs:** 16, then 17, with the both-ways runtime-database equality test.

After step 4, `NameKeyedFoundationFiles` is empty, and `RunDatabaseLoader.ApplyCatalogs` and the trace session's database unload can go.

**Gameplay-neutral:** all steps, for the default game and the v3-parity preset. **The only observable change** is the content-hash footer on the second and later runs of a session (step 4, file 16), which becomes the first-run hash. That is the R-73 fix. This is a presentation phase with respect to traces, so no trace run is needed; the unit equality tests prove it.
