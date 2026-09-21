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
3. Add the asset to **`GameDatabase.enemies`**, or run **HighSeas → Sync Enemies Folder To GameDatabase** to register **every** `EnemyDefinition` under `Assets/_Game/Data/Enemies/` (sorted by `id`, duplicates skipped with a Console warning).

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
| *(playtest)* | **Enemy identity labels** above each active enemy (`EnemyIdentityDebugLabels`) — id, behavior, profile, HP/dmg/speed when **`EnableEnemyIdentityLabels`** is true |
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

### SpawnProfile asset (all Inspector sections)

Create via **Assets → Create → HighSeas → Spawn Profile**. Nested structs live in `SpawnProfileModules.cs`.

#### Identity & enemy

| Field | Type | Default / range | Purpose |
|-------|------|-----------------|--------|
| **profileId** | string | empty → asset name | Optional stable id for logs/tools; **`ResolvedId`** uses this or the asset file name |
| **enemy** | EnemyDefinition | — | **Required.** Which unit to spawn; must exist on **`GameDatabase.enemies`** |

#### Spawn (top-level)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **spawnMode** | `SpawnMode` | Single | How **`EncounterSpawner`** executes this profile (see table below) |
| **groupLayout** | `SpawnGroupLayout` | Scatter | Offset pattern when a burst spawns **multiple** positions in one execution |
| **timing** | `SpawnTimingSettings` | see below | Counts, delays, intervals, caps |
| **placement** | `SpawnPlacementSettings` | see below | Off-screen ring, arc, retries |

**`SpawnMode`**

| Value | Spawner behavior |
|-------|------------------|
| **Single** | Spawn **1** per tick until **`timing.spawnCount`** total for this schedule/trigger run |
| **Sequence** | Same as Single (1 per tick); use **`spawnIntervalSeconds`** between ticks |
| **Burst** | One execution tries **`timing.spawnCount`** spawns at once (group layout applies) |
| **Group** | Same count as Burst; uses **`groupLayout`** offsets |
| **Continuous** | Spawn 1 per tick until **`durationSeconds`** elapses (**0** = never auto-complete) |
| **TimedWave** | 1 per tick until **`spawnCount`** reached |
| **Triggered** | Not driven by ambient schedule ticks; use **`EncounterSpawner.TriggerProfile`** only |

**`SpawnGroupLayout`** (multi-spawn offsets via **`SpawnPlacementService.OffsetGroupPosition`**)

| Value | Layout |
|-------|--------|
| **Cluster** | Tight cluster |
| **Ring** | Ring around anchor |
| **Line** | Line formation |
| **Arc** | Arc in front/side |
| **FlankPair** | Pair on flanks |
| **Scatter** | Scattered offsets (default) |

#### Timing — `timing` (`SpawnTimingSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **spawnCount** | int ≥ 0 | 1 | Total spawns for Single / Sequence / Burst / Group / TimedWave (not total for Continuous) |
| **startDelaySeconds** | float ≥ 0 | 0 | Added to schedule entry **`startTimeSeconds`** before first spawn attempt |
| **triggerTimeSeconds** | float ≥ 0 | 0 | *Authoring hook; not read by **`EncounterSpawner`** today* |
| **repeatIntervalSeconds** | float ≥ 0 | 0 | *Authoring hook; not read by spawner today* |
| **durationSeconds** | float ≥ 0 | 0 | **Continuous** only: stop after this many seconds of profile run time (**0** = infinite) |
| **spawnIntervalSeconds** | float ≥ 0.05 | 0.5 | Gap between spawns for Sequence / Continuous / TimedWave; also scales with spawn rate multiplier |
| **cooldownAfterCompleteSeconds** | float ≥ 0 | 0 | After a profile run marks **complete**, block that run until run time passes this cooldown (loop entries) |
| **maxActiveFromProfile** | int ≥ 0 | 0 | Max living agents with this profile’s asset identity (**0** = no per-profile cap) |
| **pauseWhenRunPaused** | bool | true | *Reserved; run pause already stops **`EncounterSpawner`** updates* |

