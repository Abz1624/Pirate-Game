# V5 M3d playable check (1 Oct 2026): boot -> menu -> Set Sail -> the run in the open Editor. The run clock is jumped to
# five moments of the keyframed crowd (0:45, 3:30, 5:00, 7:30, 9:30); at each the ship (invulnerable, cards answered)
# plays ~15 s of real time, then the crowd (alive enemies, by id) is recorded with a screenshot showing the enemies' looks.
# Output: plan/v5/phase-03d-pressure/playable/ (steps.txt, *.png, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-03d-pressure\playable"
New-Item -ItemType Directory -Force $out | Out-Null

$tmp = Join-Path $env:TEMP "v5-m3d-eval.cs"
function Eval([string]$code) {
    Set-Content -Path $tmp -Value $code -Encoding UTF8
    $r = unity command eval_file --file $tmp 2>&1 | Out-String
    if ($r -match '"result":"([^"]*)"') { return $Matches[1] } else { return $r.Trim() }
}
function Shot([string]$name) {
    $shot = unity command capture_game_view --source screen --width 1280 --height 720 --timeout 60 2>$null | Out-String
    if ($shot -match '"base64":"([^"]+)"') { [IO.File]::WriteAllBytes((Join-Path $out $name), [Convert]::FromBase64String($Matches[1])) }
}
function WaitFor([string]$code, [int]$tries = 40) {
    for ($i = 0; $i -lt $tries; $i++) { Start-Sleep -Seconds 2; if ((Eval $code) -eq "yes") { return $true } }
    return $false
}

$lines = @()
$dirtyCheck = 'var d = false; for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) d |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty; return d ? "dirty" : "clean";'
if ((Eval $dirtyCheck) -eq "dirty") { Write-Output "UNSAVED SCENES: save or discard them in the Editor first"; exit 5 }
unity command clear_console | Out-Null
unity command open_scene --path "Assets/_Game/Scenes/V4/V4Boot.unity" | Out-Null
Start-Sleep -Seconds 2
unity command editor_play | Out-Null

$menu = 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>(); return c != null && c.ArtView != null ? "yes" : "no";'
if (-not (WaitFor $menu)) { $lines += "MENU NEVER SHOWED" }
else {
    Eval 'UnityEngine.GameObject.Find("SetSail").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
    Start-Sleep -Seconds 2
    Eval 'UnityEngine.GameObject.Find("SetSailButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
}
$playing = 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing ? "yes" : "no";'
$crowd = @'
var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var sim = g.Shell.Enemies.Simulation;
var parts = new System.Collections.Generic.List<string>();
for (var i = 0; i < sim.Definitions.Length; i++) { var n = sim.CountActiveByDefinition(i); if (n > 0) parts.Add(sim.Definitions[i].id + " " + n); }
var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
return "t " + run.ElapsedSeconds.ToString("0") + " s, alive " + sim.ActiveCount + ", level " + g.Build.Progression.Level + ": " + string.Join(", ", parts);
'@
$answer = 'var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) { m.Pick(0); return "picked"; } return "live";'
if (-not (WaitFor $playing 60)) { $lines += "RUN NEVER STARTED" }
else {
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return 1;' | Out-Null
    $moments = @(@(45, "01-0m45-early-peak"), @(210, "02-3m30-big-peak"), @(300, "03-5m00-valley"), @(450, "04-7m30-late-peak"), @(570, "05-9m30-climax"))
    foreach ($m in $moments) {
        Eval ("var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; if (run.ElapsedSeconds < " + $m[0] + "f) run.JumpClock(" + $m[0] + "f); return 1;") | Out-Null
        $start = Get-Date
        while (((Get-Date) - $start).TotalSeconds -lt 15) { Eval $answer | Out-Null; Start-Sleep -Milliseconds 800 }
        Eval $answer | Out-Null
        Start-Sleep -Milliseconds 500
        $lines += Eval $crowd
        Shot ($m[1] + ".png")
    }
}
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
if ((Eval $dirtyCheck) -eq "dirty") { Eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/V4/V4Boot.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); return 1;' | Out-Null; $lines += "the scene this check dirtied was reloaded from disk" }
$lines | Set-Content (Join-Path $out "steps.txt")
unity command console --tail 400 --level error | Out-String | Set-Content (Join-Path $out "console-errors.txt")
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api|Main thread operation timed out)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 10 }
