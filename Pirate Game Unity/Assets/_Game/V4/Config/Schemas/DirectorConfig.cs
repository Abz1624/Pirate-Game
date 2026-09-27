using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>What the difficulty director does with Pressure (M10 observes only; M11 adds the levers).</summary>
    public enum DirectorMode { Off = 0, Observe = 1, Adaptive = 2 }

    /// <summary>
    /// The difficulty director (M10, gameplay doc §5.3): once a second it samples the player's power — offense from the
    /// carried weapons, defense from the hull, performance from kills against spawns and hull lost — and turns it into a
    /// Pressure value, smoothed, slope-limited and clamped (the clamp is the difficulty curve's pressureMin/Max). In
    /// Observe mode it only logs Pressure; nothing reads it for gameplay. Only simulation state feeds it (no random draws),
    /// so the same run always gives the same Pressure series.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Director", fileName = "Director")]
    public sealed class DirectorConfig : V4ConfigAsset
    {
        [Tooltip("Off: Pressure stays 1. Observe: computed and logged, no effect (M10). Adaptive: the levers read it (M11).")]
        public DirectorMode mode = DirectorMode.Observe;
        [Tooltip("Stamped in trace headers and run records; change it when the formula changes (ranked runs compare within one version).")]
        public string version = "director-1";
        [Tooltip("Seconds between samples.")] public float sampleIntervalSeconds = 1f;
        [Tooltip("Kills, spawns and hull loss are measured over this many seconds.")] public float performanceWindowSeconds = 30f;

        [Header("Gains (how much each part moves Pressure; 0 = ignored)")]
        [Tooltip("Offense above what the curve expects raises Pressure.")] public float offenseGain = 0.25f;
        [Tooltip("Hull above what the curve expects raises Pressure.")] public float defenseGain = 0.15f;
        [Tooltip("Killing faster than enemies arrive (and losing little hull) raises Pressure.")] public float performanceGain = 0.2f;

        [Header("Shaping")]
        [Tooltip("Exponential smoothing time constant (s).")] public float smoothingSeconds = 20f;
        [Tooltip("Pressure never changes faster than this per second.")] public float maxChangePerSecond = 0.02f;
        [Tooltip("Performance: the kill/spawn ratio that counts as even (no push).")] public float evenKillRatio = 0.9f;
        [Tooltip("Performance: hull lost per second (fraction of max hull) that cancels a full kill lead.")] public float hullLossPenaltyPerSecond = 0.02f;

        [Header("Coverage (offense per behaviour: how much of the field a hit reaches)")]
        [Tooltip("Weight per weapon behaviour (ConfigEnums.WeaponBehavior order). Area and orbit weapons hit more enemies per shot.")]
        public float[] behaviorCoverage = { 1f, 1.6f, 1.8f, 1.5f, 1.2f, 1.4f, 1.6f, 1.3f, 1.5f, 1f }; // Projectile, Fan, Orbit, ConeStream, Trail, Mine, AreaStrike, Aura, PredictedArea, Summon

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(sampleIntervalSeconds), sampleIntervalSeconds);
            report.Positive(this, nameof(performanceWindowSeconds), performanceWindowSeconds);
            report.Positive(this, nameof(smoothingSeconds), smoothingSeconds);
            report.Positive(this, nameof(maxChangePerSecond), maxChangePerSecond);
            if (string.IsNullOrWhiteSpace(version)) report.Error(this, nameof(version), "missing version");
            if (behaviorCoverage == null || behaviorCoverage.Length == 0) report.Error(this, nameof(behaviorCoverage), "needs a weight per behaviour");
        }
    }
}
