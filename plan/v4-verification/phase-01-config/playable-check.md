# M1 playable check

## Automated (`tools/playable-check-m1.ps1`)

| # | Step | Result | Screenshot |
|---|---|---|---|
| 1 | `V4Boot` → menu → Set Sail → `V4SaltwindRun` plays with config set `saltwind-default` | OK | |
| 2 | Tuning Console → Config section, search `ratePerSecond` | OK: only the timeline's matching fields are listed | `playable-check/01-console-config.png` |
| 3 | Live edit: band 2 rate 1.8 → 4 | OK: running schedule interval 0.5556 → 0.25 s, 1 edit logged, set hash changes | `02-after-live-edit.png` |
| 4 | Restart on seed 101 with the edited values as a preset | OK: band 2 interval is 0.25 s from the start of the new run | `03-preset-run.png` |
| 5 | Reset all | OK: back to 1.8 / 0.5556 s | |

Console errors: 0. Trace measurements (band 0 doubled → 1.97× spawns in minute 0) are in CHECKLIST.md.

## Manual playtest (you)

Status: **Pending playtest**.

What to try:
1. Play from `Assets/_Game/Scenes/V4/V4Boot.unity`. With no edits, it should play exactly like M0 and V3.
2. Press F1 and open **Config**, or type a field name in Search (for example `ratePerSecond`, `reservedEncounterSlots`, `outsideDamagePerSecond`).
3. Early in a run, set **band_0 ratePerSecond** from 0.6 to 1.2 and press Enter. The early game should get busier right away.
4. Change a profile value, for example storm `outsideDamagePerSecond` or `bossAmbientRateMultiplier`, and check it takes effect.
5. Type a preset name and **Save**, then use **Restart with it**. The run restarts on the same seed with your values from the start.
6. Fields marked **(restart)** only apply when a run starts with them (for example band trigger times). Fields marked **(M2)**, **(M3)** and so on are stored now and take effect in that phase.
7. Gate question **R-01**: keep band 0 at the live 0.6 enemies/s, or switch to the doc's 0.5?

Notes from your playtest:

- 
