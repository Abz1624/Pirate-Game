# Encounter spawn setup guide (enemies, profiles, V3 Saltwind)

This guide covers how **enemy definitions**, **spawn profiles**, and the **encounter spawn schedule** fit together, and how to play-test them in **`V3SaltwindRun`** (the V3 Saltwind / “saltwater” run scene).

---

## Architecture (30-second mental model)

| Layer | Asset / component | Role |
|--------|-------------------|------|
| **Enemy stats & art** | `EnemyDefinition` on `GameDatabase` | Base HP, damage, speed, behavior class, prefab, boss flags |
| **Spawn recipe** | `SpawnProfile` | Which enemy, how many, where, movement/attack overrides, elites, special behaviors |
| **When things spawn** | `EncounterSpawnSchedule` | Ambient timeline (`entries`), pressure beats, overtime, route/POI hooks |
| **Runtime** | `EncounterSpawner` on `Systems` | Reads schedule, places enemies off-screen, applies profile params in `EnemyWorld` |

**Important:** Play mode loads the schedule from **`Assets/_Game/Resources/EncounterSpawnSchedule.asset`**. After editing **`Assets/_Game/Data/EncounterSpawnSchedule.asset`**, sync to Resources (menu below).

**Ambient vs triggered**

- **Ambient** — `EncounterSpawnSchedule.entries` (time-based loops). Controlled by `CombatExperimentProfile.encounterSpawnEnabled` and paused during **overtime**.
- **Triggered** — `EncounterSpawner.TriggerProfile(...)` (POI powder cache, boss, route events, etc.). Still runs when ambient is off, as long as the run is in `Playing` / `BossFight` and not paused.

---

## 1. Set up enemies

### Option A — Use the project menu (recommended first time)

1. Open the Unity project: `Pirate Game Unity/`.
2. Run **HighSeas → Setup Enemy Visuals**.
   - Creates/updates `EnemyDefinition` assets under `Assets/_Game/Data/Enemies/`.
   - Writes the roster into **`Assets/_Game/Data/GameDatabase.asset`** (ids like `shark`, `cannonboat`, `hammerhead`, `kraken_spawn`, `barnacle_king`, …).
3. Run **HighSeas → Encounter Spawns → Sync Schedule To Resources** (keeps spawn schedule in sync; see section 3).

### Option B — Author manually

