# M5 automated playable check (Module Lab): each of the 10 weapons soloed with enemies around fires from the V4 Weapon
# Manager (ledger emissions, screenshots); a live cooldown edit speeds Deck Cannons up; forcing Stormshot turns its
# shots into lanes with bursts; projectile views come from the pool. Console errors must be 0.
param([string] $OutDir = 'plan/v4-verification/phase-05-weapons/playable-check')
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
$game = 'var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>(); RoguePirates.V4.Weapons.IWeaponManager wm = null; game.Context.TryGet(out wm);'
$spawn = 'var world = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var ship = UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>(); if (world.ActiveCount < 8) { var shark = System.Array.FindIndex(world.Definitions, d => d != null && d.id == "shark"); var f = ship.MovementForward.sqrMagnitude > 0.01f ? ship.MovementForward.normalized : UnityEngine.Vector3.forward; var r = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, f); foreach (var o in new[] { f * 6f, f * 11f, r * 7f, -r * 7f, -f * 5f, f * 8f + r * 3f, f * 8f - r * 3f, r * 12f }) world.Spawn(shark, ship.Position + o, HighSeas.Core.EliteAffix.None, 0.01f, 40f); }'
$emissions = 'System.Func<string, int> em = id => HighSeas.Combat.WeaponDamageLedger.Snapshot().TryGetValue(id, out var s) ? s.emissions : 0;'
try {
    Begin 'Assets/_Game/Scenes/V4/V4ModuleLab.unity'
    WaitFor 'lab-start' "$game var rm = HighSeas.Core.RunManager.Instance; if (wm == null || rm == null || rm.ElapsedSeconds < 2f) return `"WAIT`"; return `$`"OK weapons: {string.Join(`", `", System.Linq.Enumerable.Select(wm.Slots, s => s.Id))}`";" | Out-Null
    Eval 'console' "$($showPools.Replace('collapsed.Add(t);', 'if (t != `"Weapons`") collapsed.Add(t); collapsed.Add(`"Pools`"); collapsed.Add(`"Run`");'))" | Out-Null
    $ids = 'deck_cannons','swivel_guns','long_nine','harpoon_gun','broadside_battery','flying_cutlasses','flamethrower','powder_kegs','lightning_mast','mortar'
    $shots = @{ 'broadside_battery' = 1; 'flying_cutlasses' = 1; 'powder_kegs' = 1; 'lightning_mast' = 1; 'long_nine' = 1; 'mortar' = 1 }
    foreach ($id in $ids) {
        Eval "solo-$id" "$game $emissions RoguePirates.V4.Legacy.LegacyWeaponLab.Solo(game, `"$id`"); UnityEngine.PlayerPrefs.SetInt(`"v4_check_em`", em(`"$id`")); return `"solo $id`";" | Out-Null
        for ($k = 0; $k -lt 6; $k++) { Eval 'spawn' "$pickCard $spawn return `"`";" | Out-Null; Start-Sleep -Milliseconds 500 }
        if ($shots.ContainsKey($id)) { Shot "weapon-$id" }
        Eval "fired-$id" "$game $emissions var before = UnityEngine.PlayerPrefs.GetInt(`"v4_check_em`"); var slot = System.Linq.Enumerable.FirstOrDefault(wm.Slots); return `$`"$id`: {em(`"$id`") - before} emissions in 3 s (slot {slot?.Id}, {slot?.Config?.behavior})`";" | Out-Null
    }
    Eval 'cooldown-before' "$game $emissions RoguePirates.V4.Legacy.LegacyWeaponLab.Solo(game, `"deck_cannons`"); UnityEngine.PlayerPrefs.SetInt(`"v4_check_em`", em(`"deck_cannons`")); return `"deck cannons, default cooldown`";" | Out-Null
    for ($k = 0; $k -lt 6; $k++) { Eval 'spawn' "$pickCard $spawn return `"`";" | Out-Null; Start-Sleep -Milliseconds 500 }
    Eval 'cooldown-edit' "$game $emissions var n = em(`"deck_cannons`") - UnityEngine.PlayerPrefs.GetInt(`"v4_check_em`"); string e; var ok = game.Config.TryApplyEdit(`"weapon.deck_cannons`", `"baseCooldown`", `"0.25`", HighSeas.Core.RunManager.Instance.ElapsedSeconds, out e); UnityEngine.PlayerPrefs.SetInt(`"v4_check_em`", em(`"deck_cannons`")); return `$`"default: {n} in 3 s; edit baseCooldown 0.25: {ok} {e}`";" | Out-Null
    for ($k = 0; $k -lt 6; $k++) { Eval 'spawn' "$pickCard $spawn return `"`";" | Out-Null; Start-Sleep -Milliseconds 500 }
    Eval 'cooldown-after' "$game $emissions string e; var n = em(`"deck_cannons`") - UnityEngine.PlayerPrefs.GetInt(`"v4_check_em`"); game.Config.TryApplyEdit(`"weapon.deck_cannons`", `"baseCooldown`", `"1`", HighSeas.Core.RunManager.Instance.ElapsedSeconds, out e); return `$`"after edit: {n} in 3 s`";" | Out-Null
    Eval 'evolve' "$game RoguePirates.V4.Legacy.LegacyWeaponLab.Evolve(`"deck_cannons`", `"stormshot_cannon`"); return `"deck cannons evolved to Stormshot`";" | Out-Null
    for ($k = 0; $k -lt 6; $k++) { Eval 'spawn' "$pickCard $spawn return `"`";" | Out-Null; Start-Sleep -Milliseconds 500 }
    Shot 'weapon-stormshot'
    Eval 'evolved' "$game var s = System.Linq.Enumerable.First(wm.Slots); HighSeas.Combat.WeaponDamageLedger.Snapshot().TryGetValue(`"deck_cannons`", out var st); return `$`"slot {s.Id} evolution {s.Evolution?.displayName} ({s.EvolutionEffect}); max concurrent targets {st?.maxConcurrentTargetsHit}`";" | Out-Null
    Eval 'pools' "$pools var proj = System.Linq.Enumerable.Where(pools.Stats(), x => x.Key.StartsWith(`"projectile/`")); return string.Join(`", `", System.Linq.Enumerable.Select(proj, x => x.Key + `" peak`" + x.Peak + `" +`" + x.CreatedAfterPrewarm));" | Out-Null
    Eval 'restore-loadout' "$game RoguePirates.V4.Legacy.LegacyWeaponLab.Solo(game, `"swivel_guns`"); UnityEngine.PlayerPrefs.DeleteKey(`"v4_check_em`"); return `"ok`";" | Out-Null
}
catch { $log.Add("FAILED: $_"); Write-Output "FAILED: $_" }
finally {
    $errors = unity command console --level error --tail 50 2>&1 | Out-String
    Set-Content (Join-Path $out 'console-errors.txt') $errors -Encoding utf8
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    Eval 'key-cleanup' 'UnityEngine.PlayerPrefs.DeleteKey("v4_check_em"); UnityEngine.PlayerPrefs.Save(); return "ok";' | Out-Null
    if ($rectBefore -match '^NONE') { Eval 'rect-restore' 'UnityEngine.PlayerPrefs.DeleteKey("v4_console_rect"); UnityEngine.PlayerPrefs.Save(); return "removed";' | Out-Null }
    elseif ($rectBefore -match '^HAS (.+)$') { $v = $Matches[1]; Eval 'rect-restore' "UnityEngine.PlayerPrefs.SetString(`"v4_console_rect`", `"$v`"); UnityEngine.PlayerPrefs.Save(); return `"restored`";" | Out-Null }
    Set-Content (Join-Path $out 'steps.log') $log -Encoding utf8
}
