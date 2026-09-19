# Game balance & progression (prototype)

**Generated for review:** September 2026  
**Authority:** In-code data under `Pirate Game Unity/Assets/_Game/Data/` and formulas in `GameConstants` / `GameMath`. Design specs (`Rogue_Pirates_*.md`, `plan/Rogue_Pirates_Cursor_Combat_Redesign_v0_2.md`) describe targets; numbers here reflect what the Unity build actually runs today.

All values are **prototype tuning**, not production balance.

## Balance Lab (single-player)

Open **HighSeas → Balance Lab** in the Unity Editor to run deterministic seeded batches without playing a live run. The lab persists a local scenario in Editor preferences and does **not** modify `Assets/_Game/Data/`. It reports objective and survival rates, average kills/level, and peak enemy count; raw runs can be exported to CSV.

The **Authored content snapshot** controls read the selected Weapon and Enemy definitions from `GameDatabase` at run time. That means the model uses the same base damage, cooldown, range, projectile speed, hull, damage, speed, and XP values that are authored for the game, before any experiment multiplier is applied.

Use **Analyze Offers** to run 200 seeded, 12-level offer sequences through the live `LevelOfferGenerator`. It compares greedy-DPS, survival, mobility, economy, and evolution-seeking choice policies, reporting the top cards shown and selected for each. Treat a card that is selected disproportionately often across several policies as a balance investigation: it may be overtuned, or its alternatives may be underpowered. Pattern output is estimated from the authored pattern, quantity, and duration so the fast combat model can distinguish projectile, fan, chain, mine, orbit, trail, aura, AoE, and summon families.

The scenario exposes the current Rook/Brine/Flint/Richie/Ember/Roxie baseline hull and speed profiles plus player modifiers, expected weapon loadout and item effects, XP pacing, enemy/director/boss tuning, and the V3 POI route assumptions. The **Kite** movement policy approximates a cautious player by steering away from nearby threats; auto-targeting is represented as a guaranteed in-range hit. POIs are modeled as route rewards for this fast simulation; validate any candidate change in `V3Boot` afterwards because the live POI interactions, targeting, and map movement remain Unity runtime systems.

V3 single-player places the enabled finite layout: 12 Salvage Chests, Repair Wreck, Powder Cache, Signal Buoy, Treasure Chart, and the Treasury Beacon. They are intentionally disabled only in PvP sessions.

---

## 1. Run structure

| Rule | Value | Source |
|------|-------|--------|
| Stage soft timer | **10 minutes** (600 s) | `GameConstants.StageDurationSeconds` |
| Overtime | After 600 s; Drowned Pursuit scaling | `RunManager.IsOvertime`, spawn overtime in `SpawnDirector.TrySpawnOvertimeEnemy` |
| Objective | Secure Beacon / boss; **no extraction**; keep fighting until death | Product handoff |
| Run seed (demo) | `12345` default | `GameConstants.DemoRunSeed` |
| Map | ~220 m play space, 14×14 grid cells | `GameConstants.MapSizeMeters`, `GridCells` |

### V3 pressure beats (Saltwind voyage)

Scheduled by `V3PressureBeatDirector` (combat spec §9):

| Time | Event |
|------|--------|
| 3:00 (180 s) | Hammerhead elite (`hammerhead`) |
| 4:00 (240 s) | **Swarm pulse** — +25% spawn rate for 20 s |
| 7:00 (420 s) | Second swarm pulse |
| 8:00 (480 s) | Kraken Spawn elite (`kraken_spawn`) |

Boss fight can defer beats by up to 20 s. Pulses call `SpawnDirector.SetSpawnRateMultiplier(1.25)`.

### Legacy miniboss schedule (non–pressure-beat runs)

When `V3PressureBeatDirector` is off, `SpawnDirector` spawns a miniboss at:

- `DemoPacingSettings.firstMinibossSeconds` (**90 s** default)
- **72%** of stage duration (~7:12)

---

## 2. Player baseline

