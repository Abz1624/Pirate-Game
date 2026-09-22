# Saltwind implementation log

Reference: `plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md`.

## Checkpoint status

| Step | Gate | Status | Evidence |
| ---: | --- | --- | --- |
| 00 | C00 | **passed** | Baseline + `PROTECTED_SETTINGS.json` |
| 01 | C01 | **passed** | Profile export hash; `StageBalanceProfileTests` |
| 02 | C02 | **passed** (automated + wired) | EditMode spawn gates; `ambient_spawn` / `spawn_checkpoint` telemetry (`spawn-telemetry-note.md`) |
| 03 | C03 | **passed** | `RunEncounterTrackerTests` 7/7; `BossEncounterPlayModeTests` 2/2 |
| 04 | C04 | **passed** | Step 04 menu + composition logic + `SaltwindScheduleGateTests` / `SaltwindAmbientCompositionGateTests` |
| 05 | C05 | **passed** (automated) | `SaltwindFinaleGateTests` 6/6; profile-driven OT/storm + Barnacle King director |
| 06 | C06 | **passed** (automated) | `SaltwindPoiLayoutGateTests` 3/3; 100-seed layout; finite POI runtime |
| 07 | C07 | **passed** (automated) | `SaltwindProgressionGateTests` 6/6; profile XP/offers/starter pool |
| 08 | C08 | **passed** (automated) | `SaltwindEconomyGateTests` 6/6; profile gold/chest/powerups |
| 09 | C09 | **passed** (automated) | `SaltwindSettlementGateTests` 6/6; save v2, Marks settlement |
| 10 | C10 | **passed** (automated) | `SaltwindResetGateTests` 6/6; retry order, snapshot validation, pause guards |
| 11 | C11 | **passed** (automated) | `SaltwindResultsGateTests` 6/6; results UX, unlock goal, telemetry |
| 12 | C12 | **passed** (automated) | `SaltwindBalanceLabGateTests` 6/6; profile-driven lab matrix |
| 13 | C13 | **passed** (directional human) | `PLAYTEST_REPORT.md` — developer Editor sessions; perf + external panel **pending** |
| 14 | C14 | **passed** (automated + docs) | `step14-regression-evidence.md`; profile re-export; delivery index — see pending rows below |

## Step 13 — human verification (2026-09-23)

**Evidence:** `plan/saltwind-verification/PLAYTEST_REPORT.md` (four informal developer attempts: discovery 2026-09-22, seeded feedback pass **1622933822**, two post-fix verification runs 2026-09-23).

**Recorded outcomes**

- Early runs surfaced powder keg damage, spawn/boss/beacon/POI/level-up/quantity/HUD/salvage chest issues; fixes landed in gameplay code (see conversation + uncommitted `Assets/_Game` changes).
- Post-fix runs: broadside starter, boss summon exercised, level-up select-only, chest E/R and HUD fixes **directionally** verified by implementer — not blind-tested by newcomers.

**C13 pending (explicit — not waived)**

| Pending item | Notes |
| --- | --- |
| Desktop standalone perf trace | No build profile, frame-time percentiles, GC, or memory trace |
| 80-enemy cap stress under VFX load | Not measured |
| Mobile 30 FPS on hardware | No device runs |
| External newcomer playtests (handoff §13) | **0** uncoached sessions; do not treat developer notes as validation of onboarding |
| Documented boss kill / late OT victory | Not recorded in playtest report |
| Optional C02 live telemetry JSON (seed 12345) | Still optional; see `spawn-telemetry-note.md` |

## Step 14 — final regression and delivery (2026-09-23)

**Automated regression (Editor connected):** `unity command run_tests` — **79/79 passed** on Saltwind slice filters (56 `Saltwind*` gates + encounter/schedule/profile/POI/spawn modifier tests). Details: `step14-regression-evidence.md`.

**Profile export:** `eval_file` → `export-profile.eval.cs` → hash **`3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d`** in `profile-export/`.

**Docs updated:** `VERIFICATION_INDEX.md`, `BALANCE_DECISIONS.md` (v2 playtest tuning), `PLACEHOLDERS.md` → visual catalogue.

**C14 still pending (explicit)**

