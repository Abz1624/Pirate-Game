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
            for (var i = 0; i < events.Count; i++) _eventsSeen.Add(events[i].Kind.ToString());
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
                        case PhaseConditionKind.PressureAtLeast: return 1f >= trigger.conditionValue; // no director until M10
                    }
                    return false;
            }
            return false;
        }
    }
}
