# M7 automated playable check (V4SaltwindRun): stage content without POIManager. The ship is moved to a chest (the
# Take/Skip choice pauses the run; Take buys an item), a Repair Wreck (hull restored), a Signal Buoy (charges, then a
# reward), a Powder Cache (ambush of 3 through the V4 encounter executor) and the Beacon (hold, telegraph, boss); the
# Barnacle King attacks through the V4 director, is killed (objective secured, run continues), then the ship dies
# (results). Screenshots at each step. Console errors must be 0.
param([string] $OutDir = 'plan/v4-verification/phase-07-boss/playable-check')
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
function WaitFor([string] $name, [string] $code, [int] $seconds = 60) {
    for ($i = 0; $i -lt $seconds; $i++) {
        $v = Eval $name $code
        if ($v -match '^OK') { return $v }
        Start-Sleep -Seconds 1
    }
    throw "timeout waiting for $name"
}
function Begin([string] $scene) {
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        $playing = @(Eval 'is-playing' 'return UnityEditor.EditorApplication.isPlaying ? "OK playing" : "no";')[-1]
        if ($playing -match '^OK') { return }
        Eval 'begin' "return RoguePirates.V4.Editor.V4TraceSession.BeginPlayableCheck(`"$scene`");" | Out-Null
        Start-Sleep -Seconds 15
    }
}

$ctx = 'var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.Stage.IStageContent stage = null; game.Context.TryGet(out stage); var ship = UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>(); var layout = stage.Module.Layout; var rm = HighSeas.Core.RunManager.Instance;'
$pickCard = 'var modal = HighSeas.UI.LevelUpModal.Instance; var card = modal != null ? modal.transform.Find("CardRow/Card0") : null; if (HighSeas.Core.RunManager.Instance.IsPaused && HighSeas.Core.RunManager.Instance.PauseReason != HighSeas.Core.PauseReason.POIChoice && card != null && card.gameObject.activeInHierarchy) card.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();'
function MoveTo([string] $name, [string] $type, [float] $dx) {
    Eval $name "$ctx var i = layout.pois.FindIndex(p => p.type == HighSeas.Core.POIType.$type && !p.used); if (i < 0) return `"none`"; ship.SetWorldPosition(stage.Module.MarkerPosition(i) + new UnityEngine.Vector3($dx, 0f, 0f)); return `$`"moved to $type {i}`";" | Out-Null
}