| Stat | Default | Notes |
|------|---------|--------|
| Move speed | 6.2 | `GameConstants.DefaultMoveSpeed` |
| Hull | 100 | `DefaultHull` × `HullHealthMultiplier` (1) |
| Pickup radius | 3.5 | `DefaultPickupRadius` |
| Contact i-frames | 0.20 s | `ContactIFrame` |
| Enemy contact cooldown | 0.80 s | `EnemyContactCooldown` |
| Weapon slots | 6 | `DefaultWeaponSlots` |
| Logbook slots | 2 | `DefaultLogbookSlots` |
| Max weapon level | 12 | `MaxWeaponLevel` |
| Max logbook level | 10 | `MaxLogbookLevel` |
| Max global damage mult | 11× | `MaxGlobalDamageMultiplier` |
| Max move speed | 12 | `MaxMoveSpeed` |

Damage pipeline (simplified): `GameMath.ComputeDamage` — weapon level, global/weapon bonuses, crit, elite mult, status, armor `100/(100+armor)`.

---

## 3. XP & leveling

### XP to next level

`GameMath.XPToNext(level, pacing)`:

```
xp = round(12 + 5.5×level + 1.35×level^1.32)
if level ≤ earlyLevelCap: xp = max(8, xp × earlyLevelXpMultiplier)
```

**Current `DemoPacingSettings`:** `earlyLevelCap = 6`, `earlyLevelXpMultiplier = 0.95`, `xpRequirementMultiplier = 1.28` (used in V3 level curve where applicable), `xpDropMultiplier = 1.05` on pickups.

| Level | XP to next (pacing on) |
|------:|----------------------:|
| 1 | 18 |
| 2 | 24 |
| 3 | 30 |
| 4 | 36 |
| 5 | 42 |
| 6 | 48 |
| 7 | 55 |
| 8 | 62 |
| 9 | 69 |
| 10 | 77 |
| 12 | 92 |
| 15 | 114 |

### Level-up offers (`LevelUpSettings`)

| Setting | Value |
|---------|-------|
| Cards per level | 3 |
| Weapon offer weight | 0.55 |
| New weapon weight mult | 2.5 |
| Script first level-up | Yes → `deck_cannons` |
| Demo: always include new weapon if possible | Yes |
| Upgrade power mult (rarity) | 1.2 |

**Weapon growth (`DemoPacingSettings`):** `upgradePowerMultiplier = 1.2`, `startingDamageMultiplier = 1.1`.

---

## 4. Weapons (authored data)

Six weapons in `Assets/_Game/Data/Weapons/` (prototype slice):

| ID | Display | Base damage | Cooldown (s) | Range |
|----|---------|------------:|-------------:|------:|
| `deck_cannons` | Deck Cannons | 11 | 1.05 | 22 |
| `broadside_battery` | Broadside Battery | 8 | 1.50 | 14 |
| `powder_kegs` | Powder Kegs | 20 | 2.20 | 14 |
| `flying_cutlasses` | Flying Cutlasses | 7 | 1.00 | 14 |
| `lightning_mast` | Lightning Mast | 14 | 1.80 | 14 |
| `flamethrower` | (burning_oil asset) | 8 | 0.70 | 14 |

Each weapon has per-axis upgrade configs (`validUpgradeAxes`, `upgradeAxisConfigs`) capped by `maxLevel` and axis `maxAccumulated`.

### Meta unlocks (V3 prototype)

With `V3PlaytestFlags.UnlockAllContent = true`, all weapons behave as unlocked in playtests. When gating is enabled (`V3ContentUnlocks`):

- Always: `deck_cannons`, `broadside_battery`
- After first conquest (or Flint captain): `powder_kegs`
- After first conquest: `flying_cutlasses`, `lightning_mast`, `flamethrower` / `burning_oil`
- Experimental six-slot weapons (`long_nine`, `swivel_guns`, etc.): conquest or explicit playtest exceptions

---

## 5. Enemies (authored base stats)

Scaling at runtime: `GameMath.EnemyHP` / `GameMath.EnemyDamage` using **elapsed minutes**, **tier** (1–3), and **stage index** (0–2). Experiment multipliers from `SpawnDirector` / `EnemyWorld` apply on top.

