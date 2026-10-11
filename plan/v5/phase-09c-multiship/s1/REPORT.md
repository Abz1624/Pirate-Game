# M9c S1 — per-session clock (11 Oct)

## Result

The match server's run moves only inside the server's tick; nothing on the server writes `Time.timeScale`. Single
player keeps Unity's frame clock and is byte-identical (fingerprints (a) and (b) unchanged). Case (c), the solo server
voyage, is single-valued again (the S0 "variant Y" is gone) and its golden was re-frozen because the server path
changed on purpose.

## Design

- `V4/Foundation/Core/SessionClock.cs`: UnityFrame mode (default, returns Unity's time) or Driven mode (moves only in
  `BeginStep`/`EndStep`; `SetHeld` makes a step a no-op). Injected, never static.
- `V4Game.StepFrame(dt)`: `Update` calls it in UnityFrame mode only; in Driven mode one call runs the whole frame on the
  session clock. Mode from `RunRequest.DrivenClock` (set by `NetServerComposition`, cleared by the client).
- Gameplay readers moved onto the clock: ship fixed step and island clock, hit immunity and rescue, boost, the frame
  driver, the simulation view, ship lookup, `PvpMatchController` (27 reads), the damage service.
- Server: `NetServer.Tick` → `StepWorld` (world steps) → ship input. Holds (join, away, cards) mark the clock held and
  skip the step; `WorldTick` counts only stepped ticks.
- `V5/Net/NetConfig.cs` + `V5NetConfig.asset` (outside the voyage ConfigSet, so the voyage config hash is unchanged):
  `worldStepsPerTick = 2` (override `RP_WORLD_STEPS_PER_TICK` only with `RP_ALLOW_TEST_PRESETS=1`).
- Host reports add world steps, steps per tick, world tick, kills per minute, hold reason, and write `runlog.json`.

## Variant Y

The server ship's 0.2 s hit immunity (exactly 6 net ticks) read the Editor process's clock, so which side of the
boundary a hit landed on depended on the process's age and history. With only that read back on Unity's clock, S1
reproduces the old S0 golden byte for byte (except `server.worldTick`, which now counts only stepped ticks). The
session clock starts at 0 for every session, so the dependency is gone. Experiments: `fingerprint-c/`.

## Steps per tick: 2 world steps of 1/60 s per 30 Hz tick

Weapons fire at most once per step and burst gaps restart on the step, so one 1/30 s step would change fast weapons'
rhythm; two steps match the M9b server and a 60 fps voyage. Swivel-gun voyage 90 s: 61 kills (1 step) vs 60 (2).

| load200, Docker, Mono, 240 s | Steps | Enemies near ship | Kills/s | Tick p50 / p95 / p99 ms |
|---|---|---|---|---|
| S0b (pre-S1) | 2 frames | follow | 3.7 | 6.60 / 9.06 / 10.57 |
| pre-S1 image a / b | 2 frames | follow | 4.8 / 4.9 | 6.49 / 8.40 / 9.78; 6.47 / 8.26 / 9.71 |
| S1 | 1 | lost / lost / follow | 3.0 / 1.8 / 4.5 | 3.90 / 8.69 / 18.3; 4.50 / 18.19 / 30.9; 3.81 / 15.96 / 41.2 |
| **S1** | **2** | **follow** | **7.9** | **5.90 / 7.96 / 12.8** |
| S1 | 2 | lost / lost | 1.7 / 1.7 | 7.44 / 18.55 / 26.4; 7.53 / 20.70 / 27.2 |

- **S0b condition (p95 <= 7 ms locally): not met.** Best 7.96 ms.
- **Open regression:** on the S1 image the load200 enemies lost the real ship in 4 of 6 runs (0 of 3 before S1); when
  they do, p95 rises to 16-21 ms. Two things changed: the order inside a tick (world, world, ship instead of world,
  ship, world) and the headless test client's stage no longer at time scale 0. To be resolved before S2 (S1b).

## Scripted Docker voyage

S1 (2 steps), 60 s: 29, 27, 32 kills (26.6-35.2 per minute); the pre-S1 image with the same client script: 31, 32.
90 s with the extra checks: 49 kills; a second player and a replayed ticket refused. Run checks (rules v7) pass on the
bounds for all six run logs; the verdict is `outdated_client` because the server's 30 Hz preset has config hash
`a49c415a…` (not in the rules; known since S0).

## Tests

EditMode 626/626 twice (new `V5SessionClockTests`: clock modes, a frame-time ban in Foundation/Runtime with a
visual-only allow-list, a `Time.timeScale` write ban); weapon goldens pass. PlayMode full 132 passed, 2 skipped
(probes), 0 failed (`playmode-full-final.json`), including fingerprints (a), (b), (c), the M9a determinism test and the
V5 net suite (new `ServerHold_NeverTouchesTimeScale`, `Away_HoldsWithoutTimeScale`).
