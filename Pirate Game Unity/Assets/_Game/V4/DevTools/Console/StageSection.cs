using HighSeas.Core;
using HighSeas.Enemies;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Views;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Landmarks, Beacon and Boss (M7): stage state (chests bought, Beacon reveal, boss) and actions: reveal the Beacon,
    /// summon the boss (the B key does the same in dev builds, R-55), set the boss's health. The numbers live in the Config
    /// section (<c>stage.saltwind</c>, <c>boss.barnacle_king</c>).
    /// </summary>
    public sealed class StageSection : IConsoleSection
    {
        public string Title => "Landmarks, Beacon & Boss";
        public string SearchText => "stage landmarks beacon boss barnacle king chest powder cache signal buoy repair wreck treasure chart summon";

        const float TextWidth = 440f;
        GUIStyle _wrap;
        string _hpPercent = "31";
        string _last = string.Empty;

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            if (game == null || game.Context == null || !game.Context.TryGet<IStageContent>(out var stage))
            {
                GUILayout.Label("No V4 stage content in this scene.");
                return;
            }
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };

            var boss = game.Context.TryGet<IBossView>(out var bossView) ? bossView : null;
            var view = stage as IStageView;
            GUILayout.Label($"Chests bought {stage.PaidChestCount} · Beacon {(view != null ? view.Beacon.ToString() : "?")} · boss {(stage.BossDefeated ? "defeated" : stage.BossSummoned ? "summoned" : "not summoned")}", _wrap, GUILayout.Width(TextWidth));
            if (boss != null && stage.BossSummoned && !stage.BossDefeated)
                GUILayout.Label($"Boss attack {boss.CurrentAttack} · {boss.Phase} · script phase {boss.ScriptPhase}", _wrap, GUILayout.Width(TextWidth));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Reveal Beacon", GUILayout.Width(120))) { stage.RevealBeacon(); _last = "Beacon revealed"; }
            if (GUILayout.Button("Summon boss (B)", GUILayout.Width(130))) _last = stage.TrySummonBoss() ? "Boss summoned" : "Summon refused (see toast)";
            GUILayout.EndHorizontal();

            // M8 playtest: find the landmarks fast (the ship is moved next to the nearest unused one).
            for (var row = 0; row < GoToTargets.Length; row += 3)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(row == 0 ? "Go to" : string.Empty, GUILayout.Width(40));
                for (var i = row; i < row + 3 && i < GoToTargets.Length; i++)
                    if (GUILayout.Button(GoToTargets[i].label, GUILayout.Width(120))) _last = GoTo(game, GoToTargets[i].type);
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Boss health %", GUILayout.Width(90));
            _hpPercent = GUILayout.TextField(_hpPercent, GUILayout.Width(50));
            if (GUILayout.Button("Set", GUILayout.Width(50))) _last = SetBossHealth(game, _hpPercent);
            GUILayout.EndHorizontal();
            if (_last.Length > 0) GUILayout.Label(_last, _wrap, GUILayout.Width(TextWidth));
        }

        static readonly (string label, POIType type)[] GoToTargets =
        {
            ("Powder Cache", POIType.MerchantSkiff),
            ("Beacon", POIType.BossBeacon),
            ("Chest", POIType.SalvageChest),
            ("Repair Wreck", POIType.GildedWreck),
            ("Signal Buoy", POIType.TrialBuoy),
            ("Treasure Chart", POIType.CursedIdol),
        };

        /// <summary>Moves the ship next to the nearest unused landmark of <paramref name="type"/> (in its interaction range).</summary>
        public static string GoTo(V4Game game, POIType type)
        {
            if (game == null || game.Context == null || !game.Context.TryGet<IStageContent>(out var stage)) return "Run not ready";
            var module = stage.Module;
            var ship = Object.FindFirstObjectByType<HighSeas.Player.PlayerShipController>();
            if (module?.Layout == null || ship == null) return "Run not ready";
            var best = -1;
            var bestDistance = float.MaxValue;
            var any = false;
            for (var i = 0; i < module.Layout.pois.Count; i++)
            {
                if (module.Layout.pois[i].type != type) continue;
                any = true;
                if (module.IsConsumed(i) || !module.IsMarkerSpawned(i)) continue;
                var distance = Vector3.Distance(ship.Position, module.MarkerPosition(i));
                if (distance >= bestDistance) continue;
                best = i;
                bestDistance = distance;
            }
            var name = System.Array.Find(GoToTargets, t => t.type == type).label;
            if (!any) return $"No {name} on this map";
            if (best < 0) return $"Every {name} is used";
            var offset = Mathf.Min(2.5f, module.InteractRange(best) * 0.6f);
            ship.SetWorldPosition(module.MarkerPosition(best) + new Vector3(offset, 0f, 0f));
            return $"Moved to {name} {best}{(module.IsHidden(best) ? " (its fight is on)" : string.Empty)}";
        }

        /// <summary>Sets the live boss's health to a percentage of its maximum.</summary>
        static string SetBossHealth(V4Game game, string percentText)
        {
            if (!float.TryParse(percentText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var percent))
                return "Not a number";
            RoguePirates.V4.Enemies.IEnemyWorldView enemies = null;
            RoguePirates.V4.Enemies.IEnemyDevControl dev = null;
            if (game == null || game.Context == null || !game.Context.TryGet(out enemies) || !game.Context.TryGet(out dev)) return "Run not ready";
            for (var i = 0; i < enemies.Capacity; i++)
            {
                if (!enemies.TryGet(i, out var boss) || !boss.isBoss) continue;
                dev.SetHealthFraction(i, percent / 100f);
                enemies.TryGet(i, out boss);
                return $"Boss health set to {boss.hp:0} / {boss.maxHp:0}";
            }
            return "No boss alive";
        }

        /// <summary>The dev-build B key (V3's V3BossSummonHotkey: run live, not paused).</summary>
        public static void PollSummonKey(TuningConsole console)
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null || !keyboard.bKey.wasPressedThisFrame || console.IsOpen) return;
            var run = RunManager.Instance;
            if (run == null || run.IsRunEnded || run.IsPaused) return;
            var game = console.Game;
            if (game == null) return;
            if (game.Context != null && game.Context.TryGet<IStageContent>(out var stage)) stage.TrySummonBoss();
        }
    }
}