### Standard roster (`Assets/_Game/Data/Enemies/`)

| ID | HP | Damage | Speed | Threat cost | XP | Notes |
|----|---:|-------:|------:|------------:|---:|-------|
| `deckhand_dinghy` | 12 | 6 | — | — | — | Specialist / pack content |
| `cutthroat_skiff` | 16 | 7 | — | — | — | |
| `gunner_cog` | 14 | 6 | — | — | — | |
| `bomb_boat` | 10 | 14 | — | — | — | |
| `rammer_raft` | 22 | 10 | — | — | — | |
| `boarding_barge` | 40 | 12 | — | — | — | |
| `swordfish` | 10 | 6 | 4.6 | 1 | 2 | Creature |
| `hammerhead` | 14 | 7 | 5.0 | 2 | 3 | Creature |
| `shark` | 16 | 9 | 4.2 | 2 | 3 | Creature |
| `manta_ray` | 22 | 8 | 3.6 | 3 | 4 | Creature |
| `cannonboat` | 18 | 8 | 3.8 | 3 | 3 | Ship |
| `kraken_spawn` | 28 | 11 | 3.2 | 4 | 5 | Creature |
| `reef_crab_miniboss` | 120 | 14 | — | — | — | Miniboss |
| `barnacle_king` | 800 | 18 | — | — | — | Boss |

Example scaled HP (tier 1, stage 0, **minute 5**):  
`shark`: 16 × (1 + 0.09×5 + 0.018×25) ≈ **30 HP**.

### Enemy HP / damage scaling formulas

```
HP  = baseHP × (1 + 0.09×minute + 0.018×minute²) × tierMult × stageMult
DMG = baseDamage × (1 + 0.055×minute) × tierMult × stageMult
```

Tier mult (HP): 1 / 1.45 / 2.05. Stage mult (HP): 1 / 1.22 / 1.5.

---

## 6. Spawn director

**Component:** `SpawnDirector`  
**Data:** `SpawnDirectorSettings` (`Assets/_Game/Data/SpawnDirectorSettings.asset`, mirrored under `Resources/`)

### Modes (`SpawnTimingMode`)

| Mode | Behavior |
|------|----------|
| `ScriptedWaves` | Megabonk-style timed waves, then `afterScriptedWaves` |
| `ContinuousThreat` | Threat budget fills and spends `threatCost` per spawn |
| `SteadyInterval` | Fixed interval spawns |
| `BurstWaves` | Wave size bursts |

**Current asset:** `timingMode = ScriptedWaves` (7 short intro waves), then `ContinueThreat` (`afterScriptedWaves: 2`).

### Scripted wave sequence (current asset)

| # | Name | Delay | Duration | Gap | Composition |
|---|------|------:|---------:|----:|-------------|
| 1 | Cannonboats | 2s | 14s | 1.0s | 6× cannonboat |
| 2 | Swordfish | 3s | 14s | 1.0s | 7× swordfish |
| 3 | Manta Rays | 3s | 15s | 1.1s | 5× manta_ray |
| 4 | Sharks | 3s | 15s | 1.1s | 6× shark |
| 5 | Hammerheads | 3s | 16s | 1.2s | 5× hammerhead |
| 6 | Kraken | 3s | 16s | 1.3s | 4× kraken_spawn |
| 7 | Full Assault | 3s | 18s | 1.1s | mixed |

### Global spawn settings (current asset)

| Field | Value |
|-------|------:|
| Grace period | 2 s |
| Min seconds between spawns | 0.3 s |
| Threat multiplier | 0.85 |
| Budget cap multiplier | 3.5 |
| Max active enemies | 55 (V3 pacing caps lower unless Director V2) |
| Elite chance multiplier | 0.5 |
| Creature mix start / late | 0.5 / 0.5 (ramp 1 min) |
| Spawn distance min / max | 10.8 / 18 m |
| Placement | Screen edge, outside camera |

### Per-enemy tuning highlights