Schedule **`ScheduledSpawnEntry`**: **`profile`**, **`startTimeSeconds`**, **`loop`**, **`loopIntervalSeconds`** — see [§3](#3-wire-the-encounter-spawn-schedule).

#### Placement — `placement` (`SpawnPlacementSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **requireOutsidePlayerFov** | bool | true | Reject candidates visible in spawn camera viewport (with margin) |
| **distanceMin** | float ≥ 0 | 10.8 | Min horizontal distance from player (m) |
| **distanceMax** | float ≥ 0 | 18 | Max horizontal distance from player (m) |
| **viewportMargin** | 0–0.35 | 0.08 | Extra inset when testing off-screen |
| **positionAttempts** | int ≥ 1 | 16 | Random tries per spawn before giving up this attempt |
| **allowedArcDegrees** | 30–360 | 360 | Limit spawn directions to a wedge |
| **arcYawOffset** | −180–180 | 0 | Rotate the allowed arc (degrees) |
| **groupSpacing** | float ≥ 0.5 | 2.5 | Meters between members in Burst/Group layouts |
| **fallbackPolicy** | `SpawnFallbackPolicy` | RetryLater | **Skip**: fail spawn; **RetryLater**: retry after ~0.35 s |

Uses **`SpawnPlacementService`** + island/water checks.

#### Elite — `elite` (`SpawnEliteSettings`)

Applied in **`EnemyWorld.SpawnFromProfile`** (stacks with affix HP/speed rules).

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **isElite** | bool | false | Enable elite package + elite health bar |
| **affix** | `EliteAffix` | None | If elite and None, runtime uses **Rich** |
| **hpMultiplier** | float ≥ 0.1 | 1 | × max HP (also affix: Armored 1.8×, Swift 0.9×, Rich 1.25×) |
| **damageMultiplier** | float ≥ 0.1 | 1 | × contact / shot damage |
| **speedMultiplier** | float ≥ 0.1 | 1 | × move speed (× **`movement.speedMultiplier`**; Swift affix +1.35×) |
| **sizeMultiplier** | float ≥ 0.1 | 1 | Scales visual root if elite |
| **goldMultiplier** | float ≥ 0 | 1 | *Authoring; not applied on kill yet* |
| **xpMultiplier** | float ≥ 0 | 1 | *Authoring; not applied on kill yet* |

#### Movement — `movement` (`SpawnMovementOverrides`)

Resolved by **`SpawnProfileResolver`** → **`EnemyPatternMovement`**. See [§5.1](#51-behavior-types-detailed-reference) for each **`pattern`**.

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **pattern** | `EnemyMovementPattern` | UseDefinition | Override movement; **UseDefinition** maps from **`enemy.behavior`** |
| **speedMultiplier** | 0.1–3 | 1 | × **`EnemyDefinition.moveSpeed`** at spawn |
| **turnResponsiveness** | 0.1–3 | 1 | *Authoring; not applied in movement code today* |
| **preferredRange** | float ≥ 0 | 10 | **MaintainRange** band center (m) |
| **interceptLead** | 0–3 | 1 | Authoring; intercept uses chase direction from `EnemyWorld` today |
| **orbitRadius** | float ≥ 0 | 12 | *Authoring; orbit uses tangent motion, not strict radius hold* |
| **orbitDirection** | −1–1 | 1 | Orbit tangent sign |
| **flankAngleDegrees** | 0–90 | 35 | Flank approach angle |
| **chargeWindUpSeconds** | float ≥ 0 | 1.5 | **TelegraphedCharge**: creep phase length |
| **chargeDurationSeconds** | float ≥ 0 | 0.35 | *Authoring label; charge uses timer threshold, not this duration directly* |
| **chargeCooldownSeconds** | float ≥ 0 | 2 | *Authoring; profile charge timer reset differs from legacy AI* |
| **chargeSpeedMultiplier** | 1–4 | 2.5 | Burst speed during charge phase |
| **separationStrength** | 0–1 | 1 | Stored on agent; **`ApplySeparation`** does not scale by this yet |

#### Attack — `attack` (`SpawnAttackOverrides`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **pattern** | `EnemyAttackPattern` | UseDefinition | Ranged vs contact; see §5.1 (`EnemyAttackPattern`) |
| **contactDamageEnabled** | bool | true | Hull bump damage when overlapping player |
| **attackRange** | float ≥ 0 | 12 | Preferred ranged band center (m); band ≈ 0.7×–1.25× |
| **windUpSeconds** | float ≥ 0 | 0 | *Authoring; ranged tick does not use wind-up yet* |
| **cooldownSeconds** | float ≥ 0.1 | 2 | Seconds between ranged shots (profile spawn) |
| **projectileCount** | int ≥ 1 | 1 | *Authoring; fire code spawns one hostile projectile today* |
| **spreadDegrees** | 0–90 | 0 | *Authoring; not applied in fire code yet* |
| **projectileSpeed** | float ≥ 0 | 9 | Authoring; `GetHostileShotProfile` uses definition defaults today |
| **projectileLifetime** | float ≥ 0 | 1.4 | *Authoring; same* |
| **burstShots** | int ≥ 1 | 1 | *Authoring; burst volley not wired* |
| **burstGapSeconds** | float ≥ 0 | 0.15 | *Authoring* |
| **areaRadius** | float ≥ 0 | 4 | *Authoring; area denial not wired* |
| **areaDurationSeconds** | float ≥ 0 | 2 | *Authoring* |

#### Special — top-level

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **specialBehavior** | `EnemySpecialBehavior` | None | Enables one module below; see §5.1 (`EnemySpecialBehavior`) |
| **formation** | `FormationBehaviorSettings` | — | Used when **specialBehavior = Formation** |
| **summon** | `SummonBehaviorSettings` | — | **Summon** |
| **tether** | `TetherBeamSettings` | — | **TetherBeam** |
| **supportAura** | `SupportAuraSettings` | — | **SupportAura** |
| **phaseDash** | `PhaseDashBehaviorSettings` | — | **PhaseDash** |
| **terrainAmbush** | `TerrainAmbushSettings` | — | **TerrainAmbush** |
| **adaptive** | `AdaptiveCounterSettings` | — | **Adaptive** |

Modules are always visible in the Inspector; only the selected **`specialBehavior`** runs at runtime.

#### Formation — `formation` (`FormationBehaviorSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **role** | `FormationRole` | Auto | **Auto**: index 0 = leader; or force Leader/Follower |
| **shape** | `FormationShape` | Line | Follower slot offsets: Line, V, Wedge, Box |
| **slotSpacing** | float ≥ 0.5 | 3 | Meters between formation slots |
| **followDistance** | float ≥ 0.5 | 4 | *Authoring; followers steer to offset, not this distance* |
| **leaderSpeedBias** | 0.8–1.5 | 1.05 | *Authoring; not applied today* |
| **breakOnLeaderDeath** | bool | true | Followers revert to **Auto** movement if leader dies |
| **breakOnPlayerProximity** | bool | false | Break formation if player within **`breakProximityRadius`** |
| **breakProximityRadius** | float ≥ 0 | 6 | Meters for proximity break |
| **regroupRadius** | float ≥ 0 | 14 | Authoring; not used in `TickFormation` today |
| **followersInheritLeaderAttack** | bool | false | *Authoring; not wired* |

#### Summon — `summon` (`SummonBehaviorSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **minionProfile** | SpawnProfile | null | Profile passed to **`TriggerProfile`** each wave |
| **minionsPerWave** | int ≥ 1 | 1 | **`burstCountOverride`** per trigger |
| **spawnIntervalSeconds** | float ≥ 0.5 | 3 | Cooldown between summon waves |
| **maxAliveMinions** | int ≥ 1 | 4 | Internal counter cap (not exact world count) |
| **minionLifetimeSeconds** | float ≥ 0 | 0 | *Authoring; minions not auto-despawn by lifetime yet* |
| **inheritElite** | bool | false | *Authoring; not wired* |
| **despawnOnParentDeath** | bool | true | *Authoring; not wired* |

Requires **`EncounterSpawner.Instance`** in scene.

#### Tether beam — `tether` (`TetherBeamSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **maxRange** | float ≥ 1 | 14 | Must be within this to start/ sustain beam |
| **breakRange** | float ≥ 0.5 | 18 | Beyond this, beam resets |
| **tickDamage** | float ≥ 0 | 3 | Damage per tick to player |
| **tickIntervalSeconds** | float ≥ 0.1 | 0.5 | Time between damage ticks |
| **rampUpSeconds** | float ≥ 0 | 0.75 | Delay before first tick after in range |
| **playerMoveSpeedPenalty** | 0–0.6 | 0.15 | *Authoring; not applied to player speed today* |
| **breakOnIslandBlock** | bool | true | Linecast to player; island blocks beam |

#### Support aura — `supportAura` (`SupportAuraSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **radius** | float ≥ 1 | 8 | Horizontal radius for ally scan |
| **healPerSecond** | float ≥ 0 | 0 | Flat HP healed per pulse (× pulse interval) |
| **healPercentMaxHpPerSecond** | 0–0.2 | 0 | % max HP healed per pulse |
| **alliesOnly** | bool | true | *Authoring; loop skips self, heals other agents* |
| **allySpeedMultiplier** | 0.5–1.5 | 1.05 | Multiplies ally **`moveSpeed`** each pulse (compounds) |
| **pulseIntervalSeconds** | float ≥ 0.1 | 0.5 | Time between aura pulses |
| **disableWhenDamaged** | bool | false | Stops aura if carrier below 98% HP |

#### Phase dash — `phaseDash` (`PhaseDashBehaviorSettings`)

Requires **`specialBehavior = PhaseDash`**. Not the same as **`EnemyDefinition.behavior = PhaseDash`** (see [§5.1](#phasedash-on-the-enemy-definition-legacy-branch)).

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **dashCooldownSeconds** | float ≥ 0.5 | 4 | Cooldown after max consecutive dashes |
| **windUpSeconds** | float ≥ 0 | 0.6 | Stand still before dash |
| **dashDistance** | float ≥ 1 | 8 | *Authoring; dash uses **`dashSpeed`** × **`dashDurationSeconds`**, not distance* |
| **dashSpeed** | float ≥ 1 | 18 | Velocity during dash |
| **dashDurationSeconds** | float ≥ 0.05 | 0.25 | How long dash velocity applies |
| **damageReductionDuringDash** | 0–1 | 0.5 | *Authoring; not applied to damage taken today* |
| **postDashPauseSeconds** | float ≥ 0 | 0.4 | Idle after dash before next wind-up |
| **axis** | `PhaseDashAxis` | TowardPlayer | Dash direction: toward player, flank, or away |
| **maxConsecutiveDashes** | int ≥ 1 | 2 | Dashes before long cooldown |

#### Terrain ambush — `terrainAmbush` (`TerrainAmbushSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **trigger** | `TerrainAmbushTrigger` | WhenOffScreenThenReveal | **OnSpawn**, **WhenPlayerEntersRadius**, or **WhenOffScreenThenReveal** (see §5.1) |
| **hideRadius** | float ≥ 0 | 20 | *Authoring; hidden state uses zero velocity, not radius* |
| **stalkSpeedMultiplier** | 0.1–1 | 0.45 | *Authoring; not applied while hidden today* |
| **revealDistance** | float ≥ 0 | 10 | *Authoring; not used in reveal check today* |
| **revealBurstSpeedMultiplier** | 1–3 | 1.8 | × **`moveSpeed`** once when ambush reveals |
| **playerTriggerRadius** | float ≥ 0 | 12 | For **WhenPlayerEntersRadius** trigger |

#### Adaptive counter — `adaptive` (`AdaptiveCounterSettings`)

| Field | Type | Default | Purpose |
|-------|------|---------|--------|
| **evaluationWindowSeconds** | float ≥ 1 | 8 | *Authoring; **`TickAdaptive`** uses signal timers, not this window* |
| **enabledSignals** | `AdaptiveSignal[]` | kite + many enemies | Which signals may trigger adaptation |
| **kiteThresholdSeconds** | float ≥ 1 | 4 | Player speed > ~4.5 m/s for this long → kite signal |
| **standStillThresholdSeconds** | float ≥ 0 | 2 | Player nearly stopped → stand-still signal |
| **highDpsThreshold** | float ≥ 0 | 40 | Recent DPS from **`RunCombatSignals`** |
| **lowHpFraction** | 0–1 | 0.35 | Player HP % threshold |
| **manyEnemiesThreshold** | int ≥ 1 | 28 | Active enemy count on **`RunCombatSignals`** |
| **kiteResponseMovement** | `EnemyMovementPattern` | Flank | Movement override when adapting |
| **kiteResponseSpeedMultiplier** | 0.5–2 | 1.15 | × speed when adapting |
| **maxAdaptationStacks** | int ≥ 1 | 3 | Max stacks per agent |
| **adaptationCooldownSeconds** | float ≥ 0.5 | 6 | Cooldown between adaptations |

**Runtime note:** **`TickAdaptive`** currently reacts only to **`PlayerKiteTime`** among enabled signals; other signals are implemented in **`RunCombatSignals.IsSignalActive`** but need extra handling in **`TickAdaptive`** to take effect.

---

*Fields marked *Authoring* / *not wired* are safe to tune for future work but do not change play mode behavior yet.*

---

## 5.1 Behavior types (detailed reference)

Enemies use **two paths** depending on how they were spawned:

| Spawn path | Movement | Attack | Extras |
|------------|----------|--------|--------|
| **`SpawnFromProfile`** (encounter spawner) | `EnemyMovementPattern` on profile (or mapped from definition if **UseDefinition**) | `EnemyAttackPattern` on profile (or mapped from definition if **UseDefinition**) | `EnemySpecialBehavior` + modules |
| **Legacy `EnemyWorld.Spawn`** (no profile) | `EnemyDefinition.behavior` switch in `EnemyWorld` | Ranged only for ships / MaintainRange / FormationRanged; creatures mostly **contact** | No profile special behaviors |

Profile-spawned enemies always run **`EnemyPatternMovement`** + **`EnemySpecialBehaviorSystem`** when `specialBehavior` is set. Tune movement/attack on the **spawn profile** for encounter content.

### `EnemyDefinition.behavior` (`EnemyBehavior`)

Set on each **`EnemyDefinition`**. Used when a spawn profile’s movement/attack pattern is **UseDefinition**, and always used for **legacy** spawns without a profile.

| Behavior | Movement (legacy `EnemyWorld`) | Typical attack (legacy) | Default profile movement if **UseDefinition** | Default profile attack if **UseDefinition** |
|----------|--------------------------------|-------------------------|-----------------------------------------------|---------------------------------------------|
| **DirectChaser** | Steady chase toward player (intercept for creatures, encircle blend for ships) | Contact bump | DirectChase | Contact |
| **TankChaser** | Same as DirectChaser | Contact | DirectChase | Contact |
| **FlankingChaser** | Chase with sideways flank offset (alternates by agent id) | Contact | Flank | Contact |
| **FastSwarm** | Chase at **1.2×** move speed | Contact | Swarm | Contact |
| **DiveLunge** | **Profile spawn:** same spring/lunge as swordfish when close (burst speed ~2.45×). **Legacy:** plain chase (swordfish id only gets lunge in legacy path). | Contact | DiveLunge | Contact |
| **TelegraphedCharge** | Slow approach → brief pause → **2.5×** speed charge, then reset | Contact | TelegraphedCharge (uses profile charge timings when spawned from profile) | Contact |
| **MaintainRange** | Close if too far; strafe if too close; hold band at `preferredRange` | **Hostile projectile** when in range band | MaintainRange | SingleShot |
| **FormationRanged** | Same band/strafe as MaintainRange | SingleShot (ships may hold still while cooling down) | MaintainRange | SingleShot |
| **StrafeBurst** | Tangent strafe + slight inward drift | Uses attack cooldown on movement tick; ranged if ship + in band | Strafe | Contact |
| **DriftPulse** | **Profile spawn:** chase with **oscillating speed** (~0.78×–1.1×) via **Drift** movement pattern. **Legacy:** plain chase + faster attack cooldown tick (mostly invisible on contact-only). | Contact | Drift | Contact |
| **SuicideApproach** | Chase at **1.4×** speed | Contact (profile maps to SuicideExplosion attack enum; see attack table) | DirectChase | SuicideExplosion |
| **StationaryTurret** | Does not move | Ranged if category/range rules allow | Stationary | Contact unless definition maps to ranged |
| **PhaseDash** | Plain chase only (see note below) | Contact | DirectChase | Contact |
| **TentacleZone** | Plain chase only (reserved enum) | Contact | DirectChase | Contact |
| **SupportBuff** | Plain chase only (reserved enum) | Contact | DirectChase | Contact |

**Stock roster examples:** `cannonboat` / reef crab use **MaintainRange**; `shark` **DirectChaser**; `swordfish` **FastSwarm**; `hammerhead` **FlankingChaser**; `kraken_spawn` **TankChaser**; `manta_ray` **FlankingChaser**; miniboss **MaintainRange**; boss **TankChaser**.

#### PhaseDash on the enemy definition (legacy branch)

This is about **two different enums** that share a similar name:

| Enum | Where you set it | What it controls |
|------|------------------|------------------|
| **`EnemyBehavior.PhaseDash`** | `EnemyDefinition.behavior` | Legacy movement switch in **`EnemyWorld`** (non-profile spawns) and the **UseDefinition** movement map |
| **`EnemySpecialBehavior.PhaseDash`** | `SpawnProfile.specialBehavior` | Wind-up → dash → pause loop in **`EnemySpecialBehaviorSystem`** |

**Legacy branch** = one `case` in the big `switch (def.behavior)` inside **`EnemyWorld.TickBehavior`**. Behaviors like **TelegraphedCharge** or **FlankingChaser** have their own `case` blocks with custom logic. **`EnemyBehavior.PhaseDash` has no `case`**, so execution hits the **`default`** branch, which is the same as a basic chaser: move toward the player at normal speed. No dash wind-up, no burst speed, no cooldown cycle.

So if you only set **`EnemyDefinition.behavior = PhaseDash`** and spawn that enemy **without** a spawn profile (or with a profile that does not set **`specialBehavior = PhaseDash`**), it will **not** phase-dash—it will behave like a generic chaser.

**How to actually get dash gameplay**

1. Spawn via **`EncounterSpawner`** / a **`SpawnProfile`** (required for special behaviors).
2. Set **`specialBehavior`** to **`PhaseDash`** on that profile.
3. Tune **`phaseDash`** (cooldown, wind-up, dash speed, axis, etc.).
4. Optionally set **`movement.pattern`** to something mild (e.g. **MaintainRange** or **DirectChase**) for movement between dashes—the dash **overrides velocity** while the dash timer is active.

**Profile movement when behavior is PhaseDash:** `SpawnProfileResolver.MapLegacyBehavior` does not list **PhaseDash**, so **UseDefinition** movement resolves to **DirectChase**, not a dash pattern. Dash comes only from the **special behavior** module, not from the definition behavior enum.

Same idea for **TentacleZone** and **SupportBuff** on **`EnemyDefinition`**: they are enum placeholders today; use spawn profiles and future/special modules rather than expecting unique logic from the definition field alone.

---

### `EnemyMovementPattern` (spawn profile → `movement.pattern`)

Overrides definition when not **UseDefinition**. Implemented in **`EnemyPatternMovement.ComputeVelocity`**.

| Pattern | What the player sees | Key tunables |
|---------|----------------------|--------------|
| **UseDefinition** | Uses **`MapLegacyBehavior`** table above from linked `EnemyDefinition.behavior` | Set behavior on definition |
| **DirectChase** | Moves along computed chase direction at full speed | `speedMultiplier` |
| **Intercept** | Same velocity as DirectChase today (chase direction already includes creature intercept / encircle from `EnemyWorld`) | `speedMultiplier`, `interceptLead` (reserved for future lead tuning) |
| **Flank** | Approach with sideways offset; flank side alternates by agent id | `flankAngleDegrees`, `speedMultiplier` |
| **Orbit** | Circles player with tangent motion + slight inward pull | `orbitRadius`, `orbitDirection`, `speedMultiplier` |
| **MaintainRange** | Closes if farther than preferred + 3 m; strafes if closer than preferred − 2 m; holds if in band | `preferredRange`, `speedMultiplier` |
| **Strafe** | Side-step around player with slight inward component | `speedMultiplier` |
| **TelegraphedCharge** | Creep (`0.2×` speed) while `chargeTimer > chargeWindUp`, then burst at `chargeSpeedMult × speed` | `chargeWindUpSeconds`, `chargeDurationSeconds`, `chargeCooldownSeconds`, `chargeSpeedMultiplier` |
| **DiveLunge** | Currently same as DirectChase in pattern-only path; **profile spawns** with **DiveLunge** behavior use swordfish-style lunge in **`EnemyWorld`** | `speedMultiplier` |
| **Swarm** | Aggressive chase at **1.2×** speed | `speedMultiplier`, `separationStrength` (via `EnemyWorld.ApplySeparation`) |
| **Drift** | Chase with **pulsing speed** (sine wave ~0.78×–1.1×) | `speedMultiplier` |
| **Stationary** | Zero velocity from pattern (still may attack at range) | — |

While **`TerrainAmbush`** is hidden (`ambushHidden`), movement is forced to **zero** regardless of pattern.

---

### `EnemyAttackPattern` (spawn profile → `attack.pattern`)

Resolved in **`SpawnProfileResolver.ResolveAttack`**. Ranged logic runs in **`EnemyWorld.TickRangedAttack`** for profile spawns.

| Pattern | Intended role | Current runtime |
|---------|---------------|-----------------|
| **UseDefinition** | Derive from `EnemyDefinition.behavior` | MaintainRange / FormationRanged → **SingleShot**; SuicideApproach → **SuicideExplosion**; else **Contact** |
| **Contact** | Hull bump damage only | Skips ranged tick; contact damage if `contactDamageEnabled` |
| **None** | No ranged; can still use contact if enabled | Skips ranged tick |
| **SingleShot** | One hostile projectile when player is in range band | Fires via `HostileProjectileWorld`; cooldown from profile `attackCooldown` or definition recovery |
| **BurstVolley** | Multiple shots per cooldown | *Enum + profile fields exist; firing still uses single-shot path today* |
| **SpreadVolley** | Cone of projectiles | *Not differentiated in fire code yet* |
| **TelegraphedCharge** | Wind-up melee/ranged charge attack | *Not differentiated in fire code yet* |
| **AreaDenial** | Persistent zone | *Not differentiated in fire code yet* |
| **SuicideExplosion** | Explode on contact / death | Mapped from SuicideApproach; still primarily **contact** damage unless extended |

**Ranged band (profile):** distance must be between **0.7×–1.25×** of `attackRange` (or definition `preferredRange`). Line-of-sight blocked by islands cancels the shot.

---

### `EnemySpecialBehavior` (spawn profile → `specialBehavior`)

Runs **after** base movement each tick in **`EnemySpecialBehaviorSystem`**. Only applies to **profile-spawned** agents.

| Special behavior | Purpose | Runtime summary |
|------------------|---------|-----------------|
| **None** | Standard move + attack only | — |
| **Formation** | Leader + followers in a shape | **Followers** steer toward slot offset (Line / V / Wedge / Box). Can **break** if leader dies, or player enters `breakProximityRadius`. Leader uses normal movement pattern. |
| **Summon** | Parent spawns minions | Every `spawnIntervalSeconds`, calls **`EncounterSpawner.TriggerProfile(minionProfile, minionsPerWave)`** up to `maxAliveMinions`. Requires minion profile assigned. |
| **TetherBeam** | Risk if you stay in beam range | After `rampUpSeconds` inside `maxRange`, applies `tickDamage` every `tickIntervalSeconds`; breaks beyond `breakRange` or island linecast if `breakOnIslandBlock`. |
| **SupportAura** | Buff nearby allies | Pulses every `pulseIntervalSeconds`: heal flat + % max HP and multiply ally `moveSpeed` within `radius`. Can disable when damaged. |
| **PhaseDash** | Burst reposition (profile only) | Cooldown → stand still (wind-up) → high-speed dash along **PhaseDashAxis** → short pause; may chain **`maxConsecutiveDashes`** before a long cooldown. **Overrides `velocity` during the dash.** Does **not** come from `EnemyDefinition.behavior = PhaseDash`; see **PhaseDash on the enemy definition (legacy branch)** above. |
| **TerrainAmbush** | Ambush reveal | Starts **hidden** (no movement). **OnSpawn**, **player radius**, or **on-screen** trigger calls reveal and applies `revealBurstSpeedMultiplier` to move speed. |
| **Adaptive** | Counter kiting | When **`RunCombatSignals`** reports enabled signals (today only **PlayerKiteTime** is checked in code), stacks up to `maxAdaptationStacks`: applies `kiteResponseSpeedMultiplier` and switches movement to `kiteResponseMovement` for a cooldown window. Other signals (stand still, DPS, low HP, many enemies) are implemented in **`RunCombatSignals`** but require extending **`TickAdaptive`** to react to them. |

**Adaptive signals** (`AdaptiveSignal` on profile): **PlayerKiteTime** (speed > ~4.5 m/s sustained), **PlayerStandStill** (speed < ~0.6 m/s), **HighPlayerDps**, **LowPlayerHp**, **ManyActiveEnemies**. Enable per profile via `adaptive.enabledSignals`.

---

### `EliteAffix` (spawn profile → `elite.affix`)

Applied in **`EnemyWorld.SpawnFromProfile`** when `elite.isElite` is true (defaults affix to **Rich** if elite flag set but affix None).

| Affix | Runtime effect |
|-------|----------------|
| **None** | No elite scaling (non-elite) |
| **Armored** | **1.8×** HP |
| **Swift** | **0.9×** HP, **1.35×** move speed |
| **Rich** | **1.25×** HP; richer pickup roll on death |
| **Volatile** | *Authoring slot; no extra hook in `EnemyWorld` spawn math today* |
| **Cursed** | *Authoring slot; no extra hook in spawn math today* |
| **Captain** | *Authoring slot; no extra hook in spawn math today* |

All elites get **~1.38×** visual scale and an elite health bar when spawned.

---

### `FormationRole` / `FormationShape` (with **Formation** special)

| Role | Meaning |
|------|---------|
| **Auto** | First spawn index in a group is leader; others followers |
| **Leader** | Drives formation; followers track this agent id |
| **Follower** | Uses formation offsets only |

| Shape | Follower offset layout |
|-------|-------------------------|
| **Line** | Side-by-side along X |
| **V** | Staggered V behind leader |
| **Wedge** | Tighter wedge behind leader |
| **Box** | Alternating left/right columns |

---

### `TerrainAmbushTrigger`

| Trigger | Reveal when |
|---------|-------------|
| **OnSpawn** | Immediately on spawn (still hidden one frame until reveal) |
| **WhenPlayerEntersRadius** | Player within `playerTriggerRadius` |
| **WhenOffScreenThenReveal** | Enemy enters camera view (visible on screen) |

---

### `PhaseDashAxis`

| Axis | Dash direction |
|------|----------------|
| **TowardPlayer** | Straight at player |
| **Flank** | Perpendicular to player (side alternates by agent id) |
| **AwayThenReengage** | Away from player (re-engage on next dash cycle) |

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
