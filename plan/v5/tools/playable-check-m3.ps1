# V5 M3 automated playable check: one real Play session per Captain in the open Editor (ship invulnerable, cards
# auto-picked, XP pulled to the ship), each using its active and jumping the clock to 1, 3 and 5 minutes, with a
# screenshot at 5:00 and every console error collected. The saved Captain choice is restored at the end.
# Output: plan/v5/phase-03-builds/evidence/playable/ (checkpoints.txt, captain-*.png, console-errors.txt).
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-03-builds\evidence\playable"
New-Item -ItemType Directory -Force $out | Out-Null

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

$original = Eval 'return RoguePirates.V4.Progression.CaptainChoice.LegacyId.ToString();'
$captains = (Eval @'
var set = UnityEditor.AssetDatabase.LoadAssetAtPath<RoguePirates.V4.Config.ConfigSet>("Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset");
return string.Join(",", System.Linq.Enumerable.Select(System.Linq.Enumerable.OrderBy(set.All<RoguePirates.V4.Config.CaptainConfig>(), c => c.menuOrder), c => c.captainKey + ":" + c.legacyId));
'@).Split(",")

$state = @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
game.Context.TryGet<RoguePirates.V4.Spawning.IEnemySpawnSink>(out var sink);
var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
if (run.IsPaused) { var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) m.Pick(0); else run.SetPause(RoguePirates.V4.Foundation.Core.PauseReason.None); }
game.Build?.Pickups.AttractAllXp();
var cap = game.Build?.Captain;
if (cap != null && cap.IsReady) cap.TryActivate();
return $"t={run.ElapsedSeconds:0} captain={cap?.Config?.captainKey} level={game.Build?.Progression.Level} weapons={game.Build?.Progression.OwnedWeapons.Count} actives={cap?.ActiveUses} alive={sink.ActiveCount}";
'@

$lines = @()
# Scenes: refuse to start on unsaved edits (never anyone else's work); reload afterwards only what this check dirtied.
$dirtyCheck = 'var d = false; for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) d |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty; return d ? "dirty" : "clean";'
if ((Eval $dirtyCheck) -eq "dirty") { Write-Output "UNSAVED SCENES: save or discard them in the Editor first"; exit 5 }
unity command clear_console | Out-Null
unity command open_scene --path "Assets/_Game/Scenes/V4/V4SaltwindRun.unity" | Out-Null
Start-Sleep -Seconds 2
unity command editor_play | Out-Null
function Wait-Playing {
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 2
        $s = Eval 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing && r.ElapsedSeconds < 30f ? "playing" : "wait";'
        if ($s -eq "playing") { return $true }
    }
    return $false
}
if (-not (Wait-Playing)) { Write-Output "RUN NEVER STARTED"; unity command editor_stop | Out-Null; exit 2 }
foreach ($entry in $captains) {
    $key, $id = $entry.Split(":")
    # As the console's "restart as": inside Play mode (entering it resets statics), any Captain, locked or not.
    Eval "RoguePirates.V4.Progression.CaptainChoice.LegacyId = $id; RoguePirates.V4.RunRequest.AllowLockedCaptainOnce(); RoguePirates.V4.V4Game.RestartRun(); return 1;" | Out-Null
    Start-Sleep -Seconds 3
    if (-not (Wait-Playing)) { $lines += "$($key): RUN NEVER STARTED"; continue }
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); return 1;' | Out-Null
    foreach ($m in 1, 3, 5) {
        Eval "RoguePirates.V4.Foundation.Core.RunManager.Instance.JumpClock($($m * 60)f); return 1;" | Out-Null
        for ($k = 0; $k -lt 5; $k++) { Start-Sleep -Seconds 2; Eval $state | Out-Null }
        $lines += "$key $($m):00  " + (Eval $state)
    }
    Shot "captain-$key.png"
}
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
Eval "RoguePirates.V4.Progression.CaptainChoice.LegacyId = $original; return 1;" | Out-Null
$lines += "saved Captain choice restored to $original"
if ((Eval $dirtyCheck) -eq "dirty") { Eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, UnityEditor.SceneManagement.OpenSceneMode.Single); return 1;' | Out-Null; $lines += "the scene this check dirtied was reloaded from disk" }
$lines | Set-Content (Join-Path $out "checkpoints.txt")
unity command console --tail 400 --level error | Out-String | Set-Content (Join-Path $out "console-errors.txt")
$lines | ForEach-Object { Write-Output $_ }
$errs = Get-Content (Join-Path $out "console-errors.txt") -Raw
$game = ([regex]::Matches($errs, '"message":"([^"]*)') | ForEach-Object { $_.Groups[1].Value } | Where-Object { $_ -notmatch "^(Command '|Failed to handle /api|Main thread operation timed out)" })
if ($game.Count -eq 0) { Write-Output "console: 0 game errors" } else { Write-Output "console: $($game.Count) game errors (see console-errors.txt)"; $game | Select-Object -First 8 }
