# V5 balance re-check (1 Oct 2026): scripted playable check in the open Editor. Boot -> menu -> Set Sail -> the run,
# then targeted checks that the live game runs the demo baseline and its retune: the config values, the ship's speed
# factor, the XP pace on a real pickup, and a screenshot at the start and after 2:00 (clock jumped).
# Output: plan/v5/balance-recheck/playable/ (steps.txt, *.png, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\balance-recheck\playable"
New-Item -ItemType Directory -Force $out | Out-Null

$tmp = Join-Path $env:TEMP "v5-recheck-eval.cs"
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
    Start-Sleep -Seconds 3
    Shot "01-run-start.png"
    $lines += "config: " + (Eval @'
var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var c = g.Config;
var d = c.Single<RoguePirates.V4.Config.DifficultyCurveConfig>();
return "xpPickupMultiplier=" + c.Single<RoguePirates.V4.Config.PickupConfig>().xpPickupMultiplier
    + " shipSpeedMultiplier=" + c.Single<RoguePirates.V4.Config.RunConfig>().shipSpeedMultiplier
    + " evolutionUnlockLevel=" + c.Single<RoguePirates.V4.Config.ProgressionConfig>().evolutionUnlockLevel
    + " enemyHpPerMinute.c1=" + d.enemyHpPerMinute.c1 + " ordinaryHp.c1=" + d.ordinaryHp.c1 + " ordinaryDamage.c1=" + d.ordinaryDamage.c1
    + " onTrackGrowth(10)=" + c.Single<RoguePirates.V4.Config.DirectorConfig>().onTrackGrowth.Evaluate(10f)
    + " lightning.baseDamage=" + c.All<RoguePirates.V4.Config.WeaponConfig>().First(w => w.weaponId == "lightning_mast").baseDamage;
'@)
    $lines += "ship: " + (Eval 'var s = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>(); return "RunSpeedMultiplier=" + s.RunSpeedMultiplier + " MoveSpeedMultiplier=" + s.MoveSpeedMultiplier.ToString("0.###");')
    # A real pickup: 2 XP dropped on the ship should pay 1 (when no level-up is due in between).
    $lines += "xp pickup: " + (Eval @'
var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var p = g.Build.Progression;
var before = p.CurrentXp; var level = p.Level; var toNext = p.XpToNext;
if (toNext - before <= 1) return "skipped: a level-up is due (" + before + "/" + toNext + ")";
var ship = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().transform.position;
g.Build.SpawnKillDrops(ship, 2, 0);
return "dropped 2 at level " + level + " with " + before + "/" + toNext;
'@)
    Start-Sleep -Seconds 4
    $lines += "xp after: " + (Eval 'var p = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>().Build.Progression; return "level " + p.Level + " xp " + p.CurrentXp + "/" + p.XpToNext;')
    Eval 'RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock(120f); return 1;' | Out-Null
    for ($k = 0; $k -lt 6; $k++) { Start-Sleep -Seconds 2; Eval 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); else run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); } return 1;' | Out-Null }
    Shot "02-after-2min.png"
    $lines += "2:00 " + (Eval 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); return "level=" + g.Build.Progression.Level + " weapons=" + g.Build.Progression.OwnedWeapons.Count + " enemies=" + UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsSortMode.None).Count(t => t.name.StartsWith("Enemy"));')
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
