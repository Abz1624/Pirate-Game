using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>What the spawn controller does with a timeline phase, from its fields.</summary>
    public static class PhaseKinds
    {
        /// <summary>Ambient: a spawner running at ratePerSecond from the phase's start until the next ambient phase.</summary>
        public static bool IsAmbient(TimelinePhase p) => p != null && p.ratePerSecond > 0f && p.spawner != null;

        /// <summary>Focus: one scripted spawn (miniboss, scheduled elite), optionally with an arena.</summary>
        public static bool IsFocus(TimelinePhase p) => p != null && p.focus != null && p.focus.enabled;

        /// <summary>Pulse: multiplies the ambient spawn rate for the phase's duration.</summary>
        public static bool IsPulse(TimelinePhase p) =>
            p != null && !IsAmbient(p) && !IsFocus(p) && !p.isOvertime && !p.isStorm && !p.isBoss && !Mathf.Approximately(p.rateMultiplier, 1f);
    }
}
