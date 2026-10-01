# V5 weapon playable check (M4, M4b): boot -> menu -> Set Sail (seed 20261001) -> the run in the open Editor, ship
# invulnerable, scripted arenas skipped, clock at 2:00. For each "weapon" or "weapon:evolution" in -Pairs: the weapon
# alone (WeaponLab.Solo), evolved when one is given, 6 s of play with the level-up cards answered (boost requested every
# second, for the Ramming Prow), then a screenshot and a line: kills in those 6 s, and the slot's evolution.
# Usage: powershell -File plan/v5/tools/weapon-check.ps1 -Out plan/v5/phase-04-weapons/m4/playable -Pairs "anchor_flail","anchor_flail:kraken_chain"
param([string]$Out, [string[]]$Pairs)
$ErrorActionPreference = "Continue"
$Pairs = ($Pairs -join ",").Split(",") | Where-Object { $_ } # -File passes the list as one string
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root $Out
New-Item -ItemType Directory -Force $out | Out-Null
$tmp = Join-Path $env:TEMP "v5-weapon-check.cs"
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
$answer = 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; if (run != null && run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); else run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); return "picked"; } foreach (var b in UnityEngine.Object.FindObjectsByType<UnityEngine.MonoBehaviour>(UnityEngine.FindObjectsSortMode.None)) if (b.GetType().Name == "ShipBoostController") b.SendMessage("RequestBoost", UnityEngine.SendMessageOptions.DontRequireReceiver); return "live";'
if (-not (WaitFor $playing 60)) { $lines += "RUN NEVER STARTED" }
else {
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.Spawning.ISpawnController spawn; if (g.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out spawn)) foreach (var p in spawn.Phases) if (p.PhaseId == "reef_crab" || p.PhaseId.StartsWith("elite_")) spawn.Skip(p.PhaseId); RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock(120f); return 1;' | Out-Null
    $start = Get-Date
    while (((Get-Date) - $start).TotalSeconds -lt 8) { Eval $answer | Out-Null; Start-Sleep -Milliseconds 700 }
    foreach ($pair in $Pairs) {
        $parts = $pair.Split(":")
        $weapon = $parts[0]
        $evo = if ($parts.Length -gt 1) { $parts[1] } else { "" }
        $setup = 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.DevTools.WeaponLab.Solo(g, "' + $weapon + '"); ' + $(if ($evo) { 'RoguePirates.V4.DevTools.WeaponLab.Evolve(g, "' + $weapon + '", "' + $evo + '"); ' } else { '' }) + 'return RoguePirates.V4.Foundation.Core.RunManager.Instance.KillCount.ToString();'
        $k0 = Eval $setup
        $start = Get-Date
        while (((Get-Date) - $start).TotalSeconds -lt 6) { Eval $answer | Out-Null; Start-Sleep -Milliseconds 600 }
        $name = if ($evo) { "$weapon-$evo.png" } else { "$weapon.png" }
        Shot $name
        $state = Eval 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.Weapons.IWeaponManager wm; g.Context.TryGet<RoguePirates.V4.Weapons.IWeaponManager>(out wm); var s = wm != null && wm.Slots.Count > 0 ? wm.Slots[0] : null; return (s != null ? s.Id + " evolution " + (s.Evolution != null ? s.Evolution.evolutionId : "none") + " quantity+" + s.QuantityBonus + " burst+" + s.ExtraBurstShots + " size x" + s.SizeMult.ToString("0.00") : "no slot") + ", kills " + RoguePirates.V4.Foundation.Core.RunManager.Instance.KillCount;'
        $lines += "$pair`: kills before $k0; $state"
    }
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
