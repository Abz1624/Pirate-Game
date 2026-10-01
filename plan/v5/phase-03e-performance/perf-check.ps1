# V5 M3e performance check: boot -> menu -> Set Sail -> the run in the open Editor (Game view 1280x720). The run clock is
# jumped to 1:00, 5:00 and 9:00 (the scripted arenas skipped, so a jump does not open them and pause spawning); at each
# the ship (invulnerable, cards answered, standing) lets the crowd build for 12 s, then RunPerfSampler records 8 s: frame
# time, main thread, GC bytes per frame, batches, draw calls, SetPass, triangles. At 5:00 the profiler also records ~3 s
# and V5GcProfile lists the markers that allocate and take time (gc-profile-<label>.txt).
# Usage: powershell -File plan/v5/phase-03e-performance/perf-check.ps1 -Label before|after
param([string]$Label = "before", [string]$ProbeFile = "", [string]$SetupFile = "", [int]$Seed = 20261001, [switch]$Profile, [switch]$Shots)
$ErrorActionPreference = "Continue"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$env:UNITY_PROJECT_PATH = Join-Path $root "Pirate Game Unity"
$out = Join-Path $root "plan\v5\phase-03e-performance"
$tmp = Join-Path $env:TEMP "v5-m3e-eval.cs"
function Eval([string]$code) {
    Set-Content -Path $tmp -Value $code -Encoding UTF8
    $r = unity command eval_file --file $tmp 2>&1 | Out-String
    if ($r -match '"result":"([^"]*)"') { return $Matches[1] } else { return $r.Trim() }
}
function Shot([string]$name) {
    $dir = Join-Path $out "playable"
    New-Item -ItemType Directory -Force $dir | Out-Null
    $shot = unity command capture_game_view --source screen --width 1280 --height 720 --timeout 60 2>$null | Out-String
    if ($shot -match '"base64":"([^"]+)"') { [IO.File]::WriteAllBytes((Join-Path $dir $name), [Convert]::FromBase64String($Matches[1])) }
}
function WaitFor([string]$code, [int]$tries = 40) {
    for ($i = 0; $i -lt $tries; $i++) { Start-Sleep -Seconds 2; if ((Eval $code) -eq "yes") { return $true } }
    return $false
}
$lines = @("# $Label ($(Get-Date -Format 'yyyy-MM-dd HH:mm'), commit $(git -C $root rev-parse --short HEAD), seed $Seed)")
$dirtyCheck = 'var d = false; for (var i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) d |= UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty; return d ? "dirty" : "clean";'
if ((Eval $dirtyCheck) -eq "dirty") { Write-Output "UNSAVED SCENES: save or discard them in the Editor first"; exit 5 }
unity command clear_console | Out-Null
unity command open_scene --path "Assets/_Game/Scenes/V4/V4Boot.unity" | Out-Null
Start-Sleep -Seconds 2
unity command editor_play | Out-Null
$menu = 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4MainMenuController>(); return c != null && c.ArtView != null ? "yes" : "no";'
if (-not (WaitFor $menu)) { $lines += "MENU NEVER SHOWED" }
else {
    if ($Seed -ne 0) { Eval ("RoguePirates.V4.RunRequest.RequestSeed(" + $Seed + "); return 1;") | Out-Null } # the same map every time
    Eval 'UnityEngine.GameObject.Find("SetSail").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
    Start-Sleep -Seconds 2
    Eval 'UnityEngine.GameObject.Find("SetSailButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); return 1;' | Out-Null
}
$playing = 'var r = RoguePirates.V4.Foundation.Core.RunManager.Instance; return r != null && r.State == RoguePirates.V4.Foundation.Core.RunState.Playing ? "yes" : "no";'
$answer = 'var m = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.Hud.LevelUpPresenter>(); if (m != null && m.IsShowing) { m.Pick(0); return "picked"; } return "live";'
if (-not (WaitFor $playing 60)) { $lines += "RUN NEVER STARTED" }
else {
    Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Foundation.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); var go = new UnityEngine.GameObject("RunPerfSampler"); go.AddComponent<RoguePirates.V4.DevTools.Diagnostics.RunPerfSampler>(); var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.Spawning.ISpawnController spawn; if (g.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out spawn)) foreach (var p in spawn.Phases) if (p.PhaseId == "reef_crab" || p.PhaseId.StartsWith("elite_")) spawn.Skip(p.PhaseId); return 1;' | Out-Null
    $lines += "run seed: " + (Eval 'return RoguePirates.V4.Foundation.Core.RunManager.Instance.RunSeed.ToString();')
    if ($SetupFile) { $lines += "setup: " + (Eval (Get-Content $SetupFile -Raw)) }
    foreach ($t in @(60, 300, 540)) {
        Eval ("var run = RoguePirates.V4.Foundation.Core.RunManager.Instance; if (run.ElapsedSeconds < " + $t + "f) run.JumpClock(" + $t + "f); return 1;") | Out-Null
        $start = Get-Date
        while (((Get-Date) - $start).TotalSeconds -lt 12) { Eval $answer | Out-Null; Start-Sleep -Milliseconds 700 }
        Eval 'UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Diagnostics.RunPerfSampler>().Begin(); return 1;' | Out-Null
        $start = Get-Date
        $paused = 0
        while (((Get-Date) - $start).TotalSeconds -lt 8) { if ((Eval $answer) -eq "picked") { $paused++ }; Start-Sleep -Milliseconds 700 }
        # The sampler stops before the profiler starts (starting the profiler stalls a frame for seconds).
        $alive = Eval 'var g = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); return g.Shell.Enemies.Simulation.ActiveCount.ToString();'
        $sum = Eval 'var s = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Diagnostics.RunPerfSampler>(); s.End(); return s.Summary();'
        if ($t -eq 300 -and $Profile) {
            Eval 'RoguePirates.V4.Editor.V5GcProfile.Start(); return 1;' | Out-Null
            Start-Sleep -Seconds 3
            $profPath = (Join-Path $out "gc-profile-$Label.txt").Replace('\', '/')
            Eval ('System.IO.File.WriteAllText("' + $profPath + '", RoguePirates.V4.Editor.V5GcProfile.Report(40)); return 1;') | Out-Null
        }
        if ($ProbeFile -and $t -eq 540) { Eval (Get-Content $ProbeFile -Raw) | Out-Null }
        if ($Shots) { Shot ("$Label-$([int]($t / 60))m00.png") }
        $lines += "- $([int]($t / 60)):00 alive $alive (cards answered while sampling: $paused): $sum"
    }
}
$pools = Eval 'var root = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); if (root == null || root.Pools == null) return "no pools"; var parts = new System.Collections.Generic.List<string>(); foreach (var p in root.Pools.Stats()) if (p.CreatedAfterPrewarm > 0) parts.Add(p.Key + " " + p.CreatedAfterPrewarm); return "pool instances created after prewarm: " + root.Pools.TotalCreatedAfterPrewarm + (parts.Count > 0 ? " (" + string.Join(", ", parts) + ")" : "");'
$lines += "- $pools"
$mem = Eval 'return "memory: total allocated " + (UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576) + " MB, graphics driver " + (UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver() / 1048576) + " MB, mono heap " + (UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576) + " MB";'
$lines += "- $mem"
unity command console --tail 300 --level error 2>$null | Out-String | Set-Content (Join-Path $out "console-$Label.txt")
unity command console --tail 400 2>$null | Out-String | Set-Content (Join-Path $env:TEMP "v5-console-all-$Label.txt")
unity command editor_stop | Out-Null
Start-Sleep -Seconds 3
if ((Eval $dirtyCheck) -eq "dirty") { Eval 'UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Game/Scenes/V4/V4Boot.unity", UnityEditor.SceneManagement.OpenSceneMode.Single); return 1;' | Out-Null }
Add-Content -Path (Join-Path $out "perf.md") -Value ($lines + "") -Encoding UTF8
$lines | ForEach-Object { Write-Output $_ }
