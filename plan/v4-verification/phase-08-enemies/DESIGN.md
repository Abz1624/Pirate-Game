# M8 — Enemy World: design record

Source: read-only design pass over the V4 code at `c26cdaf7` (M7) and the legacy enemy code, before any M8 code was written. File:line references are to that commit.

## Decisions taken from the design pass

| # | Decision | Why |
|---|---|---|
| 1 | The frame-order adapters (`LegacyUpdateSlot`, `LegacyOrder50Slot`, `LegacySpawnSlots`, the frame parts of `LegacyStageWiring`) are re-tagged `DeletedInPhase(14)` instead of 8 | They keep the ambient, weapon, stage, overtime, storm, beat, storm-edge, discovery and interaction ticks at V3's positions relative to `RunManager.Update` (the run clock). They can only go when the clock and ship are V4 (M14). Recorded as a deviation |
| 2 | The V4 enemy world ticks inside the shell tick, exactly where `LegacyEnemyWorldPort.Tick` runs today (`V4Game.Update`, order −100): boss director → enemies → player projectiles → hostile projectiles → legacy pickups + powerups → statuses → area-denial release. Not in the fixed-step `TickAll` | Same sub-positions and `Time.deltaTime` as V3 |
| 3 | Enemy views stay on V3's `EnemyViewPool` rooted at `/Systems`, created synchronously inside `Spawn` (`LegacyEnemyViews`, deleted M14); health bars, burn flash, wake, spring too. `PoolCatalogConfig.prewarmEnemyViews` stays off | Each new view instantiate toggles `/Systems` off/on (OnDisable/OnEnable on its components), and six ship models keep Island-layer colliders until end of frame (R-81), which feed island checks and spawn placement |
| 4 | Hit/kill feedback stays legacy (`LegacyEnemyFeedback`, M14): damage highlight, `FloatingDamageNumbers` (draws `UnityEngine.Random`, 14/frame cap), camera shake call, SFX, splash, explosions, `TriggerBus.RaiseHit/RaiseKill` | Global Random draws feed pickup offsets → pilot → trajectory |
| 5 | Enemy slots: plain array, lowest-free linear scan (not `SlotPool`); unit ids per run starting at 1, incremented only on a successful spawn; same for hostile projectiles (96) and hazard slots (24, lazy) | Slot order drives targeting, kill order, Director-stream gold draws and pilot sums; unit ids drive orbit sign, encircle angle, flank parity, steer bias, GunnerCog stagger, Marks mapping, trace uids |
| 6 | Statuses keyed by unit id (plan's intended difference) becomes a config switch, default on, off in the v3-parity preset | Burn applied after a killing hit lands on the freed slot and a same-frame spawn inherits it in V3 |
| 7 | Silent-loss ports: `HellfirePatchSystem` (tick slot where `HellfirePatchWorld.Ensure` was), barrel enemy damage (slot on the barrel world object), `LegacyTelemetrySampler` replacing `CombatTelemetryDriver`, DevTools recorder/pilot/console switched to V4 contracts with the V3-golden fallback kept | Each reads the legacy EnemyWorld and would silently stop |
| 8 | Existing retry bug: `SaltwindHazardTelegraph._player` is cached statically, so from the second run in a process Kraken traps, mines and nets never hurt. The per-run V4 port fixes it (register R-99) | Goldens are first runs; traces unaffected |
| 9 | Hammerhead latch: the first elite hammerhead sets the second charge on the shared registry profile for every hammerhead (for the process in V3). Ported as a per-run latch (register R-100) | Faithful within a run |

## Build order

0. Contracts (`IEnemyWorldView`, `IHostileProjectileView`, `IHazardView`, `IEnemyDevControl`, …) implemented over the **legacy** world; switch recorder, pilot, console and `LegacyEnemyView` to them (behaviour-neutral; checked with targeted tests, no extra trace run).
1. `EnemyRegistry` + archetype runtime + spawn resolver (IEnemySpawnSink, IEnemyQuery) — lockstep spawn test vs legacy.
2. Movement system + per-archetype modules, separation, overlaps — per-archetype lockstep tests (60 s scripted paths).
3. Attacks, specialist attacks, hostile projectiles, hazard telegraphs, encounter budget, `PlayerDamageService` — lockstep damage/projectile/hazard logs.
4. Statuses, hit path, kill fan-out (V3 order), encounter tracker, hellfire, barrel damage — order tests with recording fakes.
5. Special behaviour modules (no data uses them; parity vs the static legacy functions).
6. Switch-over in one batch: bind V4 contracts, rebind weapons/boss adapters, replace the shell's enemy tick, stop the telemetry driver, stop Hit/Kill republishing in `LegacyEventFeed`, remove `EnemyWorld`/`StatusController` from the V4 scenes (`V4SceneSetup.ApplyM8`), delete the M8 adapters, `V4Phase.Current = 8`, then the single end-of-phase trace set.
   Switch-over checklist (from the design pass and the independent review of steps 0–5):
   - Build the world in the shell with `LegacyEnemyViewSink` rooted at **/Systems** (V3's EnemyWorld had no enemy root; `EnemyViewPool.Acquire` toggles its root), capacity `QualityProfile.enemyCap`, definitions + `BuildRules` after the runtime database, scene values (`EnemyWorldSettings`).
   - `Despawned` → statuses `ClearIndex` → `RunEncounterTracker.ReleaseAgentMapping` → `EncounterSpawner.Instance?.NotifyAgentDespawned(profile)`, in that order.
   - `SuppressHitHighlight` from `WeaponConfig.visual.noHitHighlight`; `EnemyStatuses.Applied` → `TriggerBus.RaiseStatusApplied`; `Summon` = V3's call (queue on the switched-off `EncounterSpawner` when it exists, return true) so `aliveMinionCount` counts as in V3.
   - Frame sequence in the shell (replacing `LegacyEnemyWorldPort.Tick`): boss director → `EnemySimulation.Tick` → player projectiles → hostile shots → legacy pickups + floor powerups (`LegacyBuildAdapter`, M9) → statuses → `EncounterBudget.TickAreaDenialRelease(run clock, areaDenialReleaseSeconds)` → same-tick boss death. `ClearTransientWorlds` keeps pickups/projectiles and clears the V4 shots.
   - Rebind: weapons (`LegacyWeaponWiring` enemies → `EnemyWorldContracts`), boss director (`LegacyBossControl`/`LegacyHostileProjectiles` → contracts/`HostileProjectiles`; `LegacyBossEffects`/`LegacyBossObjective` re-tagged to their real owners), spawn sink, presenters' enemy view, DevTools views, `LegacyHazardTelegraphView` → V4 hazards (add the hazard kind to the view for its colours; keep the mortar timed rings).
   - Hellfire: a frame slot added where `HellfirePatchWorld.Ensure()` ran; `SpawnFlamePatch` → `HellfirePatches`. Barrels: keep `V3ExplosiveBarrelWorld.Ensure` (its markers), disable the component, tick `ExplosiveBarrels` from a slot on the same object.
   - Stop: `EnsureHostileProjectiles`, `EnsureTelemetryDriver` (→ a V4 sampler), `EnemyIdentityDebugLabels.Ensure` (reads the legacy world; DevTools port later). Remove `EnemyWorld`/`StatusController` from V4SaltwindRun and V4ModuleLab (`V4SceneSetup.ApplyM8`).
   - Re-tag the frame slots and `LegacyCombatProfileOverride` (the registry still feeds spawn parameters and the combat dispatch) to M14; delete the adapters the switch replaces; `V4Phase.Current = 8`.
   - Unit ids: a V4 run starts at 1 like a first V3 run (V3 never reset its counter; golden runs are first runs).
7. Only after exact parity: fix switches (R-19/R-20 readers, R-22 knockback, unit-id statuses, run-clock wobble), each off in the parity preset.

## Determinism hazards to preserve

- Director stream: kill gold (`ResolveKillGoldDrop`) shares the stream with spawning. Powerup stream: floor powerup roll. Combat stream: hellfire damage rolls. No RunRng draws in the enemy AI.
- `Time.time` reads (Deckhand wobble, Drift pulse); `Time.deltaTime` used instead of dt in a few places (TreasureRaider flee, hellfire) — read through an `ILegacyTime` seam.
- Physics/IslandObstacles queries (depenetrate, avoidance steering by unit id, capsule movement, charge island stop, line-of-sight linecasts, hostile shot blocking) — view colliders must exist at the same moments.
- Scene values become `EnemyWorldConfig` defaults: island avoidance probe 6 (code default 10), island padding 0.35, ship padding 0.55, separation padding 0.2, collide with islands on. Capacity from `QualityProfile.enemyCap`; statuses 512; hostile shots 96.
- Mid-tick despawn aliasing (bomb boat explodes inside TickCombat, the loop continues on the cleared agent), shared spatial hash rebuilt at tick start and in every nearest query with caps 8/32/64, separation accumulation order (2 passes, i<j), verbatim Vector3/Quaternion expression order.
- Reef Crab uses `def.moveSpeed` (ignores elite/speed multipliers); `TryReserveCharge` never released; area denial never released (R-06).

## Recorded deviations of the port (review of steps 0–5)

- `Time.time` / `Time.deltaTime` are read directly where V3 read them (Deckhand wobble, Drift pulse, Treasure Raider flee, Shark recover) instead of through an `ILegacyTime` seam: same frame, same values; a run-clock switch comes with the R-69 fix.
- V4 code still reads V3's `EnemyCombatProfileRegistry` (combat dispatch and profile values, filled from config by `LegacyCombatProfileOverride`) and the shared `PlayBounds`, `IslandObstacles`, `GameConstants`, `GameMath`, `SpatialHash` and Unity physics; none holds per-run state of the old world.
- Statuses stay slot-keyed as V3 through the switch-over; the unit-id switch (default on, off in the parity preset) is added afterwards (step 7).

## Name branches → archetype fields

Reachable in V4 runs: initial charge timer (`EnemyWorld.cs:249-251`), swordfish spring view (`:831`), initial attack cooldown (`:1377-1382`), `powder_kegs` hit flag (`:1527`, weapon config), hammerhead latch (`SaltwindCombatBehaviorSystem.cs:64`), repair-tender exclusion (`:685`, `SaltwindSpecialistCombat.cs:147`), powder barge initial phase timer (`SaltwindCombatAgentStateStore.cs:34`), ranged support (`EnemyRangedAttackRules.cs:11-17`), cannonboat default single shot (`EnemySpawnRuntimeParams.cs:206`), speed cap multiplier (`SaltwindCombatBehaviorSystem.cs:12-21`). Dead in V4 runs (ported, tested only against the legacy functions): `EnemyWorld.cs:919, 958, 1083, 1276, 1333-1334, 1361, 1387, 1403-1416`, `ReefCrabMinibossBehavior.Handles`.

## Legacy reader audit

See the CHECKLIST once M8 is implemented; the design pass classified every reader of `EnemyWorld`, `StatusController`, `HostileProjectileWorld`, the static `RunEncounterTracker` and `CombatTelemetryDriver` reachable from V4SaltwindRun and V4ModuleLab (ported / switched / null-safe with the loss stated / not in V4 runs).
