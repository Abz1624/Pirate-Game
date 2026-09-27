using System.Linq;
using HighSeas.Core;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Build (M9): the run's level-ups, Logbooks, items, floor powerups and Captain — live state and actions (grant a
    /// level or XP, add an item or a Logbook rank, drop a powerup at the ship, use the Captain's active, play as another
    /// Captain). Their numbers (XP curve, offer weights, item effects, powerup magnitudes, Captain passives and actives)
    /// are edited live in the Config section (progression.*, offers.*, logbook.*, item.*, powerups.*, captain.*).
    /// </summary>
    public sealed class BuildSection : IConsoleSection
    {
        /// <summary>Uses the active and says what happened (off, cooling down, or what it does).</summary>
        static string UseActive(RoguePirates.V4.Legacy.LegacyBuildWiring b)
        {
            var captain = b.Captain.Config;
            if (captain?.active == null) return "No Captain active.";
            if (!b.ActivesEnabled()) return "Captain actives are off (ExperimentConfig.captainActivesEnabled; the v3-parity preset turns them off).";
            if (!b.Captain.IsReady) return $"{captain.active.displayName} is cooling down: {b.Captain.CooldownRemaining:0.0} s left.";
            if (!b.Captain.TryActivate()) return $"{captain.active.displayName} did not fire.";
            var a = captain.active;
            var effects = new System.Collections.Generic.List<string>();
            if (a.fireAllWeapons) effects.Add("every weapon fires once at its targets (weapons with nothing in range hold fire)");
            if (a.armPowderKegs) effects.Add("powder kegs arm at once");
            if (a.fireLightningSalvo) effects.Add("a lightning salvo");
            if (a.braceSeconds > 0f) effects.Add($"incoming damage ×{a.braceIncomingDamageMultiplier} for {a.braceSeconds} s");
            if (a.moveSeconds > 0f) effects.Add($"speed ×{a.moveMultiplier} for {a.moveSeconds} s");
            if (a.damageBuffSeconds > 0f) effects.Add($"+{a.damageBuffAdditive * 100f:0}% damage for {a.damageBuffSeconds} s");
            if (a.kegRadiusSeconds > 0f) effects.Add($"keg blast radius ×{a.kegRadiusMultiplier} for {a.kegRadiusSeconds} s");
            if (a.flameSeconds > 0f) effects.Add($"flame range ×{a.flameRangeMultiplier}, burn ×{a.burnDamageMultiplier} for {a.flameSeconds} s");
            if (a.slowSeconds > 0f) effects.Add($"own speed ×{a.slowMultiplier} for {a.slowSeconds} s");
            if (a.pickupRadiusSeconds > 0f) effects.Add($"pickup radius ×{a.pickupRadiusMultiplier} for {a.pickupRadiusSeconds} s{(a.pickupRadiusApplied ? "" : " (off: R-49 switch)")}");
            if (a.nearMissWindowSeconds > 0f) effects.Add($"near misses within {a.nearMissWindowSeconds} s refund {a.nearMissCooldownRefund} s");
            if (effects.Count == 0) effects.Add("no effect configured");
            return $"Used {a.displayName}: {string.Join("; ", effects)}. Cooldown {a.cooldownSeconds:0} s.";
        }

        public string Title => "Build";
        public string SearchText => "build level xp level-up offers cards logbook logbooks item items powerup powerups captain active cooldown dead calm magnetic tide";

        const float TextWidth = 440f;
        GUIStyle _wrap;
        bool _items, _logbooks, _powerups, _captains;
        string _activeResult = string.Empty; // what the last "Use active" did (M9 playtest: the press gave no feedback)

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            var b = game != null ? game.Build : null;
            if (b == null) { GUILayout.Label("No V4 build modules in this scene."); return; }
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var levels = b.Progression;
            var stats = RunManager.Instance != null ? RunManager.Instance.Stats : null;

            GUILayout.Label($"Level {levels.Level}  ·  XP {levels.CurrentXp:0}/{levels.XpToNext}  ·  pending {levels.PendingLevelUps}  ·  refreshes {levels.RefreshesRemaining}", _wrap, GUILayout.Width(TextWidth));
            GUILayout.Label("Weapons: " + string.Join(", ", levels.OwnedWeapons.Select(w => $"{w} lv{levels.WeaponLevel(w)}{(levels.EvolutionOf(w) != null ? " (" + levels.EvolutionOf(w) + ")" : "")}")), _wrap, GUILayout.Width(TextWidth));
            GUILayout.Label("Logbooks: " + (levels.OwnedLogbooks.Count == 0 ? "none" : string.Join(", ", levels.OwnedLogbooks.Select(l => $"{l} r{levels.LogbookLevel(l)}"))), _wrap, GUILayout.Width(TextWidth));
            GUILayout.Label("Items: " + (b.Items.Items.Count == 0 ? "none" : string.Join(", ", b.Items.Items.Select(i => $"{i} ×{b.Items.StackCount(i)}"))), _wrap, GUILayout.Width(TextWidth));
            if (stats != null)
                GUILayout.Label($"Damage {stats.GlobalDamage:0.00}  ·  attack speed {stats.AttackSpeed:0.00}  ·  crit {stats.CritChance:0.00}  ·  luck {stats.Luck:0.00}  ·  move {stats.MoveSpeed:0.00}  ·  pickup {stats.PickupRadius:0.00}  ·  XP gain {stats.XPGain:0.00}", _wrap, GUILayout.Width(TextWidth));
            var captain = b.Captain.Config;
            GUILayout.Label($"Captain {captain?.displayName ?? "none"}  ·  active {captain?.active?.displayName} {(b.Captain.IsReady ? "ready" : $"cooldown {b.Captain.CooldownRemaining:0.0}s")}  ·  incoming damage ×{b.IncomingDamageMultiplier():0.00}", _wrap, GUILayout.Width(TextWidth));
            GUILayout.Label($"Floor powerups {b.Powerups.ActiveCount} on the water ({b.Powerups.SpawnedThisStage} this stage)  ·  Dead Calm ×{b.Powerups.SpawnRateMultiplier:0.00}  ·  pickups {b.Pickups.ActiveCount}/{b.Pickups.Capacity}", _wrap, GUILayout.Width(TextWidth));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1 level", GUILayout.Width(80))) levels.AddXp(Mathf.Max(1, levels.XpToNext - Mathf.FloorToInt(levels.CurrentXp)));
            if (GUILayout.Button("+100 XP", GUILayout.Width(80))) levels.AddXp(100);
            if (GUILayout.Button("Use active", GUILayout.Width(90))) _activeResult = UseActive(b);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_activeResult)) GUILayout.Label(_activeResult, _wrap, GUILayout.Width(TextWidth));

            var ship = Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>();
            _items = GUILayout.Toggle(_items, "Items (add a stack)");
            if (_items)
                foreach (var item in game.Config.All<ItemConfig>().OrderBy(i => i.itemId))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{item.itemId} ({item.rarity}){(item.inChestPool ? "" : " — not in chests")}", GUILayout.Width(300));
                    if (GUILayout.Button("Add", GUILayout.Width(44))) b.Items.TryAddItem(item.itemId);
                    GUILayout.EndHorizontal();
                }
            _logbooks = GUILayout.Toggle(_logbooks, "Logbooks (one Common rank)");
            if (_logbooks)
                foreach (var logbook in game.Config.All<LogbookConfig>().OrderBy(l => l.databaseOrder))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{logbook.logbookId} (stat {(StatResolver.IsStat(logbook.stat) ? ((StatId)logbook.stat).ToString() : "none")}){(logbook.inSaltwindPool ? "" : " — not offered")}", GUILayout.Width(300));
                    if (GUILayout.Button("Rank", GUILayout.Width(44))) levels.DevRankLogbook(logbook.logbookId);
                    GUILayout.EndHorizontal();
                }
            _powerups = GUILayout.Toggle(_powerups, "Floor powerups (drop at the ship)");
            if (_powerups && ship != null)
                foreach (PowerupKind kind in System.Enum.GetValues(typeof(PowerupKind)))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(kind.ToString(), GUILayout.Width(300));
                    if (GUILayout.Button("Drop", GUILayout.Width(44))) b.Powerups.DevSpawn(kind, ship.Position);
                    GUILayout.EndHorizontal();
                }
            _captains = GUILayout.Toggle(_captains, "Captains (restart as)");
            if (_captains)
                foreach (var c in game.Config.All<CaptainConfig>().OrderBy(c => c.menuOrder))
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{c.displayName} — {c.startingWeaponId}, {c.active?.displayName} ({c.active?.cooldownSeconds:0} s)", GUILayout.Width(300));
                    if (GUILayout.Button("Play", GUILayout.Width(44)))
                    {
                        HighSeas.V3.V3CaptainSelection.Selected = (HighSeas.V3.V3CaptainId)c.legacyId;
                        V4Game.RestartRun();
                    }
                    GUILayout.EndHorizontal();
                }
        }
    }
}
