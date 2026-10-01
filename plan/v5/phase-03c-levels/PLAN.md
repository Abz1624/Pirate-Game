# V5 M3c: level-up rhythm

Owner's note a (1 Oct 2026): too many level-ups break the flow; answer: gaps growing like a Fibonacci series (sketch:
"1 1 2 3 5"). Built while the owner was away (decisions D6, D11-D14 in `plan/v5/DECISIONS-WHILE-AWAY.md`). Start
commit: `5bd8bd4f`.

| Task | What | Where | Status |
|---|---|---|---|
| I3c.1 | A per-level XP table (empty = the old curve), extended past its end by its last step | `Config/Schemas/ProgressionConfig.cs` (`xpToNextTable`), `Runtime/Progression/ProgressionMath.cs` | done |
| G3c.1 | Target times: level 1 with the first kill (~12 s), then gaps of 10, 10, 20, 30, 50 s (Fibonacci × 10 s), then 60 s: 4 level-ups in the first minute as before, about 13 by 10:00 (was ~24). The tuner fits the table from the curve in unsinkable simulator runs of the baseline personality, scored on the mean time error over the 10-minute voyage; converged in round 4 (4.9%) | `DevTools/Balance/XpTableTuner.cs`, `SimBuild`/`SimRun` `Unsinkable`, `Editor/V5LevelRhythm.cs`; log `xp-table-tuning.md` | done |
| G3c.2 | Builds keep their power with fewer picks: weapon cards grant 2/2/3/3/4 levels by rarity (was 1/1/2/2/3), the evolution card from weapon level 5 (was 6), Roxie's hull 104 (was 88); an interim flat enemy scale (HP and damage × 0.85, not tied to time; M3d replaces it) holds every personality's run length and boss wins | `Data/Build/Progression_Saltwind.asset`, `Captains/roxie.asset`, `Data/Run/ExperimentConfig.asset` | done |
| - | Pace targets hold the rhythm to about ±20% (the owner's rule): the level at each minute's end (4 at 1:00 to 13 at 10:00), the Steady Captain's level at 10:00 (10-16), the gaps in minute 1 (12-18 s) and over 0-10 min (37-55 s) | `Data/Balance/BalanceTargets.asset` (`XpTableTuner.TargetEdits`) | done |
| - | v3-parity preset: the table empty (the curve), one level per card, Roxie's hull 88, the enemy scale 1 | `plan/v4-verification/presets/v3-parity.json` | done |
| T3c | `V5LevelRhythmTests` (gap series, table maths and validation, the data, parity, each level near its target time on another seed, every personality's outcome targets with two bounded known misses, the level and pace targets); `V5BuildM3Tests` read the card and evolution levels from the config; `V5BalanceM2Tests` (calibration reverts M3c; per-minute crowd and kill misses bounded until M3d; the elite HP check uses the first elite beat that happens); `V5DemoBaselineTests` (skips what M3c replaced); `V4RunPlayModeTests` (a card adds its levels); `V4AdaptivePlayModeTests` (the experiment HP scale) | `Tests/V4/` | pass |

## Results (simulator, 200 runs per personality, seed 1: `model-report.md`)

- Targets missed: 2 of 76, the same two as M3 (the Glass Cannon's run 13.2 min against 8-13; the minute-6 kills in the
  baseline's boss fight). The median run for most players is 10.6 min (10.3 before M3c).
- Level-ups: 13 by 10:00 for the Steady Captain (was 24), 4 in the first minute; each level within about ±25% of its
  target time on another seed (`level-times.md`); single gaps vary by up to about ±40% (levels 2, 3, 6, 9, 11, 12),
  most around the boss fight, when kills dip.
- Evolutions: focused builds (Build Planner) in 65% of runs by minute 5.6; the Steady Captain in 53%.
- Without the stronger cards and the interim scale, 13 level-ups left builds far weaker (median run ~7.4 min,
  evolutions rare): see D11 and D13.
- In the Editor (`playable/`): with the ship standing still (so enemies arrive slowly) the first levels come later than in
  the simulator, which models a moving, collecting player; the check confirms there are no chained cards and 0 game
  errors, not the rhythm itself (accepted: the simulator measures the rhythm; the owner's playtest confirms it).
- The table holds the 14 measured levels (it was fitted before the interim enemy scale, evolution level 5 and Roxie's
  hull; `level-times.md`, measured on the final data, shows the drift is small); past level 14 each level adds the
  average step of the last four (about 12 XP), so levelling slows gently in overtime.

Carried to M3d: remove the interim enemy scale; clear the bounded known misses.

Evidence: `editmode.json` (381/381), `playmode.json` (57/57), `xp-table-tuning.md`, `level-times.md`, `model-report.md`,
`playable/`.
