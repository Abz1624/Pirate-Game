# M11 playable check (automated part)

Scripted Editor run of `V4SaltwindRun` in the default Adaptive mode (`scratchpad/m11play.py`, god mode, idle pilot), log in `playable-check/log.txt`.

| Step | Result |
|---|---|
| Weak build (one weapon), 0:19–0:54 | Pressure 0.89–0.91; threat rate ×0.89–0.91 (fewer spawns), tier lead 0, cap HP ×1, composition off |
| Strong build (six weapons from ~1:00) | Pressure 1.05 → 1.34 over ~80 s; threat ×1.34, tier lead up to 59 s, cap HP up to ×1.20, composition on (close-range share 34 %); alive enemies 13 → 35 |
| Force Pressure 1.0 / 1.35 / 1.0 | Levers switch between neutral and their maxima at once (threat ×1.35, tier lead 60 s, cap HP ×1.20). Spawn counts per 20 s window are not comparable here (the run crossed bands and the swarm pulse); the exact rate change is proven by the PlayMode test |
| Console | `01-difficulty-levers-forced.png`: Pressure 1.35 forced, levers line, ambient rate ×1.69 (swarm pulse ×1.25 × threat ×1.35) |

## Your playtest

(Pending.) See [TODO-FOR-YOU.md](../TODO-FOR-YOU.md#m11).
