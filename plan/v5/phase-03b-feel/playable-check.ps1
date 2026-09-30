# V5 M3b playable check (1 Oct 2026): boot -> menu -> Set Sail -> the run in the open Editor, then the ability button
# with touch controls forced on, and a real island hit (bounce and hull), with screenshots and the console's errors.
# Output: plan/v5/phase-03b-feel/playable/ (steps.txt, *.png, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-03b-feel\playable"
New-Item -ItemType Directory -Force $out | Out-Null

$tmp = Join-Path $env:TEMP "v5-m3b-eval.cs"
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
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(false); return 1;' | Out-Null
    # Answer any level-up cards first (the first level comes within seconds; the button takes no touch while paused).
    for ($k = 0; $k -lt 5; $k++) { Eval 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); } return 1;' | Out-Null; Start-Sleep -Milliseconds 400 }
    # 1. The ability button on a (simulated) touch device.
    Eval 'RoguePirates.V4.Foundation.Platform.VirtualJoystick.ForceTouchControls = true; return 1;' | Out-Null
    Start-Sleep -Seconds 2
    $lines += "button: " + (Eval 'var r = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); var b = r.CaptainAbilityButton; return b == null ? "missing" : "shown=" + b.IsShown + " stickArt=" + b.HasStickArt + " shade=" + b.CooldownShade.ToString("0.00");')
    Shot "01-ability-button-ready.png"
    # Rook's All Guns! waits for a target in range: put a shark in front of the ship first.
    $lines += "target: " + (Eval 'var ship = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>(); var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); var world = g.Shell.Enemies.Simulation; var shark = System.Array.FindIndex(world.Definitions, d => d != null && d.id == "shark"); world.Spawn(shark, ship.Position + ship.MovementForward.normalized * 6f, RoguePirates.V4.Foundation.Core.EliteAffix.None, 0.01f, 50f); return "shark in range: " + g.Build.AnyEnemyInWeaponRange();')
    $lines += "press: " + (Eval 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; for (var i = 0; i < 10 && run.IsPaused; i++) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); } var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>().Build.Captain; var before = c.ActiveUses; RoguePirates.V4.Foundation.Platform.TouchAbilityButton.Press(); return "uses before " + before + " paused " + run.IsPaused;')
    Start-Sleep -Milliseconds 300
    $lines += "after press: " + (Eval 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>().Build.Captain; var b = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>().CaptainAbilityButton; return "uses " + c.ActiveUses + " cooldown " + c.CooldownRemaining.ToString("0.0") + "s shade " + b.CooldownShade.ToString("0.00");')
    Shot "02-ability-button-cooling.png"
    # 2. A real island hit.
    $lines += "island: " + (Eval @'
var ship = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>();
UnityEngine.Collider best = null;
foreach (var c in UnityEngine.Object.FindObjectsByType<UnityEngine.Collider>(UnityEngine.FindObjectsSortMode.None))
    if (c.enabled && !c.isTrigger && c.gameObject.layer == RoguePirates.V4.Foundation.World.IslandObstacles.Layer && (best == null || (c.bounds.center - ship.transform.position).sqrMagnitude < (best.bounds.center - ship.transform.position).sqrMagnitude)) best = c;
if (best == null) return "no island";
var center = best.bounds.center;
var from = center + UnityEngine.Vector3.forward * (best.bounds.extents.magnitude + 20f); from.y = ship.transform.position.y;
var dir = center - from; dir.y = 0f;
UnityEngine.RaycastHit hit;
if (!UnityEngine.Physics.Raycast(from, dir.normalized, out hit, dir.magnitude + 50f, RoguePirates.V4.Foundation.World.IslandObstacles.Mask)) return "no shore";
var n = hit.normal; n.y = 0f; n.Normalize();
ship.PlaceForTest(hit.point + n * (ship.IslandCollisionRadius + 3f), -n * 12f);
return "placed; hull " + ship.Damage.CurrentHP.ToString("0.0") + " impacts " + ship.IslandImpactCount;
'@)
    # Wait for the hit (answering any level-up cards, which pause the ship), then capture the bounce.
    for ($k = 0; $k -lt 20; $k++) {
        $st = Eval 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); } return UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().IslandImpactCount > 0 ? "hit" : "no";'
        if ($st -eq "hit") { break }
        Start-Sleep -Milliseconds 150
    }
    Shot "03-island-bounce.png"
    $lines += "after hit: " + (Eval 'var s = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>(); RoguePirates.V4.Spawning.IPlayerDamageSink sink; UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>().Context.TryGet<RoguePirates.V4.Spawning.IPlayerDamageSink>(out sink); return "hull " + s.Damage.CurrentHP.ToString("0.0") + " impacts " + s.IslandImpactCount + " damaging " + s.IslandDamageCount + " impact hull " + ((RoguePirates.V4.Spawning.PlayerDamageService)sink).ImpactDamageTotal.ToString("0.0") + " speed " + s.Velocity.magnitude.ToString("0.0");')
    Eval 'RoguePirates.V4.Foundation.Platform.VirtualJoystick.ForceTouchControls = false; return 1;' | Out-Null
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
