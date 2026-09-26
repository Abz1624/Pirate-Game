# M6 automated playable check (V4SaltwindRun): the V4 presenters show the run. Screenshots of the HUD with a full
# loadout (weapons, items, logbook), the minimap and beacon guide once the beacon is revealed, a Captain-tier spawn
# warning (toast, edge arrow, minimap ping), the aim overlay, weapon effects, the minimap's boss/miniboss glyphs with
# the creation probe running (R-86), and the results screen after death. Console errors must be 0.
param([string] $OutDir = 'plan/v4-verification/phase-06-presentation/playable-check')
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

$pickCard = 'var modal = HighSeas.UI.LevelUpModal.Instance; var card = modal != null ? modal.transform.Find("CardRow/Card0") : null; if (HighSeas.Core.RunManager.Instance.IsPaused && card != null && card.gameObject.activeInHierarchy) card.GetComponent<UnityEngine.UI.Button>()?.onClick.Invoke();'
$root = 'var root = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.Presentation.V4PresentationRoot>();'
$world = 'var world = UnityEngine.Object.FindFirstObjectByType<HighSeas.Enemies.EnemyWorld>(); var ship = UnityEngine.Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>();'
$spawn = "$world if (world.ActiveCount < 8) { var shark = System.Array.FindIndex(world.Definitions, d => d != null && d.id == `"shark`"); var f = ship.MovementForward.sqrMagnitude > 0.01f ? ship.MovementForward.normalized : UnityEngine.Vector3.forward; var r = UnityEngine.Vector3.Cross(UnityEngine.Vector3.up, f); foreach (var o in new[] { f * 6f, f * 11f, r * 7f, -r * 7f, -f * 5f, f * 8f + r * 3f }) world.Spawn(shark, ship.Position + o, HighSeas.Core.EliteAffix.None, 0.01f, 40f); }"
$text = 'System.Func<string, string> T = p => { var g = UnityEngine.GameObject.Find(p); var c = g != null ? g.GetComponent("TextMeshProUGUI") : null; return c != null ? (string)c.GetType().GetProperty("text").GetValue(c) : "(none)"; };'

unity command set_autotick --enable true 2>&1 | Out-Null
unity command clear_console 2>&1 | Out-Null
try {
    Begin 'Assets/_Game/Scenes/V4/V4SaltwindRun.unity'
    WaitFor 'run-start' "$root var rm = HighSeas.Core.RunManager.Instance; if (root == null || root.Hud == null || rm == null || rm.ElapsedSeconds < 4f) return `"WAIT`"; return `"OK run playing, presenters bound`";" | Out-Null
    Eval 'god-mode' "$world ship.Damage.SetPlaytestInvulnerable(true); return `"invulnerable for the check`";" | Out-Null
    Eval 'v3-views' "return `$`"V3 HUD {UnityEngine.Object.FindObjectsByType<HighSeas.V3.V3AuthoredGameplayHud>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length}, results {UnityEngine.Object.FindObjectsByType<HighSeas.V3.V3RunResultsScreen>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length}, minimap {UnityEngine.Object.FindObjectsByType<HighSeas.V3.V3MinimapHud>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length}, guide {UnityEngine.Object.FindObjectsByType<HighSeas.V3.V3BossBeaconGuide>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length}, visualizer {UnityEngine.Object.FindObjectsByType<HighSeas.Combat.WeaponTargetingVisualizer>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None).Length}, juice enabled {UnityEngine.Object.FindFirstObjectByType<HighSeas.VFX.WeaponJuiceController>()?.enabled}`";" | Out-Null
    Shot 'hud-start'

    # Full loadout: six weapons (one evolved), four items, a logbook.
    Eval 'loadout' "var rec = UnityEngine.Object.FindFirstObjectByType<HighSeas.Weapons.WeaponController>(); rec.EnsureWeaponSlotCapacity(6); var ids = new[] { `"deck_cannons`", `"broadside_battery`", `"powder_kegs`", `"flying_cutlasses`", `"lightning_mast`", `"mortar`" }; rec.SetWeaponLoadout(ids); HighSeas.UI.LevelSystem.Instance.SetPlaytestWeaponLoadout(ids); var inv = HighSeas.V3.V3RunItemInventory.Instance; foreach (var i in new[] { `"gunners_gloves`", `"captains_bell`", `"admirals_ledger`", `"reinforced_keel`", `"gunners_gloves`" }) inv?.TryAddItem(i); return `$`"weapons set; items {inv?.Items.Count}`";" | Out-Null
    for ($k = 0; $k -lt 8; $k++) { Eval 'spawn' "$pickCard $spawn return `"`";" | Out-Null; Start-Sleep -Milliseconds 500 }
    Shot 'hud-loadout-and-effects'
    Eval 'loadout-hud' "$text var row = UnityEngine.GameObject.Find(`"Canvas/Weapon_Slot`").transform; var labels = new System.Collections.Generic.List<string>(); for (var i = 0; i < row.childCount; i++) { var t = row.GetChild(i).Find(`"Weapon/Lvl_txt`"); var c = t != null ? t.GetComponent(`"TextMeshProUGUI`") : null; if (c != null && row.GetChild(i).gameObject.activeSelf) labels.Add(((string)c.GetType().GetProperty(`"text`").GetValue(c)).Replace(`"\n`", `" `")); } return `$`"weapons [{string.Join(`"; `", labels)}] timer {T(`"Canvas/TImer/Text (TMP)`")} gold {T(`"Canvas/Coin_Count/Text (TMP)`")} {T(`"Canvas/Kill_Count/Text (TMP)`")}`";" | Out-Null
    Eval 'effects' "$root var fx = root.WeaponEffects; return `$`"effects live {fx.LiveTimedCount}, kegs/skiffs {fx.LiveHandleCount}, blades {fx.BladeCount}, pools created after prewarm {root.Pools.TotalCreatedAfterPrewarm}`";" | Out-Null

    # Aim overlay (O key in V3 and V4).
    Eval 'overlay-on' 'HighSeas.Combat.CombatDebugOverlays.SetVisible(true); return "overlays on";' | Out-Null
    Start-Sleep -Seconds 1
    Shot 'aim-overlay'
    Eval 'overlay-off' 'HighSeas.Combat.CombatDebugOverlays.SetVisible(false); return "overlays off";' | Out-Null

    # Beacon discovered: the minimap marker, the screen arrow and the skylight.
    Eval 'beacon' "var d = HighSeas.V3.V3DiscoverySystem.Instance; typeof(HighSeas.V3.V3DiscoverySystem).GetField(`"_beaconState`", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(d, HighSeas.V3.BeaconRevealState.Discovered); return `"beacon revealed`";" | Out-Null
    Start-Sleep -Seconds 1
    Shot 'beacon-guide'
    Eval 'beacon-state' "$root $text return `$`"arrow {root.BeaconGuide.ArrowVisible}, skylight {root.BeaconGuide.SkylightVisible}, quest '{T(`"Canvas/Quest/View/Content/Quest_Slot (1)/Text (TMP)`")}'`";" | Out-Null

    # A Captain-tier ambient spawn off screen: toast, edge arrow, minimap ping.
    Eval 'spawn-warning' "$root $world var captain = System.Array.FindIndex(world.Definitions, d => d != null && HighSeas.Balance.EnemyTierLogic.ResolveTier(d) == HighSeas.Core.EnemyTier.Captain); var pos = ship.Position + UnityEngine.Vector3.right * 45f; root.SpawnWarnings.Notify(pos, captain); return `$`"notified tier-Captain def {captain}: warnings {root.SpawnWarnings.WarningsShown}, pings {root.Minimap.ActiveSpawnPings}`";" | Out-Null
    Start-Sleep -Milliseconds 300
    Shot 'spawn-warning'

    # R-86: a miniboss in an explored cell; count legacy objects created per second.
    Eval 'miniboss' "$world var crab = System.Array.FindIndex(world.Definitions, d => d != null && d.isMiniboss); world.Spawn(crab, ship.Position + ship.MovementForward.normalized * 12f, HighSeas.Core.EliteAffix.None, 0.01f, 40f); var probe = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Diagnostics.SceneObjectCreationProbe>(); probe.ResetCounts(); probe.SetTracking(true); return `$`"miniboss def {crab} spawned; probe on`";" | Out-Null
    for ($k = 0; $k -lt 8; $k++) { Eval 'spawn' "$pickCard return `"`";" | Out-Null; Start-Sleep -Milliseconds 700 }
    Shot 'minimap-miniboss'
    Eval 'probe' "var probe = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.DevTools.Diagnostics.SceneObjectCreationProbe>(); var top = string.Join(`", `", System.Linq.Enumerable.Select(probe.Top(6), kv => kv.Key + `" `" + kv.Value)); var r = `$`"legacy creations {probe.Total} in {probe.TrackedSeconds:0} s ({probe.RatePerSecond:0.0}/s now): {top}`"; probe.SetTracking(false); return r;" | Out-Null

    # The juice controller is switched off but V3 hellfire patches and enemy blasts still draw through it (until M8).
    Eval 'juice-still-works' "$world var root = UnityEngine.GameObject.Find(`"Systems/VFXRoot`"); var before = root != null ? root.transform.childCount : -1; var p = ship.Position + ship.MovementForward.normalized * 6f; HighSeas.Combat.SaltwindCombatEffects.PlayExplosion(p, 3f); UnityEngine.Object.FindFirstObjectByType<HighSeas.VFX.WeaponJuiceController>().OnBurningOilDropped(p + UnityEngine.Vector3.right * 3f, 2.4f); var after = root != null ? root.transform.childCount : -1; var live = UnityEngine.Object.FindObjectsByType<UnityEngine.ParticleSystem>(UnityEngine.FindObjectsSortMode.None).Length; return `$`"VFXRoot children {before} -> {after}; live particle systems {live}`";" | Out-Null
    Start-Sleep -Milliseconds 300
    Shot 'juice-blast-and-oil'

    # Death: the results screen.
    Eval 'die' "$world ship.Damage.SetPlaytestInvulnerable(false); ship.Damage.TakeDamage(99999f, UnityEngine.Vector3.zero, false); return `"ship sunk`";" | Out-Null
    WaitFor 'results' "$root if (root == null || root.Results == null || !root.Results.IsShowing) return `"WAIT`"; return `"OK results showing: `" + root.Results.HeadlineText;" 15 | Out-Null
    Start-Sleep -Milliseconds 500
    Shot 'results'
    Eval 'results-text' "$root return root.Results.SummaryText;" | Out-Null
}
catch { $log.Add("FAILED: $_"); Write-Output "FAILED: $_" }
finally {
    $errors = unity command console --level error --tail 50 2>&1 | Out-String
    Set-Content (Join-Path $out 'console-errors.txt') $errors -Encoding utf8
    unity command editor_stop 2>&1 | Out-Null
    Start-Sleep -Seconds 3
    Set-Content (Join-Path $out 'steps.log') $log -Encoding utf8
}
