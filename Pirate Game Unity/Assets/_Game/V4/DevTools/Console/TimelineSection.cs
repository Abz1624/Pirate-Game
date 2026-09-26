using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HighSeas.Core;
using RoguePirates.V4.Config;
using RoguePirates.V4.Spawning;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Console
{
    /// <summary>
    /// Wave Timeline (M3): every phase with its live state, Trigger now / Skip, and duplicate, delete and reorder.
    /// Structural edits are ordinary logged config edits (list resize + field copies), so presets record them. Phase
    /// fields themselves are edited in the Config section (timeline.saltwind).
    /// </summary>
    public sealed class TimelineSection : IConsoleSection
    {
        public string Title => "Wave Timeline";
        public string SearchText => "timeline phase band beat pulse elite miniboss overtime storm trigger skip";

        string _status = string.Empty;
        int _frame = -1;
        readonly Dictionary<string, PhaseStatus> _states = new Dictionary<string, PhaseStatus>();
        readonly Dictionary<TimelinePhase, string> _labels = new Dictionary<TimelinePhase, string>();

        public void Draw(TuningConsole console)
        {
            var game = console.Game;
            var runtime = game != null ? game.Config : null;
            var timeline = runtime?.Single<WaveTimelineConfig>();
            if (timeline == null) { GUILayout.Label("No wave timeline in the config set."); return; }
            ISpawnController spawn = null;
            game.Context?.TryGet(out spawn);
            var runTime = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            if (_frame != Time.frameCount)
            {
                _frame = Time.frameCount;
                _states.Clear();
                if (spawn != null) foreach (var p in spawn.Phases) _states[p.PhaseId] = p;
                _labels.Clear();
            }
            var states = _states;

            if (spawn != null)
                GUILayout.Label($"Now: {(string.IsNullOrEmpty(spawn.CurrentPhaseLabel) ? "—" : spawn.CurrentPhaseLabel)}   ·   spawn rate ×{spawn.EffectiveRateMultiplier:0.##}   ·   arena {(spawn.ArenaActive ? "on (unit " + spawn.ArenaFocusUnitId + ")" : "off")}", GUI.skin.box);
            if (_status.Length > 0) GUILayout.Label(_status);

            for (var i = 0; i < timeline.phases.Count; i++)
            {
                var phase = timeline.phases[i];
                states.TryGetValue(phase.phaseId, out var st);
                GUILayout.BeginHorizontal();
                if (!_labels.TryGetValue(phase, out var label)) _labels[phase] = label = Describe(phase);
                GUILayout.Label(label, GUILayout.Width(200));
                GUILayout.Label(st.PhaseId != null ? st.State.ToString() : "-", GUILayout.Width(56));
                if (spawn != null && GUILayout.Button("Trigger", GUILayout.Width(58)))
                    _status = spawn.TriggerNow(phase.phaseId) ? $"{phase.phaseId}: triggered" : $"{phase.phaseId}: follows the run clock or the legacy executor";
                if (spawn != null && GUILayout.Button("Skip", GUILayout.Width(40)))
                    _status = spawn.Skip(phase.phaseId) ? $"{phase.phaseId}: skipped" : $"{phase.phaseId}: cannot skip";
                if (GUILayout.Button("⧉", GUILayout.Width(24))) _status = Duplicate(runtime, timeline, i, runTime);
                if (i > 0 && GUILayout.Button("▲", GUILayout.Width(24))) _status = Swap(runtime, timeline, i - 1, i, runTime);
                if (i < timeline.phases.Count - 1 && GUILayout.Button("▼", GUILayout.Width(24))) _status = Swap(runtime, timeline, i, i + 1, runTime);
                if (GUILayout.Button("✕", GUILayout.Width(24))) _status = Delete(runtime, timeline, i, runTime);
                GUILayout.EndHorizontal();
            }
        }

        static string Describe(TimelinePhase p)
        {
            string when;
            switch (p.trigger.kind)
            {
                case PhaseTriggerKind.Time: when = $"{Mathf.FloorToInt(p.trigger.timeSeconds / 60f)}:{Mathf.FloorToInt(p.trigger.timeSeconds % 60f):00}"; break;
                case PhaseTriggerKind.Event: when = "on " + p.trigger.eventName; break;
                case PhaseTriggerKind.AfterPhaseCleared: when = "after " + p.trigger.afterPhaseId; break;
                default: when = $"{p.trigger.conditionKind} ≥ {p.trigger.conditionValue:0.##}"; break;
            }
            var what = PhaseKinds.IsAmbient(p) ? $"{p.ratePerSecond:0.##}/s"
                : PhaseKinds.IsFocus(p) ? "focus" : PhaseKinds.IsPulse(p) ? $"×{p.rateMultiplier:0.##}"
                : p.isOvertime ? "overtime" : p.isStorm ? "storm" : p.isBoss ? "boss" : "encounter";
            return $"{when}  {p.label}  ({what})";
        }

        // ---- Structural edits as logged field edits ------------------------------------------------------------

        const string ListPath = "phases";

        string Duplicate(ConfigSetRuntime runtime, WaveTimelineConfig timeline, int index, float runTime)
        {
            var id = timeline.configId;
            var count = timeline.phases.Count;
            var snapshot = Capture(timeline.phases[index]);
            if (!PresetCodec.TryResize(runtime, id, ListPath, (count + 1).ToString(CultureInfo.InvariantCulture), runTime, out var error)) return error;
            if (!ApplyElement(runtime, id, count, snapshot, runTime, out error)) return error;
            var newId = UniqueId(timeline, timeline.phases[index].phaseId + "_copy");
            runtime.TryApplyEdit(id, $"{ListPath}[{count}].phaseId", newId, runTime, out _);
            // A copy is not one of the stage's scheduled elites (Marks count each index once).
            if (timeline.phases[count].focus.scheduledEliteIndex >= 0)
                runtime.TryApplyEdit(id, $"{ListPath}[{count}].focus.scheduledEliteIndex", "-1", runTime, out _);
            return $"added {newId} at the end (edit its trigger and pools in Config; bands run in time order)";
        }

        string Delete(ConfigSetRuntime runtime, WaveTimelineConfig timeline, int index, float runTime)
        {
            var id = timeline.configId;
            var count = timeline.phases.Count;
            var removed = timeline.phases[index].phaseId;
            for (var j = index; j < count - 1; j++)
                if (!ApplyElement(runtime, id, j, Capture(timeline.phases[j + 1]), runTime, out var error)) return error;
            return PresetCodec.TryResize(runtime, id, ListPath, (count - 1).ToString(CultureInfo.InvariantCulture), runTime, out var e2)
                ? $"removed {removed}" : e2;
        }

        string Swap(ConfigSetRuntime runtime, WaveTimelineConfig timeline, int a, int b, float runTime)
        {
            var id = timeline.configId;
            var first = Capture(timeline.phases[a]);
            var second = Capture(timeline.phases[b]);
            if (!ApplyElement(runtime, id, a, second, runTime, out var error)) return error;
            if (!ApplyElement(runtime, id, b, first, runTime, out error)) return error;
            return $"moved {timeline.phases[b].phaseId}";
        }

        static string UniqueId(WaveTimelineConfig timeline, string wanted)
        {
            var id = wanted;
            for (var n = 2; timeline.phases.Any(p => p.phaseId == id); n++) id = wanted + n;
            return id;
        }

        /// <summary>One phase as edits relative to the element: list counts (outer first), then leaf values as text.</summary>
        static List<(string path, string value, bool isCount)> Capture(TimelinePhase phase)
        {
            var edits = new List<(string, string, bool)>();
            ConfigFieldWalker.Walk(phase, f =>
            {
                if (f.Index < 0 && f.Kind == ConfigFieldKind.List)
                    edits.Add((f.Path, ((f.GetValue() as IList)?.Count ?? 0).ToString(CultureInfo.InvariantCulture), true));
                else if (f.Kind != ConfigFieldKind.List && f.Kind != ConfigFieldKind.Nested && f.Kind != ConfigFieldKind.Curve && f.Kind != ConfigFieldKind.UnityReference)
                    edits.Add((f.Path, ConfigFieldWalker.Format(f.GetValue()), false));
            });
            return edits;
        }

        static bool ApplyElement(ConfigSetRuntime runtime, string configId, int index, List<(string path, string value, bool isCount)> edits, float runTime, out string error)
        {
            error = null;
            var prefix = $"{ListPath}[{index}].";
            foreach (var (path, value, _) in edits.Where(e => e.isCount).OrderBy(e => e.path.Count(c => c == '[')))
                if (!PresetCodec.TryResize(runtime, configId, prefix + path, value, runTime, out error)) return false;
            foreach (var (path, value, _) in edits.Where(e => !e.isCount))
                if (!runtime.TryApplyEdit(configId, prefix + path, value, runTime, out error)) { error = $"{path}: {error}"; return false; }
            return true;
        }
    }
}
