# V5 M3c playable check (1 Oct 2026): boot -> menu -> Set Sail -> the run in the open Editor; about 100 s of real play
# (invulnerable, cards answered, all XP pulled in each poll like a player who collects) recording the run time of every
# level-up against the rhythm, with screenshots.
# Output: plan/v5/phase-03c-levels/playable/ (steps.txt, *.png, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-03c-levels\playable"
New-Item -ItemType Directory -Force $out | Out-Null

$tmp = Join-Path $env:TEMP "v5-m3c-eval.cs"
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
if (-not (WaitFor $playing 60)) { $lines += "RUN NEVER STARTED" }
else {
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return 1;' | Out-Null
    $last = 0; $shotCards = $false
    $start = Get-Date
    while (((Get-Date) - $start).TotalSeconds -lt 100) {
        $st = Eval 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; var g0 = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); if (!run.IsPaused) g0.Build.Pickups.AttractAllXp(); var p = g0.Build.Progression; var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); return p.Level + "|" + run.ElapsedSeconds.ToString("0") + "|" + (m != null && m.IsShowing ? "cards" : "live");'
        $parts = $st -split '\|'
        if ($parts.Count -eq 3) {
            $lvl = [int]$parts[0]
            if ($lvl -gt $last) { for ($l = $last + 1; $l -le $lvl; $l++) { $lines += "level $l at ~$($parts[1]) s" }; $last = $lvl }
            if ($parts[2] -eq "cards") {
                if (-not $shotCards) { Shot "01-level-up-cards.png"; $shotCards = $true }
                Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>().Pick(0); return 1;' | Out-Null
            }
        }
        Start-Sleep -Milliseconds 700
    }
    Shot "02-after-100s.png"
    $lines += "end: " + (Eval 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); var p = g.Build.Progression; return "run " + run.ElapsedSeconds.ToString("0") + " s, level " + p.Level + ", weapons " + p.OwnedWeapons.Count + ", levels " + string.Join("/", p.OwnedWeapons.Select(w => p.WeaponLevel(w)));')
    $lines += "targets (level: s): 1 first kill (~12), 2 +10, 3 +10, 4 +20, 5 +30, 6 +50, then +60 each (12, 22, 32, 52, 82, 132, 192)"
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
