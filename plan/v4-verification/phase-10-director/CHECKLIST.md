# M10 — Difficulty Curve, Player Power Index, Director (observe)

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `7d1aa4ac` (M9 passed) |
| Status | **Implemented, awaiting playtest** (28 Sep, run unattended while you were away; questions in [TODO-FOR-YOU.md](../TODO-FOR-YOU.md)) |

## What was built

| Task | What | State |
|---|---|---|
| I10.1 | `CurveEvaluator` | Done. Enemy HP and damage growth per minute from `DifficultyCurveConfig` (Polynomial or Keyframes), × tier and stage multipliers × global HP. The polynomial is written in V3's exact expression shape (Mono evaluates float expressions at higher precision, so a factored form differed by 1 ULP). The enemy simulation's spawn now reads it (`EnemySimulation.Curve`); with the default config it equals `GameMath.EnemyHP`/`EnemyDamage` bit for bit. The curve fields are live from M10 (`[AppliesFrom(10)]`) |
| I10.2 | `PlayerPowerIndex` | Done. Offense = Σ slots of resolved damage × rate × projectiles × a per-pattern coverage factor (`DirectorConfig.behaviorCoverage`), × the build's damage multiplier. Defense = hull fraction ÷ incoming multiplier (Brace!/Keel raise it). Performance = kill rate vs spawn rate (even = 90 % killed) minus a hull-loss penalty, over a 30 s window of events. Inputs through `IPowerInputs` (`LegacyPowerInputs`, deleted in M14) |
| I10.3 | `DifficultyDirector` | Done. Module "difficulty" (tick order 50): samples at 1 Hz on the run clock; target = clamp(1 + gains · (norm − 1), 0.85, 1.35); exponential smoothing 20 s, slope limit 0.02/s, band clamp; Off / Observe / Adaptive (this phase Observe); `PowerChanged` event each sample; version `director-1` stamped in the trace header. Only simulation state; no RNG. A failing sample stops the director instead of the run; Off resets Pressure to 1 and forgets what happened meanwhile |
| I10.4 | `DirectorConfig` + console | Done. `director.saltwind` (gains, window, smoothing, slope, even kill ratio, hull-loss penalty, coverage per pattern, mode, version) in the default set via `V4DirectorConfigGenerator`. Console Difficulty section: Pressure with band, offense/defense/performance, 3-minute graph, mode buttons; header shows live Pressure; curves and gains are Config rows |

## Tests

EditMode `V4DifficultyM10Tests` (10): curve defaults = V3's formulas bit for bit over 0–20 min at 0.1 s steps (every tier and stage); keyframe curves; clamp/slope/smoothing; replay gives the same Pressure series; performance math (even point, faster kills, hull loss, nothing spawned = even); performance from the real event stream; power index grows with the build and defense follows hull/incoming; one sample per interval, `PowerChanged` published, stays in band; Off mode keeps 1 and forgets; Observe: nothing outside the difficulty module (except the composition root and the event enum) names `IPressureView`, `DifficultyDirector` or `PowerChanged`, and no presenter does.
Suites: EditMode 261/261, PlayMode 46/46.

## Trace set (end of phase, run once)

Three seeds, v3-parity preset (director in its default Observe mode), Windows player: **exact match** with the V3 goldens (437 / 409 / 523 lines) — [trace-comparison.txt](trace-comparison.txt). This proves the curve rewiring and that Observe changes nothing, and confirms M9's unlock-rule fix in a real run.

## Playable check

[playable-check.md](playable-check.md): one weapon → Pressure ≈ 0.89–0.90; six weapons → rises smoothly to 1.32–1.34 (slope-limited), offense 11.1 → 69.9.

## Independent review

A read-only reviewer checked the phase against the plan and V3. Findings and fixes:

| # | Finding | Fix |
|---|---|---|
| 1 | Off mode only stopped sampling: Pressure froze at its last value and kills/hull loss piled up for when it came back | Off resets Pressure and parts to 1, clears pending counters and the window |
| 2 | An exception in a sample would propagate into the module loop | try/catch latches the director off and logs; the run continues |
| 3 | NaN (e.g. max hull 0) could poison Pressure forever | NaN target → 1 in `Target`, and `Step` keeps the old value |
| 4 | Performance with nothing spawned (boss arena, pauses) read as "all killed" (a lead) | Counts as even |
| 5 | Window used `<`, keeping one sample too many; used sim time, not the run clock (the console jump moves the run clock) | `<=`; time from `IRunStatus.ElapsedSeconds` |
| 6 | `Single<T>()` (allocating scan) called every sample and per spawn | Configs cached (edits change the run copy in place) |
| 7 | Offense baseline taken at the first sample even if weapons arrive late | Baseline normalised back to time 0 |
| 8 | 60 × 1/60 never reaches 1 in floats → 118 samples in 120 s | Epsilon and a remainder loop |
| 9 | Console header still showed "Pressure —"; mode edits stamped `Time.time` and swallowed errors | Header shows Pressure; edits use the run clock and log errors |
| 10 | Curve fields still labelled "applies from M8"; the migrator rebuilt the set without the M9 build configs and the director | Labels → M10; migrator keeps configs other generators own |
| 11 | Test gaps (Off mode, performance from events, wider Observe scan) | Added |

Re-verified with the M10 tests and the full suites above.

## Deviations from the plan text (logged for you)

- Expected offense curve = the run's own first offense × the HP growth curve, not "generated from the M0 V3 traces" (the traces have no weapon stats; this makes "keeping up with the enemy HP curve" = 1).
- Defense = hull fraction **÷** incoming multiplier (the plan says ×; with × a Brace! would read as weaker).
- The director config field is `version`, which M11's ranked flag will use (plan says `rankedVersion`).
- Ordinary-time/overtime HP multipliers stay in the spawn controller (M3), not `CurveEvaluator`; the expected curve leaves out overtime.
- The power index leaves out crit and evolution modifiers (approximation; tune in M11).
- Trace header stamps the director version, not the mode (the preset hash covers the mode).

## Decisions for you

See [TODO-FOR-YOU.md](../TODO-FOR-YOU.md#m10).
