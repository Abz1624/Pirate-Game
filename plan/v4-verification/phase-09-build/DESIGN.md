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

## Legacy reader audit

To be written at the switch-over (step 9.8) from the research maps: every reader of `LevelSystem`, `WeaponController`, `PickupWorld`, `EncounterSpawner`, `V3RunItemInventory`/`Effects`, `V3CaptainController`/`ActiveController` and `V3FloorPowerupWorld` in V4 runs, classified ported / adapted / safe when absent.
