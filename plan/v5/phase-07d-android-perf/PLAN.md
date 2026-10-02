# V5 M7d: Android APK performance

Asked by the owner on 2 Oct: "once the webgl optimizations are done for both mobile and desktop browsers, proceed with
android apk optimizations as well". Start commit: `f96467a3` (after M7c). Decision D39; question 44.

Evidence: `plan/v5/perf/phone-bench/android.jsonl`, `run-android*.log`, APK reports in `Pirate Game Unity/Builds/Android/`
(git-ignored). Tool: `plan/v5/tools/phone-bench.mjs` with `ANDROID=1` (installs the APK, starts it with the benchmark query
as the launch intent's `unity` extra, reads the results from logcat, memory from `dumpsys meminfo`).

## Builds

`Editor/V5AndroidProfiles.cs`: IL2CPP, 64-bit ARM, ASTC (through the build option), release logs without stack traces,
debug signing. Variants: graphics (project default Vulkan then GLES3, or GLES3 only) and Optimized Frame Pacing. Benchmark
builds carry the `V5_PHONE_BENCH` define and their own package name (`com.x2c.roguepirates.bench`), so they never touch a
playtest install. The project's settings, the Unity Connect file and the active build target are restored after a build.
`RoguePirates-Playtest.apk`: the release APK for your playtest (no benchmark code, the game's own package).

## Results (Pixel 9a, Android 17, Mali-G715, native 2424×1080, warm at 40-41 °C)

| APK | Size | Avg fps, 80 enemies | Slowest 1% | Frames > 33 ms | App memory (PSS), 80 enemies | App memory, natural game |
|---|---|---|---|---|---|---|
| Vulkan first (the project's default) | 155.5 MB | 59.9 | 16.8 ms | 0.0% | 877 MB | 884 MB |
| GLES3 only | 151.8 MB | 59.9 | 16.8 ms | 0.0% | 872 MB | 902 MB |
| Vulkan + Optimized Frame Pacing | 155.6 MB | 59.8 | 16.8 ms | 0.1% | 888 MB | 991 MB |
| Capped textures (world 1024, UI 2048) | **127.9 MB** | 59.9 | 16.8 ms | 0.0% | **803-843 MB** | **797-810 MB** |
| Playtest APK (capped, no benchmark code) | **126.9 MB** | - | - | - | - | - |

- The native app is locked at the screen's 60 Hz in every variant, even with 80 enemies (the web build on the same phone:
  37.5 fps). On this phone the graphics API and frame pacing make no measurable difference. A 120 fps cap did not lift the
  display above 60 Hz (Android needs the app to ask for the refresh rate; the benchmark now does, and records CPU and GPU
  time per frame for the next phone). Kept: the project default (Vulkan, GLES3 fallback, frame pacing off). The choice
  belongs to the low-end Galaxy A15.
- The texture cap is the measured win: the APK is 18% smaller (155.5 to 127.9 MB) and the app uses about 60 MB less. Only
  44 of the 400 textures the build uses were above their cap (the project holds 1,185 textures; most are in no build).
- Memory spread between single runs is about 30-100 MB (the natural game's load varies), so only the crowd runs compare
  memory. Runs were single per variant except the crowd runs (two).
- One capped run timed out: the driver missed the peak line (it tracked lines by position; it now tracks them by content).

## Tests

`V5BenchToolsTests` (2): the Android cap rules and the benchmark's arguments. Full suites once on the final code:
EditMode 466/466, PlayMode 63/63 (`editmode.json`, `playmode.json`).

## For you

`Pirate Game Unity/Builds/Android/RoguePirates-Playtest.apk` (git-ignored) is installed on your phone as "Rogue Pirates"
(the benchmark installs were removed). To install it elsewhere: `adb install -r` the file, or copy it to the phone and open
it (allow installs from that app). For Google Play: an AAB, an upload keystore and a target API level are not set up
(question 45).


## Independent review (v5-reviewer, 2 Oct)

No blockers; 4 majors, 9 minors; one pass.

| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | Major | At 60 fps every variant is locked to the screen: the data cannot pick a graphics API or frame pacing | A 120 fps run (the Pixel 9a's screen) to see the headroom; the default kept unless it shows a clear winner |
| 2 | Major | No phase page, no test results, capped results missing | This page; tests below; capped APK measured |
| 3 | Major | No tests for the cap rules or the Android argument parse | `V5BenchToolsTests` (2): the cap rules (`CapFor`) and the URL and command-line parse |
| 4 | Major | The Editor was left on Android; Unity turned services on in every Android build | The profile restores the active target and the Unity Connect file; the Editor is back on WebGL |
| 5 | Minor | UI images not typed as sprites got the world cap; unused textures got overrides | The cap now covers only textures the build uses (scene and Resources dependencies) |
| 6 | Minor | A text atlas (SDF) was capped and compressed | Skipped |
| 7 | Minor | A chosen Android format could be overwritten | Kept when already overridden |
| 8 | Minor | The driver cleared the phone's whole log; buffer wrap; other apps' crashes counted | Reads from the run's start time (`logcat -T`); a crash counts only with our process id or package |
| 9 | Minor | Pinned preferences restored only after the warm-up | Restored on the run's first frame; benchmark APKs have their own package |
| 10 | Minor | No playtest APK, no command-line entry, double log | Added (`BuildPlaytestFromCommandLine`, a menu item); log once |
| 11 | Minor | D39 rendered outside the table | Fixed |
| 12 | Minor | URP global settings and font atlases changed by builds | Not committed |
| 13 | Minor | The web profile changed after M7c was measured | Noted on the M7c page |
