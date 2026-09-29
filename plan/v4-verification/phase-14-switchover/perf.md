# M14 — zero creates after prewarm, and frame time

Method (targeted, per the phase protocol; not a whole run): Editor Play Mode in `V4SaltwindRun`, ship invulnerable,
the six starter weapons equipped (`DevSetWeapons`), the DevTools autopilot playing, the run clock jumped to the heavy
last band (samples taken between 10:02 and 11:28 in-run, about 70 enemies alive). Script:
`scratchpad/zerocreate.py` (DevTools `SceneObjectCreationProbe` and `FrameTimeSampler`).

Machine: the development PC (Windows 11, Unity 6000.3.11f1 Editor, Play Mode). Editor numbers, not a player build.

## Objects created outside the view pool

| Run | Window | Created outside the pool |
|---|---|---|
| 1 | 41 s at ~10:50, 72 enemies | **0** |
| 2 (after raising prewarms) | 41 s at ~10:55, 72 enemies | **0** |

Every GameObject the run makes while playing comes from the V4 `PoolService` (enemy models and health bars, projectiles
and enemy shots, pickups, powerups, all effects, damage numbers) or was made when the presentation root bound (markers,
rings, walls, templates).

## Pool growth past prewarm

Pools grow (policy Grow) when a peak exceeds the prewarm; each growth is an instantiate. Run 1 at the old prewarm counts
grew 230 instances by 11:27; the largest were enemy models (SmuggleBoat 24, RamShip 20, HammerHead 18), hit sparks (33),
keg blasts (9). Prewarms were raised to those peaks; run 2 still grew 165, mostly in the first seconds of the band-4 peak
(158 before the measured window, 7 during it): SmuggleBoat models (57), gem/loot/health barrels (101/75/15), enemy
health bars (73), keg models (29). Those were then raised too (SmuggleBoat enemies `visual.poolPrewarm` 60, health bars
80, barrels 112/80/16, kegs 32, cutlasses 8). Remaining growth after that is not re-measured (no further runs, per the
phase's run budget). Prewarm counts are config (`PoolCatalogConfig`, archetype `visual.poolPrewarm`) or presenter
constants.

## Frame time (Editor, six weapons, ~70 enemies)

| Run | Frames | Average | 95th percentile | Worst |
|---|---|---|---|---|
| 1 | 4,492 (40 s) | 9.13 ms | 10.31 ms | 80.6 ms |
| 2 | 2,997 (40 s) | 13.89 ms | 17.98 ms | 99.2 ms |

Run 2 ran while three trace players were running on the same machine, so run 1 is the representative number. The worst
frames are single spikes (garbage collection or pool growth); no frame budget is defined for V4 yet.

## Release build

`V4BuildSettings.BuildRelease` (non-development, active target WebGL): succeeded, 162 MB, 21 min, scenes V4Boot,
V4MainMenu, V4SaltwindRun, V4ModuleLab. The IL2CPP input (`Library/Bee/artifacts/WebGL/ManagedStripped`) holds
`RoguePirates.V4`, `RoguePirates.V4.Config` and `RoguePirates.V4.Presentation` and no `RoguePirates.V4.DevTools` or
`RoguePirates.V4.Editor`: the Tuning Console, trace harness and autopilot are not in release builds (the DevTools
assembly's define constraint `UNITY_EDITOR || DEVELOPMENT_BUILD`). A development build was not made again (the trace
player is one and contains DevTools).
