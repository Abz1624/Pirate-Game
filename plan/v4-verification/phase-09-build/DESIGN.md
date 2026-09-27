# M9 — Build and Progression: design record

Scope (plan §6 M9, steps 9.1–9.8, one gate): items, floor powerups, Captains, level-ups and offers, Logbooks and pickups become V4 config and modules; the adopted Captain and Logbook decisions (D6) land; `WeaponController` (the switched-off loadout record), `LevelSystem`, `PickupWorld` and the switched-off `EncounterSpawner` leave the V4 scenes; the M9-tagged adapters are deleted.

Research (read-only maps with file:line): [research-progression.md](research-progression.md), [research-items-captains.md](research-items-captains.md), [research-pickups-powerups.md](research-pickups-powerups.md).

## Decisions taken from the design pass

1. **Exact V3 parity under the v3-parity preset, as in M8.** Every port keeps V3's arithmetic, call order and random draws:
   - offers on the **Loot** stream (`LootFloat`, `LootInt`, `RollRarity` with luck read once per generation), in `LevelOfferGenerator`'s exact order (first stage level-up, fill-state roll per attempt, draws before duplicate checks, pity replacement of the last card);
   - kill drops: **Director** draw for the health chance (always drawn), then **UnityEngine.Random** for drop offsets and bob phases, then the **Powerup** stream for the floor powerup chance and kind;
   - barrel bursts and flying pickups keep their UnityEngine.Random draws (count, scatter, fly delay, bob) — traces pin `Random.InitState`. The plan's "seeded offsets" becomes a switch for later, off by default, because it changes every run.
2. **The kept shared pieces stay:** `PlayerStats` (mirrored by the V4 stat resolver), `DamageReceiver`, `PlayerShipController`, `LevelUpModal` (driven through `TryShow`), `PickupViewPool` and its prefabs (the barrel alignment reads renderer bounds, R-74), `V3PlaceholderFactory` powerup views, toasts. They go in M14/M15.
3. **Config first, then lockstep ports, then the switch-over** (the M8 recipe): each module is built next to the V3 system and compared frame by frame or call by call in tests before any run uses it; the switch-over is the last step.
4. **Default config = V3 values plus the adopted decisions and approved fixes; the v3-parity preset restores V3.** D6 lands as defaults: Captain starting weapons, actives on, Captain locks, the four Logbook stat fixes. Gate decisions (below) become switches with V3 behaviour until you decide.
5. **Module boundaries** (plan §4.2): `StatResolver` (+ source-tagged ledger mirrored into `PlayerStats`), `ItemModule` (inventory + triggered effects), `PowerupModule`, `CaptainModule`, `ProgressionModule` + `OfferGenerator`, `PickupModule`. Contracts replace the singletons the weapon, stage and damage paths read today (`IWeaponModifiers`, `IStageEconomy`, the incoming-damage multipliers, kill fan-out targets).
6. **Frame order stays V3's** (legacy update slots): pickups and powerups tick inside the enemy frame (`TickFrame`) where `PickupWorld`/`V3FloorPowerupWorld` ticked; item effects and the Captain controller tick where their V3 components updated; level-ups stay event-driven (XP grants flush at the end of the pickup tick).

## Build order

