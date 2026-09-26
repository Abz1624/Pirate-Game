using System.Collections.Generic;
using System.Reflection;
using HighSeas.Combat;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Frees V3's area-denial slots (Kraken trap casts) after <see cref="WaveTimelineConfig.areaDenialReleaseSeconds"/>.
    /// V3 reserves a slot per cast in <see cref="SaltwindCombatEncounterBudget"/> and never releases it (register R-06),
    /// so after two casts per run no Kraken lays traps again. 0 keeps V3's behavior (the v3-parity preset).
    /// </summary>
    [DeletedInPhase(8, "V4 enemy world owns the encounter budget")]
    public sealed class LegacyAreaDenialRelease
    {
        static readonly FieldInfo ActiveField =
            typeof(SaltwindCombatEncounterBudget).GetField("_activeAreaDenial", BindingFlags.Static | BindingFlags.NonPublic);

        readonly ConfigSetRuntime _config;
        readonly Queue<float> _releaseAt = new Queue<float>();
        int _tracked;

        public LegacyAreaDenialRelease(ConfigSetRuntime config) { _config = config; }

        public static bool Available => ActiveField != null;

        /// <summary>Call once per simulation tick with the run clock (it stops while the run is paused).</summary>
        public void Tick(float runSeconds)
        {
            if (ActiveField == null) return;
            var active = (int)ActiveField.GetValue(null);
            if (active < _tracked) { _releaseAt.Clear(); _tracked = 0; } // the budget was reset (new run)
            var hold = _config?.Single<WaveTimelineConfig>()?.areaDenialReleaseSeconds ?? 0f;
            for (; _tracked < active; _tracked++) _releaseAt.Enqueue(runSeconds + hold); // casts reserved since the last tick
            if (hold <= 0f) return;
            while (_releaseAt.Count > 0 && _releaseAt.Peek() <= runSeconds)
            {
                _releaseAt.Dequeue();
                _tracked--;
                active = Mathf.Max(0, active - 1);
            }
            ActiveField.SetValue(null, active);
        }
    }
}
