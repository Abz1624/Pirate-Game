# 12 — Traceability and Delivery Checklist

## Requirement map

| Range | Subsystem | Primary evidence |
|---|---|---|
| RP-CORE-001..099 | loop, modes, progression | unit + full-run PlayMode |
| RP-SIM-001..199 | simulation, pause, combat | golden replay + unit |
| RP-CONT-001..299 | content records | schema + behavior fixture |
| RP-WORLD-001..199 | generation, POIs, encounters | property + playable |
| RP-ECO-001..199 | economy, raids, ads/IAP | ledger/model/integration |
| RP-UI-001..199 | screens, controls, accessibility | device walkthrough |
| RP-ONL-001..199 | account/API/data/verifier | integration/load/security |
| RP-SOC-001..199 | Crew/events/cosmetics/narrative | boundary/event rehearsal |
| RP-ASSET-001..299 | production assets | provenance/performance/review |
| RP-OPS-001..199 | analytics, operations, release | staging/restore/launch drill |

## Gate checklist

### G0 — Feasibility
- [ ] Canonical vocabulary/spec conflicts resolved.
- [ ] Unity/package/build profiles pinned.
- [ ] Authoritative Unity randomness removed.
- [ ] Numeric/RNG contract approved.
- [ ] Stress scene profiled on every platform class.
- [ ] Golden replay matches native, WebAssembly and server.
- [ ] Verifier and hosting benchmark replaces estimates.

### G1 — Foundation
- [ ] Pure simulation assemblies and deterministic collision.
- [ ] Pause-reason set and lifecycle behavior.
- [ ] Exact tick ordering and proc budgets.
- [ ] Content export/validation/version retention.
- [ ] Automated replay, frame-rate and simultaneous-event suite.

### G2 — Saltwind
- [ ] First conquest tutorial and reserved home.
- [ ] Four Weapons, eight Logbooks, twelve Items.
- [ ] Seven enemies, two overtime enemies, Barnacle King.
- [ ] 12 chests/four POIs, Beacon/minimap and pressure beats.
- [ ] Post-boss survival, death settlement and results.
- [ ] Production-quality representative art/audio/UI *(Unity prototype may ship `V3PlaceholderFactory` placeholders; replace with licensed production assets before G2 exit — tracked as M2-034 / `plan/10_ASSET_PRODUCTION.md`)*.
- [ ] Comprehension/retry/performance targets pass.

### G3 — Persistence
- [ ] Accounts/linking/recovery and one-run lease.
- [ ] PostgreSQL ledger and migrations.
- [ ] Input upload/verifier/checkpoint/resume/takeover.
- [ ] Idempotent rewards and 100% Gold banking.
- [ ] Ledger, holdings, contracts, queue and cosmetics.
- [ ] Backup/restore and duplicate/fraud tests.

### G4 — Full game
- [ ] Six Weapons, fourteen Logbooks, twenty-eight Items.
- [ ] Three biomes/bosses and 1–3-stage Voyages.
- [ ] Concentric expansion and three masteries.
- [ ] Defense Director/practice.
- [ ] Raid generation/matchmaking/theft/shields/refunds.
- [ ] Four-week economy/progression test passes.

### G5 — Social/content
- [ ] Crew membership/roles/moderation.
- [ ] Event locks/queued leave/boundary order.
- [ ] Verified global/Crew/partner rankings.
- [ ] Operator finalization/reward reconciliation.
- [ ] Cosmetic inventory/partner pack.
- [ ] Narrative and marketing baseline.

### G6 — Commerce/channels
- [ ] Every platform identity/lifecycle adapter.
- [ ] Purchase validation/restore/refund/revocation.
- [ ] Sponsored Cache and portal limits.
- [ ] Daily/weekly/Chronicle/LiveOps configuration.
- [ ] Consent, audience, ratings and event eligibility reviewed.
- [ ] No paid/advertising competitive advantage.

### G7 — Launch
- [ ] All production assets accepted/licensed.
- [ ] Accessibility/localization/store media complete.
- [ ] Device/load/security/economy QA pass.
- [ ] Staging season, recovery and rollback rehearsed.
- [ ] Runbooks, alerts, owners and spending limits assigned.
- [ ] All named platform approvals complete.

