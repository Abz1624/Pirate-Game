using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Events (M6): counts per event kind, a filterable log of the most recent events, and "Export session", which
    /// writes the event log, the tuning change log, the current preset and the config hash to one file.
    /// </summary>
    public sealed class EventsSection : IConsoleSection
    {
        public string Title => "Events";
        public string SearchText => "events event log combat spawned killed hit fired level export session telemetry";

        const int LogCapacity = 400;
        const float TextWidth = 440f;
        static readonly CombatEventKind[] Kinds = (CombatEventKind[])Enum.GetValues(typeof(CombatEventKind));

        readonly long[] _counts = new long[Kinds.Length];
        readonly CombatEvent[] _log = new CombatEvent[LogCapacity];
        readonly bool[] _shown = new bool[Kinds.Length];
        int _logStart, _logCount;
        CombatEvents _events;
        GUIStyle _wrap;
        string _lastExport;
        string _filter = string.Empty;
        bool _showLog = true;

        /// <summary>Subscribes at once, so counts and the log start with the run, not when the console first opens.</summary>
        public EventsSection(V4Game game)
        {
            for (var i = 0; i < _shown.Length; i++) _shown[i] = true;
            Attach(game);
        }

        void Attach(V4Game game)
        {
            var events = game != null ? game.Events : null;
            if (events == _events) return;
            if (_events != null) _events.TickPublished -= OnTick;
            _events = events;
            Array.Clear(_counts, 0, _counts.Length);
            _logStart = _logCount = 0;
            if (_events != null) _events.TickPublished += OnTick;
        }

        void OnTick(IReadOnlyList<CombatEvent> batch)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                _counts[(int)e.Kind]++;
                if (_logCount < LogCapacity) _log[(_logStart + _logCount++) % LogCapacity] = e;
                else { _log[_logStart] = e; _logStart = (_logStart + 1) % LogCapacity; }
            }
        }

        public void Draw(TuningConsole console)
        {
            Attach(console.Game);
            if (_events == null) { GUILayout.Label("No V4 run in this scene."); return; }
            if (_wrap == null) _wrap = new GUIStyle(GUI.skin.label) { wordWrap = true };

            GUILayout.Label($"{_events.TotalPublished} events published this run ({Sum()} counted below).", _wrap, GUILayout.Width(TextWidth));
            for (var row = 0; row < Kinds.Length; row += 3)
            {
                GUILayout.BeginHorizontal();
                for (var i = row; i < Mathf.Min(row + 3, Kinds.Length); i++)
                    _shown[i] = GUILayout.Toggle(_shown[i], $"{Kinds[i]} {_counts[i]}", GUILayout.Width(145));
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All", GUILayout.Width(50))) SetAll(true);
            if (GUILayout.Button("None", GUILayout.Width(50))) SetAll(false);
            _showLog = GUILayout.Toggle(_showLog, "Show log (newest first)", GUILayout.Width(170));
            if (GUILayout.Button("Export session", GUILayout.Width(120))) _lastExport = Export(console.Game);
            GUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_lastExport)) GUILayout.Label(_lastExport, _wrap, GUILayout.Width(TextWidth));

            if (!_showLog) return;
            // Its own filter: the console-wide search box is for finding sections ("events" would empty the log).
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter log", GUILayout.Width(70));
            _filter = GUILayout.TextField(_filter ?? string.Empty, GUILayout.Width(TextWidth - 80));
            GUILayout.EndHorizontal();
            var filter = _filter ?? string.Empty;
            var shown = 0;
            for (var n = _logCount - 1; n >= 0 && shown < 60; n--)
            {
                var e = _log[(_logStart + n) % LogCapacity];
                if (!_shown[(int)e.Kind]) continue;
                var line = Describe(e);
                if (filter.Length > 0 && line.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                GUILayout.Label(line, _wrap, GUILayout.Width(TextWidth));
                shown++;
            }
        }

        long Sum()
        {
            long total = 0;
            foreach (var c in _counts) total += c;
            return total;
        }

        void SetAll(bool on)
        {
            for (var i = 0; i < _shown.Length; i++) _shown[i] = on;
        }

        static string Describe(in CombatEvent e)
        {
            var ic = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append(e.SimTime.ToString("0.00", ic)).Append("s  ").Append(e.Kind);
            if (e.A != 0 || e.B != 0) sb.Append("  a=").Append(e.A).Append(" b=").Append(e.B);
            if (e.X != 0f || e.Y != 0f || e.Z != 0f)
                sb.Append("  (").Append(e.X.ToString("0.#", ic)).Append(", ").Append(e.Y.ToString("0.#", ic)).Append(", ").Append(e.Z.ToString("0.#", ic)).Append(')');
            if (!string.IsNullOrEmpty(e.Tag)) sb.Append("  ").Append(e.Tag);
            if (e.Flags != 0) sb.Append("  flags=").Append(e.Flags);
            return sb.ToString();
        }

        /// <summary>One file with the event log, the tuning change log, the current preset and the config hash.</summary>
        string Export(V4Game game)
        {
            try
            {
                var ic = CultureInfo.InvariantCulture;
                var config = game != null ? game.Config : null;
                var sb = new StringBuilder();
                sb.Append("{\n  \"exportedUtc\": \"").Append(DateTime.UtcNow.ToString("o", ic)).Append("\",\n");
                sb.Append("  \"configHash\": \"").Append(config != null ? config.Hash : string.Empty).Append("\",\n");
                sb.Append("  \"eventCounts\": {");
                for (var i = 0; i < Kinds.Length; i++) sb.Append(i == 0 ? "" : ",").Append("\"").Append(Kinds[i]).Append("\":").Append(_counts[i]);
                sb.Append("},\n  \"events\": [\n");
                for (var n = 0; n < _logCount; n++)
                {
                    var e = _log[(_logStart + n) % LogCapacity];
                    sb.Append("    {\"t\":").Append(e.SimTime.ToString("R", ic)).Append(",\"kind\":\"").Append(e.Kind)
                        .Append("\",\"a\":").Append(e.A).Append(",\"b\":").Append(e.B)
                        .Append(",\"x\":").Append(e.X.ToString("R", ic)).Append(",\"y\":").Append(e.Y.ToString("R", ic)).Append(",\"z\":").Append(e.Z.ToString("R", ic))
                        .Append(",\"tag\":\"").Append(Escape(e.Tag)).Append("\",\"flags\":").Append(e.Flags).Append('}')
                        .Append(n < _logCount - 1 ? ",\n" : "\n");
                }
                sb.Append("  ],\n  \"changeLog\": [\n");
                if (config != null)
                {
                    var log = config.ChangeLog;
                    for (var i = 0; i < log.Count; i++)
                        sb.Append("    \"").Append(Escape(log[i].ToString())).Append(i < log.Count - 1 ? "\",\n" : "\"\n");
                }
                sb.Append("  ],\n  \"preset\": ");
                sb.Append(config != null ? PresetCodec.ToJson(PresetCodec.Capture(config, "session", config.Hash)) : "null");
                sb.Append("\n}\n");

                var folder = Path.Combine(Application.persistentDataPath, "V4", "Sessions");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, $"session-{DateTime.Now.ToString("yyyyMMdd-HHmmss", ic)}.json");
                File.WriteAllText(file, sb.ToString());
                return "Exported " + file;
            }
            catch (Exception ex)
            {
                return "Export failed: " + ex.Message;
            }
        }

        static string Escape(string s) => string.IsNullOrEmpty(s) ? string.Empty : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
    }
}