| Item | Notes |
| --- | --- |
| Desktop perf trace | `PERF_TRACE_PENDING.md` — not captured |
| External newcomer playtests | `PLAYTEST_REPORT.md` |
| PlayMode CLI `--mode PlayMode` | 0 tests in `playmode-results.xml`; boss **2/2** in `playmode-boss-evidence.md` / `editmode-results.xml` |
| Optional seed **12345** full telemetry JSON | Excerpt: `c02-telemetry-excerpt-seed1622933822.json` |

**C14 completed (2026-09-23 follow-up)**

- `editmode-results.xml` — EditMode assembly **124/124** pass (full CLI with Editor closed)
- `c02-telemetry-excerpt-seed1622933822.json` — spawn telemetry sign-off sample
- Delivery commit recorded: **`0ee6b0fc66dc79e87765517e6d75a7d6e8075fe1`** (owner)
- Test fix: `LevelOfferGeneratorTests` sets `startingWeaponId = deck_cannons` for scripted first level-up assertion

**Next (optional):** Desktop perf capture; external playtest panel; amend commit with test fix if needed.

## Step 12 — code landed (2026-09-22)

- **`SaltwindBalanceLabScenarioFactory`** + **`SaltwindBalanceLabBatchRunner`** — 3 build policies × 2 skills × 2 unlock pools × 100 seeds (1200 runs via menu)
- **`SaltwindLabRuntimeParityLogic`** — chest/XP/deadline/OT parity with `StageBalanceProfileExportCodec`
- **`CombatSimulator`** — profile XP curve, escalating chest prices, policy weapon growth over levels
- Menu **HighSeas → Saltwind → Run Balance Lab Matrix (1200 sim runs)** writes `plan/saltwind-verification/balance-lab/`

## Step 11 — code landed (2026-09-22)

- **`SaltwindRunResultsLogic`** / **`SaltwindMarksUnlockLogic`** — headlines, Marks reconciliation, display names, goal select vs purchase
- **`V3RunResultsScreen`** — victory-after-death copy, final build, Marks breakdown, retry/menu, goal buttons
- **`RunCombatTelemetry`** — profile hash/run ID, settlement, boss lifecycle, death, results shown; null-safe `Record`
- **`V3WeaponDisplayNames`**, **`PLACEHOLDER_VISUAL_CATALOG.md`**, enemy damage number rate limit

## Step 10 — code landed (2026-09-22)

- **`SaltwindRunResetLogic`** — documented retry generation + multiplayer profile skip
- **`V3RunBootstrap`** — explicit teardown before new run; director resets on every retry; snapshot cleared on restart
- **`RunSnapshotData` v2** + **`RunSnapshotRestoreLogic`** — profile hash, POI/encounter/Marks fields; reject incomplete active encounters
- **Pause guards** — Marks minute buckets, signal buoy charge, floor powerup buff timers
- **`V3PressureBeatDirector` / `V3RouteEventDirector`** — `ResetForRun` on retry

## Step 09 — code landed (2026-09-22)

- **`SaveService` / `MetaSaveData` v2** — Marks treasury, unlock ownership, settlement receipts; migration from v1
- **`SaltwindStageSettlementLogic`**, **`RunMarksTracker`**, **`SaltwindRunSettlement`** — credit tracking + idempotent death settlement
- **`RunManager.RunId`**, hooks for kills/minutes; **`V3ContentUnlocks`** Marks gates when profile active
- POI / pressure elite / boss victory wiring; results screen Marks breakdown

## Step 05 — code landed (2026-09-22)

- **`SaltwindFinaleLogic`** — overtime at 600s, storm at 720s, safe-region contraction, OT spawn interval/weights
- **`BarnacleKingBossAttackDirector`** + **`BarnacleKingBossAttackLogic`** — telegraphed spread / committed charge / marked bombard; enraged combo below 50% HP
- **`V3SaltwindStormDirector`** + **`V3SaltwindStormRingVisual`** — warning, safe scale synced to `PlayBounds` + damage via `V3StormEdgeDamage`
- **`V3OvertimeDirector`** — profile spawn interval + pursuer/harrier weights; legacy dread tide only when no stage profile
- **`RunManager.IsOvertime`** — uses profile stage deadline when active
- Boss summon **time HP scaling**; OT spawn **HP/damage** from export; **boss-fight ambient rate halved**

## Tests (Step 05)

