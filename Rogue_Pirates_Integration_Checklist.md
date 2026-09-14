# Rogue Pirates — GDD Integration Master Checklist

**Created:** 2026-09-14  
**Updated:** 2026-09-14  
**Authority:** Owner decisions → [`plan/`](plan/README.md) (primary implementation spec) → this checklist (build status + evidence) → [`Rogue_Pirates_Implementation_Backlog.md`](Rogue_Pirates_Implementation_Backlog.md) (milestone index). Historical inputs: GDD v3.1, Combat v3.1, Economy LiveOps v3.2, Technical Design. GDD v3.0, `docs/gdd to implmentation plan.md`, 25%-banking proposal, trading/marketplace text, and `references/**` are superseded or excluded.

**Status values:** `NOT_STARTED` | `IN_PROGRESS` | `IMPLEMENTED_UNVERIFIED` | `PASS` | `NEEDS_FIX` | `BLOCKED` | `DEFERRED`

---

## Current build dashboard

| Field | Value |
|---|---|
| Active slice | M2 Saltwind (V3Boot → V3MainMenu → V3SaltwindRun) |
| Unity entry | `Assets/_Game/Scenes/v3/V3Boot.unity` (build index 0) |
| Reference run scene | `Assets/_Game/Scenes/Run.unity` (ship/camera tuning preserved) |
| Last verification build | 2026-09-14 — integration pass in progress |
| Blocking defects | See Known Issues queue |

---

## Known issues queue (priority)

| ID | Issue | Status | Next action |
|---|---|---|---|
| KI-001 | Proximity auto-consumption on POIs | FIXED | `V3InteractionController` + `POIInteractable` |
| KI-002 | Chest increments price index on failed gold spend | FIXED | `POIManager.TryOpenSalvageChest` spend-before-increment |
| KI-003 | Minimap shows all POIs/beacon at start | FIXED | `V3DiscoverySystem` + `V3MinimapHud` fog |
| KI-004 | Player spawn inside island | FIXED | `V3SpawnAnchorValidator` + islands-before-POIs build order |
| KI-005 | Touch joystick disabled in V3 | FIXED | `V3RunBootstrap.EnsureTouchJoystickActive` + composite input |
| KI-006 | Level starts at 1 with 6 weapon slots | FIXED | `LevelSystem.ConfigureV3` + `WeaponController.ConfigureV3` |
| KI-007 | Overtime label only; no Drowned Pursuit | FIXED | `V3OvertimeDirector` + `drowned_*` enemies |
| KI-008 | Main menu missing camera warning | FIXED | `HighSeasV3Setup.CreateMainMenuScene` camera |
| KI-009 | Menu lacks collection/quest panels | FIXED | `V3MainMenuController` + `V3MainMenuCollectionData` |
| KI-010 | Legacy POI types vs GDD slice names | FIXED | `V3PlaceholderFactory.GetPoiDisplayName` labels |

---

## Decision log

| Date | ID | Decision | Source |
|---|---|---|---|
| 2026-09-14 | D-001 | V3 enum POI types map to GDD slice: GildedWreck=Repair, MerchantSkiff=Powder Cache, TrialBuoy=Signal, CursedIdol=Treasure Chart | Combat §11 |
| 2026-09-14 | D-002 | First conquest always Deck Cannon regardless of Captain | Captains §1 |
| 2026-09-14 | D-003 | Local prototype progression in menu until M3 accounts | Backlog M2 |
| 2026-09-14 | D-004 | `references/**` excluded from implementation | Owner instruction |
| 2026-09-14 | D-005 | Saltwind map seed is random each run until first boss defeat; then locked locally as home geometry (`V3HomeSeed`) | `plan/02_GAMEPLAY_MODES.md` |

---

## Verification log

| Date | Build | Scope | Result | Evidence |
|---|---|---|---|---|
| 2026-09-14 | dev | EditMode V3 economy/XP/POI count/discovery | ADDED | `Assets/_Game/Tests/EditMode/V3IntegrationTests.cs` |
| 2026-09-14 | dev | EditMode home seed lock after first boss | ADDED | `V3IntegrationTests.V3HomeSeed_LocksGeographyAfterFirstBoss` |
| 2026-09-14 | dev | PlayMode Saltwind boot → death | PENDING MANUAL | Route: V3Boot → menu → Saltwind → chest/POI/beacon → boss → death → results |
| 2026-09-14 | dev | Scene setup regeneration | PENDING MANUAL | Run `HighSeas → V3 → Setup All Scenes` in Unity Editor |

