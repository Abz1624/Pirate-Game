# M4 automated playable check: Module Lab pool stress from the F1 Pools section (0 creates after prewarm while
# churning, overflow policies act) -> V4SaltwindRun: the view pool exists and creates nothing, then the creation probe
# records the legacy object-creation baseline for 60 s from 4:00 (the number M14 must bring to 0). Screenshots + log.
param([string] $OutDir = 'plan/v4-verification/phase-04-pools/playable-check')
$ErrorActionPreference = 'Continue'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$env:UNITY_PROJECT_PATH = Join-Path $repo 'Pirate Game Unity'
$out = Join-Path $repo $OutDir
New-Item -ItemType Directory -Force $out | Out-Null
$evalDir = Join-Path $env:TEMP 'v4-playable-eval'
New-Item -ItemType Directory -Force $evalDir | Out-Null
$log = New-Object System.Collections.Generic.List[string]

function Eval([string] $name, [string] $code) {
    $f = Join-Path $evalDir "$name.cs"
    Set-Content -Path $f -Value $code -Encoding utf8
    $r = unity command eval_file --file $f 2>&1 | Out-String
    $m = [regex]::Match($r, '"result":"((?:[^"\\]|\\.)*)"')
    $v = if ($m.Success) { $m.Groups[1].Value -replace '\\n', ' | ' } else { "ERROR: $($r.Trim())" }
    $log.Add("[$name] $v"); Write-Output "[$name] $v"
    return $v
}
function Shot([string] $name) {
    $p = Join-Path $out "$name.png"
    $r = unity command capture_game_view --source screen --format json 2>&1 | Out-String
    $m = [regex]::Match($r, '([A-Za-z0-9+/]{5000,}={0,2})')
    if ($m.Success) { [IO.File]::WriteAllBytes($p, [Convert]::FromBase64String($m.Groups[1].Value)) } else { $log.Add("[shot] FAILED $name") }
    $log.Add("[shot] $name.png"); Write-Output "[shot] $name.png"
}
# Every wait has a limit: a failed step is logged instead of hanging the script.
function WaitFor([string] $name, [string] $code, [int] $seconds = 60) {
    for ($i = 0; $i -lt $seconds; $i++) {
        $v = Eval $name $code
        if ($v -match '^OK') { return $v }
        Start-Sleep -Seconds 1
    }
    throw "timeout waiting for $name"
}

