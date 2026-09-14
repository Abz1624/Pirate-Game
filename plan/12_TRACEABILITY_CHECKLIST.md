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
- [ ] Production-quality representative art/audio/UI.
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

## Task definition of done

Every task records: requirement ID; decision IDs; owner; dependencies; implementation reference; content/schema version; automated result; playable/operational evidence; telemetry; platform coverage; asset provenance; rollback; status. `PASS` requires both automated and human evidence where presentation or comprehension matters.

