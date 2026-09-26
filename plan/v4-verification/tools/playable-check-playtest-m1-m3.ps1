# M1-M3 playtest-fix check (targeted, no full run): console at its default size -> elite beat spawns the elite variant
# (R-03) -> jump past 10:00, overtime spawns Drowned enemies (R-05). The console's saved rect PlayerPref is restored.
param([string] $OutDir = 'plan/v4-verification/playtest-m1-m3/playable-check')
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$env:UNITY_PROJECT_PATH = Join-Path $repo 'Pirate Game Unity'
$out = Join-Path $repo $OutDir
New-Item -ItemType Directory -Force $out | Out-Null
$evalDir = Join-Path $env:TEMP 'v4-playable-eval'
New-Item -ItemType Directory -Force $evalDir | Out-Null
$log = New-Object System.Collections.Generic.List[string]

function Eval([string] $name, [string] $code) {
    $f = Join-Path $evalDir "$name.cs"
    Set-Content -Path $f -Value $code -Encoding utf8
    $r = unity command eval_file --file $f 2>&1 | Out-String
    $m = [regex]::Match($r, '"result":"((?:[^"\\]|\\.)*)"')
    $v = if ($m.Success) { $m.Groups[1].Value -replace '\\n', ' | ' } else { "ERROR: $($r.Trim())" }
    $log.Add("[$name] $v"); Write-Output "[$name] $v"
    return $v
}
function Shot([string] $name) {
    $p = Join-Path $out "$name.png"
    $r = unity command capture_game_view --source screen --format json 2>&1 | Out-String
    $m = [regex]::Match($r, '([A-Za-z0-9+/]{5000,}={0,2})')
    if ($m.Success) { [IO.File]::WriteAllBytes($p, [Convert]::FromBase64String($m.Groups[1].Value)) } else { $log.Add("[shot] FAILED $name") }
    $log.Add("[shot] $name.png"); Write-Output "[shot] $name.png"
}
# Every wait has a limit: a failed step is logged instead of hanging the script.
function WaitFor([string] $name, [string] $code, [int] $seconds = 60) {
    for ($i = 0; $i -lt $seconds; $i++) {
        $v = Eval $name $code
        if ($v -match '^OK') { return $v }
        Start-Sleep -Seconds 1
    }
    throw "timeout waiting for $name"
}

$spawnCtl = 'RoguePirates.V4.Spawning.ISpawnController s = null; UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>()?.Context?.TryGet(out s);'
# Level-ups pause the run (nothing plays the cards in this check): pick the first card, as the trace harness does.
$pickCard = 'var modal = HighSeas.UI.LevelUpModal.Instance; var card = modal != null ? modal.transform.Find("CardRow/Card0") : null; if (HighSeas.Core.RunManager.Instance.IsPaused && card != null && card.gameObject.activeInHierarchy) card.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();'
unity command set_autotick --enable true 2>&1 | Out-Null
unity command clear_console 2>&1 | Out-Null
$rectBefore = Eval 'rect-before' 'return UnityEngine.PlayerPrefs.HasKey("v4_console_rect") ? "HAS " + UnityEngine.PlayerPrefs.GetString("v4_console_rect") : "NONE";'
for ($attempt = 0; $attempt -lt 6; $attempt++) {
    $playing = Eval 'is-playing' 'return UnityEditor.EditorApplication.isPlaying ? "OK playing" : "no";'
    if ($playing -match '^OK') { break }
    Eval 'begin' 'return RoguePirates.V4.Editor.V4TraceSession.BeginPlayableCheck("Assets/_Game/Scenes/V4/V4SaltwindRun.unity");' | Out-Null
    Start-Sleep -Seconds 15
}
try {
    WaitFor 'run-start' "$spawnCtl var rm = HighSeas.Core.RunManager.Instance; if (rm == null || s == null || rm.ElapsedSeconds < 5f) return `"WAIT`"; UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return `$`"OK t={rm.ElapsedSeconds:0.0} phase={s.CurrentPhaseLabel}`";" | Out-Null
    Eval 'console' 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>(); c.SetOpen(true); return "console open";' | Out-Null
    Start-Sleep -Seconds 2; Shot '01-console-default-size'
    Eval 'console-rect' 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>(); var r = (UnityEngine.Rect)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_window", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c); c.SetOpen(false); return $"window {r.width:0}x{r.height:0} at {r.x:0},{r.y:0} on {UnityEngine.Screen.width}x{UnityEngine.Screen.height}";' | Out-Null

    Eval 'trigger-elite' "$spawnCtl return `$`"trigger elite_hammerhead: {s.TriggerNow(`"elite_hammerhead`")}`";" | Out-Null
    WaitFor 'elite' "$pickCard $spawnCtl if (!s.ArenaActive) return `"WAIT`"; var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); w.TryGetAgentByUnitId(s.ArenaFocusUnitId, out var i); var a = w.Agents[i]; return `$`"OK focus {w.Definitions[a.definitionIndex].id}, elite={a.isElite}, hp {a.hp:0}/{a.maxHp:0}`";" 20 | Out-Null
    Start-Sleep -Seconds 1; Shot '02-elite-hammerhead'
    Eval 'kill-elite' "$pickCard $spawnCtl var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); w.TryGetAgentByUnitId(s.ArenaFocusUnitId, out var i); var a = w.Agents[i]; w.ApplyDamage(i, UnityEngine.Mathf.Max(a.hp, a.maxHp) + 500f, false, weaponId: `"deck_cannons`"); return `"elite killed`";" | Out-Null

    Eval 'jump-overtime' "$pickCard var rm = HighSeas.Core.RunManager.Instance; rm.RestoreSuspendSnapshot(new HighSeas.Save.RunSnapshotData { runId = rm.RunId, elapsedSeconds = 601f, gold = rm.Gold, kills = rm.KillCount, objectiveSecured = rm.ObjectiveSecured }); return `"jumped to 601 s`";" | Out-Null
    WaitFor 'drowned' "$pickCard var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); int p = 0, h = 0; foreach (var a in w.Agents) { if (!a.active) continue; var d = w.Definitions[a.definitionIndex]; if (d == null) continue; if (d.id == `"drowned_pursuer`") p++; if (d.id == `"drowned_harrier`") h++; } return p + h >= 3 ? `$`"OK drowned alive: pursuer {p}, harrier {h}, t={HighSeas.Core.RunManager.Instance.ElapsedSeconds:0}`" : `"WAIT`";" 40 | Out-Null
    Shot '03-overtime-drowned'
}
catch { $log.Add("FAILED: $_"); Write-Output "FAILED: $_" }
finally {
    $errors = unity command console --level error --tail 50 2>&1 | Out-String
    Set-Content (Join-Path $out 'console-errors.txt') $errors -Encoding utf8
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    if ($rectBefore -match '^NONE') { Eval 'rect-restore' 'UnityEngine.PlayerPrefs.DeleteKey("v4_console_rect"); UnityEngine.PlayerPrefs.Save(); return "removed";' | Out-Null }
    elseif ($rectBefore -match '^HAS (.+)$') { $v = $Matches[1]; Eval 'rect-restore' "UnityEngine.PlayerPrefs.SetString(`"v4_console_rect`", `"$v`"); UnityEngine.PlayerPrefs.Save(); return `"restored`";" | Out-Null }
    Set-Content (Join-Path $out 'steps.log') $log -Encoding utf8
}
