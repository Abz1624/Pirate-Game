# Playtest branch (2 Oct 2026): a short scripted play in the open Editor (about 10 minutes, ship invulnerable, cards
# auto-picked). Real time from 0:00 to about 2:00 (kill bands judged, first heals), then clock jumps to the beats:
# 2:35 the cannon crab, 5:05 the second crab, 10:05 the big Kraken, 13:58 to 14:10 the boss from its whirlpool.
# Output: plan/playtest/evidence/ (checkpoints.txt, PNGs, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\playtest\evidence"
New-Item -ItemType Directory -Force $out | Out-Null

$tmp = Join-Path $env:TEMP "pt-eval.cs"
function Eval([string]$code) {
    Set-Content -Path $tmp -Value $code -Encoding UTF8
    $r = unity command eval_file --file $tmp 2>&1 | Out-String
    if ($r -match '"result":"([^"]*)"') { return $Matches[1] } else { return $r }
}
function Shot([string]$name) {
    $shot = unity command capture_game_view --source screen --width 1280 --height 720 --timeout 60 2>$null | Out-String
    if ($shot -match '"base64":"([^"]+)"') { [IO.File]::WriteAllBytes((Join-Path $out $name), [Convert]::FromBase64String($Matches[1])) }
}

$state = @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
game.Context.TryGet<RoguePirates.V4.Spawning.IEnemySpawnSink>(out var sink);
game.Context.TryGet<RoguePirates.V4.Difficulty.IDifficultyLevers>(out var lv);
var dd = lv as RoguePirates.V4.Difficulty.DifficultyDirector;
var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); else run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); }
game.Build?.Pickups.AttractAllXp();
var bars = 0; foreach (var b in UnityEngine.Object.FindObjectsByType<RoguePirates.V4.Foundation.VFX.WorldHealthBar>(UnityEngine.FindObjectsSortMode.None)) if (b.isActiveAndEnabled) bars++;
var kraken = ""; var crab = ""; var boss = "";
foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsSortMode.None)) {
  if (!t.gameObject.activeInHierarchy || !t.name.Contains("(Clone)")) continue;
  var n = t.name.ToLowerInvariant();
  if (n.Contains("kraken")) kraken += $"{t.lossyScale.x:0.00} ";
  if (n.Contains("crab")) crab += $"{t.lossyScale.x:0.00} ";
}
var beacons = 0; var sv = game.Stage?.Module?.Layout; if (sv?.pois != null) foreach (var q in sv.pois) if (q.type == RoguePirates.V4.Foundation.Core.POIType.BossBeacon) beacons++;
return $"beacons={beacons} t={run.ElapsedSeconds:0} state={run.State} level={game.Build?.Progression.Level} alive={sink.ActiveCount} bars={bars} band(last {dd?.LastBandRatio:0.00} rate x{dd?.KillBandRate:0.00} elite {dd?.KillBandEliteChance:0.00}) kraken[{kraken}] crab[{crab}]";
'@

$lines = @()
$dirtyCheck = 'var d = false; for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) d |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty; return d ? "dirty" : "clean";'
if ((Eval $dirtyCheck) -eq "dirty") { Write-Output "UNSAVED SCENES: save or discard them in the Editor first"; exit 5 }
unity command clear_console | Out-Null
unity command open_scene --path "Assets/_Game/Scenes/V4/V4SaltwindRun.unity" | Out-Null
Start-Sleep -Seconds 2
unity command editor_play | Out-Null
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep -Seconds 2
    $s = Eval 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing ? "playing" : "wait";'
    if ($s -eq "playing") { $ready = $true; break }
}
if (-not $ready) { Write-Output "RUN NEVER STARTED"; unity command editor_stop | Out-Null; exit 2 }
Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return 1;' | Out-Null

# Real time: the first two minutes (bands at 0:30, 1:00, 1:30 judged; levels 1-5).
for ($k = 0; $k -lt 12; $k++) { Start-Sleep -Seconds 10; $lines += "real   " + (Eval $state) }
Shot "01-early.png"
foreach ($beat in @(@(155, "02-crab.png"), @(305, "03-crab2.png"), @(605, "04-kraken.png"), @(838, "05-before-boss.png"))) {
    Eval "RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock($($beat[0])f); return 1;" | Out-Null
    for ($k = 0; $k -lt 6; $k++) { Start-Sleep -Seconds 3; Eval $state | Out-Null }
    $lines += "jump $($beat[0]) " + (Eval $state)
    Shot $beat[1]
}
for ($k = 0; $k -lt 6; $k++) { Start-Sleep -Seconds 3; $lines += "boss   " + (Eval $state) }
Shot "06-boss.png"
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
if ((Eval $dirtyCheck) -eq "dirty") { Eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, UnityEditor.SceneManagement.OpenSceneMode.Single); return 1;' | Out-Null; $lines += "the scene this check dirtied was reloaded from disk" }
$lines | Set-Content (Join-Path $out "checkpoints.txt")
unity command console --tail 400 --level error | Out-String | Set-Content (Join-Path $out "console-errors.txt")
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api|Main thread operation timed out)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 8 }
