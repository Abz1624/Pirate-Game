using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    public enum PhaseTriggerKind { Time, Event, AfterPhaseCleared, Condition }
    public enum PhaseConditionKind { PlayerLevelAtLeast, KillsAtLeast, PressureAtLeast }
    public enum PhaseDurationKind { UntilNext, Seconds, UntilCleared }
    public enum SpawnBudgetUnit { Spawns, Threat }
    public enum TagCapScope { Any, SpecialistPool }

    [Serializable]
    public sealed class PhaseTrigger
    {
        public PhaseTriggerKind kind = PhaseTriggerKind.Time;
        [Tooltip("Run time in seconds (Time trigger). Beats and the storm: live. Ambient bands: on restart (V3 caches band starts at run start). Overtime: follows the stage deadline. Reef Crab: from M3.")]
        public float timeSeconds;
        [Tooltip("CombatEvents kind name (Event trigger).")] public string eventName = string.Empty;
        [Tooltip("Phase id that must be cleared first (AfterPhaseCleared).")] public string afterPhaseId = string.Empty;
        public PhaseConditionKind conditionKind = PhaseConditionKind.PlayerLevelAtLeast;
        public float conditionValue;
    }

    [Serializable]
    public sealed class PhaseDuration
    {
        public PhaseDurationKind kind = PhaseDurationKind.UntilNext;
        public float seconds;
    }

    [Serializable]
    public sealed class PhaseWarning
    {
        [Tooltip("Seconds before the trigger; 0 = no warning.")] public float leadSeconds;
        [Tooltip("{label} is replaced with the phase label.")] public string template = "WARNING — {label} incoming!";
    }

    [Serializable]
    public sealed class PoolEntry
    {
        [Tooltip("Legacy enemy id; specialist variants get archetype assets in M2.")] public string enemyId = string.Empty;
        public EnemyArchetypeConfig archetype;
        public float weight = 1f;
    }

    [Serializable]
    public sealed class SpawnPool
    {
        public string poolId = "ordinary";
        [Tooltip("Chance a spawn uses this pool (ordinary share; the specialist pool gets the rest).")]
        [Range(0f, 1f)] public float share = 1f;
        public List<PoolEntry> entries = new List<PoolEntry>();
    }

    [Serializable]
    public sealed class TagCap
    {
        [Tooltip("Concurrency tag on archetypes, e.g. ranged_gunner, area_denial, charge_windup.")]
        public string tag = string.Empty;
        public int max = 2;
        public TagCapScope scope = TagCapScope.Any;
    }

    [Serializable]
    public sealed class TimelinePhase
    {
        [ConfigIdentity] public string phaseId = "phase";
        public string label = "Phase";
        public PhaseTrigger trigger = new PhaseTrigger();
        public PhaseDuration duration = new PhaseDuration();
        [Tooltip("Spawner this phase drives (ambient bands, beats, encounters).")]
        public EnemySpawnerConfig spawner;
        [Tooltip("Second spawner (overtime harrier).")] public EnemySpawnerConfig alternateSpawner;
        [Tooltip("Ambient spawns per second while the phase is active (0 = not an ambient phase).")]
        public float ratePerSecond;
        [AppliesFrom(3)] public SpawnBudgetUnit budgetUnit = SpawnBudgetUnit.Spawns;
        [AppliesFrom(3, "V3 composition is hard-coded until the V4 Spawn Controller")] public List<SpawnPool> pools = new List<SpawnPool>();
        [AppliesFrom(3)] public bool pauseAmbient;
        [Tooltip("Multiplier on the ambient rate while active (swarm pulses). V3 uses the first swarm pulse's value for both pulses until M3.")]
        public float rateMultiplier = 1f;
        [AppliesFrom(3)] public int burstCount;
        [Tooltip("Beat warnings use the stage eventWarningSeconds until M3; the storm phase's lead is live.")]
        public PhaseWarning warning = new PhaseWarning();
        public bool isBoss, isOvertime, isStorm;
    }

    [Serializable]
    public sealed class OvertimeSettings
    {
        public float initialSpawnIntervalSeconds = 1.4f;
        public float intervalFloorSeconds = 0.35f;
        public float intervalDecayPerStep = 0.85f;
        public float pressureStepSeconds = 30f;
        public float pursuerWeightFirstTwoMinutes = 0.8f;
        [Tooltip("Stored for the profile hash; V3 rolls harrier as 1 - pursuer.")] public float harrierWeightFirstTwoMinutes = 0.2f;
        public float pursuerWeightAfterTwoMinutes = 0.6f;
        [Tooltip("Stored for the profile hash; V3 rolls harrier as 1 - pursuer.")] public float harrierWeightAfterTwoMinutes = 0.4f;
    }

    [Serializable]
    public sealed class StormSettings
    {
        [Tooltip("Storm start and warning lead are the storm phase's trigger time and warning lead.")]
        public float safeRegionStartFraction = 1f;
        public float safeRegionEndFraction = 0.4f;
        public float contractionStartSeconds = 720f;
        public float contractionEndSeconds = 900f;
        public float outsideDamagePerSecond = 8f;
    }

    /// <summary>
    /// The Saltwind run schedule: stage limits, ambient bands, scheduled beats, overtime and storm. In M1 its values
    /// reach the game through the legacy profile and a cloned encounter schedule; from M3 the V4 timeline runs it.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Wave Timeline", fileName = "WaveTimeline")]
    public sealed class WaveTimelineConfig : V4ConfigAsset
    {
        [Header("Stage")]
        public float stageDeadlineSeconds = 600f;
        [Tooltip("No ambient spawns before this run time.")] public float graceSeconds = 2f;
        public int maxActiveEnemies = 80;
        [Tooltip("Slots kept free for Beacon and encounter spawns (ambient cap = max - reserved).")] public int reservedEncounterSlots = 8;
        [AppliesFrom(0, "cached by the legacy spawner at run start")] public float globalMinSpawnGapSeconds = 0.35f;
        public float spawnRetryIntervalSeconds = 0.35f;
        [Tooltip("Stored for the profile hash; V3 hard-codes 10 s.")] public float requiredEncounterTimeoutSeconds = 10f;
        public float bossAmbientRateMultiplier = 0.5f;
        public float eventWarningSeconds = 5f;
        [Tooltip("Stored for the profile hash; the live band length comes from the phase triggers.")] public float ambientBandDurationSeconds = 120f;
        [Tooltip("Stored for the profile hash only (band 0 reads 0.5 here). The live band rates are the phases' ratePerSecond (band 0 = 0.6/s). Register R-01.")]
        public float[] profileAmbientRatePerSecond = { 0.5f, 1.2f, 1.8f, 2.4f, 3f };
        [Tooltip("Stored for the profile hash; V3 does not read these.")] public float minChargeWarningSeconds = 1f, minExplosiveWarningSeconds = 1.2f;

        [Header("Phases")]
        public List<TimelinePhase> phases = new List<TimelinePhase>();

        [Header("Concurrency")]
        public List<TagCap> tagCaps = new List<TagCap>();

        [Header("Overtime and storm")]
        public OvertimeSettings overtime = new OvertimeSettings();
        public StormSettings storm = new StormSettings();

        public TimelinePhase Phase(string id) => phases.FirstOrDefault(p => p.phaseId == id);
        public TagCap Cap(string tag) => tagCaps.FirstOrDefault(c => c.tag == tag);

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(stageDeadlineSeconds), stageDeadlineSeconds);
            report.NotNegative(this, nameof(graceSeconds), graceSeconds);
            if (maxActiveEnemies <= reservedEncounterSlots)
                report.Error(this, nameof(reservedEncounterSlots), $"reserved slots ({reservedEncounterSlots}) must be below max active enemies ({maxActiveEnemies})");
            report.Positive(this, nameof(globalMinSpawnGapSeconds), globalMinSpawnGapSeconds);
            var ids = new HashSet<string>();
            for (var i = 0; i < phases.Count; i++)
            {
                var p = phases[i];
                var at = $"phases[{i}]";
                if (string.IsNullOrWhiteSpace(p.phaseId)) report.Error(this, at + ".phaseId", "missing phase id");
                else if (!ids.Add(p.phaseId)) report.Error(this, at + ".phaseId", $"duplicate phase id '{p.phaseId}'");
                report.NotNegative(this, at + ".trigger.timeSeconds", p.trigger.timeSeconds);
                report.NotNegative(this, at + ".ratePerSecond", p.ratePerSecond);
                report.NotNegative(this, at + ".warning.leadSeconds", p.warning.leadSeconds);
                if (p.rateMultiplier <= 0f) report.Error(this, at + ".rateMultiplier", "must be greater than 0");
                if (p.burstCount > 0 && p.spawner != null && p.spawner.timing.maxActiveFromProfile > 0 && p.spawner.timing.maxActiveFromProfile < p.burstCount)
                    report.Error(this, at + ".burstCount", $"burst {p.burstCount} is larger than the spawner cap {p.spawner.timing.maxActiveFromProfile}");
                for (var j = 0; j < p.pools.Count; j++)
                    report.WeightsNotZero(this, $"{at}.pools[{j}].entries", p.pools[j].entries.Select(e => e.weight));
            }
            ValidateTriggerCycles(report);
            foreach (var cap in tagCaps) if (cap.max < 0) report.Error(this, "tagCaps", $"{cap.tag}: max must not be negative");
            report.Positive(this, "overtime.intervalFloorSeconds", overtime.intervalFloorSeconds);
            report.Positive(this, "overtime.pressureStepSeconds", overtime.pressureStepSeconds);
            if (storm.contractionEndSeconds < storm.contractionStartSeconds)
                report.Error(this, "storm.contractionEndSeconds", "contraction ends before it starts");
            foreach (var p in phases.Where(p => p.isOvertime && p.trigger.kind == PhaseTriggerKind.Time))
                if (!Mathf.Approximately(p.trigger.timeSeconds, stageDeadlineSeconds))
                    report.Error(this, "phases", $"overtime phase '{p.phaseId}' starts at {p.trigger.timeSeconds}s but the stage deadline is {stageDeadlineSeconds}s");
            if (phases.Count(p => p.isStorm) > 1) report.Error(this, "phases", "more than one storm phase");
        }

        /// <summary>AfterPhaseCleared triggers must not form a loop (A waits for B, B waits for A).</summary>
        void ValidateTriggerCycles(ConfigValidationReport report)
        {
            var after = phases.Where(p => p.trigger.kind == PhaseTriggerKind.AfterPhaseCleared)
                              .ToDictionary(p => p.phaseId, p => p.trigger.afterPhaseId);
            foreach (var start in after.Keys)
            {
                var seen = new HashSet<string> { start };
                var current = start;
                while (after.TryGetValue(current, out var next) && !string.IsNullOrEmpty(next))
                {
                    if (!seen.Add(next)) { report.Error(this, "phases", $"trigger cycle through '{start}'"); break; }
                    current = next;
                }
            }
        }
    }
}
