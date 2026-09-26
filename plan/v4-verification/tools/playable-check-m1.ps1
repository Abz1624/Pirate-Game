# M1 automated playable check: V4Boot -> menu -> run -> Tuning Console Config section -> live band edit ->
# restart with preset (applies from t=0) -> reset. Screenshots and a step log go to the phase folder.
param([string] $OutDir = 'plan/v4-verification/phase-01-config/playable-check')
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
function WaitFor([string] $name, [string] $code, [int] $seconds = 40) {
    for ($i = 0; $i -lt $seconds; $i++) {
        $v = Eval $name $code
        if ($v -match '^OK') { return $v }
        Start-Sleep -Seconds 1
    }
    throw "timeout waiting for $name"
}

$band2 = 'var t = game.Config.Single<RoguePirates.V4.Config.WaveTimelineConfig>(); var band2 = t.phases.FindIndex(p => p.phaseId == "band_2"); var legacy = game.Shell.LegacyConfig; var interval = legacy.RuntimeSchedule.entries[2].profile.timing.spawnIntervalSeconds;'
$running = @'
var rm = HighSeas.Core.RunManager.Instance;
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var ok = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "V4SaltwindRun" && rm != null && rm.State == HighSeas.Core.RunState.Playing && game != null && game.IsRunning;
if (!ok) return "WAIT";
'@

unity command set_autotick --enable true 2>&1 | Out-Null
unity command clear_console 2>&1 | Out-Null
Eval 'begin' 'return RoguePirates.V4.Editor.V4TraceSession.BeginPlayableCheck("Assets/_Game/Scenes/V4/V4Boot.unity");' | Out-Null
Start-Sleep -Seconds 20
try {
    WaitFor 'menu' 'var cs = UnityEngine.Object.FindFirstObjectByType<HighSeas.V3.V3MainMenuCharacterSelect>(); return (cs != null && cs.PlayButton != null ? "OK" : "WAIT") + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;' | Out-Null
    Eval 'play' 'UnityEngine.Object.FindFirstObjectByType<HighSeas.V3.V3MainMenuCharacterSelect>().PlayButton.onClick.Invoke(); return "clicked Set Sail";' | Out-Null
    WaitFor 'run-start' ($running + "`n" + $band2 + ' return $"OK config={game.Config.SetId} hash={game.Config.Hash.Substring(0, 12)} band2 rate={t.phases[band2].ratePerSecond} interval={interval:R} edits={game.Config.ChangeLog.Count}";') | Out-Null
    Eval 'god-on' 'UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return "invulnerable for the check";' | Out-Null
    Eval 'console-config' @'
var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();
typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_search", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(c, "ratePerSecond");
c.SetOpen(true);
return $"open={c.IsOpen} search={c.SearchText}";
'@ | Out-Null
    Start-Sleep -Seconds 2; Shot '01-console-config'
    Eval 'live-edit' ($running + "`n" + $band2 + @'
if (!game.Config.TryApplyEdit("timeline.saltwind", $"phases[{band2}].ratePerSecond", "4", HighSeas.Core.RunManager.Instance.ElapsedSeconds, out var error)) return "ERROR " + error;
var after = legacy.RuntimeSchedule.entries[2].profile.timing.spawnIntervalSeconds;
return $"band2 rate 1.8 -> 4: interval {interval:R} -> {after:R}; edits={game.Config.ChangeLog.Count}; hash={game.Config.Hash.Substring(0, 12)}";
'@) | Out-Null
    Start-Sleep -Seconds 1; Shot '02-after-live-edit'
    Eval 'restart-with-preset' @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var json = RoguePirates.V4.Config.PresetCodec.ToJson(RoguePirates.V4.Config.PresetCodec.Capture(game.Config, "band2x", game.Config.Hash));
RoguePirates.V4.RunRequest.RequestPreset(json);
RoguePirates.V4.V4Game.RestartRunWithSeed(101);
return "restart on seed 101 with preset band2x";
'@ | Out-Null
    Start-Sleep -Seconds 3
    WaitFor 'preset-run' ($running + "`n" + $band2 + ' return (interval == 0.25f && rm.RunSeed == 101 ? "OK" : "WAIT") + $" seed={rm.RunSeed} t={rm.ElapsedSeconds:0.0} band2 interval={interval:R} (preset applied before the run)";') | Out-Null
    Eval 'console-again' 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>(); typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_search", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(c, "ratePerSecond"); c.SetOpen(true); return "open";' | Out-Null
    Start-Sleep -Seconds 2; Shot '03-preset-run'
    Eval 'reset-all' ($running + "`n" + $band2 + ' foreach (var cfg in System.Linq.Enumerable.ToList(game.Config.Configs)) game.Config.ResetConfig(cfg.configId, rm.ElapsedSeconds); var back = legacy.RuntimeSchedule.entries[2].profile.timing.spawnIntervalSeconds; return $"reset: band2 rate={t.phases[band2].ratePerSecond} interval={back:R}";') | Out-Null
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
