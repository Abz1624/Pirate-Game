# Rogue Pirates Production Specification

**Status:** authoritative implementation baseline  
**Audience:** design, Unity, backend, art, QA, LiveOps and release teams  
**Rule:** implementation must use this folder. Older root-level GDD documents are historical inputs and do not override this specification.

## Authority order

1. [Decision register](01_DECISION_REGISTER.md)
2. [Master implementation specification](00_MASTER_IMPLEMENTATION_SPEC.md)
3. Numbered subsystem specifications in this folder
4. [Traceability and delivery checklist](12_TRACEABILITY_CHECKLIST.md)

When two subsystem documents conflict, stop the affected task, add a decision entry, update both documents and add a regression test. Never resolve a conflict only in code.

## Specification map

- [00 — Master implementation specification](00_MASTER_IMPLEMENTATION_SPEC.md)
- [01 — Decision register](01_DECISION_REGISTER.md)
- [02 — Core loop, modes and progression](02_GAMEPLAY_MODES.md)
- [03 — Deterministic simulation and combat](03_SIMULATION_COMBAT.md)
- [04 — Content catalogue](04_CONTENT_CATALOGUE.md)
- [05 — World, encounters, bosses and raids](05_WORLD_ENCOUNTERS_RAIDS.md)
- [06 — Economy, ads, IAP and LiveOps](06_ECONOMY_LIVEOPS.md)
- [07 — UI, UX, controls and accessibility](07_UI_UX.md)
- [08 — Accounts, backend, data and APIs](08_ONLINE_TECHNICAL.md)
- [09 — Crews, events, cosmetics and narrative](09_SOCIAL_NARRATIVE.md)
- [10 — Art, VFX, audio and production assets](10_ASSET_PRODUCTION.md)
- [11 — QA, analytics, operations and release](11_QA_RELEASE.md)
- [12 — Traceability and delivery checklist](12_TRACEABILITY_CHECKLIST.md)
- [Appendix A — complete combat/content values](appendices/A_COMBAT_CONTENT_VALUES.md)
- [Appendix B — detailed economy transactions](appendices/B_ECONOMY_TRANSACTION_DETAILS.md)
- [Appendix C — detailed asset checklist](appendices/C_ASSET_CHECKLIST.md)

## Change protocol

- Every rule has a stable requirement ID.
- Every implementation task cites one or more requirement IDs.
- Every balance change increments the content manifest version.
- Active runs keep their starting content version.
- A requirement becomes `PASS` only with automated evidence and the specified playable or operational check.
- Allowed states: `NOT_STARTED`, `IN_PROGRESS`, `IMPLEMENTED_UNVERIFIED`, `PASS`, `NEEDS_FIX`, `BLOCKED`, `DEFERRED`.
- Prototype numbers remain tunable, but code must implement them exactly until a recorded decision changes them.
