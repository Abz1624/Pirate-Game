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
| Conservative | Kite pre-boss; boss orbit + arena snap | 1.26 hull; boss HP ×1.28 (×1.12 Marks) |
| Average | Orbit | 1.10 hull; boss HP ×0.92 (×2.05 Marks) |
| Expert | Orbit | 1.08 FreshSave; Marks ×0.36 hull (×0.47 AreaControl); Marks boss HP ×3.10 |

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
