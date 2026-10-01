# V5 M7: mobile UI and input (phase plan and results)

Plan: `V5_PLAN.md` "#### M7". Start commit: `081e47c6`. Built after the owner's "continue M7 and M8" (1 Oct).

| Item | What | Where |
|---|---|---|
| I7.1 Input | Touch BOOST button (beside the Captain's ability button, on the side away from the stick) and a pause button (top, every device); Escape and the gamepad Start pause too; a touch SKIP on the chest choice; the stick's side (left or right half) and dead zone from Settings; the ability and boost buttons move to the left when the stick is on the right. Keyboard, mouse and gamepad unchanged | `TouchActionButton`, `TouchControlsPresenter`, `PauseMenuPresenter`, `ShipBoostController`, `MoveInput`, `VirtualJoystick.ApplySide`, `InteractionPromptPresenter` |
| Pause | The player's pause sits on top of the run's own: it pauses only a running run and never overrides a level-up or chest choice (`HudCommands.TogglePause`); Resume, Settings, Quit to menu | `PauseMenuPresenter`, `HudCommands`, `V4Game` |
| I7.2 Safe area and scale | `SafeAreaFitter.Wrap` puts the run canvas's and the menu canvas's content under one rect that follows `Screen.safeArea` (the authored HUD already fits its own); a UI scale setting (1.0-1.4) on every screen-size scaler | `SafeAreaFitter`, `UiScale`, `V4PresentationRoot`, `V4MainMenuController` |
| I7.3 Targets | Level-up footer buttons, results buttons, the USE and SKIP buttons, settings and pause controls at least 72 units tall; at the minimum UI scale a 72-unit target is at least 48 px on a 720p screen (tested at 1280×720, 1600×720, 1920×1080, 2400×1080, 2048×1536) | `LevelUpUiFactory`, `ResultsPresenter`, `InteractionPromptPresenter`, `SettingsPanel` |
| I7.4 Settings | UI scale, music, effects volume, quality (Auto, Low, Mid, High), reduced effects (half the effect density), screen shake, stick side, stick dead zone; saved as one PlayerPrefs value (`v5_settings`), from the menu's Settings button and the pause menu | `GameSettings`, `SettingsPanel`, `SfxService`, `MusicPlayer`, `QualityProfile`, `AppRoot` |
| I7.5 WebGL profile | Automatic quality: with Auto, the frame time 20-30 s into a run (under load) picks the tier, stepping down only (above 30 ms Low, above 20 ms Mid, else High) and, on a phone, the render scale (0.6, 0.7, 0.75), kept for the next runs. Build profiles: mobile (256 MB heap ceiling, ASTC, Brotli) and desktop (1 GB, DXT), the project's settings restored after; a texture pass capping every texture at 1024 on WebGL; `plan/v5/tools/serve-webgl.py` serves a build to phones on the Wi-Fi (HTTPS with an mkcert certificate) | `AutoQuality`, `Editor/V5WebGLProfiles.cs` |
| T7 | `V5MobileUiTests` (safe-area anchors and wrapping, 48 px rule at five sizes, UI scale without compounding, settings save and limits and damaged values, reduced effects, touch presses read once, the pause never over a level-up, the automatic tier, generated control sizes) | `Tests/V4/EditMode/` |

Needs the owner: frame time (95% under 34 ms over 15 minutes), memory and the enemy cap on the reference phone and the
floor phone; the phone playtest of the WebGL build on the local network.

## Independent review (v5-reviewer, 1 Oct)

No blockers; 7 majors, 8 minors. One pass (targeted re-tests on the fixes: `V5MobileUiTests` 11/11, `V4HudPlayModeTests` 8/8).

| # | Sev | Finding | Fix |
|---|---|---|---|
| 1 | Major | The safe-area wrap ran before the level-up panel and the interaction prompt existed (they stayed outside; the prompt lost its order above the toast) | Wrap and UI scale run once at the end of `V4PresentationRoot.Install`, after every panel |
| 2 | Major | Building the level-up panel reset the run canvas's scaler, so the UI scale missed the run screens | `UiCanvasConfigurator.Configure` re-applies the player's UI scale |
| 3 | Major | Settings rows squeezed to ~64 units (10 rows in a 756-unit panel); taller than a phone at scale 1.4 | The panel follows the screen height; the rows scroll (`ScrollRect`), each with a 72-unit minimum; a real-layout test on a 600-unit-tall screen |
| 4 | Major | Quality tiers changed the player projectile cap, so a phone fought weaker | Gameplay caps are Mid's on every tier; a tier sets only effect density and the phone render scale. Your question (DECISIONS) |
| 5 | Major | Brotli over plain HTTP does not load on a LAN phone | The builds turn on Unity's decompression fallback (restored afterwards); HTTPS with mkcert still works. Your question |
| 6 | Major | Auto quality measured 2-10 s in (the quietest moment), flipped between runs, did not save on WebGL | Measures 20-30 s in, steps down only, `PlayerPrefs.Save()`; manual tiers apply the render scale; a warning if the pipeline has no render scale |
| 7 | Major | Plan items neither built nor recorded | Recorded below |
| 8 | Minor | BOOST placement allocated every frame | A static corner buffer |
| 9 | Minor | A boost tap in the frame a pause opens fired after the card pick | Taps while paused are dropped |
| 10 | Minor | BOOST can overlap the loadout when there is no ability pad | Recorded (playtest) |
| 11 | Minor | The pause menu had no gamepad selection; stayed open if the pause ended elsewhere | Resume selected on open; closes when the player's pause ends |
| 12 | Minor | Missing tests (real layout, PlayMode touch) | Real-layout settings test added; PlayMode touch recorded below |
| 13 | Minor | The texture pass would compress deliberately uncompressed textures | Skipped |
| 14, 15 | Minor | Desktop texture cap, HTTPS choice | Your questions |

## Not built in M7 (recorded)

- The input-router refactor (one place that maps keyboard, gamepad and touch to actions) and its tests: the existing readers work and are kept; a refactor without a behaviour change was not worth the risk now.
- PlayMode touch simulation and a scripted touch run with screenshots at three aspect ratios: the touch paths are tested in EditMode (presses read once, pause rules, control sizes, safe area).
- Long-press in place of hover tooltips, text minimums, a 4:3 reflow: after the phone playtest shows where they hurt.
- The safe area on the authored (scene) HUD canvas: it already keeps its own insets; the generated canvases are wrapped.
- The texture pass and the WebGL mobile and desktop builds: built once after M8, so one phone build carries M7 and M8 for your playtest (decision logged).
- Frame time, memory and the enemy cap on the reference and floor phones: yours, with that build.
