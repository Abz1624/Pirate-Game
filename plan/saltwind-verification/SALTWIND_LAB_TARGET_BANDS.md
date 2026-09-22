# Saltwind balance lab — target win-rate bands

Reference: `SaltwindLabMatrixCalibration` in `RoguePirates.Simulation.Balance`.

## Matrix shape (production menu)

| Dimension | Values |
| --- | --- |
| Build policy | Precision, Broadside, AreaControl |
| Skill | **Conservative**, **Average**, **Expert** |
| Unlock pool | FreshSave, MarksUnlocked |
| Runs per cell | 100 (1800 total) |

## Movement model

| Skill | Sim input | Hull multiplier (lab tuning) |
| --- | --- | --- |
| Conservative | Kite | 1.26 |
| Average | Kite | 1.10 |
| Expert | Orbit | 1.08 FreshSave; Marks ×0.36 (×0.50 AreaControl) + ×2.0 boss HP |

## Documented objective (boss win) bands

These are **lab** targets for calibration regression, not promised live player win rates.

| Skill | Target win-rate band |
| --- | --- |
| Expert | 40% – 70% |
| Conservative | 55% – 95% |
| Average | Between Expert and Conservative |

The matrix summary appends **Band warning** lines when a cell falls outside its band.

## When to re-run

After changing `SaltwindSoloStageBalanceProfile`, weapon/boss snapshots, or lab boss/spawn logic:

1. **Export Profile Evidence (Codex)**
2. **Run Balance Lab Matrix (1800 sim runs)**
3. Commit `plan/saltwind-verification/profile-export/` and `balance-lab/` artifacts with the new hash.
