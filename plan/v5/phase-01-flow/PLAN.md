# V5 M1 — Enemy flow: progress

Started 30 Sep 2026 at `0b523f0d` (V5 M0), built unattended at the owner's request ("as far as I can", commit and push
each phase that passes its tests and review; playtests when the owner is back).

| Step | Content | Status |
|---|---|---|
| I1.1 | Minimum-alive floor (`MinuteWave.minAliveShare` × ambient cap): one enemy per global spawn gap (0.35 s) while below the floor, at most `maxTopUpPerTick` per `topUpIntervalSeconds`, from the current band's spawner and pools, respecting the ambient cap, tag caps and placement; scaled by the adaptive threat-rate lever and the experiment's density (weak builds get relief) | Done: `Runtime/Spawning/FlowDirector.cs`; `AmbientSpawner.TryGetCurrent` exposes the current band. The floor counts every enemy alive (boss and encounters included), a deliberate simplification |
| I1.2 | Minute waves: ten 1-minute waves over the five V4 bands, each multiplying the band rate and setting its floor (jagged: 6 → 14 → 23 → 16 → 27 → 22 → 32 → 40 → 29 → 45 at the ambient cap of 72) | Done: `WaveTimelineConfig.flow.waves`. The bands and their pools stay (tiers still enter by band and tier timers; per-minute pools are left for M2's retune if the model asks for them) |
| I1.3 | Hordes: `Ring`, `Line`, `Cluster` formations formed just beyond the camera's view (island and bounds checks on), warned 3 s ahead with a toast, an edge marker and a minimap ping, a 10 s lull before each (release rate and floor) to make room, allowed half the encounter-reserved slots above the ambient cap, never during the boss fight, held during an arena or an open release window, dropped when more than 30 s late; counted only if at least one enemy spawned | Done: 4 hordes at 1:45, 5:30, 7:40, 9:15 (swordfish and dinghy, the two weakest of the first band's pool; cutthroat skiff, the toughest of the second band's; no content ids in code). Edge marker: `SpawnWarningPresenter.NotifyHorde` on Warning events flagged `HordeWarning` |
| I1.4 | Release windows: after an arena ends (20 s), after the Barnacle King is sunk (25 s), 20 s after each horde (15 s): ambient rate × 0.35 and floor × 0.4; during the boss fight the floor × 0.3 | Done: `ReleaseSettings` |
| I1.5 | Overtime: the endless ramp as today (Q22) | Unchanged |
| I1.6 | Enemy cap and pickups: XP merges into the nearest XP pickup when every slot is full (no XP lost; the view is re-drawn with the merged value); gold, health and barrels keep the V4 rule; the cap stays 80 (see measurement) | Done: `PickupConfig.mergeIntoNearestWhenFull` |
| G1.1 | First-pass data written by the Editor generator `V4/Editor/V5FlowDefaults.cs` (menu RoguePirates/V5); balance version `saltwind-v5-m1` | Done |
| — | v3-parity preset: `flow.enabled = false`, `mergeIntoNearestWhenFull = false` | Done |
| — | Tuning Console: a flow line in the Wave Timeline section (wave, floor, release, top-ups, hordes) | Done |
| T1.x | EditMode `V5FlowM1Tests` (15): data shape, parity switch, validation, floor top-up and cap and per-tick limit, wave rate, boss-fight floor, horde warning and formation and boss skip, formation geometry, release after the boss / an arena / a horde, pickup merge. PlayMode `V5FlowPlayModeTests` (1): real scene, clock jumps to 1, 3, 6, 9 and 13 minutes | Done, all green |

## Results (after the review fixes)

| Check | Result | Evidence |
|---|---|---|
| EditMode, all assemblies | **331 / 331** passed (51.6 s), incl. 24 `V5FlowM1Tests` | `evidence/editmode.json` |
| PlayMode, all assemblies | **50 / 50** passed (167.9 s), incl. `V5FlowPlayModeTests` | `evidence/playmode.json` |
| PlayMode, M1 flow checkpoints | the crowd reached each wave's floor at 1, 3, 6 and 9 min (at 9:05 the lull before the 9:15 horde halves the floor, as designed); overtime keeps spawning | `evidence/playmode-flow-checkpoints.txt` |
| Playable check (real Play session, clock jumps, screenshots) | 0 game errors (one Pipeline "main thread timed out" from the tool itself is filtered); alive 15 / 25 / 51 / 72 / 71 at 1 / 3 / 6 / 9 / 13 min; the 5:30 horde spawned and is on screen at 5:39; Editor frame 5–10 ms | `evidence/playable/` (checkpoints.txt, minute-*.png, horde-05m30.png, console-errors.txt) |

Older tests changed: `V4SpawnM3Tests` switches the flow off in its harness, because those tests pin the V4 band
semantics alone; with the flow on, three of them counted the floor's extra spawns (28–30 became 33; the arena catch-up
89 became 93; the gap test saw two top-ups in one frame). The gap invariant is now kept by the flow itself and tested
with the flow on (`Floor_TopUps_KeepTheGlobalSpawnGap`); the extra spawns are the floor working as designed.

