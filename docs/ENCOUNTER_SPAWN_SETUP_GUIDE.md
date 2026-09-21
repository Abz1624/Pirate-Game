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
| **DiveLunge** | Direct chase ( **`swordfish`** has a dedicated spring/lunge routine when legacy-spawned) | Contact | DiveLunge | Contact |
| **TelegraphedCharge** | Slow approach → brief pause → **2.5×** speed charge, then reset | Contact | TelegraphedCharge (uses profile charge timings when spawned from profile) | Contact |
| **MaintainRange** | Close if too far; strafe if too close; hold band at `preferredRange` | **Hostile projectile** when in range band | MaintainRange | SingleShot |
| **FormationRanged** | Same band/strafe as MaintainRange | SingleShot (ships may hold still while cooling down) | MaintainRange | SingleShot |
| **StrafeBurst** | Tangent strafe + slight inward drift | Uses attack cooldown on movement tick; ranged if ship + in band | Strafe | Contact |
| **DriftPulse** | Normal chase speed | Contact (cooldown ticks on movement) | Drift | Contact |
| **SuicideApproach** | Chase at **1.4×** speed | Contact (profile maps to SuicideExplosion attack enum; see attack table) | DirectChase | SuicideExplosion |
| **StationaryTurret** | Does not move | Ranged if category/range rules allow | Stationary | Contact unless definition maps to ranged |
| **PhaseDash** | *No dedicated legacy branch* — falls through to default chase | Contact | DirectChase (maps like default) | Contact |
| **TentacleZone** | *Reserved* — default chase in legacy | Contact | DirectChase | Contact |
| **SupportBuff** | *Reserved* — default chase in legacy | Contact | DirectChase | Contact |

**Stock roster examples:** `cannonboat` / reef crab use **MaintainRange**; `shark` **DirectChaser**; `swordfish` **FastSwarm**; `hammerhead` **FlankingChaser**; `kraken_spawn` **TankChaser**; `manta_ray` **FlankingChaser**; miniboss **MaintainRange**; boss **TankChaser**.

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
| **DiveLunge** | Currently same as DirectChase (use legacy swordfish or **PhaseDash** special for lunge fantasy) | `speedMultiplier` |
| **Swarm** | Aggressive chase at **1.2×** speed | `speedMultiplier`, `separationStrength` (via `EnemyWorld.ApplySeparation`) |
| **Drift** | Steady chase at normal speed | `speedMultiplier` |
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
| **PhaseDash** | Burst reposition | Cooldown → wind-up (stand still) → dash at `dashSpeed` along **TowardPlayer / Flank / AwayThenReengage** → pause; repeats up to `maxConsecutiveDashes` before long cooldown. Overrides velocity during dash. |
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
