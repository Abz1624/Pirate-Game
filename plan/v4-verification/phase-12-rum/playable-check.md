# M12 playable check (automated part)

Scripted Editor run of `V4SaltwindRun` (`scratchpad/m12play.py`; the playable check snapshots and restores your save, so the real treasury is untouched). Log in `playable-check/log.txt`.

| Step | Result |
|---|---|
| 2:10 of play, four weapons | Live rows: Active minutes 2 credits → 4 Rum (minutes 0 and 1 had ≥ 10 kills; the minute in progress does not count) |
| Death | Settled once: 4 Rum paid (treasury 802 → 806), no Doubloons, run gold not banked; toast "Run ended." and "Rum settled: +4" |
| Results screen | `01-results-rum.png`: "Rum earned this run: 4" with the per-row breakdown and "Treasury Rum", goal text in Rum |
| Not covered here | The Signal Buoy step failed (script namespace error) and the run was paused at 2:10; special landmarks, elites, boss, overtime, the chest row and settle-once are covered by `V4RewardsM12Tests` and the PlayMode boss-then-death test |

## Your playtest

(Pending.) See [TODO-FOR-YOU.md](../TODO-FOR-YOU.md#m12).