| Enemy | Max concurrent | Speed mult |
|-------|---------------:|-----------:|
| swordfish | 12 | 1.035 |
| manta_ray | 8 | 1.05 |
| kraken_spawn | 4 | 0.95 |
| reef_crab_miniboss / barnacle_king | spawn disabled in director | — |

### V3 continuous threat (active in V3 runs)

`ConfigureV3Pacing(true)` forces **off-camera** spawns and **ContinuousThreat** after scripted flow; **no spawns during overtime** from this loop.

**Legacy V3 rate** (Director V2 off):

```
threatPerSec = min(7, (0.9 + 0.22×minute) × (1 + 0.15×(depth-1))) × 1.8
```

**Director V2 rate** (`D_FULL_COMBAT_V2` profile, minute ≤ 10):

```
enemiesPerSec = min(16, (2.5 + 0.55×minute + 0.025×minute²) × (1 + 0.25×(depth-1)) × spawnDensityMult × itemDensityMult)
```

Example **minute 5**, depth 1, mult 1: ≈ **5.9 enemies/s** (budget-limited by cap and spawn gaps).

**Legacy threat curve** (non-V3 continuous): `RunManager.ThreatPerSecond` from `GameMath.ComputeThreatPerSecond` × `threatMultiplier` × spawn rate mult.

| Minute | Threat/s (tier 1, stage 0, no overtime) |
|-------:|------------------------------------------:|
| 0 | 2.6 |
| 5 | ~4.0 |
| 10 | ~5.5 |

Overtime multiplies threat: `1.5 × 1.2^overtimeMinutes`.

### Director V2 extras

- **Composition weights** by minute bucket: `CombatDirectorV2Composition` (0–2, 2–4, 4–6, 6–8, 8+ min).
- **Spawn packs** every 24 s (&lt;6 min) or 18 s (≥6 min): `SpawnPackRegistry` (Swarm → Gunline → AnchorAndFire → ZonePressure → LateMix).
- **Enemy cap** up to **180** when V2 enabled; V3 pacing otherwise caps around **36** active.

### Elites

- V3 pacing: **0%** until minute 3, then **2%** flat.
- Legacy: `(0.02 + 0.008×minute) × eliteChanceMultiplier`, cap 18%.

### Overtime spawns

`TrySpawnOvertimeEnemy`: HP mult `2^(0.5×overtimeMinutes)`, damage `1 + 0.12×overtimeMinutes`.

---

## 7. Economy & pickups (run)

| System | Rule |
|--------|------|
| Run gold | Spent on chests; **banked 100% at death** in V3 (`RunManager.FinalizeRunOnDeath`) |
| Chest cost | `GameMath.ChestCost(n)` = `25 + 34×(n-1) + 4.5×(n-1)²` doubloons |
| First chest discount | 50% (`DemoPacingSettings.firstChestDiscount`) |
| XP pickup radius | 4.25 m |
| Gold auto-pickup radius | 26 m |
| Barrel touch radius | 5 m |
| Health drop chance from barrels | 12% |

### Saltwind POI layout (V3 conquest)

Placed by `V3StagePoiPlacer` after `V3SpawnAnchorValidator` resolves spawn + treasury beacon (RNG stream: **run seed + 404**). World markers are built for **every** entry in `StageLayout.pois` via `POIManager.BuildMarkers`.

| Count | `POIType` | Player-facing name | Interaction / reward |
|------:|-----------|-------------------|----------------------|
| 12 | `SalvageChest` | Salvage Chest | Spend run gold (`V3Economy.GetChestPrice`); rolls a run **item** from `V3ChestLoot` |
| 1 | `GildedWreck` | Repair Wreck | One-time **+25% max hull** heal |
| 1 | `MerchantSkiff` | Powder Cache | Proximity **ambush** (3 enemies, threat ≤ 4); clear fight → random chest-pool **item** + **20** run gold |
| 1 | `TrialBuoy` | Signal Buoy | Stand in **4 m** circle **8 s** to charge; reward: **+10% damage**, **+0.5 move speed**, or **+0.8 pickup radius** (random) |
| 1 | `CursedIdol` | Treasure Chart | Reveals **3** nearest unopened salvage chests on the minimap |
| 1 | `BossBeacon` | Treasury Beacon | Hold to telegraph (`bossTelegraphSeconds` **0.7 s**), then summon **Barnacle King**; secures objective on kill |

