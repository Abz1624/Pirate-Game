# M2 automated playable check: V4ModuleLab -> lab isolation (no spawns, spawner/POIs/interaction off) -> spawn every
# archetype and variant in a ring (screenshot grid) -> elite row from the lab section -> inspector for a specialist ->
# live variant edit reaches the definition -> Retry reloads the lab. Log + screenshots.
param([string] $OutDir = 'plan/v4-verification/phase-02-archetypes/playable-check')
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
function WaitFor([string] $name, [string] $code, [int] $seconds = 40) {
    for ($i = 0; $i -lt $seconds; $i++) {
        $v = Eval $name $code
        if ($v -match '^OK') { return $v }
        Start-Sleep -Seconds 1
    }
    throw "timeout waiting for $name"
}

unity command set_autotick --enable true 2>&1 | Out-Null
unity command clear_console 2>&1 | Out-Null
Eval 'begin' 'return RoguePirates.V4.Editor.V4TraceSession.BeginPlayableCheck("Assets/_Game/Scenes/V4/V4ModuleLab.unity");' | Out-Null
Start-Sleep -Seconds 20
try {
    WaitFor 'lab-start' 'var rm = HighSeas.Core.RunManager.Instance; var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); if (rm == null || game == null || !game.IsRunning || rm.ElapsedSeconds < 6f) return "WAIT"; var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var sp = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EncounterSpawner>(); var poi = UnityEngine.Object.FindFirstObjectByType<HighSeas.POI.POIManager>(); var ic = UnityEngine.Object.FindFirstObjectByType<HighSeas.V3.V3InteractionController>(); return $"OK lab={game.IsModuleLab} t={rm.ElapsedSeconds:0.0} alive={w.ActiveCount} spawner={sp.enabled} poi={poi.enabled} interaction={(ic != null && ic.enabled)}";' | Out-Null
    Eval 'spawn-all' @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>();
var ship = UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>();
var ids = new System.Collections.Generic.List<string>();
for (var i = 0; i < w.Definitions.Length; i++) if (w.Definitions[i] != null && !w.Definitions[i].isBoss) ids.Add(w.Definitions[i].id);
for (var k = 0; k < ids.Count; k++)
{
    var angle = k * UnityEngine.Mathf.PI * 2f / ids.Count;
    var pos = ship.Position + new UnityEngine.Vector3(UnityEngine.Mathf.Cos(angle), 0f, UnityEngine.Mathf.Sin(angle)) * 16f;
    w.Spawn(System.Array.FindIndex(w.Definitions, d => d != null && d.id == ids[k]), pos);
}
return $"spawned {ids.Count}: {string.Join(",", ids)}; alive={w.ActiveCount}";
'@ | Out-Null
    Start-Sleep -Seconds 2; Shot '01-all-enemies'
    Eval 'elite-row' @'
var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();
var sections = (System.Collections.IList)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_sections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c);
object lab = null; foreach (var s in sections) if (s.GetType().Name == "ModuleLabSection") lab = s;
var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>();
var ship = UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>();
var before = w.ActiveCount;
lab.GetType().GetMethod("Spawn", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(lab, new object[] { w, ship, "hammerhead", true });
var elites = 0; foreach (var a in w.Agents) if (a.active && a.isElite) elites++;
return $"elite_hammerhead row: alive {before} -> {w.ActiveCount}, elite agents={elites}";
'@ | Out-Null
    Eval 'inspector' @'
var c = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Console.TuningConsole>();
var sections = (System.Collections.IList)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_sections", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c);
object lab = null; foreach (var s in sections) if (s.GetType().Name == "ModuleLabSection") lab = s;
if (lab == null) return "ERROR no Module Lab section";
lab.GetType().GetField("_selected", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(lab, "needle_sloop");
var collapsed = (System.Collections.Generic.HashSet<string>)typeof(RoguePirates.V4.DevTools.Console.TuningConsole).GetField("_collapsed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(c);
collapsed.Add("Run"); collapsed.Add("Config");
c.SetOpen(true);
return "Module Lab section open, needle_sloop selected";
'@ | Out-Null
    Start-Sleep -Seconds 2; Shot '02-lab-inspector'
    Eval 'live-variant-edit' @'
var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
var w = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>();
var def = System.Array.Find(w.Definitions, d => d != null && d.id == "ram_raider");
var before = def.baseHP;
if (!game.Config.TryApplyEdit("variant.ram_raider", "baseHP.value", "60", HighSeas.Core.RunManager.Instance.ElapsedSeconds, out var error)) return "ERROR " + error;
return $"ram_raider baseHP {before} -> {def.baseHP} (new spawns use it)";
'@ | Out-Null
    Eval 'retry' 'RoguePirates.V4.V4Game.RestartRun(); return "Retry requested";' | Out-Null
    Start-Sleep -Seconds 8
    WaitFor 'retry-scene' 'var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name; if (game == null || !game.IsRunning) return "WAIT " + scene; return $"OK scene={scene} lab={game.IsModuleLab}";' | Out-Null
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
