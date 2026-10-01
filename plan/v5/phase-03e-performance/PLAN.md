# V5 M3e: performance (pooling, garbage, batching, culling), then the enemy cap

Owner's notes d, f, i, j (1 Oct 2026): pooling and garbage collection for everything spawned; batching (static and
dynamic) where it helps; occlusion culling; and the answer to h, a higher enemy cap. Built while the owner was away
(decisions D23-D27 in `plan/v5/DECISIONS-WHILE-AWAY.md`, questions 14-16). Audit: `AUDIT.md`. Start commit: `3d934792`.

## How it was measured

`perf-check.ps1` plays the run in the open Editor on a fixed seed (20261001: the same map each time; `run seed:` in
`perf.md` confirms it), the scripted arenas skipped so a clock jump does not pause spawning, the ship invulnerable and
standing. At 1:00, 5:00 and 9:00 the crowd builds for 12 s, then `DevTools/Diagnostics/RunPerfSampler` records 8 s from
the profiler's counters (frame time with median and p95, main thread, GC bytes per frame, batches, draw calls, SetPass,
triangles). With `-Profile`, after the 5:00 sample has stopped, `Editor/V5GcProfile` records ~3 s and lists the markers
that allocate and take time (`gc-profile-*.txt`). After the run it records pool instances created after prewarm, the
console's errors (`console-<label>.txt`), and with `-Shots` screenshots (`playable/`). New zero-cost profiler markers name
each frame step (`V4.Frame.*`), each simulation module (`V4.Module.*`), the enemy world (`V4.Enemies.*`) and the
simulation's sections (`V4.Sim.*`).

Three runs per side on the same seed; the table gives the medians. The runs are not fully deterministic (real-time
frames), and Editor frame times are indicative only: the Editor costs ~2.6 ms a frame and allocates on the main thread
(the sampler's GC column includes it; the profiler's game-only figure does not). The phone budget stays with M7.

## Results (before = `3d934792` with the tools only, runs `before-a/b/c`; after = this phase, `after-a/b/c`)

| Moment | Frame ms (median of means) | Batches | Draw calls | Triangles | Pool instances made mid-run |
|---|---|---|---|---|---|
| 1:00 before / after | 7.5 / 6.9 | 538 / 522 | 548 / 535 | 842k / 839k | |
| 5:00 before / after | 8.1 / 7.7 | 657 / 594 | 669 / 607 | 915k / 880k | |
| 9:00 before / after (~72 alive) | 10.2 / 9.1 | 991 / 784 | 1,034 / 822 | 1,569k / 1,449k | 1-2 / 0 |

- The game's own garbage per frame at 5:00 (profiler, game markers only): **28,325 → 1,342 bytes**
  (`gc-profile-before-a.txt`, `gc-profile-after-a.txt`). The sampler's GC column (with the Editor's own) fell from
  46-87 KB to 34-37 KB a frame.
- The enemy world's main-thread time at 5:00 (the `V4Game.Update` subtree): **1.42 → 0.67 ms**.
- What still allocates (bytes a frame, `gc-profile-after-a.txt`): the loadout HUD's signature 368 (it reads the
  evolution-aware weapon and item names, which are built strings), the developer F1 console 368 + 78 (not in release
  builds), the authored HUD 175, the player ship's FixedUpdate 92, the simulation 64 + hull separation 64 (not yet
  traced; likely the Editor's allocating "missing component" result of the player-ship lookup, to check in M7),
  weapons 24, views 17, ambient 12.
  Recorded for M7 (the phone budget); none grows with the crowd.
- The "Failed to create agent because there is no valid NavMesh" warning, logged each time a pooled enemy model woke
  for the first time (99 times in a run, with a stack trace each: a hitch), fell to 2: models are now stripped while
  still inactive in the pool.

## What changed

