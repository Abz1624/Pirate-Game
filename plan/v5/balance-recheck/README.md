# V5 balance re-check after the demo (30 Sep – 1 Oct 2026)

The owner kept the demo's tuning as the new baseline and asked for the balance to be re-checked before M4.

## 1. What the demo changed (findings, `before-retune/`)

The demo's XP and ship-speed switches were moved into config: `PickupConfig.xpPickupMultiplier` (0.5) and
`RunConfig.shipSpeedMultiplier` (1.625). The simulator now models both. Every demo value keeps its pre-demo value in
`DevTools/Balance/DemoBaseline.PreDemo` and its V3 value in the v3-parity preset.

The simulator was run on the demo data and on the V5 M3 data (the same code, 200 runs per personality, seed 1):

- Targets missed went from **2 of 76 to 24 of 70**: six more targets could not be measured, because runs ended before
  those minutes, so the true count is 24 or more. The median run for most players fell from 10.2 to 8.1 minutes, and the
  Steady Captain beat the boss in 49% of runs instead of 82%.
- The **XP pace alone** caused it. Putting only XP back to ×1 restored the M3 result (2 missed). Ship speed, weapon
  ranges and shot lifetimes, and the enemy changes each moved the median run by less than half a minute.
- Weapon parity did not move: every weapon in a role lost range together, so the ratios cancel out.
- XP sweep (`before-retune/xp-sweep.md`): ×0.8 → 4 missed, ×0.9 → 3 missed.

**Owner's decision:** keep ×0.5 and retune around it.

## 2. The retune (`DemoBaseline.Retune`, written by RoguePirates ▸ V5 ▸ Write demo-baseline retune)

Grid searches: `before-retune/retune-grid-*.md`.

| Value | V5 M3 | Now | Why |
|---|---|---|---|
| Enemy HP growth per minute (c1, c2) | 0.09, 0.018 | 0.0495, 0.0099 | ×0.55: builds are weaker at each minute with half the levels |
| Ordinary-time HP growth (c1, c2) | 0.08, 0.01 | 0.044, 0.0055 | ×0.55 |
| Enemy damage growth per minute (c1) | 0.025 | 0.0175 | ×0.7 |
| Ordinary-time damage growth (c1, c2) | 0.06, 0.01 | 0.042, 0.007 | ×0.7 |
| Evolution card from weapon level | 8 | 6 | Focused builds still evolve in most runs (Build Planner 59% by 6.6 min; was 33% at level 8) |
| Lightning Mast base damage | 10.78 | 10.56 | −2%: at the new parity level 6 its extra strike put it at ×1.20 of its role (the band is ±20%) |
| Director on-track growth | M2 curve | re-measured | The director's idea of an average build follows the slower levelling |
| Level targets 6:00–12:00, Steady Captain level at 10:00 | 18–29 … 32–48; 30–46 | 15–26 … 23–39; 21–35 | Follow the chosen slower pace |
| Seconds between level-ups (minute 1, 0–10 min) | 4–12, 13–20 | 12–18, 20–30 | ±20% around the chosen pace (15 s, 25 s); owner, 1 Oct |

Outcome targets (how long runs last and how often the boss is beaten) were **not** changed.

## 3. Result (`comparison.md`, `model-report.md`, `weapon-parity.md`, `build-pieces.md`)

The review fixed the simulator's XP order after these findings (the XP gain stat now applies per pickup before the
pace, as in the game), so the final reports below re-measured M3 too; `before-retune/` keeps the earlier runs. In
`comparison.md`, the row that puts back "the retune" is scored against the M3 targets, because the targets are part
of what it puts back.

- Targets missed: **2 of 76**, the same as M3. Most players' median run lasts 10.3 minutes (10.1 in M3). Every
  personality's boss-win rate is within its band.
- The two misses were already known:
  - The Glass Cannon's median run is 13.2 minutes against a band of 8–13 (13.7 in M3).
  - Kills in minute 6 are 26 against a floor of 30. That minute is the Steady Captain's boss fight, and the M2 test
    already lists it as a known miss.
- Evolutions: the Steady Captain evolves in 44% of runs (41% in M3), the Build Planner in 52% (69%; the M3 test's
  floor is 50% over its own 150 runs, and it passes).