```text
unity command run_tests --mode EditMode --filter HighSeas.Tests.SaltwindFinaleGateTests --filter_type testname  → 6/6
```

## Profile export

| Hash | `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d` (Step 14 export) |
| Path | `plan/saltwind-verification/profile-export/` |

## Step 06 — code landed (2026-09-22)

- **`V3StagePoiPlacementOptions`** + **`SaltwindPoiLayoutLogic`** — 12 chests, first-chest ~15s travel budget, barrel count, layout validation
- **`V3StagePoiPlacer`** — Saltwind finite placement, explosive barrel positions on `StageLayout`
- **`POIManager`** — spawn all chests at run start when profile disables gold-triggered chests; signal buoy occupancy from profile; Gilded Wreck hull gate; chart/beacon bearing hints
- **`V3RouteEventDirector`** — skips automatic route events when profile flag set
- **`V3DiscoverySystem`** — beacon direction/exact hints at 5:00 / 7:00 from profile timing
- **`V3ExplosiveBarrelWorld`** + **`SaltwindExplosiveBarrelLogic`** — profile fuse/radius/damage; wired in **`V3RunBootstrap`**

## Tests (Step 06)

```text
unity command run_tests --mode EditMode --filter SaltwindPoiLayoutGateTests --filter_type testname  → 3/3
unity command run_tests --mode EditMode --filter V3StagePoiPlacer_PlacesConquestPois --filter_type testname  → 1/1
```

## Step 07 — code landed (2026-09-22)

- **`SaltwindStageProgressionLogic`** — profile XP curve, multi-level grants, starter weapon pool, evolution-item eligibility, proc cascade budget helper
- **`LevelSystem.ApplyStageBalanceProgression`** — 6 weapon / 2 logbook slots, profile XP (no demo pacing or global XP multiplier), fill-state **`LevelOfferGenerator`** path (no scripted first level-up)
- **`V3ContentUnlocks`** — when stage profile active, weapons/items gated to starter pool + evolution base-weapon rules
- **`LevelOfferGenerator`** — pity thresholds from profile `newWeaponOfferProtectionLevels`
- **`DamageProcPolicy`** — `MaxProcEventsPerResolution` budget hooks for proc chains
- **`V3RunBootstrap`** — applies progression profile before run init; captain starting weapon when in pool

## Tests (Step 07)

```text
unity command run_tests --mode EditMode --filter SaltwindProgressionGateTests --filter_type testname  → 6/6
```

## Step 08 — code landed (2026-09-22)

- **`SaltwindStageEconomyLogic`** — profile chest quotes (20/31/38/46/56), atomic chest purchase planning, kill gold classification, pickup merge helpers
- **`POIManager`** — plan-then-spend chest flow; authoritative price in interaction prompt; profile powder-cache gold
- **`RunManager`** — gross gold earned/spent; Saltwind death does not bank run gold to doubloons when profile disables it
- **`PickupWorld`** — no legacy gold scale / XP demo mult under profile; `AttractLooseRewards` for Magnetic Tide
- **`EnemyWorld`** — profile kill gold drops
- **`V3FloorPowerupWorld` / `V3PowerupCatalog`** — four Saltwind powerups, drop cooldown, elite guarantee, Powder Rush refresh, Dead Calm boss exclusion, repair clamp

## Tests (Step 08)

```text
unity command run_tests --mode EditMode --filter SaltwindEconomyGateTests --filter_type testname  → 6/6
```

## Step 04 composition + C02 telemetry (2026-09-22)

- **`SaltwindAmbientCompositionLogic`** — five-band ordinary/specialist roster (100/0 → 60/40), specialist caps, runtime pick in `EncounterSpawner`
- **`SaltwindSpawnTelemetryLogic`** — `ambient_spawn` + 2/4/6 min `spawn_checkpoint` events
- **`SaltwindEncounterSpawnSetup`** — band anchor enemies (shark → cannonboat → manta → hammerhead → manta)
- **`spawn-telemetry-note.md`** — capture procedure for seeded JSON sign-off

## Next (optional)

- Git commit for Saltwind delivery slice when requested
- Close Editor → full `editmode-results.xml`
- PlayMode `BossEncounterPlayModeTests` (2) manual or CLI
- C13 perf trace + external newcomers when available
- Optional: C02 telemetry JSON seed **12345** (`spawn-telemetry-note.md`)
