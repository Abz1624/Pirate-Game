# M0 automated playable check: V4Boot -> menu -> Play -> run -> console -> boss -> kill -> death -> results
# -> Retry (rebound button) -> console restart on seed 101. Screenshots and a step log go to the phase folder.
# Prefs are snapshotted/restored by V4TraceSession, so the user's save is not touched.
param([string] $OutDir = 'plan/v4-verification/phase-00-composition/playable-check')
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
    # save_path always lands under Assets/ (it would import stray PNGs), so take the inline base64 image instead.
    $r = unity command capture_game_view --source screen --format json 2>&1 | Out-String
    $m = [regex]::Match($r, '"(?:image|data|base64|png)"\s*:\s*"([A-Za-z0-9+/=]{1000,})"')
    if (-not $m.Success) { $m = [regex]::Match($r, '([A-Za-z0-9+/]{5000,}={0,2})') }
    if ($m.Success) { [IO.File]::WriteAllBytes($p, [Convert]::FromBase64String($m.Groups[1].Value)) } else { $log.Add("[shot] FAILED $name") }
    $log.Add("[shot] $name.png"); Write-Output "[shot] $name.png"
}
function WaitFor([string] $name, [string] $code, [int] $seconds = 30) {
    for ($i = 0; $i -lt $seconds; $i++) {
        $v = Eval $name $code
        if ($v -match '^OK') { return $v }
        Start-Sleep -Seconds 1
    }
    throw "timeout waiting for $name"
}

$status = @'
var rm = HighSeas.Core.RunManager.Instance;
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
var console = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var ok = scene == "V4SaltwindRun" && rm != null && rm.State == HighSeas.Core.RunState.Playing && game != null && game.IsRunning;
return (ok ? "OK " : "WAIT ") + $"scene={scene} state={rm?.State} seed={rm?.RunSeed} runId={rm?.RunId} v4game={game != null} console={console != null}";
'@