1. **Create → HighSeas → Enemy Definition** (or duplicate an existing asset in `Data/Enemies/`).
2. Assign fields (see [EnemyDefinition fields](#enemydefinition-fields)).
3. Add the asset to **`GameDatabase.enemies`**.

Enemy ids must stay **stable** — spawn profiles and code lookups (`hammerhead`, `kraken_spawn`, …) reference **`EnemyDefinition.id`**, not the asset file name.

---

## 2. Create spawn profiles

### Folder and assets

1. **HighSeas → Encounter Spawns → Ensure Spawn Profile Folder**  
   Ensures `Assets/_Game/Data/SpawnProfiles/`.
2. **Create → HighSeas → Spawn Profile** (save under `SpawnProfiles/`).
3. On each profile:
   - Set **`profileId`** (optional; defaults to asset name).
   - Assign **`enemy`** → pick the `EnemyDefinition` from the database.
   - Configure **spawn mode**, **timing**, **placement**, and optional **movement / attack / elite / special** blocks.

### Spawn modes (`SpawnMode`)

| Mode | Runtime behavior |
|------|------------------|
| **Single** | Spawns **one** enemy per schedule tick, until `timing.spawnCount` total spawns for that run instance. |
| **Sequence** | Same as Single (one per tick); use **`spawnIntervalSeconds`** between spawns. |
| **Burst** | Tries to spawn **`timing.spawnCount`** in one burst (respects cap/placement). |
| **Group** | Same count as Burst, but uses **`groupLayout`** for formation offsets. |
| **Continuous** | Spawns **one per tick** until **`timing.durationSeconds`** elapses (0 = never auto-complete). Interval = `spawnIntervalSeconds` (also scaled by spawn rate multiplier). |
| **TimedWave** | One enemy per tick like Sequence; completion when **`spawnCount`** reached. |
| **Triggered** | Not driven by ambient ticks; use only via **`TriggerProfile`** (POI, scripts, debug). |

### Group layouts (`SpawnGroupLayout`)

Used when spawning more than one in a burst/group: **Cluster, Ring, Line, Arc, FlankPair, Scatter**.

### Worked example — ambient shark trickle (good first test)

Create profile **`Shark Trickle`**:

| Section | Suggested values |
|---------|------------------|
| **Enemy** | `shark` |
| **Spawn mode** | `Continuous` |
| **Timing → spawnCount** | `1` (ignored for continuous except burst modes) |
| **Timing → startDelaySeconds** | `0` |
| **Timing → spawnIntervalSeconds** | `2.5` |
| **Timing → durationSeconds** | `0` (infinite) |
| **Timing → maxActiveFromProfile** | `12` (optional cap per profile) |
| **Placement** | Defaults OK: off-screen FOV, distance 10.8–18 m |

Add to schedule (next section) with **start time 5 s** so grace period clears first.

---

## 3. Wire the encounter spawn schedule

### Assets

| Path | Purpose |
|------|---------|
| `Assets/_Game/Data/EncounterSpawnSchedule.asset` | **Author here** |
| `Assets/_Game/Resources/EncounterSpawnSchedule.asset` | **Loaded at runtime** |

### Editor menus

| Menu | Action |
|------|--------|
| **HighSeas → Encounter Spawns → Ensure Empty Schedule Shell** | Creates/clears Data + Resources schedule (empty `entries`, null hooks). |
| **HighSeas → Encounter Spawns → Sync Schedule To Resources** | Copies Data → Resources (**required before Play**). |

### Schedule globals

| Field | Meaning |
|-------|---------|
| **gracePeriodSeconds** | No ambient spawns until run time exceeds this (default 2 s). |
| **maxActiveEnemies** | Hard cap on simultaneous enemies (default 55). |
| **globalMinSpawnGapSeconds** | Minimum time between spawn attempts for non-continuous modes (default 0.3 s). |

### Ambient entries (`entries[]`)

Each **Scheduled Spawn Entry**:

| Field | Meaning |
|-------|---------|
| **profile** | `SpawnProfile` to run |
| **startTimeSeconds** | Run elapsed time before this entry’s timeline starts |
| **loop** | If true, restarts after completion + cooldown |
| **loopIntervalSeconds** | Delay before loop restart (default 30 s) |

On a new run, `EncounterSpawner.ResetForNewRun()` registers all entries with profiles assigned.

### Fixed schedule slots (assign profiles on the schedule asset)

These are **references** used by V3 systems (not the `entries` array):

| Schedule field | Used by | Notes |
|----------------|---------|--------|
| **hammerheadBeat** | `V3PressureBeatDirector` @ 3:00 | Elite hammerhead; needs profile with `hammerhead` enemy + elite settings |
| **krakenBeat** | Pressure beat @ 8:00 | `kraken_spawn` elite |
| **swarmPulse** | Optional `GetScheduleProfile("swarm_pulse")` | Minute 4/7 pulses use **+25% spawn rate** via code; slot available for custom pulse content |
| **drownedPursuer** | Overtime director | `GetScheduleProfile("drowned_pursuer")` |
| **drownedHarrier** | Overtime | `GetScheduleProfile("drowned_harrier")` |
| **routeTreasureRaider** | Route event | Key `route_raider` |
| **routeCursedRingSwordfish** | Cursed chest event | Key `route_swordfish` |
| **routeCursedRingCannon** | Cursed chest event | Key `route_cannon` |
| **powderCacheAmbush** | Powder Cache POI | Key `powder_cache`; ambush count 3 |
| **bossSummon** | Treasury boss / **B** hotkey | Key `boss`; assign boss profile (e.g. `barnacle_king` + placement) |

If a slot is **empty**, that feature **silently skips** or logs a warning (POI/boss).

---

## 4. Play-test in V3 Saltwind Run

### Open the scene

- **Scene path:** `Assets/_Game/Scenes/v3/V3SaltwindRun.unity`  
  (Related WIP UI variant: `V3SaltwindRun UI STuff.unity`.)

CLI (optional):

```powershell
unity open "Pirate Game Unity"
# Then open V3SaltwindRun in the Editor and press Play
```

### Run flow

1. **Sync schedule:** **HighSeas → Encounter Spawns → Sync Schedule To Resources**.
2. Press **Play** on `V3SaltwindRun`.
3. Start the run from the V3 flow (main menu / run bootstrap in scene). `V3RunBootstrap` calls **`EncounterSpawner.ResetForNewRun()`** and applies the combat experiment profile.
4. Sail for **> grace period**; ambient entries should begin spawning.
5. Watch Console for placement warnings if islands block off-screen picks.

### In-run debug tools

| Input | Tool |
|-------|------|
| **F9** | **Encounter Spawner** debug panel (`SpawnDirectorDebugUI`) — spawn rate multiplier, toggle **ambient schedule**, live enemy cap |
| **F8** | V3 playtest hotkeys help (`V3RunPlaytestHotkeys`) |
| **B** | Summon treasury boss (requires **`bossSummon`** profile on schedule) |

### Combat experiment (density / ambient master switch)

Loaded from **`Resources/CombatExperiment/D_FULL_COMBAT_V2`** (or built-in defaults):

| Field | Effect on spawns |
|-------|------------------|
| **encounterSpawnEnabled** | `false` → ambient **`entries`** off; triggers still work |
| **spawnDensityMultiplier** | Scales ambient tick rate (`SetSpawnRateMultiplier`) |
| **enemyHpMultiplier / enemyDamageMultiplier** | Applied on spawned enemies via `EnemyWorld` |

Create asset: **Create → HighSeas → Combat Experiment Profile** and place under `Resources/CombatExperiment/` if you need custom tuning.

### Automated tests

```powershell
$env:UNITY_PROJECT_PATH = "c:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game\Pirate Game Unity"
unity test "Pirate Game Unity" --mode EditMode
```

Relevant fixture: **`EncounterSpawnerTests`** (profile resolver, experiment ambient flag, placement).

---

## 5. Parameter reference

### EnemyDefinition fields

| Field | Purpose |
|-------|---------|
| **id / displayName** | Stable id for code & telemetry; UI name |
| **category** | Ship vs creature (metadata) |
| **visualPrefab, visualScale, visualYawOffset, visualHeightOffset** | Presentation |
| **baseHP, baseDamage, moveSpeed, radius** | Combat & collision |
| **threatCost** | Legacy budgeting metadata |
| **xpValue, goldValue** | Rewards |
| **behavior** | Default AI when profile movement = **UseDefinition** (see `EnemyBehavior` enum) |
| **attackCooldown, preferredRange** | Default ranged/melee cadence |
| **isBoss / isMiniboss** | Boss checks, POI logic |
| **tags, biomeTag, placeholderColor** | Filtering / debug |

### Spawn profile — Identity & enemy

| Field | Purpose |
|-------|---------|
| **profileId** | Optional string id; `ResolvedId` falls back to asset name |
| **enemy** | Required `EnemyDefinition` reference |

### Spawn profile — Timing (`SpawnTimingSettings`)

| Field | Purpose |
|-------|---------|
| **spawnCount** | Target spawns for Single/Sequence/Burst/Group/TimedWave |
| **startDelaySeconds** | Added on top of schedule entry start time |
| **triggerTimeSeconds** | Reserved for trigger-authored flows |
| **repeatIntervalSeconds** | Repeat cadence where applicable |
| **durationSeconds** | **Continuous** mode: stop after this many seconds (0 = no limit) |
| **spawnIntervalSeconds** | Seconds between spawns (Sequence / Continuous / TimedWave) |
| **cooldownAfterCompleteSeconds** | After a run completes, block re-loop until elapsed |
| **maxActiveFromProfile** | Cap concurrent enemies from this profile (0 = unlimited) |
| **pauseWhenRunPaused** | Honor run pause (default true) |

### Spawn profile — Placement (`SpawnPlacementSettings`)

| Field | Purpose |
|-------|---------|
| **requireOutsidePlayerFov** | Prefer off-camera spawn ring |
| **distanceMin / distanceMax** | Annulus around player (meters) |
| **viewportMargin** | Extra off-screen margin (0–0.35) |
| **positionAttempts** | Retries if blocked by land/camera |
| **allowedArcDegrees / arcYawOffset** | Limit spawn arc (360 = full ring) |
| **groupSpacing** | Offset between group members |
| **fallbackPolicy** | **Skip** vs **RetryLater** when placement fails |

### Spawn profile — Elite (`SpawnEliteSettings`)

| Field | Purpose |
|-------|---------|
| **isElite** | Enable elite stat package |
| **affix** | `EliteAffix`: None, Armored, Swift, Volatile, Rich, Cursed, Captain |
| **hpMultiplier, damageMultiplier, speedMultiplier, sizeMultiplier** | Combat scaling |
| **goldMultiplier, xpMultiplier** | Reward scaling |

### Spawn profile — Movement (`SpawnMovementOverrides`)

| Field | Purpose |
|-------|---------|
| **pattern** | `EnemyMovementPattern` (UseDefinition maps from `EnemyDefinition.behavior`) |
| **speedMultiplier, turnResponsiveness** | Locomotion tuning |
| **preferredRange, interceptLead** | Ranged / intercept AI |
| **orbitRadius, orbitDirection** | Orbit pattern |
| **flankAngleDegrees** | Flank approach |
| **chargeWindUpSeconds, chargeDurationSeconds, chargeCooldownSeconds, chargeSpeedMultiplier** | Charge patterns |
| **separationStrength** | Swarm separation |

**Movement patterns:** UseDefinition, DirectChase, Intercept, Flank, Orbit, MaintainRange, Strafe, TelegraphedCharge, DiveLunge, Swarm, Drift, Stationary.

### Spawn profile — Attack (`SpawnAttackOverrides`)

| Field | Purpose |
|-------|---------|
| **pattern** | Contact, SingleShot, BurstVolley, SpreadVolley, TelegraphedCharge, AreaDenial, SuicideExplosion, None, UseDefinition |
| **contactDamageEnabled** | Bump damage on/off |
| **attackRange, windUpSeconds, cooldownSeconds** | Ranged/melee timing |
| **projectileCount, spreadDegrees, projectileSpeed, projectileLifetime** | Projectile volleys |
| **burstShots, burstGapSeconds** | Burst fire |
| **areaRadius, areaDurationSeconds** | Zone denial |

### Spawn profile — Special behavior

Set **`specialBehavior`** to one of:

| Behavior | Settings block | Summary |
|----------|----------------|---------|
| **None** | — | Definition + movement/attack only |
| **Formation** | `formation` | Leader/follower, shapes (Line, V, Wedge, Box), break conditions |
| **Summon** | `summon` | Spawns **`minionProfile`** on interval, max alive, lifetime |
| **TetherBeam** | `tether` | Range-limited beam damage + slow |
| **SupportAura** | `supportAura` | Heal/speed aura for allies |
| **PhaseDash** | `phaseDash` | Telegraphed dash phases |
| **TerrainAmbush** | `terrainAmbush` | Hide until reveal trigger |
| **Adaptive** | `adaptive` | Reacts to kite/stand-still/DPS/low HP/enemy count signals |

Each block’s fields are defined in `SpawnProfileModules.cs` (Inspector tooltips match code ranges).

---

## 6. Troubleshooting

| Symptom | Likely cause |
|---------|----------------|
| No enemies at all | Empty **`entries`** and no triggers; or **`encounterSpawnEnabled`** false; or still in **grace period** |
| Changes ignored in Play | Forgot **Sync Schedule To Resources** |
| Pressure beat toast but no elite | **`hammerheadBeat` / `krakenBeat`** not assigned on schedule |
| Powder cache / boss does nothing | **`powderCacheAmbush` / `bossSummon`** null on schedule |
| “Skipped — no spawn slot” | At **maxActiveEnemies** or placement failed repeatedly |
| Spawns only on-screen | Disable **`requireOutsidePlayerFov`** temporarily to debug placement |

---

## 7. File index

| Path | Description |
|------|-------------|
| `Assets/_Game/Scripts/Enemies/SpawnProfile.cs` | Profile asset |
| `Assets/_Game/Scripts/Enemies/SpawnProfileModules.cs` | Nested setting types |
| `Assets/_Game/Scripts/Enemies/SpawnProfileEnums.cs` | Enums |
| `Assets/_Game/Scripts/Enemies/EncounterSpawnSchedule.cs` | Schedule asset |
| `Assets/_Game/Scripts/Enemies/EncounterSpawner.cs` | Runtime spawner |
| `Assets/_Game/Scripts/Enemies/EnemySpawnRuntimeParams.cs` | `SpawnProfileResolver` |
| `Assets/_Game/Scripts/Editor/HighSeasEncounterSpawnSetup.cs` | Editor menus |
| `Assets/_Game/Scripts/V3/V3PressureBeatDirector.cs` | Timed beats & pulses |
| `Assets/_Game/Scenes/v3/V3SaltwindRun.unity` | Primary V3 play-test scene |

For older spawn-director documentation, see **`docs/GAME_BALANCE_AND_PROGRESSION.md`** — that doc still describes the **retired** `SpawnDirector`; use **this guide** for the current **`EncounterSpawner`** pipeline.
