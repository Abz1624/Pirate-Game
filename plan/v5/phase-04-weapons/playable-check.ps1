# V5 M4a playable check (1 Oct 2026): boot -> menu -> Set Sail (seed 20261001) -> the run in the open Editor, ship
# invulnerable, scripted arenas skipped, clock at 2:00. 1) The flamethrower alone: screenshots while it streams, and
# the flame's shaped reach against the damage cone's range. (Slow and knockback are measured in the EditMode tests: a live
# sample through the CLI, seconds apart, against chasing and dashing enemies, was meaningless.) Output: plan/v5/phase-04-weapons/playable/.
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-04-weapons\playable"
New-Item -ItemType Directory -Force $out | Out-Null
$tmp = Join-Path $env:TEMP "v5-m4a-eval.cs"
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
    Eval 'RoguePirates.V4.RunRequest.RequestSeed(20261001); UnityEngine.GameObject.Find("SetSail").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
    Start-Sleep -Seconds 2
    Eval 'UnityEngine.GameObject.Find("SetSailButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
}
$playing = 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing ? "yes" : "no";'
$answer = 'var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) { m.Pick(0); return "picked"; } return "live";'
if (-not (WaitFor $playing 60)) { $lines += "RUN NEVER STARTED" }
else {
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.Spawning.ISpawnController spawn; if (g.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out spawn)) foreach (var p in spawn.Phases) if (p.PhaseId == "reef_crab" || p.PhaseId.StartsWith("elite_")) spawn.Skip(p.PhaseId); RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock(120f); return 1;' | Out-Null
    $start = Get-Date
    while (((Get-Date) - $start).TotalSeconds -lt 10) { Eval $answer | Out-Null; Start-Sleep -Milliseconds 700 }

    # 1) The flamethrower alone.
    Eval 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.DevTools.WeaponLab.Solo(g, "flamethrower"); return 1;' | Out-Null
    $flame = 'var f = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Weapons.FlameStreamPresenter>(); var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.Weapons.IWeaponManager wm; g.Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out wm); var s = wm != null && wm.Slots.Count > 0 ? wm.Slots[0] : null; return "emitting " + (f != null && f.IsEmitting) + ", shaped reach " + (f != null ? f.ShapedReach.ToString("0.00") : "-") + " m, arc " + (f != null ? f.ShapedArc.ToString("0") : "-") + ", cone range " + (s != null ? s.StreamRange.ToString("0.00") : "-") + " m (muzzle offset " + (s != null ? s.Config.stream.forwardOffset.ToString("0.00") : "-") + ")";'
    $shots = 0
    $start = Get-Date
    while (((Get-Date) - $start).TotalSeconds -lt 20 -and $shots -lt 3) {
        Eval $answer | Out-Null
        $f = Eval $flame
        if ($f -match "emitting True") { $shots++; Shot ("flame-$shots.png"); $lines += "flame $shots`: $f" }
        Start-Sleep -Milliseconds 900
    }
    if ($shots -eq 0) { $lines += "flame never streamed: $(Eval $flame)" }

    Shot "after-flame.png"
}
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
if ((Eval $dirtyCheck) -eq "dirty") { Eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/V4/V4Boot.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); return 1;' | Out-Null }
$lines | Set-Content (Join-Path $out "steps.txt")
unity command console --tail 300 --level error 2>$null | Out-String | Set-Content (Join-Path $out "console-errors.txt")
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api|Main thread operation timed out)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 10 }