| Item | What | Where |
|---|---|---|
| I3e.2 Garbage | Ship wakes: the wake parts are found once per life of a pooled view and touched only when a ship starts or stops (the old helper searched children and names every frame: ~20 KB/frame) | `Presentation/Effects/EnemyViewPresenter.cs` (old `Foundation/Enemies/EnemyShipWakeJuice.cs` removed) |
| I3e.2 | The spatial hash reuses its cell lists | `Runtime/Enemies/SpatialHash.cs` |
| I3e.2 | Config lookups `Single<T>`/`All<T>` cached by type, under a lock (the balance console simulates on a worker thread); `Phase(id)`/`Cap(tag)` as loops | `Config/Core/ConfigSetRuntime.cs`, `WaveTimelineConfig.cs` |
| I3e.2 | The module tick loop by index; the enemy frame's callbacks made once; event kind names cached; the loadout HUD rebuilds its labels only when what it shows changes; `DemoMode` loads its settings once; the player's contact radius once a frame and the ship looked up once per receiver (per simulation) | `RunContext.cs`, `RunBootstrapSequence.cs`, `EnemyWorldModule.cs`, `TimelineTriggers.cs`, `LoadoutHudPresenter.cs`, `DemoMode.cs`, `EnemySimulation.Tick.cs` |
| I3e.2 O(n²) | The enemy overlap passes walk the active enemies only (all ~260 slots' pairs before), the same pairs in the same order (0.065 ms at 5:00 with ~31 alive; the work grows with the square of the crowd) | `EnemySimulation.Tick.cs` |
| I3e.1 Pooling | Enemy models are stripped (colliders, rigidbodies, NavMesh components) while inactive in the pool, through a new pool preparer, not on first acquire; VFX-profile effects and trails prewarmed at bind (2 each) | `Presentation/Pooling/PoolService.cs` (`RegisterPreparer`), `EnemyViewPresenter.cs`, `VfxProfilePlayer.cs` |
| I3e.3 Batching | Generated Mesh LODs for the 14 models over 6,000 triangles a game prefab uses (the Salvage Chest is ~77,650 triangles and a voyage shows about eleven; the three `island*.fbx` are used only by the unused `Cannon_Islands` prefabs: harmless, they ship nowhere); pickups cast no shadows | `Editor/V5MeshLods.cs` + 14 `.fbx.meta`, `PickupPresenter.cs` |
| I3e.3 Static batching | Tried on the islands and taken out: on the same map it saved ~40-55 of ~500 batches early and none at 9:00, for +25 MB of combined meshes (413k vertices, 31.9 MB against 7.1 MB, `perf.md` "Island static batching memory") on a build already over its memory budget; the SRP Batcher batches the islands (D25). The Editor's whole-process memory lines in `perf.md` are noise (they include the Editor) | `perf.md` (`after-2`, `after-nobatch`) |
| I3e.3 Dynamic batching | Measured with the SRP Batcher on: no gain (700 vs 740 batches at 9:00, run noise; unseeded run), stays off (D25) | `perf.md` (`dynbatch-on`) |
| I3e.4 Culling | Occlusion culling does not apply (a stage generated at run start has nothing baked; a steep top-down camera over open sea; no GPU occlusion on WebGL), recorded (D25). The top-down camera's far clip 1000 → 250 m (the view sees ~60 m of sea; the boost zoom reaches ~55 m; the third-person views keep the scene's far clip) | `Foundation/Player/FollowCamera.cs` |
| I3e.6 Cap | Measured at cap 120: 99 alive at 9:00 cost about what 72 cost before this phase. The cap stays 80 (D24), question 14 | `gc-profile-cap120.txt`, `perf.md` |
| Tools | `RunPerfSampler`, `V5GcProfile`, `perf-check.ps1`, the profiler markers | `DevTools/Diagnostics`, `Editor`, here |

## Plan items not done here (review M3e #1; D27)

| Plan item | Why not | Where it goes |
|---|---|---|
| I3e.1 capped pools | Measured: with prewarm and the new preparer no pool makes an instance mid-run (0 in each after run); a cap would only add a recycle rule to tune | M7 if the phone shows a spike |
| I3e.1 flame-stream prewarm, minimap glyph reuse | Not seen in the measured runs (the standing ship does not fire the flamethrower; glyphs change on a marker kind change, rare) | M4 (the flamethrower VFX is rebuilt there), M7 |
| I3e.1 a test listing every per-event spawn site | Replaced by the pool count after each perf run (`perf.md`: 0) and the existing pool tests; a list of spawn sites would go stale | - |
| I3e.2 projectile checks on the spatial hash | The player projectiles' step is not in the top 40 of the 5:00 profile (~31 alive, `gc-profile-after-a.txt`); not measured at 9:00, so unproven at a full crowd | M7 if the phone shows it |
| I3e.2 zero bytes a frame | 1.3 KB a frame left, listed above with causes; none grows with the crowd | M7 |
| I3e.3 per-instance material copies (projectile and hostile tints, burn flash, feedback, pickups, POI markers) | SetPass stays ~120-180; the copies are made once per pooled instance, not per frame; replacing them changes how hits and burns look | M7, with the owner's look check |
| I3e.4 per-layer cull distances | The 250 m far clip already cuts what the 60 m view cannot see; per-layer distances would cut nearer objects, which changes the look | M7 |
| I3e.5 quick wins: the second directional light, the mobile opaque texture, shadows on kegs, cutlasses, the muzzle and the damage sphere, the damage-highlight shader, bloom quality | Each changes the look (lighting, the water's refraction, shadows, the hit flash) and needs the owner's eye; pickups' shadows were the one taken (they were ~200 barrels) | Owner's playtest of M3e, then M7 (questions 15-16) |
| I3e.5 the explosion prefabs' point lights | They have intensity 0, so nothing on screen changes; whether URP still counts them as lights is a device question, and they live in shared effect prefabs | M7 (measured on the phone) |
| The health-bar canvases (76, one batch each) and the pickup sparkles (three particle systems per loot barrel) | Now the biggest batch counts; both change the look | Questions 15-16 |

## Tests

- EditMode `V5PerformanceTests`: the allocation check uses Unity's GC constraint and a test proves it catches an
  allocation; config lookups return what LINQ returned and allocate nothing; the spatial hash reuses its cells and
  queries the same neighbours in the same order; the ship wake switches on movement, allocates nothing while it holds,
  and a reused view drives the trail it has now; the loadout HUD's signature changes with a level, an evolved name, a
  stack, an item name, a new weapon and the slot cap; VFX-profile effects and trails are prewarmed at bind; every listed
  model has Mesh LODs and no game model over the limit is missing. `V4WorldViewsM14Tests` (pickups cast no shadows).
- PlayMode `V5PerformancePlayModeTests`: in a real run the top-down far clip is 250 m. `V5PressurePlayModeTests` (M3d)
  now spawns the same band-0 spawner on purpose at 1:00 and 8:00 (the natural-spawn version was flaky); it goes through
  the encounter path, so it also asserts the director's HP levers are off (else ambient enemies could still grow).
- The after runs (`after-a/b/c`) predate the last change (the flying-health billboard's shadow switched off after its setup): one small billboard, no effect on the numbers.
- Evidence: `editmode.json` (408/408), `playmode.json` (60/60; the last change after it added one EditMode test), `perf.md` (every run, including the probes made while finding the
  causes), `gc-profile-*.txt`, `console-*.txt`, `playable/` (before and after screenshots at 1:00, 5:00, 9:00 on the seed).
