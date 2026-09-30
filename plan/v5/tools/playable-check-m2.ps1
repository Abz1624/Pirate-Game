# V5 M2 automated playable check: plays the Saltwind run in the open Editor (ship invulnerable; cards auto-picked; XP pulled
# to the ship before each reading), lets
# the first minute run on its own (the level at 1:00 is logged; a standing ship kills far less than a player, so it
# cannot confirm the XP pace — that is the owner's playtest), then jumps the clock to 3, 6 and 9 minutes and checks
# the enemies alive against the balance targets' alive band; finally opens the Tuning Console's Balance section, runs the
# model in-game and takes a screenshot of it.
# Output: plan/v5/phase-02-model/evidence/playable/ (checkpoints.txt, PNGs, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-02-model\evidence\playable"
New-Item -ItemType Directory -Force $out | Out-Null

# Code goes through a file: Windows PowerShell 5.1 strips double quotes from native-command arguments.
$tmp = Join-Path $env:TEMP "v5-eval.cs"
function Eval([string]$code) {
    Set-Content -Path $tmp -Value $code -Encoding UTF8
    $r = unity command eval_file --file $tmp 2>&1 | Out-String
    if ($r -match '"result":"([^"]*)"') { return $Matches[1] } else { return $r }
}
function Shot([string]$name) {
    $shot = unity command capture_game_view --source screen --width 1280 --height 720 --timeout 60 2>$null | Out-String
    if ($shot -match '"base64":"([^"]+)"') { [IO.File]::WriteAllBytes((Join-Path $out $name), [Convert]::FromBase64String($Matches[1])) }
}

# Scenes: refuse to start on unsaved edits (never anyone else's work); reload afterwards only what this check dirtied.
$dirtyCheck = 'var d = false; for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) d |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty; return d ? "dirty" : "clean";'
if ((Eval $dirtyCheck) -eq "dirty") { Write-Output "UNSAVED SCENES: save or discard them in the Editor first"; exit 5 }
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

Eval @'
var ship = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>();
ship.Damage.SetPlaytestInvulnerable(true);
return "ok";
'@ | Out-Null

# Picks a card whenever the level-up cards are up, then reports the state and the alive band for the current minute.
$state = @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
game.Context.TryGet<RoguePirates.V4.Spawning.IEnemySpawnSink>(out var sink);
var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); else run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); }
game.Build?.Pickups.AttractAllXp(); // the standing ship collects its XP (as a moving player would), so its level shows the XP pace
var targets = game.Config.Single<RoguePirates.V4.Config.BalanceTargetsConfig>();
var cap = game.Config.Single<RoguePirates.V4.Config.WaveTimelineConfig>().maxActiveEnemies;
var minute = (int)(run.ElapsedSeconds / 60f);
var band = targets.minutes.Find(x => x.minute == minute);
var lo = band != null ? band.aliveShare.min * cap : float.NaN;
var hi = band != null ? band.aliveShare.max * cap : float.NaN;
var alive = sink.ActiveCount;
var ok = band == null || (alive >= lo && alive <= hi);
game.Context.TryGet<RoguePirates.V4.Views.IRunViews>(out var views); var prog = views != null ? views.Progression : null;
return $"t={run.ElapsedSeconds:0} level={(prog != null ? prog.Level : -1)} alive={alive} band={lo:0}-{hi:0} {(ok ? "IN" : "OUT")} frame={UnityEngine.Time.smoothDeltaTime * 1000f:0.0}ms";
'@

$lines = @()
# Minute 1 plays on its own (no jump); the level at 1:00 is logged for reference only.
for ($k = 0; $k -lt 40; $k++) {
    Start-Sleep -Seconds 2
    $s = Eval $state
    if ($s -match 't=(\d+)' -and [int]$Matches[1] -ge 65) { break }
}
$lines += "1:05  " + (Eval $state)
Shot "minute-01.png"
# Each jump lands at m:05; readings are taken ~20 s later (at 9:25 the 9:15 horde has arrived, past the lull before it).
foreach ($m in 3, 6, 9) {
    $at = if ($m -eq 9) { 9 * 60 + 5 } else { $m * 60 + 5 }
    Eval "RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock($($at)f); return 1;" | Out-Null
    # A standing scripted ship reads noisy one-off counts (level-up pauses stop spawns): take the median of 8 readings.
    for ($k = 0; $k -lt 10; $k++) { Start-Sleep -Seconds 2; Eval $state | Out-Null } # ~20 s for the crowd to build after the jump
    $samples = @()
    for ($k = 0; $k -lt 8; $k++) { Start-Sleep -Seconds 2; $r = Eval $state; if ($r -match 'alive=(\d+)') { $samples += [int]$Matches[1] } }
    $last = Eval $state
    $median = ($samples | Sort-Object)[[int][math]::Floor($samples.Count / 2)]
    $inBand = if ($last -match 'band=(\d+)-(\d+)') { $median -ge [int]$Matches[1] -and $median -le [int]$Matches[2] } else { $true }
    $lines += "$($m):00  $last  median-alive=$median ($($samples -join ',')) $(if ($inBand) { 'IN' } else { 'OUT' })"
    Shot ("minute-{0:00}.png" -f $m)
}

# The Balance section: open the console filtered to it, run the model (40 runs per personality) and capture it.
$console = @'
var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();
if (c == null) return "no console";
c.SetOpen(true);
typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_search", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(c, "monte carlo");
var sections = (System.Collections.IList)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_sections", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(c);
foreach (var s in sections)
    if (s is RoguePirates.V4.DevTools.Console.BalanceSection b)
    {
        typeof(RoguePirates.V4.DevTools.Console.BalanceSection).GetMethod("Start", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(b, new object[] { c.Game.Config });
        return "model started";
    }
return "no balance section";
'@
$lines += "console: " + (Eval $console)
Start-Sleep -Seconds 30
Eval $state | Out-Null
Start-Sleep -Seconds 2
Shot "console-balance.png"

$lines | Set-Content (Join-Path $out "checkpoints.txt")
unity command console --tail 200 --level error | Out-String | Set-Content (Join-Path $out "console-errors.txt")
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
if ((Eval $dirtyCheck) -eq "dirty") { Eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, UnityEditor.SceneManagement.OpenSceneMode.Single); return 1;' | Out-Null; $lines += "the scene this check dirtied was reloaded from disk" }
$lines | Set-Content (Join-Path $out "checkpoints.txt")
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api|Main thread operation timed out)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 5 }
