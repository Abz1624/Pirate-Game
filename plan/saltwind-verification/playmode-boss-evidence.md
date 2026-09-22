# PlayMode evidence — Boss encounter gate

## BossEncounterPlayModeTests (C03)

Captured inside full CLI **`unity test ... --mode EditMode`** run (`editmode-results.xml`, 2026-09-23). Unity’s runner also executes `HighSeas.Tests.PlayMode.dll` in that sweep:

| Test | Result |
| --- | --- |
| `FailedBossEncounter_DoesNotSecureObjective` | **Passed** |
| `TrackedBossKill_SecuresObjectiveOnce` | **Passed** |

**2/2 passed** (see `editmode-results.xml`, fixture `BossEncounterPlayModeTests`).

## CLI PlayMode-only run

`unity test 'Pirate Game Unity' --mode PlayMode --output playmode-results.xml` currently reports **0 tests discovered** (empty XML). Same with `unity` Pipeline async PlayMode. Treat boss gate evidence as the **embedded results above** until PlayMode CLI discovery is fixed or Test Runner PlayMode is run manually.

## Other PlayMode fixtures

`LevelUpPlayModeTests` and `PlayModeSmokeTests` **fail when executed in EditMode context** (`Destroy may not be called from edit mode`). They are not Saltwind delivery gates; run them from Test Runner → **PlayMode** tab if needed.
