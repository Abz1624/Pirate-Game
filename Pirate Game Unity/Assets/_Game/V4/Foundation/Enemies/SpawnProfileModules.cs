using System;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
{
    [Serializable]
    public sealed class SpawnEliteSettings
    {
        public bool isElite;
        public EliteAffix affix = EliteAffix.None;
        [Min(0.1f)] public float hpMultiplier = 1f;
        [Min(0.1f)] public float damageMultiplier = 1f;
        [Min(0.1f)] public float speedMultiplier = 1f;
        [Min(0.1f)] public float sizeMultiplier = 1f;
        [Min(0f)] public float goldMultiplier = 1f;
        [Min(0f)] public float xpMultiplier = 1f;
    }

    [Serializable]
    public sealed class SpawnTimingSettings
    {
        [Min(0)] public int spawnCount = 1;
        [Min(0f)] public float startDelaySeconds;
        [Min(0f)] public float triggerTimeSeconds;
        [Min(0f)] public float repeatIntervalSeconds;
        [Min(0f)] public float durationSeconds;
        [Min(0.05f)] public float spawnIntervalSeconds = 0.5f;
        [Min(0f)] public float cooldownAfterCompleteSeconds;
        [Min(0)] public int maxActiveFromProfile;
        public bool pauseWhenRunPaused = true;
    }

    [Serializable]
    public sealed class SpawnPlacementSettings
    {
        public bool requireOutsidePlayerFov = true;
        [Min(0f)] public float distanceMin = 10.8f;
        [Min(0f)] public float distanceMax = 18f;
        [Range(0f, 0.35f)] public float viewportMargin = 0.08f;
        [Min(1)] public int positionAttempts = 16;
        [Range(30f, 360f)] public float allowedArcDegrees = 360f;
        [Range(-180f, 180f)] public float arcYawOffset;
        [Min(0.5f)] public float groupSpacing = 2.5f;
        public SpawnFallbackPolicy fallbackPolicy = SpawnFallbackPolicy.RetryLater;
    }

    [Serializable]
    public sealed class SpawnMovementOverrides
    {
        public EnemyMovementPattern pattern = EnemyMovementPattern.UseDefinition;
        [Range(0.1f, 3f)] public float speedMultiplier = 1f;
        [Range(0.1f, 3f)] public float turnResponsiveness = 1f;
        [Min(0f)] public float preferredRange = 10f;
        [Range(0f, 3f)] public float interceptLead = 1f;
        [Min(0f)] public float orbitRadius = 12f;
        [Range(-1f, 1f)] public float orbitDirection = 1f;
        [Range(0f, 90f)] public float flankAngleDegrees = 35f;
        [Min(0f)] public float chargeWindUpSeconds = 1.5f;
        [Min(0f)] public float chargeDurationSeconds = 0.35f;
        [Min(0f)] public float chargeCooldownSeconds = 2f;
        [Range(1f, 4f)] public float chargeSpeedMultiplier = 2.5f;
        [Range(0f, 1f)] public float separationStrength = 1f;
    }

    [Serializable]
    public sealed class SpawnAttackOverrides
    {
        public EnemyAttackPattern pattern = EnemyAttackPattern.UseDefinition;
        public bool contactDamageEnabled = true;
        [Min(0f)] public float attackRange = 12f;
        [Min(0f)] public float windUpSeconds;
        [Min(0.1f)] public float cooldownSeconds = 2f;
        [Min(1)] public int projectileCount = 1;
        [Range(0f, 90f)] public float spreadDegrees;
        [Min(0f)] public float projectileSpeed = 9f;
        [Min(0f)] public float projectileLifetime = 1.4f;
        [Min(1)] public int burstShots = 1;
        [Min(0f)] public float burstGapSeconds = 0.15f;
        [Min(0f)] public float areaRadius = 4f;
        [Min(0f)] public float areaDurationSeconds = 2f;
    }

    [Serializable]
    public sealed class FormationBehaviorSettings
    {
        public FormationRole role = FormationRole.Auto;
        public FormationShape shape = FormationShape.Line;
        [Min(0.5f)] public float slotSpacing = 3f;
        [Min(0.5f)] public float followDistance = 4f;
        [Range(0.8f, 1.5f)] public float leaderSpeedBias = 1.05f;
        public bool breakOnLeaderDeath = true;
        public bool breakOnPlayerProximity;
        [Min(0f)] public float breakProximityRadius = 6f;
        [Min(0f)] public float regroupRadius = 14f;
        public bool followersInheritLeaderAttack;
    }

    [Serializable]
    public sealed class SummonBehaviorSettings
    {
        public SpawnProfile minionProfile;
        [Min(1)] public int minionsPerWave = 1;
        [Min(0.5f)] public float spawnIntervalSeconds = 3f;
        [Min(1)] public int maxAliveMinions = 4;
        [Min(0f)] public float minionLifetimeSeconds;
        public bool inheritElite;
        public bool despawnOnParentDeath = true;
    }

    [Serializable]
    public sealed class TetherBeamSettings
    {
        [Min(1f)] public float maxRange = 14f;
        [Min(0.5f)] public float breakRange = 18f;
        [Min(0f)] public float tickDamage = 3f;
        [Min(0.1f)] public float tickIntervalSeconds = 0.5f;
        [Min(0f)] public float rampUpSeconds = 0.75f;
        [Range(0f, 0.6f)] public float playerMoveSpeedPenalty = 0.15f;
        public bool breakOnIslandBlock = true;
    }

    [Serializable]
    public sealed class SupportAuraSettings
    {
        [Min(1f)] public float radius = 8f;
        [Min(0f)] public float healPerSecond;
        [Range(0f, 0.2f)] public float healPercentMaxHpPerSecond;
        public bool alliesOnly = true;
        [Range(0.5f, 1.5f)] public float allySpeedMultiplier = 1.05f;
        [Min(0.1f)] public float pulseIntervalSeconds = 0.5f;
        public bool disableWhenDamaged;
    }

    [Serializable]
    public sealed class PhaseDashBehaviorSettings
    {
        [Min(0.5f)] public float dashCooldownSeconds = 4f;
        [Min(0f)] public float windUpSeconds = 0.6f;
        [Min(1f)] public float dashDistance = 8f;
        [Min(1f)] public float dashSpeed = 18f;
        [Min(0.05f)] public float dashDurationSeconds = 0.25f;
        [Range(0f, 1f)] public float damageReductionDuringDash = 0.5f;
        [Min(0f)] public float postDashPauseSeconds = 0.4f;
        public PhaseDashAxis axis = PhaseDashAxis.TowardPlayer;
        [Min(1)] public int maxConsecutiveDashes = 2;
    }

    [Serializable]
    public sealed class TerrainAmbushSettings
    {
        public TerrainAmbushTrigger trigger = TerrainAmbushTrigger.WhenOffScreenThenReveal;
        [Min(0f)] public float hideRadius = 20f;
        [Range(0.1f, 1f)] public float stalkSpeedMultiplier = 0.45f;
        [Min(0f)] public float revealDistance = 10f;
        [Range(1f, 3f)] public float revealBurstSpeedMultiplier = 1.8f;
        [Min(0f)] public float playerTriggerRadius = 12f;
    }

    [Serializable]
    public sealed class AdaptiveCounterSettings
    {
        [Min(1f)] public float evaluationWindowSeconds = 8f;
        public AdaptiveSignal[] enabledSignals =
        {
            AdaptiveSignal.PlayerKiteTime,
            AdaptiveSignal.ManyActiveEnemies
        };
        [Min(1f)] public float kiteThresholdSeconds = 4f;
        [Min(0f)] public float standStillThresholdSeconds = 2f;
        [Min(0f)] public float highDpsThreshold = 40f;
        [Range(0f, 1f)] public float lowHpFraction = 0.35f;
        [Min(1)] public int manyEnemiesThreshold = 28;
        public EnemyMovementPattern kiteResponseMovement = EnemyMovementPattern.Flank;
        [Range(0.5f, 2f)] public float kiteResponseSpeedMultiplier = 1.15f;
        [Min(1)] public int maxAdaptationStacks = 3;
        [Min(0.5f)] public float adaptationCooldownSeconds = 6f;
    }
}
