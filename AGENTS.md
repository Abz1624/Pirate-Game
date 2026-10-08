# Agent guide — Rogue Pirates / Pirate Game Unity

This repo is wired for **Unity CLI + Cursor** so agents can drive a live Editor, run tests, and verify changes without hand-editing scene files.

## Unity project

| Item | Value |
|---|---|
| Project path | `Pirate Game Unity/` |
| Editor version | `6000.3.11f1` (Unity 6.3 LTS) |
| Pipeline package | `com.unity.pipeline` `0.7.0-exp.1` |

Open the project:

```powershell
unity open "Pirate Game Unity"
```

## Cursor integration (already configured)

Project-local files (commit these):

- `.cursor/mcp.json` — Unity MCP server (`unity mcp`)
- `.cursor/rules/unity-cli.mdc` — Unity CLI skill for agents

**After changing MCP config:** restart Cursor (or reload MCP servers in settings).

**After installing/updating `com.unity.pipeline`:** restart the Unity Editor so the Pipeline server starts. Verify:

```powershell
unity pipeline list
unity status --json
```

`Server Reachable` must be `true` and `state` should be `ready` before `unity command` / MCP tools work.

### Global MCP note

Your user-level `~/.cursor/mcp.json` still points at the old **Coplay** `unityMCP` HTTP server (`http://127.0.0.1:8080/mcp`). That entry is broken and can be removed or disabled in Cursor → Settings → MCP. This repo uses Unity's official CLI MCP instead.

The Coplay package (`com.coplaydev.unity-mcp`) was removed from `Packages/manifest.json` in V4 M15.

## Code layout (since V4 M15)

The old V3 game is gone. The game lives under `Pirate Game Unity/Assets/_Game/V4/` (the engine-independent simulation, content, account and platform assemblies stay in `Assets/_Game/Simulation`, `Content`, `AccountClient` and `PlatformAdapters`):

| Folder | Assembly | Contents |
|---|---|---|
| `Foundation/` | `RoguePirates.V4.Foundation` | Shared base the run is built on (run manager, ship, stage builder and islands, POI placement, save and unlock state, definitions). References no other V4 assembly. |
| `Config/` | `RoguePirates.V4.Config` | ScriptableObject config schemas, hashing, presets. No behaviour. |
| `Runtime/` | `RoguePirates.V4` | Modules, contracts, the frame pipeline; `Runtime/Ports/` is the one boundary to the Foundation MonoBehaviours. |
| `Presentation/` | `RoguePirates.V4.Presentation` | Presenters on the view pool; read views and events only. |
| `DevTools/` | `RoguePirates.V4.DevTools` | Tuning Console, trace harness, autopilot (Editor and development builds only). |
| `../V5/Net/` | `RoguePirates.V5.Net` | V5 M9b match server and client (Netcode for GameObjects 2.13.3, Unity Transport 2.7.4, WebSockets); scene `V5/Net/Scenes/V5NetSpike`. Compiled only in the Editor or with the `V5_NET` define, so release builds leave it out. |
| `Editor/` | `RoguePirates.V4.Editor` | Build settings, trace build, island authoring tools. |

Scenes: `Assets/_Game/Scenes/V4/` (`V4Boot` first in the build). Tests: `Assets/_Game/Tests/V4/` (EditMode, PlayMode) and `Assets/_Game/Tests/RoguePirates/` (simulation).

## Agent workflow (preferred order)

1. **Check connection** — `unity status --json`
2. **Inspect live Editor** — `unity command` (list tools), `unity command eval "return Application.version;"`
3. **Make code changes** in `Pirate Game Unity/Assets/`
4. **Let Unity recompile** (or use CLI to drive the Editor)
5. **Run tests** — `unity test "Pirate Game Unity" --mode EditMode` (and `--mode PlayMode`)
6. **Report** what changed, commands run, and remaining failures

When a Unity Editor is connected, **prefer `unity command` over editing `.unity` / `.prefab` YAML by hand**.

## Useful CLI commands

```powershell
# Environment
unity doctor
unity editors --format json

# Project
unity open "Pirate Game Unity"
unity pipeline list
unity status --json

# Live Editor (requires open Editor + Pipeline server)
unity command
unity command editor_play
unity command eval "return UnityEditor.EditorApplication.isPlaying;"

# Batch / CI
unity test "Pirate Game Unity" --mode EditMode --output test-results.xml
unity build "Pirate Game Unity" --help
```

Set the project path for any command:

```powershell
$env:UNITY_PROJECT_PATH = "c:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game\Pirate Game Unity"
```

## Safe Mode

If the project has compile errors, Unity boots into **Safe Mode** and Pipeline does not load. Fix compile errors and restart the Editor before using `unity command` or MCP.

## V5 work (since 29 Sep 2026)

New features are built on branch `v5-live-game` following [plan/v5/V5_PLAN.md](plan/v5/V5_PLAN.md): one phase at a
time, each gated by targeted tests, a scripted playable check, an independent review and the owner's playtest, then one
commit `V5 M<n>: <module>`. The independent reviewer is the read-only agent
[.claude/agents/v5-reviewer.md](.claude/agents/v5-reviewer.md); run it on the plan after it changes and on every
phase's diff before reporting the phase. In tools without Claude Code agents, give a separate read-only session that
file as its instructions. Evidence goes in `plan/v5/phase-NN-<name>/`; owner to-dos in `plan/v5/TODO-FOR-YOU.md`.

## Product context

Read [Rogue_Pirates_Cursor_Implementation.md](Rogue_Pirates_Cursor_Implementation.md) for milestone scope, non-negotiable gameplay rules, and what not to build yet.
