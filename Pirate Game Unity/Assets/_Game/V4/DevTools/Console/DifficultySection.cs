using RoguePirates.V4.Config;
using RoguePirates.V4.Difficulty;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Difficulty (M10–M11): the director's live Pressure with its parts (offense, defense, performance), a graph of the
    /// last three minutes, the mode (Off / Observe / Adaptive), what each adaptive lever is doing now, and a test-only
    /// "force Pressure" slider (a forced run is never ranked). The curves, gains, clamps, lever switches and ranges are
    /// edited live in the Config section (difficulty.*, director.*).
    /// </summary>
    public sealed class DifficultySection : IConsoleSection
    {
        public string Title => "Difficulty";
        public string SearchText => "difficulty director pressure power offense defense performance curve adaptive observe lever elite threat tier force ranked";

        const float Width = 440f, GraphHeight = 90f;
        GUIStyle _wrap;
        static Texture2D _pixel;

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            if (game?.Context == null || !game.Context.TryGet<IPressureView>(out var view)) { GUILayout.Label("No V4 difficulty director in this scene."); return; }
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var curve = game.Config?.Single<DifficultyCurveConfig>();
            var min = curve != null ? curve.pressureMin : 0.85f;
            var max = curve != null ? curve.pressureMax : 1.35f;

            GUILayout.Label($"Pressure {view.Pressure:0.000}  (band {min:0.00}–{max:0.00})  ·  mode {view.Mode}  ·  {view.Version}", _wrap, GUILayout.Width(Width));
            GUILayout.Label($"Offense {view.Offense:0.0} dps-eq (×{view.OffenseNorm:0.00} of what the curve expects)  ·  defense ×{view.DefenseNorm:0.00}  ·  performance ×{view.Performance:0.00}", _wrap, GUILayout.Width(Width));
            if (view.Mode == DirectorMode.Observe) GUILayout.Label("Observe: Pressure is logged only; the levers stay neutral.", _wrap, GUILayout.Width(Width));
            if (game.Context.TryGet<IDifficultyLevers>(out var levers))
            {
                var spawn = game.Context.TryGet<RoguePirates.V4.Spawning.ISpawnController>(out var sc) ? sc : null;
                GUILayout.Label($"Levers{(levers.Adaptive ? "" : " (neutral: not Adaptive)")}: threat rate ×{levers.ThreatRateMultiplier:0.00}  ·  tier lead {levers.TierLeadSeconds:0}s  ·  " +
                    $"cap HP ×{levers.CapHpMultiplier:0.00}  ·  composition {(levers.CompositionActive ? "on" : "off")} (close-range build {view.CloseRangeShare:P0})" +
                    (spawn != null ? $"  ·  ambient rate now ×{spawn.EffectiveRateMultiplier:0.00}" : ""), _wrap, GUILayout.Width(Width));
                if (levers is DifficultyDirector dd && levers.Config != null && levers.Config.killBandLever)
                    GUILayout.Label($"Kill band: last band killed {(dd.LastBandRatio < 0f ? "-" : dd.LastBandRatio.ToString("P0"))} of its spawns  ·  this band: rate ×{dd.KillBandRate:0.00}, elite chance {dd.KillBandEliteChance:P0}", _wrap, GUILayout.Width(Width));
            }

            var rect = GUILayoutUtility.GetRect(Width, GraphHeight, GUILayout.Width(Width));
            DrawGraph(rect, view, min, max);

            GUILayout.BeginHorizontal();
            var forced = view.ForcedPressure.HasValue;
            var force = GUILayout.Toggle(forced, "Force Pressure (test only)", GUILayout.Width(170));
            var value = view.ForcedPressure ?? view.Pressure;
            if (force) value = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(180));
            GUILayout.Label(force ? value.ToString("0.00") : "", GUILayout.Width(40));
            GUILayout.EndHorizontal();
            if (force) view.ForcedPressure = value; else if (forced) view.ForcedPressure = null;

            var director = game.Config?.Single<DirectorConfig>();
            if (director == null) return;
            GUILayout.BeginHorizontal();
            foreach (DirectorMode mode in System.Enum.GetValues(typeof(DirectorMode)))
            {
                GUI.enabled = director.mode != mode;
                if (GUILayout.Button(mode.ToString(), GUILayout.Width(90)))
                {
                    var t = RoguePirates.V4.Foundation.Core.RunManager.Instance != null ? RoguePirates.V4.Foundation.Core.RunManager.Instance.ElapsedSeconds : 0f;
                    if (!game.Config.TryApplyEdit(director.configId, "mode", mode.ToString(), t, out var error)) Debug.LogWarning("Difficulty: " + error);
                }
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }

        static void DrawGraph(Rect rect, IPressureView view, float min, float max)
        {
            if (_pixel == null) { _pixel = new Texture2D(1, 1); _pixel.SetPixel(0, 0, Color.white); _pixel.Apply(); }
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(rect, _pixel);
            float Y(float p) => rect.yMax - Mathf.InverseLerp(min, max, p) * rect.height;
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(rect.x, Y(1f), rect.width, 1f), _pixel); // Pressure 1
            var history = view.History;
            GUI.color = new Color(1f, 0.75f, 0.3f, 1f);
            var n = history.Count;
            for (var i = 0; i < n; i++)
            {
                var x = rect.x + (i + (180 - n)) / 180f * rect.width;
                GUI.DrawTexture(new Rect(x, Y(history[i]) - 1f, Mathf.Max(1f, rect.width / 180f), 2f), _pixel);
            }
            GUI.color = old;
        }
    }
}
