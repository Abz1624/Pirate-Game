# V5 M7c: WebGL performance (phone and desktop browsers)

Asked by the owner on 1-2 Oct: "check the performance and optimizations by running different builds on my phone ... create
a report on different things you tried", then "once the webgl optimizations are done for both mobile and desktop browsers,
proceed with android apk optimizations". Start commit: `8d993014`. Decisions: D37, D38; questions 39-43.

Evidence: `plan/v5/perf/` (phone and desktop results `phone-bench/*.jsonl` and `run-*.log`, Editor profiles
`editor-profile/`). Tools: `plan/v5/tools/phone-bench.mjs` (drives Chrome on the phone over USB, or a desktop Chrome),
`bench-report.py`, `editor-profile.py`; the benchmark harness `V4/DevTools/Bench/PhoneBench.cs` (only in builds made with
the `V5_PHONE_BENCH` define).

## How it was measured

A bench build opened with `?bench=...` skips the menu, sails a seeded Voyage with the human-like pilot (invulnerable,
six weapons), and logs frame times in two windows: the natural first minute, and a "peak" (the run clock at 3:35, or a fixed
crowd of N enemies held around the ship). Settings come from the URL (quality tier, render scale, shadows, post, MSAA,
HDR, frame cap, crowd size, and two switches: enemy bars off, minimap frozen). On the phone the driver also reads the
Chrome renderer's and GPU process's memory (adb) and the battery temperature. Heat matters: the same build lost 29% of its
frame rate over 28 minutes (38 to 43 °C), so later runs wait for the phone to cool and alternate the builds compared.

Phone: Pixel 9a (Tensor G4, Mali-G715), Chrome 153, landscape 2272×807. Desktop: this PC's Chrome 154 on an RTX 3050
laptop GPU, 1200×750 canvas, background throttling off, own profile.

## What was found

| Finding | Evidence | Change |
|---|---|---|
| The phone is CPU-bound: render scale 0.5 to 1.0, shadows, post, MSAA/HDR change little | run 1, 2a | none (kept) |
| Enemies dominate: at 80 alive the phone falls to 21-28 fps (36-48% of frames over 33 ms), 160: 20 fps, 260: 13 fps | run 2a, 3 | the cap question (14) now has data |
| 80 enemy health bars = 80 world canvases and 80 LateUpdates: about 60% of the crowd's cost | Editor profile; bars off on the phone: 48% to 9.5% frames over 33 ms | shared canvas, one script (WorldHealthBarBatch) |
| The minimap rebuilt its whole fog texture each new explored cell (~14 ms desktop): the regular 100+ ms hitches | Editor hierarchy profile; minimap frozen on the phone: slowest 1% 138 to 45 ms (80 enemies), 93 to 28 ms (natural) | rebuild over 8 frames, one upload |
| Phones got DXT textures (the Editor's texture setting is reset by the build in Unity 6) and expand them: GPU process ~1.3 GB, heap 397 MB | run 2b: ASTC build 590 MB GPU process, 330 MB heap, 87 MB download | ASTC through `BuildPlayerOptions.subtarget` (mobile), DXT (desktop) |
| Code optimized for build time | run 1, 2a: no clear frame-time change | runtime-speed code (no measured cost); WebAssembly 2023 left off (question 41) |
| The 256 MB heap is still out of reach (330 MB with ASTC) | info lines | question 39/43 (asset streaming) |
| Tried and reverted: compressing "uncompressed UI art" (it was already compressed; the build report lists uncompressed sizes) | review | reverted |

## Code

- `Presentation/Hud/MinimapPresenter.cs`: fog rebuild spread over `FogRowsPerFrame` rows a frame, uploaded once; immediate
  on a new texture, run or stage; a change mid-rebuild runs one more pass.
- `Foundation/VFX/WorldHealthBar.cs`, `WorldHealthBarBatch.cs`: bars turn off their own canvas and move under one shared
  world canvas beside the pool root; one LateUpdate places and billboards them, sorts them far to near every 8 frames;
  a bar whose ship is gone is destroyed (as before).
- `Editor/V5WebGLProfiles.cs`, `V4BuildSettings.cs`: per-build texture subtarget, runtime-speed code, a bench option,
  settings restored and saved after a build.
- `DevTools/Bench/PhoneBench.cs` + `.jslib`, the DevTools define `V5_PHONE_BENCH`.

## Tests

`V5PerfPlayModeTests` (2): the fog spread over frames, uploaded once, equal to a one-frame rebuild, and a restart;
bars under one shared canvas with their own canvas off, over their ships facing the camera, sorted far to near, a pool
release and re-acquire placed once, a bar destroyed with its ship. Re-run green: V4HudPlayModeTests 8/8,
V4HudM6Tests 15/15, V4WorldViewsM14Tests 12/12, V4EnemyTickM8Tests 9/9.

## Independent review (v5-reviewer, 2 Oct)

1 blocker, 5 majors, 6 minors; one pass, all fixed or recorded.

| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | Blocker | The shipped code was never measured (only switches on older builds); no phase page | Final builds measured against build F on the phone and desktop (below); this page |
| 2 | Major | The bench's render scale was saved into the PC pipeline asset | Reverted; the bench sets the render scale only with rs= and restores pipeline values in the Editor |
| 3 | Major | The bench wrote the player's prefs and settings | In-memory save before the menu runs, a settings copy, pinned prefs restored once the run has read them |
| 4 | Major | The "UI art" compression pass touched other textures; the download drop was ASTC | Pass reverted; D38 and question 40 corrected |
| 5 | Major | Bar draw order changed; pooling untested | Sorted far to near every 8 frames; pool round-trip test; screenshots in the playable check |
| 6 | Major | WebAssembly 2023 cuts off Safari < 16.4 with no measured gain | Off by default; question 41 |
| 7 | Minor | Batch lookup scanned the pool root each bind | The bar keeps its batch |
| 8 | Minor | bars=0 broke with the shared canvas | Hides the bar images |
| 9 | Minor | A new stage showed the old fog for 8 frames; no equality or restart test | Immediate fill on a new run or stage; both tests added |
| 10 | Minor | The bench forced 0.7 render scale on desktop | Only with rs= |
| 11 | Minor | Heat and short cool-downs in run 2b | Cool-down to a set temperature, alternating builds |
| 12 | Minor | Font atlases cleared by the build | Not committed |
