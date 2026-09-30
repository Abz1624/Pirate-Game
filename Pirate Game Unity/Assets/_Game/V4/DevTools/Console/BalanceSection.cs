using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// V5 M2: the balance model in the console. "Run model" plays the Monte Carlo simulator on a worker thread against a
    /// snapshot of the run's configs (so edits made in the Config section count) for every player personality; the graph
    /// then shows, per minute, the target band, the baseline personality's median (the model) and this run's live value
    /// (enemies alive, kills, level), and the table each personality's end minute and boss win rate against its targets.
    /// </summary>
    public sealed class BalanceSection : IConsoleSection
    {
        public string Title => "Balance";
        public string SearchText => "balance model simulator monte carlo personality target calibration level kills alive ttk report";

        enum Metric { Alive, Kills, Level }

        const float Width = 440f, GraphHeight = 110f;
        static readonly string[] MetricNames = { "Alive", "Kills", "Level" };
        static Texture2D _pixel;
        GUIStyle _wrap;
        Metric _metric;
        int _runs = 40;
        Task<SimBatchResult> _job;
        ConfigSetRuntime _snapshot; // disposed on the main thread when the job ends (Unity objects cannot be destroyed off it)
        SimBatchResult _result;
        string _error;
        CombatEvents _events;
        readonly List<(float alive, int samples, int kills, int level)> _live = new List<(float, int, int, int)>();

        public BalanceSection(V4Game game) => Attach(game);

        void Attach(V4Game game)
        {
            var events = game != null ? game.Events : null;
            if (events == _events) return;
            if (_events != null) _events.TickPublished -= OnTick;
            _events = events;
            _live.Clear();
            if (_events != null) _events.TickPublished += OnTick;
        }

        V4Game _game;

        /// <summary>Live values per minute of this run (alive averaged over the minute's ticks).</summary>
        void OnTick(IReadOnlyList<CombatEvent> batch)
        {
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            if (run == null || _game?.Context == null) return;
            var minute = Mathf.FloorToInt(run.ElapsedSeconds / 60f);
            if (minute < 0 || minute > 60) return;
            if (minute < _live.Count - 1) _live.Clear(); // a new run
            while (_live.Count <= minute) _live.Add((0f, 0, 0, _live.Count > 0 ? _live[_live.Count - 1].level : 0));
            var cur = _live[minute];
            if (_game.Context.TryGet<RoguePirates.V4.Enemies.IEnemyWorldView>(out var enemies)) { cur.alive += enemies.ActiveCount; cur.samples++; }
            for (var i = 0; i < batch.Count; i++)
            {
                if (batch[i].Kind == CombatEventKind.Killed) cur.kills++;
                else if (batch[i].Kind == CombatEventKind.LevelUp) cur.level = Mathf.Max(cur.level, batch[i].A);
            }
            _live[minute] = cur;
        }

        public void Draw(TuningConsole console)
        {
            _game = console.Game;
            Attach(console.Game);
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };
            var config = console.Game?.Config;
            if (config == null || config.Single<BalanceTargetsConfig>() == null || config.Single<PlayerPersonalityConfig>() == null)
            {
                GUILayout.Label("No balance targets or personalities in this config set.", _wrap, GUILayout.Width(Width));
                return;
            }

            // IMGUI draws each frame twice (Layout, then Repaint): the control count must not change between them.
            if (_job != null && _job.IsCompleted && Event.current.type == EventType.Layout)
            {
                if (_job.IsFaulted) _error = _job.Exception?.GetBaseException().Message;
                else _result = _job.Result;
                _job = null;
                _snapshot?.Dispose();
                _snapshot = null;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            GUILayout.Label("The model needs a worker thread: run it in the Editor or a desktop development build.", _wrap, GUILayout.Width(Width));
#else
            GUILayout.BeginHorizontal();
            GUI.enabled = _job == null;
            if (GUILayout.Button(_job == null ? "Run model" : "Running…", GUILayout.Width(100))) Start(config);
            GUI.enabled = true;
            GUILayout.Label("Runs per personality", GUILayout.Width(130));
            if (int.TryParse(GUILayout.TextField(_runs.ToString(), GUILayout.Width(50)), out var runs)) _runs = Mathf.Clamp(runs, 1, 500);
            GUILayout.EndHorizontal();
#endif
            if (_error != null) GUILayout.Label("Model failed: " + _error, _wrap, GUILayout.Width(Width));

            _metric = (Metric)GUILayout.Toolbar((int)_metric, MetricNames, GUILayout.Width(Width));
            var rect = GUILayoutUtility.GetRect(Width, GraphHeight, GUILayout.Width(Width));
            DrawGraph(rect, config.Single<BalanceTargetsConfig>(), config.Single<WaveTimelineConfig>());
            GUILayout.Label("Band = target · orange = model (baseline median) · cyan = this run", _wrap, GUILayout.Width(Width));

            if (_result == null) { GUILayout.Label("Run the model to see the personalities and the pacing checks.", _wrap, GUILayout.Width(Width)); return; }
            GUILayout.Label($"{_result.RunsPerPersonality} runs each · director {_result.DirectorMode} · {_result.Seconds:0.0} s · targets missed {_result.Missed} of {_result.AllChecks.Count()}", _wrap, GUILayout.Width(Width));
            foreach (var p in _result.Personalities)
            {
                var ok = p.Checks.All(c => c.Met);
                GUILayout.Label($"{(ok ? "✓" : "✗")} {p.Personality.displayName}: ends {p.DeathMedian:0.0} min (p10 {p.DeathP10:0.0}, p90 {p.DeathP90:0.0}) · boss {p.BossWinRate:P0} · level 10:00 {(float.IsNaN(p.LevelAt10) ? "–" : p.LevelAt10.ToString("0"))}",
                    _wrap, GUILayout.Width(Width));
            }
            foreach (var c in _result.Pacing) GUILayout.Label((c.Met ? "✓ " : "✗ ") + c, _wrap, GUILayout.Width(Width));
        }

        void Start(ConfigSetRuntime live)
        {
            _error = null;
            // A snapshot of the live copies (Config section edits included); the simulator must not touch the run's own.
            _snapshot = new ConfigSetRuntime(live.SetId, live.Configs);
            var content = new SimContent(_snapshot); // on the main thread: it reads the config hash
            var runs = _runs;
            _job = Task.Run(() => SimBatch.Run(content, runs, 1));
        }

        void DrawGraph(Rect rect, BalanceTargetsConfig targets, WaveTimelineConfig timeline)
        {
            if (_pixel == null) { _pixel = new Texture2D(1, 1); _pixel.SetPixel(0, 0, Color.white); _pixel.Apply(); }
            var old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.35f);
            GUI.DrawTexture(rect, _pixel);
            var cap = timeline != null && timeline.maxActiveEnemies > 0 ? timeline.maxActiveEnemies : 80;
            const int minutes = 15;
            float Value(ModelMinute m) => _metric == Metric.Alive ? m.AliveShare * cap : _metric == Metric.Kills ? m.Kills : m.Level;
            float Live((float alive, int samples, int kills, int level) l) => _metric == Metric.Alive ? (l.samples > 0 ? l.alive / l.samples : 0f) : _metric == Metric.Kills ? l.kills : l.level;
            TargetBand Band(MinuteTargets t) => _metric == Metric.Alive ? new TargetBand(t.aliveShare.min * cap, t.aliveShare.max * cap) : _metric == Metric.Kills ? t.killsPerMinute : t.level;

            var top = 1f;
            foreach (var t in targets.minutes) { var b = Band(t); if (!float.IsNaN(b.max)) top = Mathf.Max(top, b.max); }
            if (_result != null) foreach (var m in _result.Model) top = Mathf.Max(top, Value(m));
            foreach (var l in _live) top = Mathf.Max(top, Live(l));
            float X(int minute) => rect.x + minute / (float)minutes * rect.width;
            float Y(float v) => rect.yMax - Mathf.Clamp01(v / top) * rect.height;
            var w = rect.width / minutes;

            GUI.color = new Color(0.4f, 0.9f, 0.4f, 0.25f);
            foreach (var t in targets.minutes)
            {
                if (t.minute >= minutes) continue;
                var b = Band(t);
                var lo = float.IsNaN(b.min) ? 0f : b.min;
                var hi = float.IsNaN(b.max) ? top : b.max;
                GUI.DrawTexture(new Rect(X(t.minute), Y(hi), w - 1f, Mathf.Max(1f, Y(lo) - Y(hi))), _pixel);
            }
            if (_result != null)
            {
                GUI.color = new Color(1f, 0.7f, 0.25f, 1f);
                foreach (var m in _result.Model)
                    if (m.Minute < minutes && m.RunsReaching > 0) GUI.DrawTexture(new Rect(X(m.Minute) + 2f, Y(Value(m)) - 1f, w * 0.45f, 3f), _pixel);
            }
            GUI.color = new Color(0.3f, 0.9f, 1f, 1f);
            for (var i = 0; i < _live.Count && i < minutes; i++) GUI.DrawTexture(new Rect(X(i) + w * 0.5f, Y(Live(_live[i])) - 1f, w * 0.45f, 3f), _pixel);
            GUI.color = old;
        }
    }
}