## M2 integration map (Unity prototype)

Maps [`Rogue_Pirates_Integration_Checklist.md`](../Rogue_Pirates_Integration_Checklist.md) M2 rows to requirement IDs. Status and code evidence live in that checklist.

| Integration ID | RP-ID | Primary plan source |
|---|---|---|
| M2-001 | RP-CONT-011 | `04_CONTENT_CATALOGUE.md` |
| M2-002 | RP-CONT-012 | `04_CONTENT_CATALOGUE.md` |
| M2-003 | RP-CONT-013 | `04_CONTENT_CATALOGUE.md` |
| M2-004 | RP-CONT-021 | `04_CONTENT_CATALOGUE.md` |
| M2-005 | RP-WORLD-041 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-006 | RP-WORLD-051 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-007 | RP-WORLD-011 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-008 | RP-WORLD-012 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-009 | RP-WORLD-013 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-010 | RP-WORLD-014 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-011 | RP-WORLD-015 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-012 | RP-WORLD-016 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-013 | RP-ECO-011 | `06_ECONOMY_LIVEOPS.md` |
| M2-014 | RP-CORE-015 | `02_GAMEPLAY_MODES.md` |
| M2-015 | RP-CORE-002 | `02_GAMEPLAY_MODES.md` |
| M2-016 | RP-WORLD-031 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-017 | RP-WORLD-032 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-018 | RP-WORLD-033 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-019 | RP-WORLD-052 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-020 | RP-WORLD-053 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-021 | RP-WORLD-054 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-022 | RP-CONT-031 | `04_CONTENT_CATALOGUE.md` |
| M2-023 | RP-CORE-013 | `02_GAMEPLAY_MODES.md` |
| M2-024 | RP-CONT-014 | `04_CONTENT_CATALOGUE.md` |
| M2-025 | RP-UI-021 | `07_UI_UX.md` |
| M2-026 | RP-WORLD-042 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-027 | RP-CORE-001 | `02_GAMEPLAY_MODES.md` |
| M2-028 | RP-CORE-001 | `02_GAMEPLAY_MODES.md` |
| M2-029 | RP-CORE-014 | `02_GAMEPLAY_MODES.md` |
| M2-030 | RP-CORE-014 | `02_GAMEPLAY_MODES.md` |
| M2-031 | RP-SIM-027 | `03_SIMULATION_COMBAT.md` (DEC-024) |
| M2-032 | RP-OPS-011 | `11_QA_RELEASE.md` |
| M2-033 | RP-UI-011 | `07_UI_UX.md` |
| M2-034 | RP-ASSET-011 | `10_ASSET_PRODUCTION.md` |
| M2-035 | RP-SIM-001 | `03_SIMULATION_COMBAT.md` |
| M2-036 | RP-ECO-012 | `06_ECONOMY_LIVEOPS.md` |
| M2-R01 | RP-OPS-012 | `07_UI_UX.md`, `11_QA_RELEASE.md` |
| M2-R02 | RP-WORLD-001 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-R03 | RP-WORLD-002 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-R04 | RP-WORLD-021 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-R05 | RP-UI-031 | `07_UI_UX.md` |
| M2-R06 | RP-UI-041 | `07_UI_UX.md` |
| M2-R07 | RP-ECO-013 | `06_ECONOMY_LIVEOPS.md` |
| M2-R08 | RP-WORLD-043 | `05_WORLD_ENCOUNTERS_RAIDS.md` |
| M2-R09 | RP-CONT-015 | `03_SIMULATION_COMBAT.md`, `04_CONTENT_CATALOGUE.md` |
| M2-R10 | RP-UI-012 | `07_UI_UX.md` |

## Task definition of done

Every task records: requirement ID; decision IDs; owner; dependencies; implementation reference; content/schema version; automated result; playable/operational evidence; telemetry; platform coverage; asset provenance; rollback; status. `PASS` requires both automated and human evidence where presentation or comprehension matters.

