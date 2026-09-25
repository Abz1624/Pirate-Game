# Player goldens (Windows development player)

The standard trace platform from M1 on. Recorded with `tools/run-traces-parallel.ps1` on the player built by
`tools/build-trace-player.ps1` (commit 008be6a0 plus the parallel-runner harness changes).

Settings: pilot `player-v1`, boss summoned 240 s and killed by the harness at 300 s, visual pins on, 1920x1080
borderless window, PlayerPrefs pinned by TraceBoot (Rook, actives off, First Conquest done, tutorial seen, no home seed).

| Pair | Result |
|---|---|
| v3-101-a vs v4-101-a | MATCH (422 lines) |
| v3-202-a vs v4-202-a | MATCH (249 lines) |
| v3-303-a vs v4-303-a | MATCH (369 lines) |
| v4-101-a vs v4-101-b (replay, run in parallel) | MATCH (422 lines) |

Survival: 101 654 s (boss killed), 202 250 s (died before the kill), 303 619 s (boss killed).
V3 goldens are one-time: V3 code does not change until M15, so later phases only record V4.
