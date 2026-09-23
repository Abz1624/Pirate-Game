# Codex review resolution log (R1–R10)

Reference: [CODEX_REVIEW_2026-09-23.md](CODEX_REVIEW_2026-09-23.md).

| ID | Status | Summary | Tests | Remaining |
| --- | --- | --- | --- | --- |
| **R1** | **implemented (v2)** | Exclusive ambient band windows + **540s band-4 rate** gate | `NineMinuteAmbient_UsesSingleBandFourExportRate` | Optional live spawn count at 9:00 in scene |
| **R2** | **implemented (v2)** | Pending encounter retries + **PlayMode partial-cache at cap** | `PartialCache_AtCap_SingleKill_DoesNotCompleteEncounter`, `PowderCache_PartialSpawn_*` | Every formation edge in scene |
| **R3** | **implemented (v1)** | Pure Marks record; elite kill credits | Codex regression gates | Full t=600 RunManager tick |
| **R4** | **implemented (v1)** | Effective ownership / evolution eligibility | Codex ownership gates | Duplicate purchase UX |
| **R5** | **implemented (v1)** | Profile-driven `V3PressureBeatDirector` | `PressureBeat_*` schedule gates | Pause/resume pulse PlayMode |
| **R6** | **implemented (v3)** | POI route/nav + **storm contracted safe-scale sail** | `StormSail_ContractedArena_KeepsSpawnAndBeaconInsideSafeZone`, 100-seed layout gate | Full human OT storm session |
| **R7** | **implemented (v1)** | Marked bombardment area blast + telegraph | `MarkedBombardment_*` | Human VFX readability |
| **R8** | **implemented (v1)** | Developer preset isolation; profile lifetime | `SaltwindModeGateTests`, mode transition PlayMode | PvP/co-op round-trip PlayMode |
| **R9** | **implemented (v4)** | v3 boss finale + **18-cell matrix** (Conservative/Average/Expert; kite vs orbit) with **documented win bands** and Marks pool boss/hull calibration | `Matrix_*`, `Calibration_MarksExpert_*`, `BossWin_*`, `SALTWIND_LAB_TARGET_BANDS.md` | Re-run **1800** menu export after tuning; live telemetry |
| **R10** | **implemented (v6)** | PlayMode suite incl. scene boss kill + single settlement + storm/mode tests | Full PlayMode sweep (see below) | Every weapon proc source |

**Profile:** `balanceVersion` **2**. Content hash **`e4ffa52b4475d137e89c960eebf2de9ee42a3280c9f859cb5ca1761c1baa7eb7`** (`ScheduleProfileSnapshots`, `WeaponStatSnapshots`, `BossStatSnapshot`, exploration route constants). Evidence: `plan/saltwind-verification/profile-export/`.

**Delivery commit:** `2bd9ca2abdf05548c57e5f39f8765cbda048e2fe` (+ local calibration edits). Review addendum: [CODEX_REVIEW_2026-09-23.md](CODEX_REVIEW_2026-09-23.md#resolution-addendum-2026-09-23-post-review-engineering).

**Interpretation (1800-run menu, pre–kite-boss fix):** Conservative/Average showed **0% objective** with **~99% survival** because **kite movement ran away from the sole living boss** after trash cull, so runs timed out without `ObjectiveSecured`. Expert orbit still won (~69–83%). **Fix:** kite switches to **orbit during boss finale**; **per-skill boss HP** scales (Conservative ×0.72, Average ×0.88, Expert Marks ×2.35). Re-run the matrix after pulling latest simulation code.

**Balance lab:** Production matrix **1800 runs** (18 cells). Menu + artifacts: [SALTWIND_LAB_TARGET_BANDS.md](SALTWIND_LAB_TARGET_BANDS.md). Re-run matrix after calibration changes.

**C13:** [C13_EXTERNAL_PLAYTEST.md](C13_EXTERNAL_PLAYTEST.md) — **pending external** (desktop perf, mobile, newcomer panel).

**Regression gates (2026-09-23 batch):**

- EditMode: `TraceParity_FixedSeedCheckpoints_MatchRuntimeAndLab` passed; `Matrix_Production1200Runs_WritesVerificationArtifacts` passed (~16s); `BossWin_SaltwindScenario_SecuresObjectiveBeforeTimeout` passed after boss finale work.
- PlayMode: **23/23** discovered (`list_tests` PlayMode); Saltwind batch **19/19**, plus LevelUp, BossEncounter (×2), Smoke — all passed in Pipeline async runs.
