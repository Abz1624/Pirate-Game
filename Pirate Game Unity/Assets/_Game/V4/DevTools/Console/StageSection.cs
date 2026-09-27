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

            GUILayout.BeginHorizontal();
            GUILayout.Label("Boss health %", GUILayout.Width(90));
            _hpPercent = GUILayout.TextField(_hpPercent, GUILayout.Width(50));
            if (GUILayout.Button("Set", GUILayout.Width(50))) _last = SetBossHealth(game, _hpPercent);
            GUILayout.EndHorizontal();
            if (_last.Length > 0) GUILayout.Label(_last, _wrap, GUILayout.Width(TextWidth));
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
            if (game == null || game.IsModuleLab) return;
            if (game.Context != null && game.Context.TryGet<IStageContent>(out var stage)) stage.TrySummonBoss();
        }
    }
}
