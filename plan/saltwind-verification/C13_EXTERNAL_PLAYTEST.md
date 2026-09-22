# C13 — External playtest and device validation (pending)

Codex review [CODEX_REVIEW_2026-09-23.md](CODEX_REVIEW_2026-09-23.md) keeps **C13 pending external playtest/device**. Engineering gates (R1–R10) can pass without replacing this checklist.

## Status

| Track | Status | Owner | Evidence path |
| --- | --- | --- | --- |
| Desktop performance (80 enemies / six weapons) | **Pending** | QA / perf | `plan/saltwind-verification/performance/` (create on capture) |
| Mobile hardware (target SKU TBD) | **Pending** | QA / devices | Same folder + device model + OS build |
| Newcomer panel (≥5 uncoached runs) | **Pending** | Design / research | `plan/saltwind-verification/playtest/C13_newcomer_panel.md` (create after sessions) |

## Desktop performance capture (when run)

- [ ] Standalone build from reviewed commit; note `StageBalanceProfile` **balance version** and **content hash** from `plan/saltwind-verification/profile-export/`.
- [ ] Trace: 10-minute Saltwind solo, six weapons, ~80 active enemies, record frame time / GC / draw calls.
- [ ] Attach Unity Profiler capture or equivalent CSV; do **not** tune balance from this trace alone.

## Newcomer panel (when run)

Record separately:

- First boss attempt success/fail and time to first summon.
- Successful run duration (objective secured + death settlement).
- Retry interest after first failure (did they start a second run within 15 minutes?).

## Sign-off rule

C13 moves to **passed** only when the three tracks above have dated artifacts checked in under `plan/saltwind-verification/`. Until then, treat any informal play as directional notes only (not checkpoint evidence).
