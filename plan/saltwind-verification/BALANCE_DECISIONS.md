# Balance decisions log (Saltwind solo profile)

## Version 1 — initial authored defaults (2026-09-22)

Source: `plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md` §3 (owner-approved plan).

| Area | Value | Provenance |
| --- | --- | --- |
| Profile ID | `saltwind_solo` | Handoff mandatory |
| Schema / balance version | `1` / `1` | Initial implementation slice |
| Stage timing | 2s grace, 600s deadline, beacon 300s/420s, storm 720s | Handoff §3 |
| Ambient bands | 0.6→3.0 / 120s ×5, cap 80 (8 reserved) | Handoff §3 + engineering default |
| Economy | Chest 20 + ceil(25×1.22^k); Marks rules as specified | Handoff §3 |
| Progression | Six starter weapons; XP curve constants as specified | Handoff §3 |
| Schedule refs | Complete boss/cache/swarm/pursuer refs authored (C04 menu) | `SaltwindScheduleGateTests` |

Asset: `Pirate Game Unity/Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset`

## Version 2 — playtest-driven runtime tuning (2026-09-23)

Directional human evidence: `PLAYTEST_REPORT.md`. **Not** claimed as calibrated to handoff FPS/victory targets.

| Area | Change | Rationale |
| --- | --- | --- |
| Default starting weapon | `broadside_battery` for Saltwind/default captains (Flint kegs, Ember flamethrower unchanged) | Developer run died with zero kills on deck cannons; broadside improves early hit reliability |
| Powder keg damage | `powder_kegs.asset`: baseDamage 14, proc 0.65; catalog proc 0.55; blast mult ~0.72, `TriggeredItem` proc kind | Kegs triggered but felt overtuned after damage fix; requires **fresh run** for placed kegs |
| Enemy threat over time | `SaltwindFinaleLogic.GetOrdinaryTimeDamageMultiplier` applied at spawn in `EncounterSpawner` | Playtest: player felt invulnerable; time-scaled enemy damage |
| Level-up UX | Select on click / 1–2–3 only (no auto-pick) | Playtest confusion with auto-select |
| Salvage chest | Take **E** / Skip **R** in `V3InteractionController` | Parity with confirm-style interactions |

Simulation calibration: `SaltwindBalanceLabGateTests` + optional menu matrix under `balance-lab/` (Step 12). Lab policies are **not** human fun validation.

## Profile content hash (export evidence)

Captured Step 14 (2026-09-23):

`3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d`

Files: `plan/saltwind-verification/profile-export/SaltwindSoloStageBalanceProfile.{json,hash.txt}`
