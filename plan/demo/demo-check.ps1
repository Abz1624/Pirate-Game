# Demo (30 Sep 2026) automated check of the art team's integration in the open Editor: boot -> the art main menu ->
# Captain select (a locked Captain, then Rook) -> Set Sail -> the run with the SaltWind HUD, level-up cards, landmarks
# and VFX-profile previews, with screenshots and every console error collected.
# Output: plan/demo/evidence/ (steps.txt, *.png, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\demo\evidence"
New-Item -ItemType Directory -Force $out | Out-Null

$tmp = Join-Path $env:TEMP "demo-eval.cs"
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
$original = Eval 'return RoguePirates.V4.Progression.CaptainChoice.LegacyId.ToString();'
unity command clear_console | Out-Null
unity command open_scene --path "Assets/_Game/Scenes/V4/V4Boot.unity" | Out-Null
Start-Sleep -Seconds 2
unity command editor_play | Out-Null

# 1. Main menu.
$menu = 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>(); return c != null && c.ArtView != null ? "yes" : "no";'
if (-not (WaitFor $menu)) { $lines += "ART MENU NEVER SHOWED: " + (Eval 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>(); return c == null ? "no controller" : c.View != null ? "generated carousel" : "no view";') }
else {
    Start-Sleep -Seconds 2
    Shot "01-main-menu.png"
    $lines += "main menu: art panel shown"
    # 2. Set Sail -> Captain select.
    $lines += "set sail: " + (Eval 'var b = UnityEngine.GameObject.Find("SetSail").GetComponent<UnityEngine.UI.Button>(); b.onClick.Invoke(); var v = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>().ArtView; return v.OnCaptainSelect ? "captain select open" : "still on main menu";')
    Start-Sleep -Seconds 2
    Shot "02-captain-select.png"
    # 3. A locked Captain (the last slot that shows a lock), then Rook.
    $lines += "locked: " + (Eval @'
foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Toggle>(UnityEngine.FindObjectsSortMode.None))
{
    var lockGo = t.transform.Find("LockOverlay");
    if (!t.name.StartsWith("CharacterSlot_") || lockGo == null || !lockGo.gameObject.activeSelf) continue;
    t.isOn = true;
    var label = t.transform.root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
    foreach (var l in label) if (l.name == "V4Label" && l.gameObject.activeInHierarchy) return t.name + " -> " + l.text;
    return t.name + " -> (no label)";
}
return "none locked";
'@)
    Start-Sleep -Seconds 1
    Shot "03-locked-captain.png"
    $lines += "rook: " + (Eval 'var t = UnityEngine.GameObject.Find("CharacterSlot_Rook").GetComponent<UnityEngine.UI.Toggle>(); t.isOn = true; return "choice=" + RoguePirates.V4.Progression.CaptainChoice.LegacyId;')
    Start-Sleep -Seconds 1
    Shot "04-rook.png"
    Eval 'UnityEngine.GameObject.Find("SetSailButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
}

# 4. The run.
$playing = 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing ? "yes" : "no";'
if (-not (WaitFor $playing 60)) { $lines += "RUN NEVER STARTED" }
else {
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return 1;' | Out-Null
    Start-Sleep -Seconds 4
    $lines += "hud: " + (Eval 'var r = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); return r.SaltWindHud != null && r.SaltWindHud.Hud != null && r.SaltWindHud.Hud.activeInHierarchy ? "SaltWind HUD shown" : "NO SALTWIND HUD";')
    Shot "05-run-start.png"
    $lines += "landmarks: " + (Eval 'var names = new System.Collections.Generic.List<string>(); foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(UnityEngine.FindObjectsSortMode.None)) if (t.name == "GildedWreck" || t.name == "CursedIdol" || t.name == "MerchantSkiff" || t.name == "SalvageChest") names.Add(t.name + (t.GetComponentInChildren<UnityEngine.MeshRenderer>() != null ? "(mesh)" : "")); names.Sort(); return string.Join(" ", names);')
    # A level-up (cards with the art team's rarity backgrounds).
    Eval 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); g.Build.Pickups.AttractAllXp(); return 1;' | Out-Null
    Eval 'RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock(60f); return 1;' | Out-Null
    $levelUp = 'var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); return m != null && m.IsShowing ? "yes" : "no";'
    for ($k = 0; $k -lt 10; $k++) { Eval 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); g.Build.Pickups.AttractAllXp(); return 1;' | Out-Null; if ((Eval $levelUp) -eq "yes") { break }; Start-Sleep -Seconds 2 }
    if ((Eval $levelUp) -eq "yes") { Start-Sleep -Seconds 1; Shot "06-level-up.png"; $lines += "level-up cards shown"; Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>().Pick(0); return 1;' | Out-Null }
    else { $lines += "no level-up within the wait" }
    Start-Sleep -Seconds 3
    Shot "07-hud-1min.png"
    # VFX profiles: the boss's tornado spawn and fire death, the kraken's whirlpool, previewed at the ship.
    $vfx = @'
var r = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>();
var before = r.VfxProfiles.Played;
r.VfxProfiles.Preview(RoguePirates.V4.Foundation.Data.VfxSubject.Enemy, "barnacle_king", RoguePirates.V4.Foundation.Data.VfxSlotKind.Spawn);
r.VfxProfiles.Preview(RoguePirates.V4.Foundation.Data.VfxSubject.Enemy, "kraken_spawn", RoguePirates.V4.Foundation.Data.VfxSlotKind.Spawn);
return "played " + (r.VfxProfiles.Played - before);
'@
    $lines += "vfx preview: " + (Eval $vfx)
    Start-Sleep -Seconds 1
    Shot "08-vfx-tornado-whirlpool.png"
    $lines += "vfx in play: " + (Eval 'var r = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); return "profile effects played so far " + r.VfxProfiles.Played;')
    Eval 'RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock(180f); return 1;' | Out-Null
    for ($k = 0; $k -lt 5; $k++) { Start-Sleep -Seconds 2; Eval 'var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); else run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); } return 1;' | Out-Null }
    Shot "09-hud-3min.png"
    $lines += "3:00 " + (Eval 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); var r = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); return "level=" + g.Build.Progression.Level + " weapons=" + g.Build.Progression.OwnedWeapons.Count + " profileFx=" + r.VfxProfiles.Played;')
}
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
Eval "RoguePirates.V4.Progression.CaptainChoice.LegacyId = $original; return 1;" | Out-Null
if ((Eval $dirtyCheck) -eq "dirty") { Eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, UnityEditor.SceneManagement.OpenSceneMode.Single); return 1;' | Out-Null; $lines += "the scene this check dirtied was reloaded from disk" }
$lines | Set-Content (Join-Path $out "steps.txt")
unity command console --tail 400 --level error | Out-String | Set-Content (Join-Path $out "console-errors.txt")
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api|Main thread operation timed out)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 10 }
