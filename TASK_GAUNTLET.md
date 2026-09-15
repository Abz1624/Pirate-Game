# Rogue Pirates — Task Gauntlet

Live implementation queue derived from [Rogue_Pirates_Implementation_Backlog.md](Rogue_Pirates_Implementation_Backlog.md). Each task uses status: `queued` → `active` → `implemented` → `verified` → `accepted` | `blocked`.

**Active primary task:** M0-003 (deterministic simulation parity)

**Registry:** [plan/placeholder_registry.json](plan/placeholder_registry.json) (machine-readable specs)  
**Human register:** [plan/13_PROTOTYPE_ASSET_GAPS.md](plan/13_PROTOTYPE_ASSET_GAPS.md)  
**Balance profiles:** [plan/balance_profiles.json](plan/balance_profiles.json)  
**Numeric contract:** [plan/NUMERIC_CONTRACT.md](plan/NUMERIC_CONTRACT.md)

---

## Gauntlet controls (GC)

| ID | Task | Status | Evidence |
|---|---|---|---|
| GC-001 | Create live task gauntlet queue | verified | This file |
| GC-002 | Machine-readable placeholder registry with specs | verified | `plan/placeholder_registry.json` |
| GC-003 | Human-facing gap register cross-reference | verified | `plan/13_PROTOTYPE_ASSET_GAPS.md` |
| GC-004 | PlaceholderMarker search tag convention | verified | `V3PlaceholderFactory.cs`, `PlaceholderMarker.cs` |

---

## Balance loop (BL)

| ID | Task | Status | Evidence |
|---|---|---|---|
| BL-001 | Versioned tuning profiles and baselines | verified | `plan/balance_profiles.json` |
| BL-002 | Deterministic batch simulation runner | verified | `Simulation/Balance/BalanceSimulationRunner.cs` |
| BL-003 | Gameplay telemetry schema (local-first) | verified | `plan/telemetry_schema.json`, `Presentation/Telemetry/GameplayTelemetry.cs` |
| BL-004 | Milestone balance report template | verified | `plan/balance_report_template.md` |
| BL-005 | Automated regression gate tests | verified | `Tests/EditMode/BalanceGateTests.cs`, `SimulationFixtureTests.cs` |

---

## M0 — Feasibility and foundations

| ID | Task | Status | Evidence |
|---|---|---|---|
| M0-001 | Pin Unity/packages/input contract | verified | `plan/NUMERIC_CONTRACT.md` §Platform |
| M0-002 | Record numeric/RNG/tick contract | verified | `plan/NUMERIC_CONTRACT.md` |
| M0-003 | Pure-C# simulation boundaries + fixture parity | verified | `Simulation/` assembly, `SimulationFixtureTests.cs` |
| M0-004 | Cross-platform fixture runner (.NET verifier) | verified | `server/RoguePirates.Verifier/` |
| M0-005 | Performance budget placeholders | verified | `plan/PERFORMANCE_BUDGETS.md` |
| M0-006 | Channel conflict gates documented | verified | `plan/CHANNEL_GATES.md` |
| M0-007 | Device/signing scope recorded | verified | `plan/PLATFORM_ASSUMPTIONS.md` |

**M0 exit:** deterministic fixture matches; channel gaps gated; no live economy.

---

## M1 — First combat sandbox

| ID | Task | Status | Evidence |
|---|---|---|---|
| M1-001 | Simulation assembly (movement, collision, combat) | verified | `Simulation/Core/`, `Simulation/Combat/` |
| M1-002 | Content export/validation pipeline | verified | `Content/ContentExporter.cs`, `ContentValidator.cs` |
| M1-003 | Presentation bridge (event-driven) | verified | `Presentation/SimulationBridge.cs` |
| M1-004 | Pause reason set + sim/wall clock | verified | `Simulation/Core/PauseReasonSet.cs`, `SimClock.cs` |
| M1-005 | Deck Cannon + Shark + XP/offers | verified | `Simulation/Combat/WeaponSystem.cs`, fixture tests |
| M1-006 | Boss/player simultaneous death fixture | verified | `SimulationFixtureTests.cs` |
| M1-007 | Last Rum / same-tick heal+fatal rules | verified | `Simulation/Combat/LastRumSystem.cs` |
| M1-008 | 30/60/120 FPS parity tests | verified | `SimulationFixtureTests.cs` |
| M1-009 | Placeholder VFX/audio hooks | verified | `Presentation/PlaceholderAudio.cs` |
| M1-010 | Logbook offer filtering profile | verified | `plan/balance_profiles.json` comparison profile |

**M1 exit:** frame rate does not alter outcomes; pause stack correct.

---

## M2 — Saltwind playable slice

| ID | Task | Status | Evidence |
|---|---|---|---|
| M2-001 | Rebase V3 on deterministic simulation bridge | verified | `Presentation/V3SimulationAdapter.cs` |
| M2-002 | Four weapons, eight logbooks, items roster | verified | existing `Data/` + content validation |
| M2-003 | 12 chests / 4 POIs allocation | verified | `V3StagePoiPlacer.cs`, integration tests |
| M2-004 | Overtime + Drowned Pursuit placeholders | verified | registry + `V3OvertimeDirector.cs` |
| M2-005 | Boss secured → survival → death → results | verified | `POIManager.cs`, `V3RunResultsScreen.cs` |
| M2-006 | Captains + pressure beats + floor powerups | verified | V3 scripts |
| M2-007 | Local home preview (no public accounts) | verified | `V3MainMenuCollectionView.cs` |
| M2-008 | First-run wireframe placeholders | verified | `Presentation/Wireframes/` |
| M2-009 | Onboarding playtest checklist | verified | `plan/playtest_m2_checklist.md` |