---

## M0 — Feasibility and foundations

| ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|
| M0-001 | Backlog §M0 | Pin Unity LTS + packages after native/Web builds | `ProjectSettings/ProjectVersion.txt` | NOT_STARTED | Builds on Android/iOS/desktop/mobile Web |
| M0-002 | Backlog §M0 | 3D water + pooled swarm stress on mobile browsers | `EnemyViewPool`, `ProjectileWorld` | IMPLEMENTED_UNVERIFIED | Profile 180+12+1 cap |
| M0-003 | Backlog §M0 | Deterministic parity native/WASM/server | `RunRng`, combat tick | NOT_STARTED | Replay fixture hash match |
| M0-004 | Backlog §M0 | Numeric/RNG contract documented | `GameConstants`, `RunRng` | IMPLEMENTED_UNVERIFIED | Contract doc + fixture |
| M0-005 | Backlog §M0 | Top-down camera ~40×30 m, touch + keyboard | `Run.unity`, `PlayerShipController` | IMPLEMENTED_UNVERIFIED | Reference device playtest |
| M0-006 | Backlog §M0 | Benchmark verifier CPU/memory/download | — | NOT_STARTED | Measured report |
| M0-007 | Backlog §M0 | No ads on Steam; channel matrix | — | DEFERRED | Written matrix |
| M0-008 | Backlog §M0 | Device matrix + signing | — | NOT_STARTED | Device list assigned |
| M0-009 | Audit A03 | Landscape top-down prototype reversible | V3 scenes | PASS | Corridor readable |
| M0-010 | Cursor §M0 | No reward-bearing accounts before parity | V3 local-only meta | PASS | No production grant endpoints |

**M0 exit gate:** `BLOCKED` on M0-003, M0-006, M0-007, M0-008.

---

## M1 — First combat sandbox

| ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|
| M1-001 | Backlog §M1 | Movement, camera, collision, input adapters | `PlayerShipController`, `IslandObstacles` | IMPLEMENTED_UNVERIFIED | Deterministic slide test |
| M1-002 | Backlog §M1 | Deck Cannon, Shark, XP/level choice | `WeaponController`, `LevelSystem` | IMPLEMENTED_UNVERIFIED | Kill → level-up offers |
| M1-003 | Backlog §M1 | Atomic tick ordering, boss/player death | `RunManager`, `DamageSystem` | NEEDS_FIX | Same-tick fixture |
| M1-004 | Backlog §M1 | Pause reason stack | `RunManager.SetPause` | IMPLEMENTED_UNVERIFIED | Choice under manual pause |
| M1-005 | Backlog §M1 | Data export/validation pipeline | `GameDatabaseLoader` | IMPLEMENTED_UNVERIFIED | Missing ID rejection |
| M1-006 | Backlog §M1 | Capped offers, proc whitelist | `LevelOfferGenerator`, `DamageProcPolicy` | IMPLEMENTED_UNVERIFIED | Max-rank Continue only |
| M1-007 | Cursor §M1 | 30/60/120 FPS identical outcomes | — | NOT_STARTED | Automated hash test |
| M1-008 | Cursor §M1 | Death same tick blocks level-up rescue | `LevelSystem` | IMPLEMENTED_UNVERIFIED | Fatal + XP same tick |
| M1-009 | Cursor §M1 | Checkpoint replay no duplicate XP | — | NOT_STARTED | Replay twice |
| M1-010 | Audit A05 | Stable IDs + eligibility matrix | `GameDatabase` | IMPLEMENTED_UNVERIFIED | Export validation |
| M1-011 | Cursor §M1 | Dev-only fake account adapters | V3 local meta | PASS | No production writes |

**M1 exit gate:** `BLOCKED` on M1-003, M1-007–M1-009.

---

## M2 — Saltwind playable slice

