# M3 automated playable check: V4SaltwindRun -> ambient spawns come from the V4 spawn controller -> Wave Timeline
# console section -> Trigger now on the Reef Crab (arena on) -> kill it (arena off, ambient resumes) -> Skip the elite
# hammerhead -> live band 0 rate edit raises spawns -> duplicate a phase from the console. Log + screenshots.
param([string] $OutDir = 'plan/v4-verification/phase-03-timeline/playable-check')
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
# Starting Play Mode can time out while the Editor is still busy (after a recompile or import): retry until it plays.
for ($attempt = 0; $attempt -lt 6; $attempt++) {
    $playing = Eval 'is-playing' 'return UnityEditor.EditorApplication.isPlaying ? "OK playing" : "no";'
    if ($playing -match '^OK') { break }
    Eval 'begin' 'return RoguePirates.V4.Editor.V4TraceSession.BeginPlayableCheck("Assets/_Game/Scenes/V4/V4SaltwindRun.unity");' | Out-Null
    Start-Sleep -Seconds 15
}
try {
    WaitFor 'run-start' "$spawnCtl var rm = HighSeas.Core.RunManager.Instance; if (rm == null || s == null || rm.ElapsedSeconds < 8f) return `"WAIT`"; var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var legacy = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EncounterSpawner>(); return `$`"OK t={rm.ElapsedSeconds:0.0} alive={w.ActiveCount} phase={s.CurrentPhaseLabel} legacyAmbientEntries={legacy.Schedule.entries.Length} rate={s.EffectiveRateMultiplier}`";" | Out-Null

    Eval 'console' @'
var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();
var collapsed = (System.Collections.Generic.HashSet<string>)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_collapsed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c);
collapsed.Add("Run"); collapsed.Add("Config");
c.SetOpen(true);
return "console open on Wave Timeline";
'@ | Out-Null
    Start-Sleep -Seconds 2; Shot '01-wave-timeline'

    Eval 'trigger-reef' "$spawnCtl var ok = s.TriggerNow(`"reef_crab`"); return `$`"trigger reef_crab: {ok}`";" | Out-Null
    WaitFor 'reef-arena' "$spawnCtl if (!s.ArenaActive) return `"WAIT`"; var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); w.TryGetAgentByUnitId(s.ArenaFocusUnitId, out var i); return `$`"OK arena on, focus {s.ArenaFocusUnitId} = {w.Definitions[w.Agents[i].definitionIndex].id}, hp {w.Agents[i].hp:0}`";" 20 | Out-Null
    Start-Sleep -Seconds 1; Shot '02-reef-crab-arena'
    Eval 'arena-blocks' "$spawnCtl var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); return `$`"alive during arena: {w.ActiveCount}`";" | Out-Null
    Start-Sleep -Seconds 3
    Eval 'arena-still' "$spawnCtl var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var reef = System.Linq.Enumerable.First(s.Phases, p => p.PhaseId == `"reef_crab`"); return `$`"after 3 s: alive {w.ActiveCount}, reef_crab {reef.State}`";" | Out-Null
    Eval 'kill-reef' "$pickCard $spawnCtl var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); w.TryGetAgentByUnitId(s.ArenaFocusUnitId, out var i); var a = w.Agents[i]; w.ApplyDamage(i, UnityEngine.Mathf.Max(a.hp, a.maxHp) + 500f, false, weaponId: `"deck_cannons`"); return `"reef crab killed`";" | Out-Null
    WaitFor 'arena-off' "$pickCard $spawnCtl if (s.ArenaActive) return `"WAIT`"; var reef = System.Linq.Enumerable.First(s.Phases, p => p.PhaseId == `"reef_crab`"); return `$`"OK arena off, reef_crab {reef.State}`";" 10 | Out-Null

    Eval 'skip-elite' "$spawnCtl var ok = s.Skip(`"elite_hammerhead`"); var e = System.Linq.Enumerable.First(s.Phases, p => p.PhaseId == `"elite_hammerhead`"); return `$`"skip elite_hammerhead: {ok}, state {e.State}`";" | Out-Null

    Eval 'rate-before' "var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var max = 0; foreach (var a in w.Agents) if (a.active && a.id > max) max = a.id; UnityEngine.PlayerPrefs.SetInt(`"v4_check_uid`", max); return `$`"newest unit {max}`";" | Out-Null
    for ($k = 0; $k -lt 5; $k++) { Eval 'unpause' "$pickCard return HighSeas.Core.RunManager.Instance.IsPaused.ToString();" | Out-Null; Start-Sleep -Seconds 1 }
    Eval 'rate-edit' @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>();
var max = 0; foreach (var a in w.Agents) if (a.active && a.id > max) max = a.id;
var before = max - UnityEngine.PlayerPrefs.GetInt("v4_check_uid");
UnityEngine.PlayerPrefs.SetInt("v4_check_uid", max);
var ok = game.Config.TryApplyEdit("timeline.saltwind", "phases[0].ratePerSecond", "3", HighSeas.Core.RunManager.Instance.ElapsedSeconds, out var error);
return $"spawns in ~5 s before: {before}; band_0 rate -> 3/s: {ok} {error}";
'@ | Out-Null
    for ($k = 0; $k -lt 5; $k++) { Eval 'unpause' "$pickCard return HighSeas.Core.RunManager.Instance.IsPaused.ToString();" | Out-Null; Start-Sleep -Seconds 1 }
    Eval 'rate-after' "var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var max = 0; foreach (var a in w.Agents) if (a.active && a.id > max) max = a.id; var after = max - UnityEngine.PlayerPrefs.GetInt(`"v4_check_uid`"); UnityEngine.PlayerPrefs.DeleteKey(`"v4_check_uid`"); return `$`"spawns in ~5 s after the edit: {after} (unit ids issued, cap permitting)`";" | Out-Null

    Eval 'duplicate' @'
var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();
var sections = (System.Collections.IList)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_sections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c);
object tl = null; foreach (var s in sections) if (s.GetType().Name == "TimelineSection") tl = s;
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var timeline = System.Linq.Enumerable.OfType<RoguePirates.V4.Config.WaveTimelineConfig>(game.Config.Configs).First();
var before = timeline.phases.Count;
var status = (string)tl.GetType().GetMethod("Duplicate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(tl, new object[] { game.Config, timeline, 2, HighSeas.Core.RunManager.Instance.ElapsedSeconds });
var last = timeline.phases[timeline.phases.Count - 1];
return $"phases {before} -> {timeline.phases.Count}: {status}; copy spawner={(last.spawner != null ? last.spawner.configId : "none")}, rate={last.ratePerSecond}";
'@ | Out-Null
    Start-Sleep -Seconds 2; Shot '03-after-edits'
}
finally {
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    $errs = unity command console --tail 200 --level error 2>&1 | Out-String
    $m = [regex]::Match($errs, '"returned":(\d+)')
    $log.Add("[console] errors returned=$($m.Groups[1].Value)")
    Set-Content -Path (Join-Path $out 'console-errors.txt') -Value $errs -Encoding utf8
    Set-Content -Path (Join-Path $out 'steps.log') -Value $log -Encoding utf8
    Write-Output "[console] errors returned=$($m.Groups[1].Value)"
}