$spawnCtl = 'RoguePirates.V4.Spawning.ISpawnController s = null; UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>()?.Context?.TryGet(out s);'
# Level-ups pause the run (nothing plays the cards in this check): pick the first card, as the trace harness does.
$pickCard = 'var modal = HighSeas.UI.LevelUpModal.Instance; var card = modal != null ? modal.transform.Find("CardRow/Card0") : null; if (HighSeas.Core.RunManager.Instance.IsPaused && card != null && card.gameObject.activeInHierarchy) card.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();'
unity command set_autotick --enable true 2>&1 | Out-Null
unity command clear_console 2>&1 | Out-Null
# Eval also writes its log line to the pipeline: keep only its return value (the last item).
$rectBefore = @(Eval 'rect-before' 'return UnityEngine.PlayerPrefs.HasKey("v4_console_rect") ? "HAS " + UnityEngine.PlayerPrefs.GetString("v4_console_rect") : "NONE";')[-1]
function Begin([string] $scene) {
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        $playing = @(Eval 'is-playing' 'return UnityEditor.EditorApplication.isPlaying ? "OK playing" : "no";')[-1]
        if ($playing -match '^OK') { return }
        Eval 'begin' "return RoguePirates.V4.Editor.V4TraceSession.BeginPlayableCheck(`"$scene`");" | Out-Null
        Start-Sleep -Seconds 15
    }
}
$pools = 'var root = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); var pools = root != null ? root.Pools : null;'
$console = 'var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();'
$showPools = "$console var collapsed = (System.Collections.Generic.HashSet<string>)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField(`"_collapsed`", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c); collapsed.Clear(); foreach (var t in new[] { `"Run`", `"Wave Timeline`", `"Config`", `"Module Lab`" }) collapsed.Add(t); c.SetOpen(true); return `"console open on Pools`";"
$poolStats = "$pools var s = System.Linq.Enumerable.ToList(pools.Stats()); return `$`"OK {s.Count} pools, prewarmed {System.Linq.Enumerable.Sum(s, x => x.Prewarmed)}, in use {System.Linq.Enumerable.Sum(s, x => x.InUse)}, created after prewarm {pools.TotalCreatedAfterPrewarm}, pooled objects {root.transform.Find(`"Pools`").childCount}`";"
try {
    # 1. Module Lab: pool stress
    Begin 'Assets/_Game/Scenes/V4/V4ModuleLab.unity'
    WaitFor 'lab-start' "$pools var rm = HighSeas.Core.RunManager.Instance; if (pools == null || rm == null || rm.ElapsedSeconds < 2f) return `"WAIT`"; $($poolStats.Substring($pools.Length))" | Out-Null
    Eval 'lab-console' $showPools | Out-Null
    Eval 'lab-stress' "$pools $console System.AppDomain.CurrentDomain.SetData(`"v4_stress`", null); var ship = UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>(); var st = new RoguePirates.V4.DevTools.Diagnostics.PoolStressScenario(pools, ship.Position + UnityEngine.Vector3.forward * 12f); c.RunEveryFrame(dt => { if (!st.Tick(dt)) return false; System.AppDomain.CurrentDomain.SetData(`"v4_stress`", st.Summary + `$`" | churnCreates={st.ChurnCreatesAfterPrewarm} recycled={st.Recycled} grown={st.OverflowCreatesAfterPrewarm} rejected={st.Rejected} acquires={st.Acquires}`"); return true; }); return `"stress started`";" | Out-Null
    Start-Sleep -Seconds 3; Shot '01-module-lab-stress-running'
    WaitFor 'lab-stress-done' 'var r = System.AppDomain.CurrentDomain.GetData("v4_stress") as string; return r == null ? "WAIT" : "OK " + r;' 40 | Out-Null
    Start-Sleep -Seconds 1; Shot '02-module-lab-pools-after'
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 5

    # 2. Saltwind run: the pool exists and creates nothing; legacy creation baseline from 4:00
    Begin 'Assets/_Game/Scenes/V4/V4SaltwindRun.unity'
    WaitFor 'run-start' "$pools var rm = HighSeas.Core.RunManager.Instance; if (pools == null || rm == null || rm.ElapsedSeconds < 5f) return `"WAIT`"; UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>().Damage.SetPlaytestInvulnerable(true); $($poolStats.Substring($pools.Length))" | Out-Null
    Eval 'run-console' $showPools | Out-Null
    Eval 'jump-240' "$pickCard var rm = HighSeas.Core.RunManager.Instance; rm.RestoreSuspendSnapshot(new HighSeas.Save.RunSnapshotData { runId = rm.RunId, elapsedSeconds = 240f, gold = rm.Gold, kills = rm.KillCount, objectiveSecured = rm.ObjectiveSecured }); return `"jumped to 240 s`";" | Out-Null
    for ($k = 0; $k -lt 5; $k++) { Eval 'unpause' "$pickCard return `"`";" | Out-Null; Start-Sleep -Seconds 1 }
    Eval 'probe-on' "$console c.CreationProbe.ResetCounts(); c.CreationProbe.SetTracking(true); return `$`"probe on at t={HighSeas.Core.RunManager.Instance.ElapsedSeconds:0}`";" | Out-Null
    $end = (Get-Date).AddSeconds(60)
    while ((Get-Date) -lt $end) { Eval 'unpause' "$pickCard return `"`";" | Out-Null; Start-Sleep -Seconds 2 }
    Shot '03-run-pools-and-creation-probe'
    Eval 'probe-result' "$console $pools var p = c.CreationProbe; var top = string.Join(`"; `", System.Linq.Enumerable.Select(p.Top(12), kv => kv.Value + `" `" + kv.Key)); var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); return `$`"t={HighSeas.Core.RunManager.Instance.ElapsedSeconds:0} enemies {w.ActiveCount} | legacy creation {p.Total} in {p.TrackedSeconds:0} s ({p.Total / UnityEngine.Mathf.Max(1f, p.TrackedSeconds):0.0}/s) | pool created after prewarm {pools.TotalCreatedAfterPrewarm} | top: {top}`";" | Out-Null
    # R-86 check: kill the miniboss/boss (their minimap markers are rebuilt every frame), then measure again.
    Eval 'kill-bosses' "$pickCard var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var n = 0; for (var i = 0; i < w.Agents.Length; i++) { var a = w.Agents[i]; var d = a.active ? w.Definitions[a.definitionIndex] : null; if (d != null && (d.isBoss || d.isMiniboss)) { w.ApplyDamage(i, a.maxHp + a.hp + 9999f, false, weaponId: `"deck_cannons`"); n++; } } return `$`"killed {n} boss/miniboss`";" | Out-Null
    Eval 'probe-reset' "$console c.CreationProbe.ResetCounts(); return `"probe reset`";" | Out-Null
    $end = (Get-Date).AddSeconds(15)
    while ((Get-Date) -lt $end) { Eval 'unpause' "$pickCard return `"`";" | Out-Null; Start-Sleep -Seconds 2 }
    Eval 'probe-no-boss' "$console var p = c.CreationProbe; var top = string.Join(`"; `", System.Linq.Enumerable.Select(p.Top(6), kv => kv.Value + `" `" + kv.Key)); var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var bosses = 0; foreach (var a in w.Agents) { var d = a.active ? w.Definitions[a.definitionIndex] : null; if (d != null && (d.isBoss || d.isMiniboss)) bosses++; } return `$`"without boss/miniboss (alive {bosses}): legacy creation {p.Total} in {p.TrackedSeconds:0} s ({p.Total / UnityEngine.Mathf.Max(1f, p.TrackedSeconds):0.0}/s) | top: {top}`";" | Out-Null
    Eval 'probe-off' "$console c.CreationProbe.SetTracking(false); return `"probe off`";" | Out-Null
}
catch { $log.Add("FAILED: $_"); Write-Output "FAILED: $_" }
finally {
    $errors = unity command console --level error --tail 50 2>&1 | Out-String
    Set-Content (Join-Path $out 'console-errors.txt') $errors -Encoding utf8
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    if ($rectBefore -match '^NONE') { Eval 'rect-restore' 'UnityEngine.PlayerPrefs.DeleteKey("v4_console_rect"); UnityEngine.PlayerPrefs.Save(); return "removed";' | Out-Null }
    elseif ($rectBefore -match '^HAS (.+)$') { $v = $Matches[1]; Eval 'rect-restore' "UnityEngine.PlayerPrefs.SetString(`"v4_console_rect`", `"$v`"); UnityEngine.PlayerPrefs.Save(); return `"restored`";" | Out-Null }
    Set-Content (Join-Path $out 'steps.log') $log -Encoding utf8
}
