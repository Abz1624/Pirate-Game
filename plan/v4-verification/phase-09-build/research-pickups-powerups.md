# M9 research: V3 pickups, floor powerups and the EncounterSpawner sink (read-only map)

Paths relative to `Pirate Game Unity/Assets/_Game/`. Collected at the M9 start (commit 33f8b95e).

Corrections to the plan's assumptions:
- `LegacyDeadCalmProbe` no longer exists: `LegacyEncounterExecutor.Multiplier` (LegacySpawnAdapters L221–234) does its job (a test name `V4SpawnM3Tests` L739 and phase-07 CHECKLIST L62 keep the old name).
- `PickupWorld` and `V3FloorPowerupWorld` raise no C# events; V4's `CombatEventKind.Pickup`, `PowerupCollected`, `PlayerHealed` (CombatEvents L13, 19, 21) have no producers.
- The trace recorder records no pickup events (only gold/level samples); pickups reach traces via R-74 visual pins and `PlayerPilot` steering.
- No V4 presenter reads pickups (`PickupWorld` is on V4HudM6Tests' forbidden-hub list, L33).
- Elites don't always drop a powerup: a forced drop only skips the cooldown and chance; the 6-per-stage cap, slot and position checks still apply.

## PickupWorld (`Scripts/Pickups/PickupWorld.cs`, 605 lines)
- `PickupAgent` L11–22 (active, homingToPlayer, type, value, position, basePosition, bobPhase, flyDelay, view); `PendingFlyingPickup` L24–30. `PickupType` (GameEnums L135–145): Infamy, Gold, GoldBarrel, Powerup (health barrel), FlyingGold, FlyingHealth, XpBarrel, FlyingXp.
- Awake L52–61: pool = `QualityProfile.pickupCap` (180 every tier); loads Resources DemoJuiceSettings / DemoPacingSettings (serialized refs null in V4 scenes); `new PickupViewPool(root ?? transform, juice)`. No Update: ticked externally.
- `SpawnFromEnemy(pos, xp, gold, rich)` L63–95: profile active → xpMult 1 (else pacing 1.05); healthChance pacing 0.12. RNG: L71 `Rng.DirectorFloat() < healthChance` (**Director stream, always drawn**). dropCount = xp>0 + gold>0 + health. XP barrel value max(1, round(xp × xpMult)) at `GetKillDropOffset(dropIndex++, count)`; gold barrel (profile: gold as is; else ScaleEnemyKillGold L107–112 and rich ×2); health `Spawn(Powerup, 25)` using dropIndex without increment.
- `GetKillDropOffset` L97–105: zero when count ≤ 1; else **two UnityEngine.Random** (angle jitter ±15°, radius jitter ±0.15 around 1.6), drawn even if Spawn then fails.
- `Spawn` L114–141: full pool → `MergeNearby` then return; height `GetPickupHeight` L146–151 (barrels 0, flying 0.45, else 0.35); `_viewPool.Acquire`; barrels `AlignBarrelSubmerged(view)`; `bobPhase = Random.Range(0, 2π)` L137 (**UnityEngine.Random**, only with a slot).
- R-74: `AlignBarrelSubmerged` L153–166 and `TryGetBarrelVisualBounds` L168–226 read renderer bounds; leaks into simulation only via `MergeNearby`'s 3-D distance (L247); Tick distances ignore Y.
- `MergeNearby` L242–253: first same-type active pickup within 2 m (3-D) gets the value; else lost.
- `AttractAllInfamy` L255–262; `AttractLooseRewards` L264–282 (Infamy, Gold, FlyingGold, FlyingXp, GoldBarrel, XpBarrel; not Powerup).
- `ClearAllActive` L284–297.
- `Tick(dt, playerPos, pickupRadius)` L299–364: returns if run ended (no pause check; callers gate). `ProcessPendingFlyingPickups` first, then `_time += dt`. Radii: xp = max(pacing 4.25, stat radius or DefaultPickupRadius); gold = pacing 26 (stat unused); barrel touch 5. Per slot (XZ distance): homing/flying → flyDelay then move at Lerp(10, 28, 1 − clamp01(dist/max(goldRadius,1))), complete at dist ≤ 0.75 (measured before moving); Infamy in xp radius → homing; Gold/FlyingGold in gold radius → homing; barrels/Powerup in touch radius → `Collect(i)`; else bob sin(_time×3.5 + bobPhase) × 0.12. View update L353–360. `FlushPendingGrants` L363.
- `CompleteHomingPickup` L385–412: run ended → release unpaid; FlyingXp/Infamy → `ComputeXpGrant`, SFX; Gold/FlyingGold → gold; FlyingHealth → heal. No barrel case (R-103).
- `ComputeXpGrant` L414–423: value × Stats.XPGain (× 0.35 only when V3RunContext active and profile inactive), max(1, round).
- `FlushPendingGrants` L425–450: XP → `LevelSystem.Instance?.AddXP`; gold → `GrantGold` L584–593 (`SaltwindStageEconomyLogic.ApplyGoldGrant`, GoldGain only without export) → `rm.AddGold`; heal → `FindFirstObjectByType<PlayerShipController>` → `Damage.Heal(min(grant, missing))`.
- `Collect` L459–477 → bursts (FlyingGold / FlyingXp / FlyingHealth); `EnqueueBarrelBurst` L488–509: `GetBurstVisualCount` L511–521 (**one UnityEngine.Random** 6–10 × vfxDensity, min 3); perPiece = max(1, total/count), remainder on piece 0; per piece **two UnityEngine.Random** (insideUnitSphere × 0.8, flyDelay 0.05–0.25 + c × 0.02). `ProcessPendingFlyingPickups` L523–532: budget pacing 18/frame; pool full → `GrantFlyingPickupValue` L568–582 pays directly. `TrySpawnFlyingPickup` L534–566 bobPhase Random L560.
- `ReleasePickup` L595–603.
- All UnityEngine.Random calls: L102, 103, 137, 496, 506, 518, 560. Only RunRng call: L71 (Director).
- View pool `Scripts/Pickups/PickupViewPool.cs`: Acquire L22–66 (FlyingHealth builds `FlyingHealthBillboard` objects; others per-prefab stacks, Instantiate + strip colliders + `PickupViewVfxCache`); Release L68–97 (destroys on name mismatch); prefab map L149–165; scales L175–195; colours L197–203; sparkle L205–231; primitive fallback L249–267. `PickupViewVfxCache` L6–51.

## V3FloorPowerupWorld (`Scripts/V3/V3FloorPowerupWorld.cs`, 405 lines)
- Constants L15–22: chance 0.01, max 6/stage, collect radius 1.5, lifetime 30, Battle Signal 8 s ×1.25, Tailwind 6 s ×1.2. Agents[6] L24–31; fields L35–48; `_deadCalmMultiplier` never written or read.
- `Ensure` L52–59 (runtime component on /Systems, not in scenes); `ResetForRun` L70–81; OnDestroy → ClearBuffs.
- `TryRollOnKill(pos, isBoss, isElite)` L83–108: needs V3RunContext, not boss, < 6 spawned; chance/cooldown from export Rewards (0.01, 20 s); forced for elites under the profile (skips cooldown and chance); else `rng.PowerupFloat() >= chance` → return (**Powerup stream**); kind = `V3PowerupCatalog.RollKind` (**second PowerupFloat**); TrySpawn fail → no cooldown; success → cooldown (also after forced drops) + toast "{name} dropped!".
- Pools (`V3PowerupCatalog`): Saltwind Repair / PowderRush / MagneticTide / DeadCalm 25 each (L28–35); legacy 35/35/20/10; expanded (no profile) L9–26.
- `TrySpawn` L119–138, `TryFindSpawnPosition` L140–162 (4 rings × 8, 1.5 m; `IsValidSpawn` L164–171 PlayBounds + IslandObstacles 0.45). Views `V3PlaceholderFactory.CreateFloorPowerup` L30–63 (Destroyed on despawn, not pooled; PowderRush/MagneticTide/DeadCalm/FullSail fall back to a grey "unknown" sphere).
- `Tick` L184–223: V3RunContext, not ended/paused; cooldown, bob time, TickBuffs L275–290, TickPowderRush L356–365, TickDeadCalm L375–381, per agent lifetime/bob/collect (flat distance ≤ 1.5) → `ApplyPickup` → Despawn.
- `ApplyPickup` L225–273: Repair heals MaxHP × (export RepairSuppliesMaxHullFraction ?? 0.15; profile 0.2) capped; BattleSignal 8 s ×1.25; Tailwind 6 s ×1.2; FullSail shares Tailwind's timer ×1.45; PowderRush `ApplyDamageBuff(1 + 0.3)` for 8 s in Battle Signal's slot (R-43); SalvageChime `AttractAllInfamy`; MagneticTide `AttractLooseRewards`; DeadCalm only outside boss fight (toast shows anyway). Toast L268–272.
- Buff slots L292–332 (Damage modifier; MoveSpeed modifier + `SyncRunSpeedMultiplier`); Battle Signal and Powder Rush timers each remove the shared damage slot on expiry.
- Dead Calm `ApplyDeadCalm` L367–373: slow 0.3 for 5 s via `FindFirstObjectByType<EncounterSpawner>()?.SetSpawnRateMultiplier(1 − slow)` (works on the disabled spawner); expiry and ClearBuffs L334–344 set 1. Sink `EncounterSpawner` L165–166 → `SpawnModifierStack.SetBaseRateMultiplier` (clamped ≥ 0.1).
- Profile export fields `StageBalanceProfile` L57–63, 189–195; passed through V4 until M9 (`LegacyConfigMapping` L58–59). V4 config knob so far: `StageContentConfig.magneticTideOpensBarrels`.

## V4 code touching these today
- `LegacyEnemyWiring` (tag M14): L85 Magnetic Tide fix; Despawned listener L88–93 (statuses, `RunEncounterTracker.ReleaseAgentMapping`, `EncounterSpawner.Instance?.NotifyAgentDespawned`); summon delegate L70–76 (`EncounterSpawner.Instance.TriggerProfile`, queue never drains); `ClearTransientWorlds` L138–143; `TickFrame` L149–164 order: boss director, simulation, player projectiles, shots, **PickupWorld.Tick** (FindFirstObjectByType each frame), tide BeforeTick, **V3FloorPowerupWorld.Tick**, tide AfterTick, statuses (= V3RunBootstrap L485–499). Chain: V4Game.Update → shell.Tick (variable dt, skipped paused/ended).
- `LegacyMagneticTide` (tag M9): reflection seams L20–26; Before/After L37–62; OpenAllBarrels L68–80.
- `LegacyEnemyKillTargets.OnKilled` L95–128 (tag M14), order = V3 EnemyWorld L1552–1582 minus POIManager: RegisterKill → `ResolveKillGoldDrop` (Director draw for Ordinary only, Rich ×2) → **PickupWorld.SpawnFromEnemy** → **TryRollOnKill** → RunEncounterTracker.RegisterAgentKill → Marks → V3MinibossArena → TriggerBus.RaiseKill → item effects → damage ledger. Per-kill RNG order: Director (gold) → Director (health) → UnityEngine.Random (offsets, bob) → Powerup (chance, kind). Called from `EnemySimulation.Tick` L982–986 before Despawn.
- `LegacyBuildAdapter.ResetPowerupsAndItems` (LegacyAdapters L185–194, tag M9) from LegacyRunShell L169.
- Spawn rate stack: `SpawnController.EffectiveRateMultiplier` L100–115 = own modifiers × `LegacyRate.Multiplier` × experiment density × boss rate; `ILegacySpawnRateSource` (SpawnContracts L105–109) registered V4Game L84, 99; `LegacyEncounterExecutor` (LegacySpawnAdapters L182–235, tag M9): Spawner lookup, InstallSchedule, EnterModuleLab, `Multiplier` (= DebugSpawnRateMultiplier without the boss factor; 0.7 under Dead Calm).
- Trace tools: RunTraceRecorder L65–75 R-74 pins (header `visualPins`); TraceBoot L53 `Random.InitState(seed)`; PlayerPilot L31, 219–240 reflects `PickupWorld._pool` for steering weights (XpBarrel 3, GoldBarrel 2.5, Powerup 2.5, Infamy 1, Gold 0.6; 34 m).
- No presenter reads pickups; XP/gold/level reach V4 via LegacyEventFeed L51, 89, 96.
- V4 weapons read `RunManager.Instance.Stats` (LegacyWeaponAdapters L43), so powerup damage modifiers reach them.
- Pools: `PoolService` L66–300, `PoolCatalog` L18–46 (no pickup entries yet), `SlotPoolCapacities.Pickups` = pickupCap (unused).

## EncounterSpawner in V4 scenes
- Both scenes: /Systems component &658786991, disabled, schedule = encounter-only copy; PickupWorld &658786989 enabled; V3FloorPowerupWorld not in scenes. File-ID order RunManager > EncounterSpawner, ambient slot between (V4SceneSetup L174–179).
- Runtime readers/writers: V3FloorPowerupWorld L343, 372, 380 (Dead Calm); LegacySpawnAdapters L191–230 (InstallSchedule from LegacyRunShell L82, EnterModuleLab L254–256, Multiplier); LegacyEnemyWiring L72–74 (summon), L92 (NotifyAgentDespawned); RunEncounterTracker L136 (`MarkFailed` → `CancelPendingEncounter`, shared); V3RunPlaytestHotkeys L62, 139, 175 (flag-gated); V3MultiShipRunSetup L168–171; LegacyConfigRuntime L32–37 (EncounterOnlySchedule only for it).
- Editor: V4SceneSetup ApplyM3 L127–165, PlaceAmbientSlotAfterRunClock L180–205, ApplyM7 L268–292 assume it exists.
- Tests pinning it: V4StructureTests L134–149; V4SpawnM3Tests L738–750; V4RunPlayModeTests L141–144, 366–370, 576; V4StagePlayModeTests L81; V4HudM6Tests L33.

## Other callers
- V3-only: V3RunBootstrap L184–185, 217, 358, 495–496; EnemyWorld L1569–1574; RunBootstrap L123; WeaponEnemyPlaytestBootstrap L110, 179; PromoTeaserDirector; HighSeasBootstrap L368; `V3RunOpeningXpScatter` (dead code); V3PressureBeatDirector, V3OvertimeDirector, V3RouteEventDirector L93, V3MinibossArena L55, EnemySpecialBehaviorSystem L119, SpawnDirectorDebugUI.
- Shared code V4 runs: V3PlaceholderFactory L30–63, V3PowerupCatalog, SaltwindStageEconomyLogic (ResolveKillGoldDrop, ApplyGoldGrant), PickupViewPool, PickupViewVfxCache, FlyingHealthBillboard, RunEncounterTracker L136, V3RunPlaytestHotkeys, V3MultiShipRunSetup.
- V4 adapters: LegacyEnemyWiring L140, 157–162; LegacyEnemyWorldAdapters L113–118; LegacyMagneticTide; LegacyAdapters L190–191; LegacySpawnAdapters L182–235.
- DevTools: PlayerPilot L31, 221–240; RunTraceRecorder L65–75.
- Tests: V4 EditMode V4SpawnM3Tests L738–750, V4StructureTests L134–149, V4HudM6Tests L33, V4PoolM4Tests L75–77; V4 PlayMode V4EnemyPlayModeTests L143–160, 165–200, 330–346, V4RunPlayModeTests L141, 366–370, 576, V4StagePlayModeTests L81; V3 tests V3IntegrationTests L168, 182–185, SaltwindScheduleGateTests L164, EncounterSpawnerTests, SaltwindPlayModeTestSupport L21–109, SaltwindScenePlayModeTests L47–49.
