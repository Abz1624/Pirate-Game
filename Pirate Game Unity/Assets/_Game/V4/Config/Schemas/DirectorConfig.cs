using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>What the difficulty director does with Pressure (M10 observes only; M11 adds the levers).</summary>
    public enum DirectorMode { Off = 0, Observe = 1, Adaptive = 2 }

    /// <summary>What the build's offense is compared with (playtest round 1).</summary>
    public enum ExpectedOffense
    {
        /// <summary>The run's first offense grown by the enemy HP curve (M10): "your damage keeps up with enemy toughness".</summary>
        EnemyHpCurve = 0,
        /// <summary>The run's first offense grown by <see cref="DirectorConfig.onTrackGrowth"/>: an average build at that minute.
        /// Pressure 1 = on track; ahead of it raises Pressure.</summary>
        OnTrackBuild = 1,
    }

    /// <summary>Which part of the build favours an enemy role in the composition lever (M11).</summary>
    public enum BuildRange { CloseRange = 0, LongRange = 1 }

    /// <summary>Composition bias (M11): enemies with <see cref="enemyTag"/> get up to <see cref="maxBoost"/> more pick
    /// weight, scaled by the share of the build's offense in <see cref="favouredBy"/> and by how far Pressure is above 1.</summary>
    [Serializable]
    public sealed class CompositionRule
    {
        public string enemyTag = "ranged";
        public BuildRange favouredBy = BuildRange.CloseRange;
        public float maxBoost = 0.6f;
    }

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
        [Tooltip("Off: Pressure stays 1. Observe: computed and logged, no effect (M10). Adaptive: the levers read it (M11, the shipping mode).")]
        public DirectorMode mode = DirectorMode.Adaptive;
        [Tooltip("Stamped in trace headers and run records; change it when the formula changes (ranked runs compare within one version).")]
        public string version = "director-1";
        [Tooltip("Seconds between samples.")] public float sampleIntervalSeconds = 1f;
        [Tooltip("Kills, spawns and hull loss are measured over this many seconds.")] public float performanceWindowSeconds = 30f;

        [Header("Gains (how much each part moves Pressure; 0 = ignored)")]
        [Tooltip("Offense above what the curve expects raises Pressure (playtest round 1: 0.25 → 0.8 so a build 1.3× ahead reads ~1.24 and 1.45× hits the top).")] public float offenseGain = 0.8f;
        [Tooltip("Hull above what the curve expects raises Pressure.")] public float defenseGain = 0.15f;
        [Tooltip("Killing faster than enemies arrive (and losing little hull) raises Pressure.")] public float performanceGain = 0.2f;

        [Header("Shaping")]
        [Tooltip("Exponential smoothing time constant (s).")] public float smoothingSeconds = 20f;
        [Tooltip("Pressure never changes faster than this per second.")] public float maxChangePerSecond = 0.02f;
        [Tooltip("Performance: the kill/spawn ratio that counts as even (no push).")] public float evenKillRatio = 0.9f;
        [Tooltip("Performance: hull lost per second (fraction of max hull) that cancels a full kill lead.")] public float hullLossPenaltyPerSecond = 0.02f;

        [Header("Expected offense (what counts as an on-track build)")]
        [AppliesFrom(10)] public ExpectedOffense expectedOffense = ExpectedOffense.OnTrackBuild;
        [Tooltip("OnTrackBuild: offense of an average build at each minute, as a multiple of the run's starting offense (x = minutes).")]
        // Calibrated in playtest round 1 from the median of three average-build human-pilot runs (director observing):
        // about ×4 by 1:00, ×8 by 3:00 (six weapons), a plateau while upgrades land, then evolutions from ~7:00.
        [AppliesFrom(10)] public AnimationCurve onTrackGrowth = LinearCurve(
            (0f, 1f), (1f, 3.9f), (2f, 6.7f), (3f, 8.1f), (4f, 8.8f), (5f, 9.9f), (6f, 10.5f), (8f, 17f), (10f, 24f), (15f, 34f));
        [Tooltip("An evolved weapon's offense × this (evolutions add effects the base numbers do not show).")] [AppliesFrom(10)] public float evolutionOffenseMultiplier = 1.6f;
        [Tooltip("A hit counts at most referenceEnemyHp × the enemy HP curve × overkillCapMultiplier in the power index (damage past a kill is wasted).")]
        [AppliesFrom(10)] public bool overkillCap = true;
        [Tooltip("Base HP of a typical ambient enemy (before the global HP multiplier).")] [AppliesFrom(10)] public float referenceEnemyHp = 16f;
        [AppliesFrom(10)] public float overkillCapMultiplier = 1.5f;

        [Header("Pressure floor (playtest round 1: weak builds are eased only at the very start)")]
        [Tooltip("After this many seconds Pressure never goes below pressureFloor.")] [AppliesFrom(11)] public float pressureFloorAfterSeconds = 30f;
        [AppliesFrom(11)] public float pressureFloor = 1f;

        [Header("Coverage (offense per behaviour: how much of the field a hit reaches)")]
        [Tooltip("Weight per weapon behaviour (ConfigEnums.WeaponBehavior order). Area and orbit weapons hit more enemies per shot.")]
        public float[] behaviorCoverage = { 1f, 1.6f, 1.8f, 1.5f, 1.2f, 1.4f, 1.6f, 1.3f, 1.5f, 1f }; // Projectile, Fan, Orbit, ConeStream, Trail, Mine, AreaStrike, Aura, PredictedArea, Summon

        // ---- M11 levers (Adaptive mode only; each can be switched off). "Above" = how far Pressure is from 1 toward the
        // top of the band (0 at 1 or below, 1 at pressureMax): strong builds only; weak builds only get the lower rate.
        [Header("Lever: threat rate (ambient spawn rate × this)")]
        [AppliesFrom(11)] public bool threatRateLever = true;
        [Tooltip("Rate multiplier = 1 + gain × (Pressure − 1), clamped.")] [AppliesFrom(11)] public float threatRateGain = 2.2f;
        [AppliesFrom(11)] public float threatRateMin = 0.85f;
        [AppliesFrom(11)] public float threatRateMax = 1.75f;

        [Header("Lever: elite chance (an ambient spawn becomes elite)")]
        [AppliesFrom(11)] public bool eliteLever = true;
        [Tooltip("Chance at the top of the band (scaled by Above).")] [AppliesFrom(11)] public float eliteChanceAtMax = 0.12f;
        [Tooltip("Adaptive elites pay the elite rewards (gold, forced powerup, elite-kill item effects): richer targets for strong builds.")] [AppliesFrom(11)] public bool eliteLeverPaysEliteRewards = true;
        [AppliesFrom(11)] public ConfigEnums.EliteAffix eliteAffix = ConfigEnums.EliteAffix.Rich;
        [AppliesFrom(11)] public float eliteHpMultiplier = 1.5f;
        [AppliesFrom(11)] public float eliteDamageMultiplier = 1.1f;
        [AppliesFrom(11)] public float eliteSizeMultiplier = 1.15f;

        [Header("Lever: kill band (playtest, 2 Oct 2026)")]
        [Tooltip("Every killBandSeconds the kills are compared with the spawns of that band: a player who kept up gets elites in the next band, one who fell behind gets fewer spawns and no elites. Adaptive mode only.")]
        public bool killBandLever;
        [Tooltip("Length of one band (s).")] public float killBandSeconds = 30f;
        [Tooltip("Kills ÷ spawns at or above this: the player kept up (elites next band, full rate).")] public float killBandHighRatio = 0.85f;
        [Tooltip("Kills ÷ spawns at or below this: the player fell behind (no elites, the lowest rate next band).")] public float killBandLowRatio = 0.3f;
        [Tooltip("Ambient rate and floor multiplier in the band after one at or below the low ratio (rises linearly to 1 at the high ratio).")] public float killBandLowRateMultiplier = 0.6f;
        [Tooltip("Chance an ambient spawn is an elite in the band after one at the high ratio (0 below it).")] public float killBandEliteChance = 0.06f;
        [Tooltip("Bands with fewer spawns than this are not judged (the next band runs at full rate, no elites).")] public int killBandMinSpawns = 6;

        [Header("Time-to-kill floors (playtest, 3 Oct 2026)")]
        [Tooltip("Bosses, minibosses and elites never die faster than this many seconds of the player's whole damage per second (single target, all weapons hitting), measured when they spawn: they can never be one-shot. HP is only ever raised. 0 = off.")]
        public float bossMinSeconds = 25f;
        [Tooltip("Arena minibosses and elites (the ones that clear the sea: the 2:30 crab, the 3:00 elite, the 5:00 and 10:00 fights).")] public float arenaFocusMinSeconds = 12f;
        [Tooltip("Minibosses and scheduled elites that fight alongside the waves.")] public float focusMinSeconds = 7f;
        [Tooltip("Elites the kill band adds to the waves.")] public float adaptiveEliteMinSeconds = 2.5f;

        [Header("Lever: tier timing (tougher tiers unlock earlier)")]
        [AppliesFrom(11)] public bool tierTimingLever = true;
        [Tooltip("Seconds earlier at the top of the band. Time-triggered beats never move.")] [AppliesFrom(11)] public float tierLeadSecondsAtMax = 90f;

        [Header("Lever: ambient toughness (every ambient spawn, scaled by Above)")]
        [AppliesFrom(11)] public bool toughnessLever = true;
        [AppliesFrom(11)] public float toughnessHpAtMax = 1.25f;
        [AppliesFrom(11)] public float toughnessDamageAtMax = 1.2f;

        [Header("Lever: minibosses and elite beats (never the boss; beat times never move)")]
        [AppliesFrom(11)] public bool focusLever = true;
        [AppliesFrom(11)] public float focusHpAtMax = 1.5f;
        [AppliesFrom(11)] public float focusDamageAtMax = 1.2f;

        [Header("Lever: composition bias")]
        [AppliesFrom(11)] public bool compositionLever = true;
        [Tooltip("Weapon behaviours counted as close range; the rest are long range.")]
        [AppliesFrom(11)] public ConfigEnums.WeaponBehavior[] closeRangeBehaviors =
            { ConfigEnums.WeaponBehavior.Orbit, ConfigEnums.WeaponBehavior.ConeStream, ConfigEnums.WeaponBehavior.Trail, ConfigEnums.WeaponBehavior.Mine, ConfigEnums.WeaponBehavior.Aura };
        [Tooltip("A close-range build draws more ranged ships; a long-range build draws more swarms (gameplay doc §5.3).")]
        [AppliesFrom(11)] public CompositionRule[] compositionRules =
        {
            new CompositionRule { enemyTag = "ranged", favouredBy = BuildRange.CloseRange, maxBoost = 0.6f },
            new CompositionRule { enemyTag = "swarm", favouredBy = BuildRange.LongRange, maxBoost = 0.6f },
        };

        [Header("Lever: enemy HP, only while the enemy cap is full")]
        [AppliesFrom(11)] public bool capHpLever = true;
        [Tooltip("HP multiplier at the top of the band for ambient spawns made while the cap blocked a spawn recently.")] [AppliesFrom(11)] public float capHpMultiplierAtMax = 1.2f;
        [Tooltip("A cap block counts for this many seconds.")] [AppliesFrom(11)] public float capBlockedWindowSeconds = 5f;

        [Header("Timeline")]
        [Tooltip("Condition triggers 'Pressure at least' read Pressure (Adaptive only; otherwise they see 1).")] [AppliesFrom(11)] public bool pressureConditionTriggers = true;

        [Header("Ranked (run records)")]
        [Tooltip("A run is ranked only in Adaptive mode, with this director version, no forced Pressure and no live tuning edits.")]
        [AppliesFrom(11)] public string rankedVersion = "director-1";

        /// <summary>A curve through the points with straight segments between them (no overshoot between keys).</summary>
        public static AnimationCurve LinearCurve(params (float x, float y)[] points)
        {
            var keys = new Keyframe[points.Length];
            for (var i = 0; i < points.Length; i++)
            {
                var inSlope = i > 0 ? (points[i].y - points[i - 1].y) / (points[i].x - points[i - 1].x) : 0f;
                var outSlope = i < points.Length - 1 ? (points[i + 1].y - points[i].y) / (points[i + 1].x - points[i].x) : 0f;
                keys[i] = new Keyframe(points[i].x, points[i].y, inSlope, outSlope);
            }
            return new AnimationCurve(keys);
        }

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(sampleIntervalSeconds), sampleIntervalSeconds);
            report.Positive(this, nameof(performanceWindowSeconds), performanceWindowSeconds);
            report.Positive(this, nameof(smoothingSeconds), smoothingSeconds);
            report.Positive(this, nameof(maxChangePerSecond), maxChangePerSecond);
            if (string.IsNullOrWhiteSpace(version)) report.Error(this, nameof(version), "missing version");
            if (behaviorCoverage == null || behaviorCoverage.Length == 0) report.Error(this, nameof(behaviorCoverage), "needs a weight per behaviour");
            if (threatRateMin <= 0f || threatRateMax < threatRateMin) report.Error(this, nameof(threatRateMin), "threat band must be 0 < min <= max");
            if (eliteChanceAtMax < 0f || eliteChanceAtMax > 1f) report.Error(this, nameof(eliteChanceAtMax), "chance must be 0..1");
            if (killBandLever && (killBandSeconds <= 0f || killBandLowRatio >= killBandHighRatio || killBandLowRateMultiplier <= 0f || killBandEliteChance < 0f || killBandEliteChance > 1f))
                report.Error(this, nameof(killBandSeconds), "kill band needs a positive length, low ratio < high ratio, a positive rate and a 0..1 elite chance");
            if (tierLeadSecondsAtMax < 0f) report.Error(this, nameof(tierLeadSecondsAtMax), "negative lead");
            if (capHpMultiplierAtMax < 1f) report.Error(this, nameof(capHpMultiplierAtMax), "the cap HP lever only raises HP");
            if (pressureFloor <= 0f) report.Error(this, nameof(pressureFloor), "must be positive (it is clamped to the curve's Pressure band at runtime)");
            if (pressureFloorAfterSeconds < 0f) report.Error(this, nameof(pressureFloorAfterSeconds), "negative time");
            if (expectedOffense == ExpectedOffense.OnTrackBuild && (onTrackGrowth == null || onTrackGrowth.length == 0))
                report.Error(this, nameof(onTrackGrowth), "needs at least one key");
            if (overkillCap && (referenceEnemyHp <= 0f || overkillCapMultiplier <= 0f)) report.Error(this, nameof(referenceEnemyHp), "the overkill cap needs positive values");
            if (evolutionOffenseMultiplier < 1f) report.Error(this, nameof(evolutionOffenseMultiplier), "an evolution never lowers offense");
            if (toughnessHpAtMax < 1f || toughnessDamageAtMax < 1f || focusHpAtMax < 1f || focusDamageAtMax < 1f)
                report.Error(this, nameof(toughnessHpAtMax), "toughness/focus levers only raise HP and damage");
        }
    }
}
