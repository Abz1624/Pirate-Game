# V5 balance re-check: V5 M3 → current data (the demo baseline)

Same code, 200 runs per personality, seed 1. Targets missed: **2 → 2** of 76.

| Personality | Median end minute | Boss won | Level 5:00 | Level 10:00 | Kills/min | Gold | Evolved | Targets now |
|---|---|---|---|---|---|---|---|---|
| First-Timer | 8.4 → **7.8** | 0% → **0%** | 24 → 15 | 38 → 26 | 74 → 55 | 391 → 261 | 31% → 27% | all met |
| Thumb Sailor | 8.2 → **7.8** | 31% → **27%** | 23 → 15 | 40 → 28 | 58 → 41 | 290 → 193 | 5% → 5% | all met |
| Casual Cruiser | 8.5 → **8.5** | 44% → **43%** | 22 → 15 | 38 → 26 | 63 → 48 | 380 → 280 | 41% → 33% | all met |
| Steady Captain | 12.2 → **13.8** | 76% → **78%** | 23 → 15 | 35 → 24 | 57 → 47 | 609 → 540 | 41% → 44% | all met |
| Glass Cannon | 13.7 → **13.2** | 94% → **81%** | 24 → 15 | 39 → 26 | 80 → 54 | 881 → 552 | 48% → 38% | ✗ median end minute 13.2 (8–13) |
| Turtle | 8.9 → **8.8** | 43% → **41%** | 21 → 13 | 37 → 24 | 53 → 38 | 310 → 205 | 37% → 29% | all met |
| Treasure Hound | 8.4 → **8.4** | 32% → **32%** | 23 → 15 | 41 → 25 | 51 → 38 | 282 → 201 | 27% → 19% | all met |
| Build Planner | 14.1 → **14.5** | 90% → **83%** | 22 → 14 | 36 → 23 | 57 → 41 | 710 → 517 | 69% → 52% | all met |
| Sea Legend | 16.5 → **18.4** | 100% → **99%** | 22 → 13 | 36 → 23 | 52 → 36 | 758 → 564 | 79% → 64% | all met |
| Speedrunner | 5.5 → **5.8** | 96% → **95%** | 23 → 14 | – → – | 64 → 38 | 365 → 285 | 0% → 0% | all met |

Most players (weighted by share): median end minute 10.1 → **10.3**.

## Pacing and per-minute targets that changed

- minute 6: kills: 33 → 26 (target 30–140) met → **MISSED**

## Weapons (power ratio to the role at each parity level)

| Weapon | L1 | L4 | L6 | L8 | L12 |
|---|---|---|---|---|---|
| deck_cannons | 1.00 → 1.00 | 0.96 → 0.96 | 1.00 → 1.00 | 1.01 → 1.01 | 1.03 → 1.03 |
| broadside_battery | 0.95 → 0.95 | 1.04 → 1.04 | 1.09 → 1.09 | 1.02 → 1.02 | 1.00 → 1.00 |
| powder_kegs | 1.05 → 1.06 | 0.96 → 0.97 | 0.94 → 0.94 | 0.97 → 0.98 | 1.16 → 1.17 |
| lightning_mast | 0.87 → 0.86 | 1.10 → 1.09 | 1.20 → 1.18 | 1.16 → 1.14 | 0.91 → 0.89 |
| flying_cutlasses | 0.95 → 0.95 | 1.05 → 1.05 | 1.10 → 1.10 | 1.06 → 1.06 | 0.92 → 0.92 |
| flamethrower | 1.11 → 1.11 | 0.91 → 0.91 | 0.83 → 0.83 | 0.92 → 0.92 | 1.08 → 1.08 |
| swivel_guns | 0.88 → 0.88 | 1.13 → 1.13 | 0.98 → 0.98 | 0.99 → 0.99 | 1.02 → 1.02 |
| long_nine | 1.01 → 1.01 | 1.04 → 1.04 | 0.97 → 0.97 | 0.99 → 0.99 | 0.97 → 0.97 |
| harpoon_gun | 1.12 → 1.12 | 0.89 → 0.89 | 1.06 → 1.06 | 1.01 → 1.01 | 0.99 → 0.99 |
| mortar | 1.10 → 1.10 | 0.94 → 0.95 | 0.89 → 0.90 | 0.89 → 0.89 | 0.95 → 0.96 |

## Which change moves the runs (each group put back alone on the current data)

| Put back | Targets missed | Most players: median end minute | Steady Captain: end / boss won / level 10:00 | Casual Cruiser: boss won |
|---|---|---|---|---|
| nothing (the current data) | 2 of 76 | 10.3 | 13.8 / 78% / 24 | 43% |
| the retune (enemy growth, evolution level 6, director curve, level targets) | 25 of 70 | 8.1 | 8 / 53% / 22 | 12% |
| XP pace (xpPickupMultiplier 0.5 → 1) | 25 of 76 | 13.6 | 15.2 / 95% / 38 | 79% |
| ship speed (shipSpeedMultiplier 1.625 → 1) | 3 of 76 | 10 | 13.8 / 82% / 24 | 47% |
| weapon ranges and shot lifetimes | 2 of 76 | 10.3 | 13.8 / 77% / 24 | 45% |
| enemies (charge timings, ranged from 1:00, band 0 specialists, kraken spawn in view) | 2 of 76 | 10.3 | 13.6 / 76% / 24 | 42% |
