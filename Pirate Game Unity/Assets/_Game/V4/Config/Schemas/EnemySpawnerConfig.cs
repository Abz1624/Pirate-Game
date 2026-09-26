using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    [Serializable]
    public sealed class SpawnerTiming
    {
        public int spawnCount = 1;
        [AppliesFrom(0, "cached by the legacy spawner at run start")] public float startDelaySeconds;
        public float triggerTimeSeconds;
        public float repeatIntervalSeconds;
        public float durationSeconds;
        [Tooltip("Seconds between spawns. Ambient band spawners get this from their timeline phase rate.")]
        public float spawnIntervalSeconds = 0.5f;
        public float cooldownAfterCompleteSeconds;
        public int maxActiveFromProfile;
        public bool pauseWhenRunPaused = true;
    }

    [Serializable]
    public sealed class SpawnerPlacement
    {
        public bool requireOutsidePlayerFov = true;
        public float distanceMin = 10.8f;
        public float distanceMax = 18f;
        [Range(0f, 0.35f)] public float viewportMargin = 0.08f;
        public int positionAttempts = 16;
        [Range(30f, 360f)] public float allowedArcDegrees = 360f;
        [Range(-180f, 180f)] public float arcYawOffset;
        public float groupSpacing = 2.5f;
        public ConfigEnums.SpawnFallbackPolicy fallbackPolicy = ConfigEnums.SpawnFallbackPolicy.RetryLater;
    }

    [Serializable]
    public sealed class SpawnerElite
    {
        public bool isElite;
        public ConfigEnums.EliteAffix affix = ConfigEnums.EliteAffix.None;
        public float hpMultiplier = 1f, damageMultiplier = 1f, speedMultiplier = 1f, sizeMultiplier = 1f;
        public float goldMultiplier = 1f, xpMultiplier = 1f;
    }

    [Serializable]
    public sealed class SpawnerMovement
    {
        public ConfigEnums.EnemyMovementPattern pattern = ConfigEnums.EnemyMovementPattern.UseDefinition;
        public float speedMultiplier = 1f, turnResponsiveness = 1f, preferredRange = 10f, interceptLead = 1f;
        public float orbitRadius = 12f, orbitDirection = 1f, flankAngleDegrees = 35f;
        public float chargeWindUpSeconds = 1.5f, chargeDurationSeconds = 0.35f, chargeCooldownSeconds = 2f, chargeSpeedMultiplier = 2.5f;
        public float separationStrength = 1f;
    }

    [Serializable]
    public sealed class SpawnerAttack
    {
        public ConfigEnums.EnemyAttackPattern pattern = ConfigEnums.EnemyAttackPattern.UseDefinition;
        public bool contactDamageEnabled = true;
        public float attackRange = 12f, windUpSeconds, cooldownSeconds = 2f;
        public int projectileCount = 1;
        public float spreadDegrees, projectileSpeed = 9f, projectileLifetime = 1.4f;
        public int burstShots = 1;
        public float burstGapSeconds = 0.15f, areaRadius = 4f, areaDurationSeconds = 2f;
    }

    /// <summary>
    /// One spawn profile: which archetype it anchors, how it spawns and the per-spawner overrides. Mirrors the V3
    /// SpawnProfile blocks (special-behavior modules pass through from the V3 asset until M8). In M1 the values are
    /// written into a per-run clone of the V3 profile, so edits reach the legacy spawner live.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Enemy Spawner", fileName = "EnemySpawner")]
    public sealed class EnemySpawnerConfig : V4ConfigAsset
    {
        [Tooltip("V3 SpawnProfile.profileId; legacy code keys some behavior on it.")] [ConfigIdentity] public string legacyProfileId;
        public EnemyArchetypeConfig anchor;
        public ConfigEnums.SpawnMode spawnMode = ConfigEnums.SpawnMode.Single;
        public ConfigEnums.SpawnGroupLayout groupLayout = ConfigEnums.SpawnGroupLayout.Scatter;
        public SpawnerTiming timing = new SpawnerTiming();
        public SpawnerPlacement placement = new SpawnerPlacement();
        public SpawnerElite elite = new SpawnerElite();
        public SpawnerMovement movement = new SpawnerMovement();
        public SpawnerAttack attack = new SpawnerAttack();
        [AppliesFrom(8, "module settings pass through from the V3 profile")] public ConfigEnums.EnemySpecialBehavior specialBehavior = ConfigEnums.EnemySpecialBehavior.None;
        [AppliesFrom(11)] public AnimationCurve pressureWeight = AnimationCurve.Constant(0.85f, 1.35f, 1f);

        public override void Validate(ConfigValidationReport report)
        {
            if (anchor == null) report.Error(this, nameof(anchor), "no anchor archetype");
            report.Positive(this, "timing.spawnIntervalSeconds", timing.spawnIntervalSeconds);
            report.NotNegative(this, "timing.spawnCount", timing.spawnCount);
            if (placement.distanceMax < placement.distanceMin) report.Error(this, "placement.distanceMax", "below distanceMin");
            if (placement.positionAttempts < 1) report.Error(this, "placement.positionAttempts", "must be at least 1");
            report.Positive(this, "elite.hpMultiplier", elite.hpMultiplier);
            report.Positive(this, "elite.damageMultiplier", elite.damageMultiplier);
            if (timing.maxActiveFromProfile > 0 && timing.spawnCount > timing.maxActiveFromProfile)
                report.Error(this, "timing.spawnCount", $"burst of {timing.spawnCount} is above the spawner cap {timing.maxActiveFromProfile}");
        }
    }
}
