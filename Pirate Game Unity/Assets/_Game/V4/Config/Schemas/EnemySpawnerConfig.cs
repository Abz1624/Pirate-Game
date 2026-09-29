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

    // M14: the special-behaviour modules (were passed through from the V3 spawn profile asset). Field names match V3's
    // module settings so the mapping copies them by name; defaults are V3's.

    [Serializable]
    public sealed class SpawnerFormation
    {
        public ConfigEnums.FormationRole role = ConfigEnums.FormationRole.Auto;
        public ConfigEnums.FormationShape shape = ConfigEnums.FormationShape.Line;
        public float slotSpacing = 3f, followDistance = 4f, leaderSpeedBias = 1.05f;
        public bool breakOnLeaderDeath = true, breakOnPlayerProximity;
        public float breakProximityRadius = 6f, regroupRadius = 14f;
        public bool followersInheritLeaderAttack;
    }

    [Serializable]
    public sealed class SpawnerSummon
    {
        [Tooltip("The spawner its minions come from (V3 minionProfile; no V3 data set one).")] public EnemySpawnerConfig minionSpawner;
        public int minionsPerWave = 1;
        public float spawnIntervalSeconds = 3f;
        public int maxAliveMinions = 4;
        public float minionLifetimeSeconds;
        public bool inheritElite, despawnOnParentDeath = true;
    }

    [Serializable]
    public sealed class SpawnerTether
    {
        public float maxRange = 14f, breakRange = 18f, tickDamage = 3f, tickIntervalSeconds = 0.5f, rampUpSeconds = 0.75f;
        [Range(0f, 0.6f)] public float playerMoveSpeedPenalty = 0.15f;
        public bool breakOnIslandBlock = true;
    }

    [Serializable]
    public sealed class SpawnerSupportAura
    {
        public float radius = 8f, healPerSecond;
        [Range(0f, 0.2f)] public float healPercentMaxHpPerSecond;
        public bool alliesOnly = true;
        public float allySpeedMultiplier = 1.05f, pulseIntervalSeconds = 0.5f;
        public bool disableWhenDamaged;
    }

    [Serializable]
    public sealed class SpawnerPhaseDash
    {
        public float dashCooldownSeconds = 4f, windUpSeconds = 0.6f, dashDistance = 8f, dashSpeed = 18f, dashDurationSeconds = 0.25f;
        [Range(0f, 1f)] public float damageReductionDuringDash = 0.5f;
        public float postDashPauseSeconds = 0.4f;
        public ConfigEnums.PhaseDashAxis axis = ConfigEnums.PhaseDashAxis.TowardPlayer;
        public int maxConsecutiveDashes = 2;
    }

    [Serializable]
    public sealed class SpawnerTerrainAmbush
    {
        public ConfigEnums.TerrainAmbushTrigger trigger = ConfigEnums.TerrainAmbushTrigger.WhenOffScreenThenReveal;
        public float hideRadius = 20f, stalkSpeedMultiplier = 0.45f, revealDistance = 10f, revealBurstSpeedMultiplier = 1.8f, playerTriggerRadius = 12f;
    }

    [Serializable]
    public sealed class SpawnerAdaptive
    {
        public float evaluationWindowSeconds = 8f;
        public ConfigEnums.AdaptiveSignal[] enabledSignals = { ConfigEnums.AdaptiveSignal.PlayerKiteTime, ConfigEnums.AdaptiveSignal.ManyActiveEnemies };
        public float kiteThresholdSeconds = 4f, standStillThresholdSeconds = 2f, highDpsThreshold = 40f;
        [Range(0f, 1f)] public float lowHpFraction = 0.35f;
        public int manyEnemiesThreshold = 28;
        public ConfigEnums.EnemyMovementPattern kiteResponseMovement = ConfigEnums.EnemyMovementPattern.Flank;
        public float kiteResponseSpeedMultiplier = 1.15f;
        public int maxAdaptationStacks = 3;
        public float adaptationCooldownSeconds = 6f;
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
        [AppliesFrom(8)] public ConfigEnums.EnemySpecialBehavior specialBehavior = ConfigEnums.EnemySpecialBehavior.None;
        [Header("Special-behaviour modules (M14: V4 data; were V3 pass-through)")]
        [AppliesFrom(14)] public SpawnerFormation formation = new SpawnerFormation();
        [AppliesFrom(14)] public SpawnerSummon summon = new SpawnerSummon();
        [AppliesFrom(14)] public SpawnerTether tether = new SpawnerTether();
        [AppliesFrom(14)] public SpawnerSupportAura supportAura = new SpawnerSupportAura();
        [AppliesFrom(14)] public SpawnerPhaseDash phaseDash = new SpawnerPhaseDash();
        [AppliesFrom(14)] public SpawnerTerrainAmbush terrainAmbush = new SpawnerTerrainAmbush();
        [AppliesFrom(14)] public SpawnerAdaptive adaptive = new SpawnerAdaptive();
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
