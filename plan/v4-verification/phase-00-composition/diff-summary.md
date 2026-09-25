# M0 diff summary

Phase start: `8a8fbd1c`. Nothing committed yet: the M0 commit follows the playtest gate.

```
 M "Pirate Game Unity/ProjectSettings/EditorBuildSettings.asset"
 M docs/V4_IMPLEMENTATION_PLAN.md
?? "Pirate Game Unity/Assets/_Game/Scenes/V4/V4Boot.unity"
?? "Pirate Game Unity/Assets/_Game/Scenes/V4/V4MainMenu.unity"
?? "Pirate Game Unity/Assets/_Game/Scenes/V4/V4ModuleLab.unity"
?? "Pirate Game Unity/Assets/_Game/Scenes/V4/V4SaltwindRun.unity"
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/RoguePirates.V4.Tests.EditMode.asmdef"
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4CoreTests.cs"
?? "Pirate Game Unity/Assets/_Game/Tests/V4/EditMode/V4StructureTests.cs"
?? "Pirate Game Unity/Assets/_Game/Tests/V4/PlayMode/RoguePirates.V4.Tests.PlayMode.asmdef"
?? "Pirate Game Unity/Assets/_Game/Tests/V4/PlayMode/V4RunPlayModeTests.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Config/RoguePirates.V4.Config.asmdef"
?? "Pirate Game Unity/Assets/_Game/V4/Config/V4ConfigInfo.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/RunSection.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Console/TuningConsole.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/RoguePirates.V4.DevTools.asmdef"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/JsonLine.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/RunTraceRecorder.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/ScriptedPilot.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceBoot.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceComparer.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/Trace/TraceRequest.cs"
?? "Pirate Game Unity/Assets/_Game/V4/DevTools/V4DevToolsInstaller.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Editor/RoguePirates.V4.Editor.asmdef"
?? "Pirate Game Unity/Assets/_Game/V4/Editor/V4SceneSetup.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Editor/V4TraceSession.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Presentation/RoguePirates.V4.Presentation.asmdef"
?? "Pirate Game Unity/Assets/_Game/V4/Presentation/V4PresentationInfo.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/IRunModule.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/RunClock.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/RunContext.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/RunRequest.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/V4Game.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Core/V4Phase.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Events/CombatEvents.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyAdapters.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyEventFeed.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/LegacyRunShell.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Legacy/V4LegacyBridge.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/RoguePirates.V4.asmdef"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Scenes/V4BootLoader.cs"
?? "Pirate Game Unity/Assets/_Game/V4/Runtime/Scenes/V4MainMenuController.cs"
?? plan/v4-verification/V3_DEFECT_REGISTER.md
?? plan/v4-verification/phase-00-composition/CHECKLIST.md
?? plan/v4-verification/phase-00-composition/baseline-editmode-results.json
?? plan/v4-verification/phase-00-composition/baseline-failures.txt
?? plan/v4-verification/phase-00-composition/baseline-playmode-results.json
?? plan/v4-verification/phase-00-composition/config-hash.txt
?? plan/v4-verification/phase-00-composition/diff-summary.md
?? plan/v4-verification/phase-00-composition/editmode-results.json
?? plan/v4-verification/phase-00-composition/playable-check/01-menu.png
?? plan/v4-verification/phase-00-composition/playable-check/02-run-start.png
?? plan/v4-verification/phase-00-composition/playable-check/03-console.png
?? plan/v4-verification/phase-00-composition/playable-check/04-boss.png
?? plan/v4-verification/phase-00-composition/playable-check/05-territory-claimed.png
?? plan/v4-verification/phase-00-composition/playable-check/06-results.png
?? plan/v4-verification/phase-00-composition/playable-check/07-after-retry.png
?? plan/v4-verification/phase-00-composition/playable-check/08-seed-101.png
?? plan/v4-verification/phase-00-composition/playable-check/console-errors.txt
?? plan/v4-verification/phase-00-composition/playable-check/steps.log
?? plan/v4-verification/phase-00-composition/playmode-results.json
?? plan/v4-verification/phase-00-composition/trace-comparison.txt
?? plan/v4-verification/phase-00-composition/v3-regression-editmode-results.json
?? plan/v4-verification/phase-00-composition/v3-regression-playmode-results.json
?? plan/v4-verification/phase-00-composition/v3-trace-seed-101-a.jsonl
?? plan/v4-verification/phase-00-composition/v3-trace-seed-101-b.jsonl
?? plan/v4-verification/phase-00-composition/v3-trace-seed-202-a.jsonl
?? plan/v4-verification/phase-00-composition/v3-trace-seed-303-a.jsonl
?? plan/v4-verification/phase-00-composition/v4-trace-seed-101-a.jsonl
?? plan/v4-verification/phase-00-composition/v4-trace-seed-101-b.jsonl
?? plan/v4-verification/phase-00-composition/v4-trace-seed-202-a.jsonl
?? plan/v4-verification/phase-00-composition/v4-trace-seed-303-a.jsonl
?? plan/v4-verification/tools/check-write-set.ps1
?? plan/v4-verification/tools/playable-check-m0.ps1
?? plan/v4-verification/tools/prefs-guard.ps1
?? plan/v4-verification/tools/run-trace.ps1
```

Counts (including .meta): 132 paths, of which modified: 2.

## Write-set check (`tools/check-write-set.ps1 -Since 8a8fbd1c`)

All V4 changes are inside the allowed write set:
- `Assets/_Game/V4/**`
- `Scenes/V4/**`
- `Tests/V4/**`
- `ProjectSettings/EditorBuildSettings.asset` (the four V4 scenes appended)
- `docs/V4_IMPLEMENTATION_PLAN.md`
- `plan/v4-verification/**`

The script also lists 12 files under `Assets/UI/Weapons/Weapon UI /`, a folder whose name ends in a space (Sep 17, before this work; see commit `8e6ba866`). Git for Windows cannot open that directory, so it reports the tracked files as changed. This is a pre-existing checkout artifact, and nothing in it was touched.

V3 files touched by test side effects and restored with `git checkout`: `plan/saltwind-verification/balance-lab/*`, `Assets/TextMesh Pro/.../LiberationSans SDF - Fallback.asset`, `Data/EncounterSpawnSchedule.asset` (line endings only).