unity command set_autotick --enable true 2>&1 | Out-Null
unity command clear_console 2>&1 | Out-Null
try {
    Begin 'Assets/_Game/Scenes/V4/V4SaltwindRun.unity'
    WaitFor 'run-start' "$ctx if (rm == null || rm.ElapsedSeconds < 3f) return `"WAIT`"; return `"OK playing`";" | Out-Null
    Eval 'setup' "$ctx ship.Damage.SetPlaytestInvulnerable(true); rm.AddGold(500); var counts = new System.Collections.Generic.Dictionary<HighSeas.Core.POIType,int>(); foreach (var p in layout.pois) counts[p.type] = counts.TryGetValue(p.type, out var n) ? n + 1 : 1; return `$`"POIs: {string.Join(`", `", System.Linq.Enumerable.Select(counts, kv => kv.Key + `" `" + kv.Value))}; markers {stage.Module.MarkerOrder.Count}; POIManager {UnityEngine.Object.FindFirstObjectByType<HighSeas.POI.POIManager>() != null}; barrels {layout.explosiveBarrelPositions.Count}`";" | Out-Null
    Shot 'stage-start'

    MoveTo 'to-chest' 'SalvageChest' 2
    WaitFor 'chest-pause' "$ctx return rm.PauseReason == HighSeas.Core.PauseReason.POIChoice ? `"OK paused for the chest choice, target `" + stage.PromptTarget : `"WAIT`";" 10 | Out-Null
    Shot 'chest-prompt'
    Eval 'chest-take' "$ctx var before = HighSeas.V3.V3RunItemInventory.Instance.Items.Count; var gold = rm.Gold; stage.Module.TryCompleteInteraction(stage.PromptTarget); return `$`"items {before} -> {HighSeas.V3.V3RunItemInventory.Instance.Items.Count}, gold {gold} -> {rm.Gold}, chests bought {stage.PaidChestCount}`";" | Out-Null
    Start-Sleep -Milliseconds 400
    Shot 'chest-opened'

    Eval 'hurt' "$ctx ship.Damage.SetPlaytestInvulnerable(false); ship.Damage.TakeDamage(ship.Damage.MaxHP * 0.5f, UnityEngine.Vector3.zero, false); ship.Damage.SetPlaytestInvulnerable(true); return `"hull `" + ship.Damage.CurrentHP;" | Out-Null
    MoveTo 'to-wreck' 'GildedWreck' 1
    Start-Sleep -Seconds 1
    Eval 'wreck' "$ctx var i = layout.pois.FindIndex(p => p.type == HighSeas.Core.POIType.GildedWreck); return `$`"wreck used {(i >= 0 && layout.pois[i].used)}, hull {ship.Damage.CurrentHP:0}/{ship.Damage.MaxHP:0}`";" | Out-Null
    Shot 'repair-wreck'

    MoveTo 'to-buoy' 'TrialBuoy' 1
    for ($k = 0; $k -lt 4; $k++) { Eval 'pick' "$pickCard return `"`";" | Out-Null; Start-Sleep -Seconds 1 }
    Shot 'buoy-charging'
    for ($k = 0; $k -lt 6; $k++) { Eval 'pick' "$pickCard $ctx var i = layout.pois.FindIndex(p => p.type == HighSeas.Core.POIType.TrialBuoy); if (i >= 0 && !layout.pois[i].used) ship.SetWorldPosition(layout.pois[i].position + new UnityEngine.Vector3(1f, 0f, 0f)); return `"`";" | Out-Null; Start-Sleep -Seconds 1 }
    Eval 'buoy' "$ctx var i = layout.pois.FindIndex(p => p.type == HighSeas.Core.POIType.TrialBuoy); return `$`"buoy used {(i >= 0 && layout.pois[i].used)}, charge {stage.Module.SignalBuoyCharge(i):0.0} s`";" | Out-Null
    Shot 'buoy-reward'

    MoveTo 'to-cache' 'MerchantSkiff' 4
    Start-Sleep -Seconds 1
    Eval 'cache' "$ctx var id = stage.Module.PowderCacheEncounterId; var rec = HighSeas.Encounters.RunEncounterTracker.Get(id); return `$`"ambush encounter {id}: spawned {rec?.SpawnedCount}, state {rec?.State}`";" | Out-Null
    Shot 'powder-cache-ambush'

    MoveTo 'to-beacon' 'BossBeacon' 3
    Eval 'hold' "$ctx RoguePirates.V4.Stage.IInteractionTouch touch = null; game.Context.TryGet(out touch); touch.TouchHolding = true; return `"holding USE`";" | Out-Null
    Start-Sleep -Milliseconds 900
    Shot 'beacon-hold-telegraph'
    Eval 'release' "$ctx RoguePirates.V4.Stage.IInteractionTouch touch = null; game.Context.TryGet(out touch); touch.TouchHolding = false; return `"released`";" | Out-Null
    WaitFor 'boss-summoned' "$ctx return stage.BossSummoned ? `"OK boss summoned, state `" + rm.State : `"WAIT`";" 10 | Out-Null
    for ($k = 0; $k -lt 6; $k++) { Eval 'pick' "$pickCard return `"`";" | Out-Null; Start-Sleep -Seconds 1 }
    Shot 'boss-fight'
    Eval 'boss-attacks' "$ctx RoguePirates.V4.Stage.IBossView boss = null; game.Context.TryGet(out boss); return `$`"boss attack {boss.CurrentAttack}, phase {boss.Phase}, script phase {boss.ScriptPhase}, marked telegraph {boss.MarkedTelegraphVisible}`";" | Out-Null
    Eval 'boss-kill' "$ctx var world = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); if (!world.TryFindActiveBoss(out var slot, out var agent)) return `"no boss`"; world.ApplyDamage(slot, agent.maxHp + 500f, false, weaponId: `"deck_cannons`"); return `"boss hit for its full health`";" | Out-Null
    WaitFor 'boss-defeated' "$ctx return stage.BossDefeated ? `$`"OK defeated; objective {rm.ObjectiveSecured}; state {rm.State}; run ended {rm.IsRunEnded}`" : `"WAIT`";" 10 | Out-Null
    Start-Sleep -Milliseconds 300
    Shot 'boss-defeated'

    Eval 'die' "$ctx ship.Damage.SetPlaytestInvulnerable(false); ship.Damage.TakeDamage(99999f, UnityEngine.Vector3.zero, false); return `"ship sunk`";" | Out-Null
    WaitFor 'results' "var root = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); return root != null && root.Results != null && root.Results.IsShowing ? `"OK results: `" + root.Results.HeadlineText : `"WAIT`";" 15 | Out-Null
    Shot 'results'
    Eval 'results-text' 'var root = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>(); return root.Results.SummaryText;' | Out-Null
}
catch { $log.Add("FAILED: $_"); Write-Output "FAILED: $_" }
finally {
    $errors = unity command console --level error --tail 50 2>&1 | Out-String
    Set-Content (Join-Path $out 'console-errors.txt') $errors -Encoding utf8
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    Set-Content (Join-Path $out 'steps.log') $log -Encoding utf8
}