| Step | What | Proof before moving on |
|---|---|---|
| 9.1 | Configs + generator: `ItemConfig` ×19, `PowerupConfig`, `CaptainConfig` ×6, `ProgressionConfig` (XP curve, slots 6/4, cards 3, refreshes 2, protection levels, fallback values), `OfferWeightsConfig` (fill table), `LogbookConfig` ×10, `UnlockConfig` (starters, weapon and Captain costs). Evolution `requiredItemId` → item reference. | Migration parity tests (every value equals its V3 source); validator rules; parity preset restores V3 values |
| 9.2 | `StatResolver` + ledger: add/remove by source, mirrored into `PlayerStats`; `SetBase` for Captain hull/speed | EditMode: same `PlayerStats.Get` as V3's modifier sequence |
| 9.6 | `ProgressionModule` + `OfferGenerator` (port of LevelSystem + LevelOfferGenerator), driving the kept modal | EditMode lockstep: same offers (kind, id, rarity, lines) as V3 for many seeds, loadouts and levels; same XP curve and grants |
| 9.3 | `ItemModule` (inventory, stat modifiers, triggered effects; hooks V3 never reads stay inert) | Lockstep vs V3RunItemInventory/Effects over scripted ship paths and kills |
| 9.5 | `CaptainModule` (profiles, passives, Running Fuse, actives with cooldowns; toggle from config) | Lockstep vs V3CaptainController/ActiveController |
| 9.4 | `PowerupModule` (drops, pools, effects; Dead Calm through the V4 spawn rate stack) | Lockstep vs V3FloorPowerupWorld (Powerup stream draws, positions, buffs) |
| 9.7 | `PickupModule` (drops, magnet, bursts, merge, grants; V3 view pool) | Lockstep vs PickupWorld (positions, grants, draws) |
| 9.8 | Switch-over: wire the modules in runs; remove `WeaponController`, `LevelSystem`, `PickupWorld`, `EncounterSpawner` from both V4 scenes; stop ensuring V3 build components; delete the M9 adapters; `V4Phase.Current = 9` | Tests, playable check, review, the single trace set (parity preset = V3 goldens) |
| 9.9 | Console sections (level-ups, Logbooks, items, powerups, Captains) | Playable check |

## Decisions needed at the gate (switches, V3 behaviour until decided)

- Richie's lock rule (his weapon, Broadside Battery, is a starter weapon).
- R-31 evolutions unreachable once every weapon is maxed.
- R-34 Lookout's Glass +2.0 crit per rank (fix 0.02).
- R-40 item hooks computed but never read (and the near-miss hook never called).
- R-43 Powder Rush gives Damage ×1.3 and shares Battle Signal's slot.
- R-49 Brine armor never applied, Ember/Roxie multipliers unused, Richie pickup-radius burst missing, gamepad South is both boost and active.
- R-84/R-102: move the boss-fight resume into the V4 progression module (the R-102 next-tick patch then goes).

## Determinism hazards to preserve

