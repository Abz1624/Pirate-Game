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

The project still lists `com.coplaydev.unity-mcp` in `Packages/manifest.json`; it is optional and can be removed once you fully migrate to `com.unity.pipeline`.

## Agent workflow (preferred order)

1. **Check connection** — `unity status --json`
2. **Inspect live Editor** — `unity command` (list tools), `unity command eval "return Application.version;"`
3. **Make code changes** in `Pirate Game Unity/Assets/`
4. **Let Unity recompile** (or use CLI to drive the Editor)
5. **Run tests** — `unity test "Pirate Game Unity" --mode EditMode`
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

## Product context

Read [Rogue_Pirates_Cursor_Implementation.md](Rogue_Pirates_Cursor_Implementation.md) for milestone scope, non-negotiable gameplay rules, and what not to build yet.