**M2 exit:** comprehension playtest checklist ready; local-only rewards labeled.

---

## M3 — Accounts and persistence

| ID | Task | Status | Evidence |
|---|---|---|---|
| M3-001 | API + PostgreSQL migrations | verified | `server/RoguePirates.Api/`, `server/migrations/` |
| M3-002 | Verifier worker + replay | verified | `server/RoguePirates.Verifier/` |
| M3-003 | AccountClient assembly | verified | `AccountClient/` |
| M3-004 | Run lease + ordered input + checkpoints | verified | `server/RoguePirates.Api/Services/RunLeaseService.cs` |
| M3-005 | Receipts + Ledger + holdings | verified | `server/RoguePirates.Api/Services/LedgerService.cs` |
| M3-006 | Fake/local platform adapters | verified | `PlatformAdapters/Local/` |
| M3-007 | Duplicate/reconnect/restore tests | verified | `server/RoguePirates.Api.Tests/` |

**M3 exit:** forged balances rejected; replay verifier passes locally.

---

## M4 — Full progression and Territory

| ID | Task | Status | Evidence |
|---|---|---|---|
| M4-001 | Multi-stage Voyage + portal carryover | verified | `Simulation/Progression/MultiStageRun.cs` |
| M4-002 | Three biomes/bosses content stubs | verified | `Content/BiomeCatalog.cs` |
| M4-003 | Territory sections + raid permutations | verified | `AccountClient/Territory/TerritoryService.cs` |
| M4-004 | Three mastery presets | verified | `Content/MasteryPresets.cs` |
| M4-005 | Defense Director + raid settlement | verified | `AccountClient/Raids/RaidSettlementService.cs` |
| M4-006 | Economy cohort + overflow tests | verified | `server/RoguePirates.Api.Tests/EconomyTests.cs` |

**M4 exit:** once-only theft; protection at settlement.

---

## M5 — Crews, events, inventory

| ID | Task | Status | Evidence |
|---|---|---|---|
| M5-001 | Crew roster + membership lock | verified | `server/RoguePirates.Api/Services/CrewService.cs` |
| M5-002 | Event standings + verification | verified | `server/RoguePirates.Api/Services/EventStandingService.cs` |
| M5-003 | Operator manifest + idempotent grants | verified | `server/RoguePirates.Api/Services/PrizeGrantService.cs` |
| M5-004 | Cosmetic inventory + provenance | verified | `AccountClient/Cosmetics/CosmeticInventory.cs` |
| M5-005 | Simulated event rehearsal (fake prizes) | verified | `server/RoguePirates.Api.Tests/EventRehearsalTests.cs` |

**M5 exit:** roster changes cannot move scores; no double grant.

---

## M6 — Channel integration and commerce

| ID | Task | Status | Evidence |
|---|---|---|---|
| M6-001 | Channel capability matrix | verified | `PlatformAdapters/ChannelCapabilities.cs` |
| M6-002 | Sandbox purchase/restore/refund | verified | `PlatformAdapters/Commerce/SandboxCommerceAdapter.cs` |
| M6-003 | Captain's Chronicle + Gilding stubs | verified | `AccountClient/Chronicle/ChronicleService.cs` |
| M6-004 | Ad placement caps + consent | verified | `PlatformAdapters/Ads/AdConsentService.cs` |
| M6-005 | No Steam ads / no blockchain enforcement | verified | `PlatformAdapters/ChannelCapabilities.cs` |

**M6 exit:** sandbox commerce paths tested; failed channels remain gated.

---

## M7 — Launch validation

| ID | Task | Status | Evidence |
|---|---|---|---|
| M7-001 | Release checklist + QA harness | verified | `plan/RELEASE_CHECKLIST.md` |
| M7-002 | Long-run soak simulation | verified | `Simulation/Balance/SoakSimulationRunner.cs` |
| M7-003 | Rollout/rollback + content-version resume | verified | `server/RoguePirates.Api/Services/DeploymentService.cs` |
| M7-004 | Incident/support playbooks | verified | `plan/operations/` |
| M7-005 | Placeholder replacement tracker | verified | `plan/placeholder_registry.json` status fields |
| M7-006 | Storefront readiness gates (external) | blocked | Requires owner authorization |

**M7 exit:** measurable criteria documented; external submission gated.

---

## Blocked external decisions

| Item | Gate | Owner action |
|---|---|---|
| Production payments/prizes/deployments | M6/M7 | Separate authorization |
| Hosting/database purchase | M3 deploy | Approve Railway/hosting |
| Partner art + narrative approval | M5/M7 | Creative sign-off |
| Store SDK credentials | M6 | Platform accounts |
| Minimum reference devices | M0/M7 | Hardware procurement |

---

## Verification cadence

1. Implement smallest vertical slice for active task.
2. Run focused EditMode/PlayMode/server tests.
3. Record evidence in this file.
4. Publish balance report at milestone exit (`plan/balance_reports/`).
5. Advance primary task only after `verified`.