- `LevelSystem.AddXP` regenerates offers (new Loot draws) on any grant while level-ups are pending.
- The stage first level-up offers the Swivel Guns upgrade whenever Swivel Guns is in the database (not only when owned).
- Upgrade levels are read back from the record after `ApplyUpgradePackage` (clamps at max level); V4 must keep the same level truth.
- `RollRarity` reads Luck once per generation; the wrong Logbook stat (Scavenger's Net → Luck) shifts rarity rolls under the parity preset.
- Kill drops draw the health chance on the Director stream even with no XP or gold.
- Merge-on-full uses a 3-D distance (renderer-bounds height, R-74).
- Powder Rush and Battle Signal share one damage slot; Full Sail shares Tailwind's.

## Legacy reader audit (step 9.8)

Every reader of the removed V3 build systems in a V4 run, from a read-only audit of `V4/**` and the shared V3 code that stays in the V4 scenes. **Ported** = now reads a V4 module; **adapted** = a kept V3 piece is driven by V4; **safe absent** = null-safe and nothing is lost; **dropped** = the behaviour is gone on purpose.

| Reader | Read | Now |
|---|---|---|
| `LegacyRunShell.BootstrapRun` | LevelSystem prepare/sync, record setup, Captain controllers, item/powerup ensure, `V3CaptainController.Instance` hull fallback | Ported: `LegacyBuildWiring` (ResetForRun, BeginCaptain, ResolveStartingWeapon, BeginProgression, BeginItems) in V3's order |
| `LegacyRunShell.OnApplicationPause` | `RunSnapshotService.CaptureFromRun(LevelSystem, POIManager)` | Dropped (never loaded by V3; plan 9.8) |
| `LegacyWeaponWiring` / `LegacyWeaponRecord` | WeaponController record as the loadout | Ported: `ProgressionLoadout` (IWeaponLoadout) |
| `LegacyWeaponModifiers` | Captain/active/item singletons | Ported: `BuildWeaponModifiers` over the stat store, `CaptainModule`, `ItemModule` |
| `LegacyActiveProbe` | active cooldown polling, Captain's Bell via TriggerBus + V3 inventory | Ported: Captain events fire the weapons in the Captain's tick; the Bell fires on the next weapon tick (as since M5) |
| `DamageReceiver.TakeDamage` (shared, kept) | Brace! and Keel multipliers, `NotifyPlayerTookDamage` from the V3 singletons (now absent) | Adapted: every V4 enemy hit goes through `EnemyCombatServices.HitPlayer` / `LegacyEnemyTarget` / the damage sink with the build multiplier; items hear `Damaged` |
| `LegacyPlayerDamageSink.DamageOverTime` | the same multipliers and item hook | Ported to the build multiplier and `ItemModule.NotifyPlayerTookDamage` |
| `LegacyEnemyKillTargets` | `PickupWorld.SpawnFromEnemy`, `V3FloorPowerupWorld.TryRollOnKill`, `V3RunItemEffects.NotifyKill` | Ported: pickups, powerups, items (same order) |
| `LegacyEnemyWiring.TickFrame` / `ClearTransientWorlds` | PickupWorld and V3FloorPowerupWorld ticks, the R-103 adapter | Ported: `LegacyBuildWiring.TickWorld` (Magnetic Tide opens barrels through the pickup module when switched on) |
| `LegacyEnemyWiring` summon / despawn | `EncounterSpawner.TriggerProfile` / `NotifyAgentDespawned` on the switched-off spawner | Summon kept as accepted (counted, never spawned, as V3); despawn count dropped (the spawner is gone) |
| `LegacyHellfireEnvironment` | `DamageSystem.RollDamage` (reads the V3 singletons), `WeaponEvolutionRuntime` | Ported: V4 `DamageCalculator` and the progression's evolutions |
| `LegacyEncounterExecutor` | `EncounterSpawner` schedule, lab mode, Dead Calm read-back | Ported: Dead Calm from `PowerupModule.SpawnRateMultiplier` (same float steps); schedule and lab mode dropped with the spawner |
| `LegacyRunState.PlayerLevel` | `LevelSystem.Level` | Ported: progression level |
| `LegacyStageEconomy.TryAddItem` | `V3RunItemInventory.TryAddItem` | Ported: `ItemModule.TryAddItem` (toast and telemetry as V3) |
| `LegacyProgressionView`, `LegacyLoadoutView` items, `LegacyResultsView` final build | LevelSystem, V3 inventory | Ported: `ProgressionView`, item module, progression weapons |
| `LegacyEventFeed` | LevelSystem `LevelChanged` / `OffersReady` | Ported: progression events |
| `LegacyPresentationAdapter` | `EnsureLevelSystem`, skip-hide reads `PendingLevelUps` | Dropped / ported (pending from the progression) |
| `CombatVfxController` (kept) | subscribes `LevelSystem.LevelChanged` for the level-up burst | Adapted: the build wiring invokes its handler on the progression's level change |
| `RunHud` (kept, left panel hidden) | LevelSystem level/XP | Safe absent (the V4 HUD presenter shows level and XP) |
| `WeaponJuiceController.Configure` (disabled) | finds the WeaponController | Safe absent |
| `V3RunPlaytestHotkeys` (dev) | WeaponController, LevelSystem, V3 inventory | Safe absent; its grant hotkeys stop working (the console sections in 9.9 replace them) |
| `V3OnboardingDirector` | `NotifyLevelUpShown` from LevelSystem | Ported: the level-up presenter calls it |
| Trace recorder / pilot | LevelSystem level, PickupWorld pool | Ported, with the V3 fallback kept for golden recordings |
| Console Weapons section (`LegacyWeaponLab`) | record + LevelSystem + `WeaponEvolutionRuntime` | Ported: progression dev edits |
| Menu Captain locks | `V3CaptainCatalog.IsLocked` (the V3 carousel draws lock badges from it) | Unchanged (V3 locks); the adopted locks are in `CaptainConfig` and need the V4 menu view — gate decision with Richie's rule |

