using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// When a phase's trigger is met: a run time, a CombatEvents kind seen this run, another phase cleared, or a
    /// condition (player level, kills; Pressure reads 1 until the director arrives in M10).
    /// </summary>
    internal sealed class TimelineTriggers
    {
        readonly SpawnServices _s;
        readonly HashSet<string> _eventsSeen = new HashSet<string>();
        readonly Func<string, bool> _isCleared;

        public TimelineTriggers(SpawnServices services, Func<string, bool> isCleared)
        {
            _s = services;
            _isCleared = isCleared;
        }

        public void BeginRun() => _eventsSeen.Clear();

        public void OnEvents(IReadOnlyList<CombatEvent> events)
        {
            for (var i = 0; i < events.Count; i++) _eventsSeen.Add(Name(events[i].Kind));
        }

        // V5 M3e: the kinds' names once (Enum.ToString allocated a string per event per tick).
        static readonly Dictionary<int, string> Names = new Dictionary<int, string>();

        static string Name(CombatEventKind kind)
        {
            if (!Names.TryGetValue((int)kind, out var name)) Names[(int)kind] = name = kind.ToString();
            return name;
        }

        public bool IsTriggered(TimelinePhase phase, float elapsed)
        {
            var trigger = phase.trigger;
            switch (trigger.kind)
            {
                case PhaseTriggerKind.Time: return elapsed >= trigger.timeSeconds;
                case PhaseTriggerKind.Event: return !string.IsNullOrEmpty(trigger.eventName) && _eventsSeen.Contains(trigger.eventName);
                case PhaseTriggerKind.AfterPhaseCleared: return !string.IsNullOrEmpty(trigger.afterPhaseId) && _isCleared(trigger.afterPhaseId);
                case PhaseTriggerKind.Condition:
                    switch (trigger.conditionKind)
                    {
                        case PhaseConditionKind.PlayerLevelAtLeast: return _s.Run.PlayerLevel >= trigger.conditionValue;
                        case PhaseConditionKind.KillsAtLeast: return _s.Run.Kills >= trigger.conditionValue;
                        // M11: Pressure only in Adaptive mode (Observe/Off see 1, as before). Time triggers never move.
                        case PhaseConditionKind.PressureAtLeast: return (_s.Levers != null ? _s.Levers.ConditionPressure : 1f) >= trigger.conditionValue;
                    }
                    return false;
            }
            return false;
        }
    }
}
