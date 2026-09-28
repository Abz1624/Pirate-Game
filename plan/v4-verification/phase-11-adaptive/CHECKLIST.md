# M11 — Adaptive difficulty

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Phase start commit | `da109d47` (M10, awaiting playtest) |
| Status | **Implemented, awaiting playtest** (28 Sep, run unattended; questions in [TODO-FOR-YOU.md](../TODO-FOR-YOU.md)) |

## What was built

Adaptive is the director's default mode (`director.saltwind` mode = Adaptive); the v3-parity preset sets Observe. The levers are read through `IDifficultyLevers` (implemented by the director); every value is neutral (1 or 0, no random draw) unless the mode is Adaptive and the lever's switch is on. Only **ambient** spawns read them.

| Lever | How | Config (`director.saltwind`) |
|---|---|---|
| 11.1 Threat rate | Ambient spawn rate × clamp(1 + gain·(P − 1), min, max) in `SpawnController.EffectiveRateMultiplier` | `threatRateLever`, `threatRateGain` 1, `threatRateMin` 0.85, `threatRateMax` 1.35 |
| 11.2 Elite chance | Each ambient spawn rolls on the director's own seed-derived stream (`seed ^ 0x5EED11`); a hit spawns an elite copy of the spawner's profile (same movement, attacks, modules; Rich affix, HP ×1.5, damage ×1.1, size ×1.15). Flagged `AdaptiveElite` on the Spawned event | `eliteLever`, `eliteChanceAtMax` 0.05, `eliteAffix`, `elite*Multiplier` |
| 11.3 Tier timing | Tier-unlock checks use run time + lead (up to 60 s, only above Pressure 1); time-triggered beats never move; "Pressure at least" condition triggers read Pressure (Adaptive only) | `tierTimingLever`, `tierLeadSecondsAtMax` 60, `pressureConditionTriggers` |
| 11.4 Composition bias | (a) each ambient spawner's `pressureWeight` curve × its rate (flat by default); (b) pick weights × 1 + maxBoost × build share × Above for enemy roles read from the combat block ("ranged" / "swarm"); close-range share of the build from the power index | `compositionLever`, `closeRangeBehaviors`, `compositionRules` (ranged ← close range +60 %, swarm ← long range +60 %) |
| 11.5 Enemy HP at the cap | Ambient spawns within 5 s of the cap blocking a spawn get HP × up to 1.2 | `capHpLever`, `capHpMultiplierAtMax` 1.2, `capBlockedWindowSeconds` 5 |

"Above" = clamp01((P − 1) / (pressureMax − 1)): strong builds only.

- **I11.6 Never adapt:** boss and encounter spawns (`EncounterExecutor`), scripted beats (`ScriptedPhases`), overtime and storm (`OvertimeAndStorm`) never read the levers; nor do stage content, progression, enemies or weapons (rewards, boss HP, kills). Enforced by a source-scan test and by the executor reading levers only for `ambient` calls.
- **I11.7 Run record:** at death (not for runs left mid-way, not in the Module Lab) `V4Game` writes a `RunScoreRecord` (seed, kills, run time, average and peak Pressure, director version and mode, config-set hash, forced flag, tuning-edit count, ranked flag) to `PlayerPrefsRunRecordStore` (`v4_run_records`, last 50). Ranked = Adaptive, `version == rankedVersion`, never forced, no live edits, director healthy. The key is cleared by the trace boot and restored by the Editor trace session and the PlayMode test isolation.
- **Console:** Difficulty section shows every lever's live value and the ambient rate, a "Force Pressure (test only)" toggle and slider (clamped to the band; the run becomes unranked); the lever switches and ranges are Config rows. Trace header adds `directorMode`.

## Tests

EditMode `V4AdaptiveM11Tests` (9): lever math at the band edges and with a big gain; neutral in Observe/Off and per switch (no elite draws); the elite stream is seed-derived, replayable and ≈ the configured rate; composition roles and weights, and the build's close-range share; adaptive replay gives the same Pressure and elite series; never-adapt source scan; the run record (average/peak beside kills; ranked needs Adaptive, the ranked version, no edits, never forced); the record store (last 50, own key, corrupt key); forced Pressure clamps and clears. M10's observe test now asserts Adaptive by default and Observe under the preset.
PlayMode `V4AdaptivePlayModeTests` (1): in the real run, forced Pressure moves the ambient rate by exactly ×1.35 / ×0.85, Observe is neutral, the elite lever at 100 % turns ambient spawns into flagged elites, and death writes exactly one record with the run's kills and hash (forced + edited → unranked).
Suites (after the review fixes): EditMode 271/271, PlayMode 48/48.

## Trace set (end of phase, run once)

Three seeds under the v3-parity preset (director Observe): **exact match** with the V3 goldens (437 / 409 / 523 lines) — [trace-comparison.txt](trace-comparison.txt). Adaptive itself is intentionally different from V3 and is covered by the tests and the playable check.

## Playable check

[playable-check.md](playable-check.md).

## Independent review

A read-only reviewer checked the phase against the plan and the spawn code. Observe/Off parity confirmed (every consumer is gated on Adaptive; no extra draws or float-order changes). Findings and fixes:

| # | Finding | Fix |
|---|---|---|
| 1 | Major: the cap-HP lever never fired — the ambient spawner checks the cap before calling the executor, so the block was never recorded | The ambient spawner records the block; the executor also records its in-loop cap stop; the HP bonus is applied after the boss check. New PlayMode test on the real spawn path: fill the cap, free a slot, the next spawn has ×1.2 HP (ordinary spawns ×1.0) |
| 2 | Major: adaptive elites paid elite rewards (Rich gold, elite payout, forced powerup, elite-kill item procs) — per-kill rewards must never adapt | Adaptive elites carry `ordinaryRewards`; the kill path pays them as ordinary units. PlayMode test: with the powerup chance at 0, killing adaptive elites drops nothing |
| 3 | Adaptive elites escaped per-spawner caps (they carried the profile copy) | The agent is pointed back at its source profile after spawning |
| 4 | Elite profile copies were cached, went stale after live edits, and were destroyed only at the next run | A fresh copy per elite spawn, destroyed right after it (DestroyImmediate outside Play Mode) |
| 5 | `Enum.Parse` per elite spawn | Cast; a test asserts both affix enums have the same names in the same order |
| 6 | A NaN forced Pressure made ambient spawns fire every tick | NaN/∞ rejected |
| 7 | Ranked flag vouched for itself; console cheats didn't unrank | Opening the dev console marks the run unranked. A published hash list is left for the leaderboard work (logged) |
| 8 | "Pressure at least" conditions could fire later for weak builds | Conditions see max(1, Pressure) |
| 9 | AdaptiveElite flag published when the profile was already elite | Already-elite profiles never roll |
| 10 | Test gaps (spawn path, rewards, cap HP) | Covered by the two PlayMode tests above; lockstep field test allows the one V4-only field and checks it stays off |

Re-verified: EditMode 271/271, PlayMode 48/48.

## Deviations (logged for you)

- Time-to-death band for strong/weak builds not measured (needs long runs); lever values over 2.5 minutes recorded instead.
- Overtime is not adapted (only ambient spawns); a possible later change.
- The spawner `pressureWeight` curves ship flat, so part (a) of composition bias does nothing until authored.
- Tier lead affects tier unlocks and "Pressure at least" triggers only, not level/kill-condition triggers.
- Ranked runs are not checked against a published config-hash list yet (needs the leaderboard backend).
