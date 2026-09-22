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
| Schedule refs | `enforceCompleteScheduleReferences = false` until C04 | Engineering gate — complete boss/cache/swarm/pursuer refs still null in `EncounterSpawnSchedule` assets |

Asset: `Pirate Game Unity/Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset`

Content hash: capture with EditMode test `Export_Hash_IsStable_AndChangesWhenGameplayChanges` or export job (pending evidence file under `plan/saltwind-verification/profile-export/`).

## Tuning changes

None yet — calibration (C12) not started.
