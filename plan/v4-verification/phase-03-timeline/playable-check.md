# M3 playable check

## Automated (`tools/playable-check-m3.ps1`, after the review fixes)

| # | Step | Result | Screenshot |
|---|---|---|---|
| 1 | Start a run in `V4SaltwindRun` | OK: ambient spawns from V4 (header "Band 0"); the legacy spawner has 0 ambient entries | |
| 2 | Open the console's Wave Timeline section | OK: every phase with its state | `playable-check/01-wave-timeline.png` |
| 3 | Trigger now on the Reef Crab | OK: arena on, focus reef_crab_miniboss | `02-reef-crab-arena.png` |
| 4 | Kill the Reef Crab | OK: arena off, phase Done, ambient spawns resume | |
| 5 | Skip the elite hammerhead | OK: phase Skipped | |
| 6 | Live edit: band 0 rate 0.5 → 3/s | OK: 25 spawns in about 5 s (5 before) | |
| 7 | Duplicate band 2 | OK: `band_2_copy` added with the band 2 spawner | `03-after-edits.png` |

Console: 0 errors.

## Manual playtest (you): M1, M2 and M3 together

Status: **Pending playtest**. The M1 and M2 steps are in `phase-01-config/playable-check.md` and `phase-02-archetypes/playable-check.md`.

**A normal run (`Assets/_Game/Scenes/V4/V4Boot.unity`)**
1. It should feel like V3. The spawns now come from V4, but on the default timeline they are the same enemies at the same moments. Differences you may notice: band 0 is 0.5 enemies/s (R-01), the eight specialists have ship models instead of cubes (R-13), and warning toasts read "WARNING — Swarm pulse incoming!" instead of naming the time.
2. The F1 header shows the current phase (Band 0, Band 1, …).

**Wave Timeline (F1 → Wave Timeline)**
3. Watch the phases change state as the run goes: Waiting → Warned → Active → Done.
4. **Trigger** the Reef Crab early: it appears about 22 m away, ambient spawns stop until you sink it, then resume at once.
5. **Skip** a band or an elite: that phase does nothing for the rest of the run.
6. **Duplicate** a band (⧉), then in F1 → Config → `timeline.saltwind` set the copy's trigger time (for example 150) and edit its pools (for example keep one entry, `bomb_boat`). At that time the bands switch to your copy: bands run in time order, each until the next one starts.
7. Put an elite behind player level: set `elite_hammerhead` → trigger `kind` Condition, `conditionKind` PlayerLevelAtLeast, `conditionValue` 8. It waits until you reach level 8.
8. Save a preset and **Restart with it**: your timeline edits (including the copy) apply from the start.

**Late game**
9. Survive to 10:00 for overtime (Drowned Pursuit: sharks and cannonboats, faster and faster) and to 12:00 for the storm (warning at 11:50, the safe water shrinks until 15:00, damage outside it).

**Module Lab (`V4ModuleLab.unity`)**
10. Still spawns nothing by itself: no bands, beats, pulses or overtime.

**Open decisions for this gate** (the register has the details)
- R-02: Reef Crab at 2:30 (V3) or 1:30 (the doc)?
- R-03: scheduled "elites" spawn as plain units in V3. Make them real elites?
- R-05: overtime spawns sharks/cannonboats with Drowned settings. Use the Drowned archetypes instead?
- R-75: a miniboss arena lasts until its enemy dies. Add a timeout?
- R-57: storm damage per frame is throttled by i-frames (about 0.7 damage/s). Switch to damage per tick?

Notes from your playtest:

-