RP-ID map for all M2 rows: [`plan/12_TRACEABILITY_CHECKLIST.md` § M2 integration map](plan/12_TRACEABILITY_CHECKLIST.md#m2-integration-map-unity-prototype).

### M2 content scope

| ID | RP-ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|---|
| M2-001 | RP-CONT-011 | `plan/04` | 4 slice Weapons | `GameDatabaseLoader` weapons | IMPLEMENTED_UNVERIFIED | Each fires at rank 1 |
| M2-002 | RP-CONT-012 | `plan/04` | 8 baseline Logbooks | `GameDatabaseLoader` logbooks | IMPLEMENTED_UNVERIFIED | All in offer pool |
| M2-003 | RP-CONT-013 | `plan/04` | 8 Common + 4 specialized Items | `V3RunItemInventory`, relic adapter | IMPLEMENTED_UNVERIFIED | Pickup + apply |
| M2-004 | RP-CONT-021 | `plan/04` | 7 enemy families | `GameDatabaseLoader.CreateEnemies` | IMPLEMENTED_UNVERIFIED | Each spawns m=0 |
| M2-005 | RP-WORLD-041 | `plan/05` | Barnacle King boss | `barnacle_king` definition | IMPLEMENTED_UNVERIFIED | Defeat → objective secured |
| M2-006 | RP-WORLD-051 | `plan/05` | Drowned Pursuer + Harrier overtime | `V3OvertimeDirector`, `drowned_*` defs | IMPLEMENTED_UNVERIFIED | m=10 distinct spawns |

### M2 finite exploration

| ID | RP-ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|---|
| M2-007 | RP-WORLD-011 | `plan/05` | 12 chests + 4 POIs, no replenish | `V3StagePoiPlacer` | IMPLEMENTED_UNVERIFIED | Count = 16 finite |
| M2-008 | RP-WORLD-012 | `plan/05` | Repair/Powder/Signal/Treasure POIs | `V3StagePoiPlacer` special types | IMPLEMENTED_UNVERIFIED | Exactly one each |
| M2-009 | RP-WORLD-013 | `plan/05` | Repair Wreck: 25% hull heal | `POIManager` GildedWreck case | IMPLEMENTED_UNVERIFIED | Heal once, consumed |
| M2-010 | RP-WORLD-014 | `plan/05` | Powder Cache: fight → Item + 20 gold | `POIManager.SpawnPowderCacheFight` | IMPLEMENTED_UNVERIFIED | Reward after fight clear |
| M2-011 | RP-WORLD-015 | `plan/05` | Signal Buoy: 8s in 4m radius → stat choice | `POIManager` TrialBuoy + charge | IMPLEMENTED_UNVERIFIED | Leave radius freezes |
| M2-012 | RP-WORLD-016 | `plan/05` | Treasure Chart: reveal 3 nearest chests | `POIManager` CursedIdol + discovery | IMPLEMENTED_UNVERIFIED | Minimap markers appear |
| M2-013 | RP-ECO-011 | `plan/06` | Chest price `ceil(min(1200,25×1.16^k))` | `V3Economy.GetChestPrice` | PASS | EditMode price table |
| M2-014 | RP-CORE-015 | `plan/02` | Finite POIs; drops continue in OT | `POIManager`, `SpawnDirector` | IMPLEMENTED_UNVERIFIED | Post-boss farm, no new chests |

### M2 timer, beats, overtime

| ID | RP-ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|---|
| M2-015 | RP-CORE-002 | `plan/02` | 10:00 soft timer; OT at m=10 | `GameConstants.StageDurationSeconds` | PASS | Boss at 8:00, OT still 10:00 |
| M2-016 | RP-WORLD-031 | `plan/05` | m=3/8 elite beats | `V3PressureBeatDirector` | IMPLEMENTED_UNVERIFIED | HUD warning + spawn |
| M2-017 | RP-WORLD-032 | `plan/05` | m=4/7 +25% spawn pulse 20s | `V3PressureBeatDirector` | IMPLEMENTED_UNVERIFIED | Rate elevated 20s |
| M2-018 | RP-WORLD-033 | `plan/05` | Early boss preserves later beats | `V3PressureBeatDirector` | IMPLEMENTED_UNVERIFIED | Boss 5:00 → 8:00 beat fires |
| M2-019 | RP-WORLD-052 | `plan/05` | Cap-safe retirement pre-OT | `V3OvertimeDirector` | IMPLEMENTED_UNVERIFIED | 60+ enemies → retire |
| M2-020 | RP-WORLD-053 | `plan/05` | OT hull `2^(o/2)` at spawn | `V3OvertimeDirector` | IMPLEMENTED_UNVERIFIED | Pre-OT hull unchanged |
| M2-021 | RP-WORLD-054 | `plan/05` | Storm front m=14+ safe space shrink | `V3OvertimeDirector` Dread Tide | IMPLEMENTED_UNVERIFIED | Boundary damage feedback |

### M2 captains, powerups, presentation

| ID | RP-ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|---|
| M2-022 | RP-CONT-031 | `plan/04` | Rook/Brine/Flint profiles | `V3CaptainProfile`, `V3CaptainController` | IMPLEMENTED_UNVERIFIED | Passive scaling caps |
| M2-023 | RP-CORE-013 | `plan/02` | First conquest Deck Cannon always | `V3RunBootstrap` first-run gate | IMPLEMENTED_UNVERIFIED | Flint still starts Deck Cannon |
| M2-024 | RP-CONT-014 | `plan/04` | 4 floor powerups, max 6/stage | `V3FloorPowerupWorld`, `V3PlaceholderFactory` | IMPLEMENTED_UNVERIFIED | Sixth blocks further |
| M2-025 | RP-UI-021 | `plan/07` | Minimap explored water + discovered objects | `V3MinimapHud`, `V3DiscoverySystem` | IMPLEMENTED_UNVERIFIED | Fog hides undiscovered |
| M2-026 | RP-WORLD-042 | `plan/05` | Beacon summons boss at 40% rate | `POIManager.SummonBoss` | IMPLEMENTED_UNVERIFIED | Timer continues |

### M2 run lifecycle

| ID | RP-ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|---|
| M2-027 | RP-CORE-001 | `plan/02` | Boss secured → survive to death, no extract | `POIManager.NotifyBossDefeated` | PASS | No Return Home |
| M2-028 | RP-CORE-001 | `plan/02` | Boss success saved before later death | `RunManager.SecureObjective` | IMPLEMENTED_UNVERIFIED | Results show both |
| M2-029 | RP-CORE-014 | `plan/02` | Random map seed until first boss defeat; then lock home geometry locally | `V3HomeSeed`, `V3RunBootstrap`, `POIManager.NotifyBossDefeated` | IMPLEMENTED_UNVERIFIED | 2 pre-boss runs differ; post-boss runs match locked seed |
| M2-030 | RP-CORE-014 | `plan/02` | Local home preview, no public accounts | `V3RunResultsScreen` | IMPLEMENTED_UNVERIFIED | Local-only label |
| M2-031 | RP-SIM-027 | `plan/03` DEC-024 | Same-tick boss kill + player death | `POIManager`, `EnemyWorld`, `V3RunBootstrap` | IMPLEMENTED_UNVERIFIED | Fixture |
| M2-032 | RP-OPS-011 | `plan/11` | Playtest exit criteria | — | NOT_STARTED | Structured playtest |
| M2-033 | RP-UI-011 | `plan/07` | First-run wireframes | Placeholder UI flows | IMPLEMENTED_UNVERIFIED | Walkthrough |
| M2-034 | RP-ASSET-011 | `plan/10` | M2 art placeholders (prototype OK; production assets required for G2 exit) | `V3PlaceholderFactory` | IMPLEMENTED_UNVERIFIED | Prototype: labels visible. G2 exit: licensed art per `plan/appendices/C_ASSET_CHECKLIST.md` |
| M2-035 | RP-SIM-001 | `plan/03` | Frame-rate independent | Deterministic core | NOT_STARTED | 30/60/120 hash |
| M2-036 | RP-ECO-012 | `plan/06` | Distinguish run vs banked gold | `V3RunResultsScreen` | PASS | Separate lines |

### M2 runtime foundation

| ID | RP-ID | Source | Expected behavior | Code evidence | Status | Acceptance test |
|---|---|---|---|---|---|---|
| M2-R01 | RP-OPS-012 | `plan/07`, `plan/11` | V3Boot build index 0, scene chain | `HighSeasV3Setup.UpdateBuildSettings` | PASS | Build settings order |
| M2-R02 | RP-WORLD-001 | `plan/05` | Islands before POI placement | `StageBuilder.Build` order | PASS | Islands then `V3StagePoiPlacer` |
| M2-R03 | RP-WORLD-002 | `plan/05` | Safe spawn 8m, chest ≤35m, beacon 60–180m | `V3SpawnAnchorValidator` | IMPLEMENTED_UNVERIFIED | EditMode fixture |
| M2-R04 | RP-WORLD-021 | `plan/05` | Reject blocked enemy spawns | `SpawnDirector.TrySpawnAtValidPosition` | IMPLEMENTED_UNVERIFIED | No island overlap spawn |
| M2-R05 | RP-UI-031 | `plan/07` | Touch + keyboard input | `CompositeShipSteerInput` in player | IMPLEMENTED_UNVERIFIED | Joystick active in V3 |
| M2-R06 | RP-UI-041 | `plan/07` | Unified interaction state machine | `V3InteractionController` | IMPLEMENTED_UNVERIFIED | E/hold/touch prompt |
| M2-R07 | RP-ECO-013 | `plan/06` | Failed gold never consumes chest | `POIManager.Interact` order | IMPLEMENTED_UNVERIFIED | Insufficient gold test |
| M2-R08 | RP-WORLD-043 | `plan/05` | Beacon hidden; 24m discover; m7 sector; m9 exact | `V3DiscoverySystem` | IMPLEMENTED_UNVERIFIED | Timeline test |
| M2-R09 | RP-CONT-015 | `plan/03`, `plan/04` | Level 0, 2 weapon/2 logbook slots, 5+2L XP | `LevelSystem.ConfigureV3` | IMPLEMENTED_UNVERIFIED | Slot + XP test |
| M2-R10 | RP-UI-012 | `plan/07` | Menu camera + captain/collection panels | `V3MainMenuController` | IMPLEMENTED_UNVERIFIED | No camera warning |

**M2 exit gate:** `IN_PROGRESS` — core systems implemented; automated + playable verification pending. G2 art/audio remains placeholder until production asset pass (M2-034).

---

## M3 — Accounts and persistence

| ID | Status | Notes |
|---|---|---|
| M3-001 | NOT_STARTED | Platform identity adapters |
| M3-002 | NOT_STARTED | Run lease + checkpoint/resume |
| M3-003 | NOT_STARTED | Ledger + holdings |
| M3-004 | NOT_STARTED | Disconnect/duplicate tests |
| M3-005 | NOT_STARTED | Replay verifier before rewards |
| M3-006 | NOT_STARTED | Saltwind conquest → Ledger rank 1 |
| M3-007 | NOT_STARTED | 100% run gold banking receipt |
| M3-008 | NOT_STARTED | Idempotent command IDs |

**M3 exit gate:** `BLOCKED` — all items NOT_STARTED.

---

## M4 — Full progression and Territory

| ID | Status | Notes |
|---|---|---|
| M4-001 | NOT_STARTED | 3 biomes, multi-stage Voyages |
| M4-002 | BLOCKED | Quest/trait/boss compatibility gates |
| M4-003 | NOT_STARTED | Remaining Captains + mastery presets |
| M4-004 | NOT_STARTED | Persistent home expansion |
| M4-005 | NOT_STARTED | Raids, once-only theft |
| M4-006 | NOT_STARTED | Economy cohorts |
| M4-007 | NOT_STARTED | Layout validation at generation |
| M4-008 | NOT_STARTED | Personal OT checkpoints |

**M4 exit gate:** `BLOCKED`.

---

## M5 — Crews, events, cosmetics

| ID | Status |
|---|---|
| M5-001 – M5-008 | NOT_STARTED |

**M5 exit gate:** `BLOCKED`.

---

## M6 — Channel integration and commerce

| ID | Status |
|---|---|
| M6-001 – M6-007 | NOT_STARTED / DEFERRED |

**M6 exit gate:** `BLOCKED`.

---

## M7 — Launch validation

| ID | Status |
|---|---|
| M7-001 – M7-007 | NOT_STARTED |

**M7 exit gate:** `BLOCKED`.

---

## Cross-milestone (GDD §13)

| ID | Expected | Status |
|---|---|---|
| MX-001 | Identical saved home after login | NOT_STARTED (M3) |
| MX-002 | No extraction after boss | PASS |
| MX-003 | Boss theft once only | NOT_STARTED (M4) |
| MX-004 | Proc chain termination | NEEDS_FIX |
| MX-005 | Weekly Voyage board rejects non-Voyage | NOT_STARTED (M5) |

---

## Ongoing protocol

1. Work one stable ID or tightly coupled group at a time.
2. Before implementation: confirm source rule, dependencies, evidence, acceptance criteria.
3. After implementation: compile, run focused tests, playable check, update this file (status + verification log).
4. Priority: crashes → reward corruption → interaction loop → visibility/UI → combat → content → persistence → polish.

*This file is the durable tracker. Do not mark `PASS` without both automated and playable evidence.*