**Beacon discovery timeline** (`V3DiscoverySystem` + `DemoPacingSettings.bossBeaconMinSeconds` = **180 s**): minimap reveal escalates from hidden → direction sector → exact marker → discovered as you sail and time advances; interaction is not time-gated once the POI exists in the world.

**Legacy demo POI** (non–V3 `StageGenerator` only): `LighthouseOfFortune` grants **+8% damage** for the run — not part of the Saltwind 12+4+beacon set.

---

## 8. Combat experiment profiles

Selected in main menu; stored in `CombatExperimentContext`. Default: **`D_FULL_COMBAT_V2`**.

| Profile | Slots | Opening draft | Directional aim | Director V2 |
|---------|------:|:-------------:|:---------------:|:-----------:|
| A_CURRENT | 2 | No | No | No |
| B_SIX_ONLY | 6 | Yes | No | No |
| C_SIX_DIRECTIONAL | 6 | Yes | Yes | No |
| D_FULL_COMBAT_V2 | 6 | Yes | Yes | Yes |

`SpawnDirector.ApplyCombatExperiment` sets density / HP / damage multipliers and enables V2 pack spawns.

---

## 9. Meta progression (prototype)

| Gate | Requirement |
|------|-------------|
| First conquest flag | `PlayerPrefs` `v3_first_conquest_complete` |
| Unlock all (playtest) | `V3PlaytestFlags.UnlockAllContent` |
| Home / Ledger preview | After first conquest |
| Captain Flint / Sparky | Tied to conquest in collection data |

Persistent **treasury doubloons** via `AppRoot.Save`; separate from in-run gold.

---

## 10. Tuning workflow

| Tool | Use |
|------|-----|
| **Editor:** `HighSeas → Tuning Window` | Edit `Assets/_Game/Data/*` assets; sync Resources before play |
| **Play mode:** **F9** — Spawn Director debug UI | Runtime clone of `SpawnDirectorSettings`; spawn rate / density / enemy HP & damage mults |
| **Play mode:** **F8** — playtest hotkeys | Weapons, items, Shift+N toggle auto spawn |
| **Play mode:** **C** — collision overlay | Hit volumes |
| Balance sim / gates | `Assets/_Game/Simulation/Balance/`, `BalanceGateTests` |

After changing spawn data in editor, run **Save & Sync Resources** (Tuning Window footer) so play mode loads updated `Resources/SpawnDirectorSettings`.

---

## 11. Known balance tensions (review notes)

1. **Two spawn brains:** Scripted waves in the asset vs V3 continuous threat + Director V2 packs — know which path is active (`ConfigureV3Pacing`, experiment profile).
2. **Enemy cap discontinuity:** V3 cap (~36) vs Director V2 (180) produces very different density feel.
3. **Creature mix** in asset is flat 50/50 with 1-minute ramp — may not match design doc minute-based ship/creature progression.
4. **XP pacing** vs **10-minute stage** — table above; adjust `DemoPacingSettings` and `GameMath.XPToNext` together.
5. All GDD **tier / biome / ledger** progression is only partially wired; most vertical power is in-run builds + experiment profile.

---

## 12. File index

| Topic | Path |
|-------|------|
| Constants & formulas | `Assets/_Game/Scripts/Core/GameConstants.cs` |
| Pacing | `Assets/_Game/Data/DemoPacingSettings.asset` |
| Level-up | `Assets/_Game/Data/LevelUpSettings.asset` |
| Spawn | `Assets/_Game/Data/SpawnDirectorSettings.asset` |
| Enemies / weapons | `Assets/_Game/Data/Enemies/`, `Weapons/` |
| V3 beats | `Assets/_Game/Scripts/V3/V3PressureBeatDirector.cs` |
| Spawn debug UI | `Assets/_Game/Scripts/Debug/SpawnDirectorDebugUI.cs` |
