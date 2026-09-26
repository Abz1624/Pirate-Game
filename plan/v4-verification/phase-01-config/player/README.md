# Player goldens from M1 on (Windows development player)

These replace the M0 player goldens. Same settings as M0, plus the harness now kills a miniboss-arena focus enemy
20 s after its arena opens (`killArenaAfter` in the header, register R-75). Without that, the kiting autopilot never
killed the Reef Crab and ambient spawning stayed paused from 2:30 for the rest of every run.

| Pair | Result |
|---|---|
| v3-101-a vs v4-101-a | MATCH (437 lines) |
| v3-202-a vs v4-202-a | MATCH (409 lines) |
| v3-303-a vs v4-303-a | MATCH (523 lines) |
| v4-101-a vs v4-101-b (replay) | MATCH (437 lines) |

V3 goldens: `v3-trace-seed-{101,202,303}-a.jsonl`. They are recorded once; V3 does not change until M15.
Preset runs (`*-band2x`, `*-band0x`) are measurements, not goldens.