unity command set_autotick --enable true 2>&1 | Out-Null
unity command clear_console 2>&1 | Out-Null
Eval 'begin' 'return RoguePirates.V4.Editor.V4TraceSession.BeginPlayableCheck("Assets/_Game/Scenes/V4/V4Boot.unity");' | Out-Null
Start-Sleep -Seconds 10  # let the Play Mode domain reload finish before polling (polls during it time out)
try {
    WaitFor 'menu' @'
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
var cs = UnityEngine.Object.FindFirstObjectByType<HighSeas.V3.V3MainMenuCharacterSelect>();
var menu = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>();
return (scene == "V4MainMenu" && cs != null && cs.PlayButton != null && menu != null ? "OK " : "WAIT ") + $"scene={scene} v4menu={menu != null}";
'@ | Out-Null
    Start-Sleep -Seconds 2; Shot '01-menu'
    Eval 'play' 'UnityEngine.Object.FindFirstObjectByType<HighSeas.V3.V3MainMenuCharacterSelect>().PlayButton.onClick.Invoke(); return "clicked Play";' | Out-Null
    WaitFor 'run-start' $status | Out-Null
    Eval 'god-on' 'UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return "invulnerable for the check";' | Out-Null
    Start-Sleep -Seconds 4; Shot '02-run-start'
    Eval 'console-open' 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>(); c.SetOpen(true); return $"open={c.IsOpen}";' | Out-Null
    Start-Sleep -Seconds 1; Shot '03-console'
    Eval 'console-close' 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>(); c.SetOpen(false); return $"open={c.IsOpen}";' | Out-Null
    Start-Sleep -Seconds 8
    WaitFor 'boss-summon' @'
var poi = UnityEngine.Object.FindFirstObjectByType<HighSeas.POI.POIManager>();
var ok = poi.BossSummoned || (poi.TrySummonBoss() && poi.BossSummoned);
return (ok ? "OK " : "WAIT ") + $"summoned={poi.BossSummoned}";
'@ | Out-Null
    $findBoss = @'
var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>();
var idx = -1; for (var i = 0; i < w.Agents.Length; i++) if (w.Agents[i].active && w.Agents[i].isBoss) { idx = i; break; }
return (idx >= 0 ? "OK " : "WAIT ") + $"bossIndex={idx}";
'@
    WaitFor 'boss-spawned' $findBoss | Out-Null
    Start-Sleep -Seconds 3; Shot '04-boss'
    Eval 'boss-kill' @'
var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>();
var idx = -1; for (var i = 0; i < w.Agents.Length; i++) if (w.Agents[i].active && w.Agents[i].isBoss) { idx = i; break; }
var a = w.Agents[idx]; w.ApplyDamage(idx, UnityEngine.Mathf.Max(a.hp, a.maxHp) + 500f, false, weaponId: "deck_cannons");
return "damage applied";
'@ | Out-Null
    WaitFor 'boss-secured' @'
var poi = UnityEngine.Object.FindFirstObjectByType<HighSeas.POI.POIManager>();
var rm = HighSeas.Core.RunManager.Instance;
return (poi.BossDefeated && rm.ObjectiveSecured && !rm.IsRunEnded ? "OK " : "WAIT ") + $"defeated={poi.BossDefeated} secured={rm.ObjectiveSecured} ended={rm.IsRunEnded} state={rm.State}";
'@ | Out-Null
    Start-Sleep -Seconds 2; Shot '05-territory-claimed'
    Eval 'death' 'var s = UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>(); s.Damage.SetPlaytestInvulnerable(false); s.Damage.TakeDamage(99999f, UnityEngine.Vector3.zero, false); return "lethal damage";' | Out-Null
    WaitFor 'settled' @'
var rm = HighSeas.Core.RunManager.Instance;
return (rm.IsRunEnded && HighSeas.Balance.SaltwindRunSettlement.LastSettlementApplied ? "OK " : "WAIT ") + $"ended={rm.IsRunEnded} settled={HighSeas.Balance.SaltwindRunSettlement.LastSettlementApplied} marks={HighSeas.Balance.SaltwindRunSettlement.LastMarksAwarded} boss={HighSeas.Balance.SaltwindRunSettlement.LastBreakdown.BossVictoryCredits}";
'@ | Out-Null
    Start-Sleep -Seconds 3; Shot '06-results'
    Eval 'first-run-id' 'return HighSeas.Core.RunManager.Instance.RunId;' | Out-Null
    Eval 'retry-click' @'
foreach (var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>(UnityEngine.FindObjectsSortMode.None))
    if (b.name == "PLACEHOLDER_v3_ui_run_results_restart" && b.gameObject.activeInHierarchy) { b.onClick.Invoke(); return "clicked Retry on " + b.transform.parent.name; }
return "ERROR no visible Retry button";
'@ | Out-Null
    Start-Sleep -Seconds 2
    WaitFor 'retry-run' $status | Out-Null
    Start-Sleep -Seconds 2; Shot '07-after-retry'
    Eval 'console-restart-101' 'RoguePirates.V4.V4Game.RestartRunWithSeed(101); return "restart on seed 101";' | Out-Null
    Start-Sleep -Seconds 2
    WaitFor 'seed-101-run' ($status -replace 'var ok = ', 'var ok = rm != null && rm.RunSeed == 101 && ') | Out-Null
    Start-Sleep -Seconds 2; Shot '08-seed-101'
}
finally {
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    $errs = unity command console --tail 200 --level error 2>&1 | Out-String
    $m = [regex]::Match($errs, '"returned":(\d+)')
    $log.Add("[console] errors returned=$($m.Groups[1].Value)")
    Set-Content -Path (Join-Path $out 'console-errors.txt') -Value $errs -Encoding utf8
    Set-Content -Path (Join-Path $out 'steps.log') -Value $log -Encoding utf8
    Write-Output "[console] errors returned=$($m.Groups[1].Value)"
}