## Measurement: the reference phone (stand-in)

The owner has no Galaxy A15/A16 yet and chose a stand-in when asked before going away (30 Sep: "Do you have a Samsung
Galaxy A15 / A16 4G?" → "No, use a stand-in"). The phone checks in the M1 exit criteria (95% of frames under 34 ms,
the floor phone at 30 fps on Low, memory against 256 MB) therefore move to M7, where the mobile profile is built and a
real phone is needed anyway. What could be measured in this session:

- **Frame time** (desktop Editor, 72 enemies at 9:00): 8–10 ms. CPU throttling of the browser pane is not available, so
  the phone's frame time could not be simulated; the cap stays **80** (the top of the research's 60–80 estimate).
- **Memory** (the existing WebGL build `Builds/V4WebGLDemo`, 28 Sep, same assets; served locally and read in the
  browser pane at run start): **WebAssembly heap 344 MB** plus about **277 MB** of JavaScript heap; the data file is
  **160 MB** compressed. The reference phone's 32-bit Chrome caps the heap near **256 MB**, so today's build would most
  likely fail to load there. This is M7's job (the mobile WebGL profile: texture compression, a 256 MB limit, a
  20–30 MB first download), but it is larger than the plan assumed; flagged to the owner.

## Independent review

The reviewer (`v5-reviewer`, read-only) found **3 blockers, 4 majors and 9 minors**; all are fixed or recorded.

| # | Severity | Finding | What was done |
|---|---|---|---|
| 1 | Blocker | The PlayMode evidence predated the V5 PlayMode test | Both suites re-run after the last change; the V5 test is in `evidence/playmode.json`; the checkpoint line saved as a file |
| 2 | Blocker | Top-ups ignored the 0.35 s global spawn gap, and the M3 test that caught it was switched off | One top-up per gap, at most maxTopUpPerTick per interval; a flow-on gap test added; the M3 harness change explained above |
| 3 | Blocker | No horde edge markers | Horde warnings carry the horde's direction; the spawn-warning presenter shows the edge chevron and a minimap ping |
| 4 | Major | Hordes could not spawn at the cap, yet counted and opened releases | A 10 s lull before each horde; hordes may use half the encounter-reserved slots; counted and released only when something spawned; tests at the cap and when nothing fits |
| 5 | Major | Horde members used the band's on-screen rule and skipped bounds checks without a band | Hordes form just beyond the view by distance, and always check islands and bounds |
| 6 | Major | The phone measurement deferral was not recorded in the plan | Recorded here, in `V5_PLAN.md` (M1 and M7) and in the owner's TODOs with the memory finding |
| 7 | Major | "Merge into nearest" applied to every pickup type, and the merged view kept its old value | XP only; the merged pickup's view is re-drawn with its value (tested) |
| 8 | Minor | A dead "arena" branch; hold, late-skip and overlapping releases untested | Branch removed (hordes wait for arenas because the flow is not ticked); tests added |
| 9 | Minor | The floor ignored the adaptive lever and counted every enemy | The floor follows the threat-rate lever and experiment density; counting all enemies is kept as a simplification |
| 10 | Minor | Balance version unchanged | `saltwind-v5-m1` on both changed assets |
| 11 | Minor | Stale status in overtime and arenas | The status reads "not running" (wave -1, floor 0) until the flow ticks that frame; tested |
| 12 | Minor | Generator text said "weakest" for the toughest pick | Text fixed |
| 13 | Minor | The cap test never exercised the cap path | Rewritten: floor at the cap, crowd two below; asserts the cap every frame |
| 14 | Minor | Warnings just before the boss; hordes right after an arena | Hordes wait for an open release window (tested); a warning shown before a boss fight is left as is |
| 15 | Minor | A Pipeline timeout in the console log | Noted in the results (the tool's own error) |
| 16 | Minor | A magenta wedge in a screenshot | A fire boat's flame effect with a missing material, the known placeholder issue from V4 (R-80/R-89 family), not from M1 |

Owner questions raised by the review, answered by default choices recorded here: top-ups obey the gap (yes); hordes may
exceed the ambient cap by half the reserved slots (yes); the floor follows the adaptive lever (yes); the stand-in and the
M7 deferral (the owner's stand-in answer).
