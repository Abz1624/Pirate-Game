# V5 M8 menu check: boot -> menu in the open Editor; grant 1,000 RUM in the save (restored after), open the shop, buy the
# first Logbook for sale, open the mode select, sail a Voyage and start the run; screenshots at desktop (1920x1080) and
# phone (2400x1080) sizes; the save is restored exactly afterwards. Output: plan/v5/phase-08-menu/menu-check/.
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-08-menu\menu-check"
New-Item -ItemType Directory -Force $out | Out-Null
$tmp = Join-Path $env:TEMP "v5-menu-check.cs"
function Eval([string]$code) {
    Set-Content -Path $tmp -Value $code -Encoding UTF8
    $r = unity command eval_file --file $tmp 2>&1 | Out-String
    if ($r -match '"result":"([^"]*)"') { return $Matches[1] } else { return $r.Trim() }
}
function Shot([string]$name) {
    foreach ($size in @(@(1920, 1080, "desktop"), @(2400, 1080, "phone"))) {
        $shot = unity command capture_game_view --source screen --width $size[0] --height $size[1] --timeout 60 2>$null | Out-String
        if ($shot -match '"base64":"([^"]+)"') { [IO.File]::WriteAllBytes((Join-Path $out "$name-$($size[2]).png"), [Convert]::FromBase64String($Matches[1])) }
    }
}
function WaitFor([string]$code, [int]$tries = 40) {
    for ($i = 0; $i -lt $tries; $i++) { Start-Sleep -Seconds 2; if ((Eval $code) -eq "yes") { return $true } }
    return $false
}
$lines = @()
unity command clear_console | Out-Null
unity command open_scene --path "Assets/_Game/Scenes/V4/V4Boot.unity" | Out-Null
Start-Sleep -Seconds 2
unity command editor_play | Out-Null
$menu = 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>(); return c != null && c.Hub != null ? "yes" : "no";'
if (-not (WaitFor $menu)) { $lines += "MENU (HUB) NEVER SHOWED" }
else {
    $lines += "before: " + (Eval 'UnityEngine.PlayerPrefs.SetString("v5_menu_check_save", UnityEngine.PlayerPrefs.GetString("highseas_meta_v1", "")); var s = RoguePirates.V4.Foundation.Core.AppRoot.Instance.Save; var before = s.Meta.marks; s.Meta.marks = before + 1000; s.Save(); return "RUM " + before + " -> " + s.Meta.marks;')
    Shot "1-menu"
    $lines += "shop: " + (Eval 'var h = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>().Hub; h.ShowShop(RoguePirates.V4.Config.StoreCategory.Logbook); return h.RowCount + " Logbooks for sale";')
    Start-Sleep -Seconds 1
    Shot "2-shop-logbooks"
    $bought = Eval 'var h = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>().Hub; RoguePirates.V5.Meta.StoreEntry first = null; foreach (var e in h.Meta.Entries(RoguePirates.V4.Config.StoreCategory.Logbook)) if (!h.Meta.Owns(e.ContentId)) { first = e; break; } if (first == null) return "none"; var r = h.Buy(first.ContentId); return first.ContentId + " " + r + " (" + h.LastMessage + "), RUM now " + h.Meta.Balance;'
    $lines += "buy: $bought"
    Start-Sleep -Seconds 1
    Shot "3-bought"
    $id = ($bought -split ' ')[0]
    Eval 'var h = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>().Hub; h.ShowCollection(); return 1;' | Out-Null
    Start-Sleep -Seconds 1
    Shot "4-collection"
    Eval 'var h = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>().Hub; h.ShowModes(); return 1;' | Out-Null
    Start-Sleep -Seconds 1
    Shot "5-modes"
    Eval 'var h = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>().Hub; h.Close(); var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>(); RoguePirates.V4.RunRequest.RequestSeed(20261001); UnityEngine.GameObject.Find("SetSail").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
    Start-Sleep -Seconds 2
    Shot "6-mode-select-from-set-sail"
    Eval 'var h = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>().Hub; h.Close(); var v = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Menu.ArtMenuView>(); if (v != null) v.OpenCaptainSelect(); return 1;' | Out-Null
    Start-Sleep -Seconds 1
    Eval 'UnityEngine.GameObject.Find("SetSailButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
    $playing = 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing ? "yes" : "no";'
    if (WaitFor $playing 60) {
        $lines += "in the run, the bought Logbook is owned (so offered): " + (Eval ('return RoguePirates.V4.Foundation.Progression.ContentUnlocks.IsContentOwned("' + $id + '").ToString();'))
        Shot "7-run"
    } else { $lines += "RUN NEVER STARTED" }
}
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
# Restore the save exactly as it was (raw, after play mode: the check's RUM and its purchase are undone).
$lines += "restore: " + (Eval 'var b = UnityEngine.PlayerPrefs.GetString("v5_menu_check_save", ""); if (string.IsNullOrEmpty(b)) { UnityEngine.PlayerPrefs.DeleteKey("highseas_meta_v1"); } else UnityEngine.PlayerPrefs.SetString("highseas_meta_v1", b); UnityEngine.PlayerPrefs.DeleteKey("v5_menu_check_save"); UnityEngine.PlayerPrefs.Save(); return "save restored";')
$lines | Set-Content (Join-Path $out "steps.txt")
unity command console --tail 300 --level error 2>$null | Out-String | Set-Content (Join-Path $out "console-errors.txt")
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api|Main thread operation timed out)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 10 }
