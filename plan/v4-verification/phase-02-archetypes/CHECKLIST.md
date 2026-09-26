# M2 — Enemy archetypes and spawners

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `72709f45` (M1) |
| Status | Implemented, reviewed and committed; R-13 decided (template models); playtest later together with M1 and M3, at your request |

## What M2 delivers

**Configs**
- `EnemyVariantConfig` covers two kinds:
  - *database aliases*, the 8 V3 specialists (needle_sloop, ram_raider, bomb_ketch, net_cutter, repair_tender, powder_barge, shield_frigate, treasure_raider). They follow V3's `RegisterAlias` rule: fields copied from the template, behavior/threat/HP/speed overridden, "specialist" tag. **R-13 decision:** they use their template's model as a stand-in (`inheritBaseVisual`, on by default) until they get their own models; V3 has none and draws cubes. The scale is not inherited (the legacy enemy world sizes the hitbox from the visual scale), and the model is a per-run copy without colliders (the template ships' colliders sit on the Island layer, R-81), so specialists play exactly as in V3. The v3-parity preset turns it off.
  - *registry elites*: elite_hammerhead and elite_kraken_spawn, which carry only their own combat profile.
- `EnemyArchetypeConfig.inLegacyDatabase` marks enemies V3's live database does not contain. drowned_pursuer and drowned_harrier come from V3's fallback database; V3 overtime actually spawns shark and cannonboat stats (R-05).
- Timeline pool entries link to their archetype or variant.
- The config set grows from 40 to 52 configs, all valid.

**Resolver.** `EnemyStatResolver` (`Runtime/Enemies`) resolves an enemy from its archetype and variant layers. It records, for every value, the layer that set it (Default, Archetype or Variant). Stage, spawner, experiment and director layers arrive in M3 and M10.

**Runtime database** (`Legacy/V4RuntimeDatabase`)
- The per-run database has the exact layout of the loaded database after V3's catalogs, so definition indices match: null slots stay, and the 8 aliases are appended where V3 appends them.
- Every enemy in it is a per-run copy written from its archetype or variant config.
- Weapons, logbooks, relics and ships still reference the loaded database's assets. Legacy HUD and meta code loads the database directly and must see V3's catalog weapons (for example Swivel Guns); weapons move in M5.
- Archetype edits rewrite the definition and all aliases built on it, in place.
- The enemy world, POI manager, level system and bridge all get this database.
- Every runtime spawn profile points at the per-run copy of its enemy, so beats, encounters, overtime, the boss and route events read config values too, not only the ambient pick (review finding 4).

**Combat profiles** (`Legacy/LegacyCombatProfileOverride`)
- Writes archetype and variant combat blocks into V3's static combat-profile registry at run start and on edits. The values reach newly spawned enemies.
- Snapshots the originals and restores them when the run ends. The registry is shared with V3 in the Editor, and V3's combat system mutates it at runtime (the elite hammerhead second-charge flag, R-78: every V4 run starts with it off).
- The two registry-elite profiles have no V3 reader (every lookup uses the database enemy id, R-77). Their combat block is shown as "(M3)" in the console and applies once elite variants spawn.

**Module Lab**
- `V4ModuleLab` is now a copy of `V4SaltwindRun` with `V4Game.moduleLab = true`:
  - the ship is invulnerable;
  - the encounter spawner is stopped (ambient schedule and trigger queue);
  - pressure beats, route events and overtime are off;
  - POIs, hold-to-interact and the B-key summon are off, so chests, the powder-cache ambush and the Beacon boss cannot spawn anything;
  - Retry and seed/preset restarts reload the lab, not the run.
- The console has a Module Lab section:
  - spawn any archetype or variant ahead of the ship, at an adjustable distance;
  - Clear all;
  - an inspector showing resolved values with their layer (elite rows show the base profile they actually use).

**Other**
- `V4Phase.Current = 2`.
- EditMode tests reload the V3 database from disk before comparing (`V3ConfigMigrator.LoadCleanDatabase`): after a PlayMode run in the same Editor session the loaded database is already catalog-mutated (R-73), and comparisons failed when EditMode ran after PlayMode.
- Structural fields (`inLegacyDatabase`, variant `kind` and `baseArchetype`) are marked "(restart)" in the console.
- The specialist templates, registry elites and fallback-only enemies are listed in `LegacyConfigMapping`, so the migrator in `Editor/` compares no content ids.

## Legacy reader audit

| Changed | Readers | Classification |
|---|---|---|
| Enemy definitions come from the runtime database | `EnemyWorld` (definitions array, index by id and reference: `EncounterSpawner.FindDefinitionIndex` matches id too), `POIManager`, `LevelSystem`, the V4 bridge, `V3MultiShipRunSetup`; `SaltwindCombatBehaviorSystem` / `SaltwindSpecialistCombat` / `EnemyIdentityDebugLabels` read `world.Definitions` | Adapted: all receive the runtime database or read through the enemy world |
| The loaded database asset is still catalog-mutated (as in V3) | `LevelSystem.Awake` fallback, `MetaSystems` (relics), `V3AuthoredBottomLoadoutHud`, `V3AuthoredGameplayHud`, `V3RunLoadoutHud`, `PlayerShipVisualBootstrap`, `WeaponStatExportLogic` / `BossStatExportLogic` (profile hash) | Unchanged: they keep reading the asset, which V3's catalogs mutate exactly as before |
| Combat-profile registry values come from config | `EnemyCombatProfileRegistry.ResolveRuntimeParams` at spawn; `SaltwindCombatBehaviorSystem` (mutates the elite hammerhead flag) | Adapted: written at run start, restored at run end |
| `V4ModuleLab` no longer contains the weapon/enemy playtest bootstrap | Nothing else loads it | Replaced by V4 lab mode |
| `V4ModuleLab` scene re-created (new GUID) | Build settings | Build list entry refreshed to the new GUID; `V4SceneSetup.AddToBuild` now refreshes stale GUIDs |
| Runtime spawn profiles' `enemy` points at the per-run copy | `EncounterSpawner` (`FindDefinitionIndex` matches reference then id; placement radius, near-player fallback, telegraph read `spawnDef`), `SaltwindAmbientCompositionLogic` (ids) | Adapted: the copy is in the enemy array, so reference and id lookups both resolve |
| `BossStatExportLogic.SnapshotSaltwindBoss` loads the asset database | Profile hash, `SaltwindLabBossParityLogic` | Unchanged: V4 boss edits are not reflected there until M7 (R-79) |

## Results

| Check | Result | Evidence |
|---|---|---|
| V4 EditMode | 56 / 56 (also passes when run after PlayMode in the same session). New: the 8 specialists use their template model by default and keep their own stats (R-13); the runtime database equals the catalog-applied V3 enemies slot for slot (22 enemies, null slots, per-run copies, weapons untouched); config alone reproduces all 22 enemies on an empty definition (catches a field `Apply` stops writing); the drowned archetypes equal V3's fallback definitions; archetype edits rewrite the definition and its aliases; combat blocks equal the registry as V3 builds it for 24+ ids including both elites; the registry override writes and restores (restores even when an assert fails); resolver provenance; no content-id literal outside `Legacy/` (any literal, two listed harness exceptions) | `editmode-results.json` |
| V4 PlayMode | 7 / 7. New: the enemy world uses the runtime database, archetype edits reach it, and every spawn profile points at the per-run copies; Module Lab spawns nothing by itself (spawner, POIs and interaction off), spawns a variant with its config values, and the ship is invulnerable | `playmode-results.json` |
| V3 regression | EditMode 242 tests (187 V3 + 55 V4): only the 3 known balance-lab matrix failures. PlayMode 36: only the known storm-arena failure. No new failures; your PlayerPrefs snapshotted and restored (First Conquest and home seed still absent) | `v3-regression-editmode-results.json`, `v3-regression-playmode-results.json` |
| Traces | Final, Windows player, seeds 101/202/303: V4 with the v3-parity preset (V3 cubes) == V3 goldens (MATCH ×3); V4 replay identical. Default runs (R-13 template models) keep spawns, stats, hitboxes and placement, and first differ from the M1 baseline by one frame in a kill time (3:24 / 2:04 / 2:25); the exact model-to-kill-timing path is not identified (the V3 simulation reads view state in places). They are the M2 baseline for M3 | `trace-comparison.txt`, `player/` |
| Playable check | Lab after 6.7 s: 0 enemies, spawner/POIs/interaction off; all 21 non-boss enemies spawned in a ring (specialists use their template models, R-13); elite_hammerhead row spawns one elite hammerhead; inspector open; live variant edit ram_raider HP 22 → 60; Retry reloads `V4ModuleLab` in lab mode. Console: 6 errors, all Pipeline "main thread operation timed out" while Play Mode started (tooling, no game errors) | `playable-check/` |

## Deviations, recorded

1. **Weapons stay on V3's shared database** until M5 (plan I2.2 said "weapons/logbooks still from the deep clone"). Legacy HUD and meta code loads the database asset directly and must keep seeing V3's catalog weapons. R-73 profile-hash drift therefore remains until M5.
2. **Module Lab is built from `V4SaltwindRun`** (plan N0.1 said from Test_WeaponEnemy): lab mode switches off spawning systems on the real run scene, so the lab uses exactly the run's systems.
3. **Elite rows in the lab spawn the base enemy with the Rich affix** (flagged elite). They use the base enemy's combat profile, as any V3 elite does (R-77); V3's scheduled elite beats spawn plain units (R-03). Elite behavior is an M3 decision.
4. **Specialist visuals (R-13, decided).** V3 aliases have no visual prefab and render as cubes. V4 gives them their template's model by default; own models are future content (register R-13). Some template prefabs have empty material slots that render magenta (R-80, V3 art).
5. **Lab spawns use `EnemyWorld.Spawn` without a spawn profile.** Run spawns go through `SpawnFromProfile`, so lab enemies have no encounter overrides and start with different attack and charge timers. Lab behavior is close to, not identical with, run behavior until the V4 enemy world (M8).
6. **No dedicated Enemy Archetypes / Variants / Spawners console sections** (plan I2.4). The generic Config section already lists each config asset with every field, grouped and searchable; the Module Lab adds spawning and the resolved inspector.
7. **Variants have no elite multiplier or affix fields** (plan I2.1). V3 has no elite variant data to port; they arrive with elite variants in M3 (R-03).
8. **Combat fields hard-coded in `EnemyWorld`** are named in the combat block's tooltip, not tagged per field (plan I2.3). They become data in M8.
9. **An M1 test fix is included.** `RuntimeSchedule_FromDefaults_EqualsTheV3Schedule` now applies the v3-parity band-0 value (0.6) before comparing, because R-01 set the default to 0.5. The committed M1 version compares the 0.5 default against V3's 0.6, so it fails at `72709f45`; the M1 test results were recorded before the R-01 change.

## Independent review

A read-only reviewer checked M2 against the plan and the V3 code (file:line evidence). No blockers. Findings and what was done:

| # | Finding | Resolution |
|---|---|---|
| 1 | Major: the lab still ran POIs and the spawner's trigger queue, so the powder-cache ambush and Beacon boss could spawn | Lab stops the spawner (`EnterManualPlaytestMode`) and disables POIs, hold-to-interact and the B-key summon; PlayMode test and playable check assert it |
| 2 | Major: registry-elite combat blocks have no V3 reader; the inspector showed them for elite spawns | Console shows them as "(M3)" (per-asset `AppliesFromFor`); elite rows show the base profile; R-77 |
| 3 | Major: slot-for-slot test could not catch a field `Apply` stops writing; drowned stats unchecked | New tests: config onto an empty definition for all 22 ids; drowned vs V3 fallback |
| 4 | Non-ambient spawns read the asset definition for placement radius, boss fallback and telegraph | Runtime spawn profiles point at the per-run copies; PlayMode test |
| 5 | Registry restore resets V3's session-long hammerhead flag | Recorded as R-78 |
| 6 | Boss snapshot ignores V4 boss config | Recorded as R-79 and in the reader audit |
| 7 | Lab spawns have no spawn profile | Deviation 5 |
| 8 | Retry and restarts left the lab | Restarts reload the current run scene; playable check asserts it |
| 9 | Build settings kept the old lab GUID | Refreshed; `AddToBuild` fixes stale GUIDs |
| 10 | Evidence missing (results json, V3 regression) | Added |
| 11 | TMP fallback font modified | Reverted before the commit (test side effect) |
| 12 | Content-id scan missed switch arms, `is`, `StartsWith` and others | Any content-id literal outside `Legacy/` now fails, with two listed exceptions |
| 13 | Registry test without try/finally; combat test compared the mutable live registry | try/finally restore; compares with the registry as V3 builds it |
| 14 | Undisclosed plan deviations; structural fields live-editable | Deviations 6–8; structural fields marked "(restart)" |
| 15 | M1 test fix bundled | Deviation 9 |
| Nits | Migrator leaked fallback definitions; lab section rebuilt lookups every frame and could throw on a missing base | Fallback definitions destroyed; lab caches the world and ship and skips variants without a base |
