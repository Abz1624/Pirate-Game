# V5 M1 automated playable check: plays the Saltwind run in the open Editor (ship invulnerable, scripted arenas skipped),
# jumps the clock to minutes 1, 3, 6, 9 and 13, and at each takes a screenshot and logs the flow state and frame time.
# Output: plan/v5/phase-01-flow/evidence/playable/ (PNG per checkpoint + checkpoints.txt + console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-01-flow\evidence\playable"
New-Item -ItemType Directory -Force $out | Out-Null

# Code goes through a file: Windows PowerShell 5.1 strips double quotes from native-command arguments.
$tmp = Join-Path $env:TEMP "v5-eval.cs"
function Eval([string]$code) {
    Set-Content -Path $tmp -Value $code -Encoding UTF8
    $r = unity command eval_file --file $tmp 2>&1 | Out-String
    if ($r -match '"result":"([^"]*)"') { return $Matches[1] } else { return $r }
}

unity command clear_console | Out-Null
unity command open_scene --path "Assets/_Game/Scenes/V4/V4SaltwindRun.unity" | Out-Null
Start-Sleep -Seconds 3
unity command editor_play | Out-Null
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $s = Eval 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing ? "playing" : "wait";'
    if ($s -eq "playing") { $ready = $true; break }
}
if (-not $ready) { Write-Output "RUN NEVER STARTED"; unity command editor_stop | Out-Null; exit 2 }

$setup = @'
var ship = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>();
ship.Damage.SetPlaytestInvulnerable(true);
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out var spawn);
foreach (var p in spawn.Phases) if (p.PhaseId == "reef_crab" || p.PhaseId.StartsWith("elite_")) spawn.Skip(p.PhaseId);
return "ok";
'@
Eval $setup | Out-Null

$state = @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out var spawn);
game.Context.TryGet<RoguePirates.V4.Spawning.IEnemySpawnSink>(out var sink);
var f = (RoguePirates.V4.Spawning.IFlowStatus)spawn;
var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); else run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); }
return $"t={run.ElapsedSeconds:0} wave={f.WaveIndex} floor={f.Floor} alive={sink.ActiveCount} release={f.InRelease} toppedUp={f.ToppedUp} hordes={f.HordesSpawned} frame={UnityEngine.Time.smoothDeltaTime * 1000f:0.0}ms";
'@

$lines = @()
foreach ($m in 1, 3, 6, 9, 13) {
    if ($m -eq 6) {
        # A horde as it arrives: jump into the lull before the 5:30 line horde, wait for it, capture it.
        Eval "RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock(322f); return 1;" | Out-Null
        for ($k = 0; $k -lt 6; $k++) { Start-Sleep -Seconds 2; Eval $state | Out-Null }
        $lines += "horde 5:30  " + (Eval $state)
        $shot = unity command capture_game_view --source screen --width 1280 --height 720 --timeout 60 2>$null | Out-String
        if ($shot -match '"base64":"([^"]+)"') { [IO.File]::WriteAllBytes((Join-Path $out "horde-05m30.png"), [Convert]::FromBase64String($Matches[1])) }
    }
    Eval "RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock($($m * 60 + 5)f); return 1;" | Out-Null
    for ($k = 0; $k -lt 6; $k++) { Start-Sleep -Seconds 2; Eval $state | Out-Null }
    $line = Eval $state
    $lines += "$($m):00  $line"
    # The screenshot comes back inline (base64): saving through the tool would write under Assets/.
    $name = "minute-{0:00}.png" -f $m
    $shot = unity command capture_game_view --source screen --width 1280 --height 720 --timeout 60 2>$null | Out-String
    if ($shot -match '"base64":"([^"]+)"') { [IO.File]::WriteAllBytes((Join-Path $out $name), [Convert]::FromBase64String($Matches[1])) }
}
$lines | Set-Content (Join-Path $out "checkpoints.txt")
unity command console --tail 200 --level error | Out-String | Set-Content (Join-Path $out "console-errors.txt")
unity command editor_stop | Out-Null
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 5 }
