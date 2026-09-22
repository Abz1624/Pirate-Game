# Human playtest report (Saltwind solo)

Status: **complete for C13 (directional)** — developer-led Editor sessions recorded below. **Not** a substitute for handoff-scale external playtests or measured desktop/mobile perf.

Reference: `plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md` §13.

## Environment (all sessions unless noted)

| Field | Value |
| --- | --- |
| Build | Unity **Editor Play Mode** (not standalone desktop build) |
| Unity | `6000.3.11f1` |
| Route | `V3Boot` → `V3MainMenu` → Saltwind solo (`V3SaltwindRun`) |
| Profile ID | `saltwind_solo` |
| Profile content hash | `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d` (see `profile-export/`) |
| Graphics / resolution | Editor Game view defaults (not captured per session) |
| Tester profile | **Project developer** — high prior experience with survivors-likes / this codebase; **not** a cold newcomer sample |

## Pending (explicit — not fabricated)

| Item | Status |
| --- | --- |
| **Desktop perf trace** | **Pending** — no standalone build profile through opening → swarm → boss → six-weapon combat → OT; no median/p95/p99 frame time, GC, or peak memory capture |
| **80-enemy cap stress** | **Pending** — not instrumented under representative VFX/projectile load |
| **Mobile / 30 FPS goal** | **Pending** — no real device runs; Editor is not device evidence |
| **External newcomers (handoff: ~5×5 attempts)** | **Pending** — zero uncoached newcomer sessions; findings below are **directional** from the implementer only |
| **First victory / long-survival targets** | **Pending honest eval** — boss reached in at least one post-fix run; no documented boss **kill** or late OT victory in these notes |
| **Balance targets (60 FPS desktop, etc.)** | **Not claimed** — no measurements attached |

## Cross-session themes (post-fix code landed 2026-09-22 – 2026-09-23)

Issues found in early sessions drove fixes (see `IMPLEMENTATION_LOG.md` Step 13 section): powder keg blast damage and tuning, powder cache marker lifecycle, level-up select-only (click / 1–2–3), projectile quantity on broadside/Long Nine/pierce lanes, salvage chest Take **E** / Skip **R**, kill count + bottom loadout HUD, enemy time-scaled damage, boss beacon marker consume, default starter **broadside_battery**, animator trigger guards (`WindUp` / `Swim` spam).

**Re-test note:** Powder kegs already placed in a run retain stats from spawn time; full keg data tuning requires a **fresh run** after asset changes.

---

## Session 1 — discovery pass

| Field | Value |
| --- | --- |
| Date | 2026-09-22 (evening, UTC+7) |
| Attempt | 1 (informal) |
| Seed | Not recorded |
| Captain / build | Not recorded |
| Boss | Did not spawn (player report) |
| Outcome | Death / incomplete run (inferred) |
| Retry interest | Yes — continued testing next day |

**Observations**

- Powder keg explosion **triggered** but **no meaningful damage** to enemies.
- Around **6–7 min**, ambient population felt **too low** (roughly 6–7 enemies visible).
- **Boss did not spawn** during the session.
- Powder cache interaction required **defeating ambush first**; player expected **more enemies to spawn** rather than a hard gate message.

**Objective clarity:** Understood explore → pressure → boss beacon flow partially; boss path blocked by above bugs.

---

## Session 2 — structured feedback pass

| Field | Value |
| --- | --- |
| Date | 2026-09-23 (~00:20, UTC+7) |
| Attempt | 2 (informal) |
| Seed | **1622933822** (from run telemetry / console context in fix pass) |
| Build notes | Mid-fix slice; several systems still broken |

**Observations (player report, pre-/mid-fix)**

1. Powder keg still **no enemy damage** despite reliable trigger.
2. Powder cache **marker remained** after ambush trigger.
3. Level-up **auto-selected** an offer — wanted explicit confirm/reject (later scoped: **chest** E/R; level-up **click/1–2–3** only).
4. **Projectile quantity** (+1) seemed to apply only to some weapons (Extra Hands Ledger, etc.).
5. **Salvage chest** interaction appeared inert.
6. Request **kill count** in HUD.
7. **Weapon/item UI** hidden — should be visible.
8. **Long Nine** quantity showed 2 in UI/logic but **one shot** in gameplay.
9. **Positive:** run felt powerful and fun; **negative:** enemy damage felt **too weak** — little threat.
10. **Boss beacon marker** still visible after successful boss summon.

**Boss / death:** Boss summon path exercised in later logs; full victory not recorded here. Combat telemetry exported on death (`combat_D_FULL_COMBAT_V2_*.json` under project telemetry folder).

**Retry interest:** Yes — requested debug/fix pass (completed in code).

---

## Sessions 3–4 — post-fix verification (couple of runs)

| Field | Value |
| --- | --- |
| Date | 2026-09-23 (~01:00+, UTC+7) |
| Attempts | 3–4 (informal) |
| Seed | Not recorded per attempt (use fresh run or telemetry export for exact IDs) |
| Starting weapon | **Broadside battery** (default captains after tuning) |

**Directional checks (developer, same machine)**

- Continued Saltwind solo after UX/combat fixes; **multiple retries** on same day.
- **Level-up:** modal with three offers; selection via card click / number keys (no auto-pick).
- **HUD:** kill counter and loadout bar expected visible after bootstrap/HUD fixes.
- **Chest:** Take **E** / Skip **R** prompt path implemented for salvage flow.
- **Boss:** at least one run reached **boss summon** (`POIManager.SummonBoss` / encounter trigger) with beacon marker cleanup fix in place.
- **Powder keg:** damage pipeline fixed; second tuning pass lowered blast scaling — **feel** should be re-checked on **new runs** only.

**Open questions for Step 14 smoke**

- Confirm salvage chest loot feedback end-to-end on a marked POI.
- Confirm enemy damage scaling feels threatening by **~8–10 min** without feeling unfair.
- Optional: one logged seed for Step 14 regression checklist.

**Retry interest:** Yes — proceed to Step 14 automated regression + delivery index.

---

## C13 checkpoint summary

| Criterion | Result |
| --- | --- |
| Human evidence separate from Balance Lab sim | **Yes** — this document only |
| Sample size | **Small / directional** (1 developer, 4 informal attempts) |
| Performance evidence | **Pending** (see table above) |
| External playtesters | **Pending** |
| Fun / readability signal | **Positive core loop**; several UX/combat bugs found and addressed — residual balance (keg, enemy DPS) may need further tuning after Step 14 |