- Weaker players' runs are shorter: the First-Timer's median run is 7.8 minutes (8.4 in M3) and the Thumb Sailor
  beats the boss in 27% of runs (31%). Stronger players' runs are longer: the Steady Captain's median is 13.8 minutes
  (12.2) and the Sea Legend's 18.4 (16.5). All are within their bands.
- The M3 column is M3's data re-run with today's simulator; the numbers in `plan/v5/phase-03-builds/` came from the
  simulator before the XP-order fix and no longer reproduce exactly (Steady Captain 12.8 min and 82% there).
- The Captains stay within ±10% of each other; runs still see about 5 items by 10:00.
- Weapon parity is now also checked at level 6, where evolution unlocks. Every weapon is within ±20% of its role
  (the Lightning Mast ×1.18 there).

## Checks

- EditMode 372/372 (`editmode.json`), PlayMode 53/53 (`playmode.json`), after both review rounds. Two earlier full
  PlayMode runs each had one intermittent failure that passes on re-runs and depends on the run's random map, not on
  this change: `PowderCache_StillSpawnsThroughTheEncounterExecutor` (2 of 3 ambush spawns placed) and
  `Presenters_ReplaceTheV3Views_AndShowTheRun` (the Treasury Beacon found in the first 3 s). Both results are kept
  (`playmode-run1-*.json`, `playmode-run2-*.json`) and a separate task will make the tests deterministic.
- Playable check (`playable-check.ps1`, `playable/`): boot → menu → Set Sail → the run; the live game reads every new
  value (XP ×0.5, ship ×1.625, evolution level 6, the enemy growth, the director curve, the Lightning Mast's damage);
  the ship's speed factor is 1.625; level 4 and three weapons at 2:00 (clock jumped); 0 game errors. The XP pace on a
  single pickup is proven by `V4PickupTests.XpPickupMultiplier_PacesTheXp_WithoutLosingFractions` (live kills also pay
  XP during the check, so it cannot isolate one pickup).

## Limits

- The simulator models ship speed only through how fast enemies close in, and weapon range only through its power
  model. The feel of the faster ship and the shorter ranges needs the owner's playtest.
- The balance targets are the simulator's; the owner's playtest remains the gate.

## 4. Independent review (v5-reviewer, 1 Oct) and fixes

No blockers. Fixed:

1. **Ship speed had no automated test.** Two new PlayMode tests: the factor comes from `RunConfig` and survives a
   move-speed change; a preset requested before the run sets it (×1).
2. **Only 2 of the 30 retune values were checked.** New `V5DemoBaselineTests`: every retune value and the director
   curve are in the data; every demo value is in the data; the parity preset takes every demo value and every retuned
   game value back to V3.
3. **The new fields were not validated.** Both must now be greater than 0 (and have `[Min]`).
4. **The simulator applied the XP gain after the pace.** It now applies it before, as the game does; the reports were
   re-run.
5. **README count.** Now quotes "24 of 70" and explains the retune row.
6. **Balance versions.** Every demo-changed or retuned config is now `saltwind-v5-demo`.
7. **Re-running the retune tool reverted later tunes.** It now writes a value only while the asset still holds its
   pre-retune value, and it reports anything it skips.
8. **Stale comments in the test fixtures** are updated.
9. **The current shot lifetimes were not tested.** `V5DemoBaselineTests` checks them.
10. **The speed factor sits outside the stat resolver.** Recorded as a config-level exception on
    `PlayerShipController.RunSpeedMultiplier`: it is a per-map tuning factor, not a build modifier.

Re-verification: all 10 fixed. It found 3 new minor issues, also fixed:

- A. The retune tool relabelled balance versions even on configs a later phase had tuned. It now relabels only V3,
  M1, M2 and M3 versions.
- B. The ship-speed stat check could not fail on its own. It now raises move speed through the game's own path (the
  move-speed card), checks that the stat rose, and then checks the factor.
- C. The director curve predated the XP-order fix. It was re-measured (the same up to 10:00 within 3%; 12:00 is 47.6,
  was 42.1) and the reports were re-run.

Owner's answers (1 Oct):
- The level-up pace targets hold the pace: ±20% around 15 s and 25 s, so minute 1 is now 12–18 s (was 10–20; the
  reports above still print 10–20) and 0–10 min stays 20–30 s. Measured 15 s and 25 s: met. `V5BalanceM2Tests` and
  `V5DemoBaselineTests` pass on the new band.
- Playtest of M1–M3 and the retune: "fine for now, we'll finetune it later".
