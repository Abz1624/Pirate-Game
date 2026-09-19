using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Enemies
{
    public struct EnemySpawnRuntimeParams
    {
        public int profileInstanceId;
        public EnemyMovementPattern movementPattern;
        public EnemyAttackPattern attackPattern;
        public EnemySpecialBehavior specialBehavior;
        public FormationRole formationRole;
        public int formationLeaderAgentId;
        public int formationSlot;
        public int minionParentAgentId;
        public float preferredRange;
        public float interceptLead;
        public float orbitRadius;
        public float orbitDirection;
        public float flankAngleRad;
        public float chargeWindUp;
        public float chargeDuration;
        public float chargeCooldown;
        public float chargeSpeedMult;
        public float separationStrength;
        public bool contactDamageEnabled;
        public float attackRange;
        public float attackWindUp;
        public float attackCooldown;
        public int projectileCount;
        public float spreadRad;
        public float projectileSpeed;
        public float projectileLifetime;
        public int burstShots;
        public float burstGap;
        public float areaRadius;
        public float areaDuration;
        public FormationBehaviorSettings formation;
        public SummonBehaviorSettings summon;
        public TetherBeamSettings tether;
        public SupportAuraSettings supportAura;
        public PhaseDashBehaviorSettings phaseDash;
        public TerrainAmbushSettings terrainAmbush;
        public AdaptiveCounterSettings adaptive;
        public float adaptiveSpeedMult;
        public EnemyMovementPattern adaptiveMovementOverride;
        public bool hasAdaptiveMovementOverride;

        public float dashCooldownTimer;
        public float dashWindUpTimer;
        public float dashActiveTimer;
        public float dashPauseTimer;
        public int dashConsecutiveCount;
        public bool ambushHidden;
        public float ambushRevealTimer;
        public float tetherActiveTimer;
        public float tetherTickTimer;
        public float summonTimer;
        public int aliveMinionCount;
        public float auraPulseTimer;
        public float adaptationCooldownTimer;
        public int adaptationStacks;
    }

    public static class SpawnProfileResolver
    {
        public static EnemySpawnRuntimeParams Resolve(SpawnProfile profile, int profileInstanceId, FormationRole role, int slot, int leaderId)
        {
            var p = new EnemySpawnRuntimeParams
            {
                profileInstanceId = profileInstanceId,
                movementPattern = ResolveMovement(profile),
                attackPattern = ResolveAttack(profile),
                specialBehavior = profile.specialBehavior,
                formationRole = role,
                formationSlot = slot,
                formationLeaderAgentId = leaderId,
                preferredRange = profile.movement.preferredRange,
                interceptLead = profile.movement.interceptLead,
                orbitRadius = profile.movement.orbitRadius,
                orbitDirection = profile.movement.orbitDirection,
                flankAngleRad = profile.movement.flankAngleDegrees * Mathf.Deg2Rad,
                chargeWindUp = profile.movement.chargeWindUpSeconds,
                chargeDuration = profile.movement.chargeDurationSeconds,
                chargeCooldown = profile.movement.chargeCooldownSeconds,
                chargeSpeedMult = profile.movement.chargeSpeedMultiplier,
                separationStrength = profile.movement.separationStrength,
                contactDamageEnabled = profile.attack.contactDamageEnabled,
                attackRange = profile.attack.attackRange,
                attackWindUp = profile.attack.windUpSeconds,
                attackCooldown = profile.attack.cooldownSeconds,
                projectileCount = Mathf.Max(1, profile.attack.projectileCount),
                spreadRad = profile.attack.spreadDegrees * Mathf.Deg2Rad,
                projectileSpeed = profile.attack.projectileSpeed,
                projectileLifetime = profile.attack.projectileLifetime,
                burstShots = Mathf.Max(1, profile.attack.burstShots),
                burstGap = profile.attack.burstGapSeconds,
                areaRadius = profile.attack.areaRadius,
                areaDuration = profile.attack.areaDurationSeconds,
                formation = profile.formation,
                summon = profile.summon,
                tether = profile.tether,
                supportAura = profile.supportAura,
                phaseDash = profile.phaseDash,
                terrainAmbush = profile.terrainAmbush,
                adaptive = profile.adaptive,
                adaptiveSpeedMult = 1f,
                ambushHidden = profile.specialBehavior == EnemySpecialBehavior.TerrainAmbush
                    && profile.terrainAmbush.trigger != TerrainAmbushTrigger.OnSpawn,
                dashCooldownTimer = profile.phaseDash.dashCooldownSeconds * 0.5f,
                summonTimer = profile.summon.spawnIntervalSeconds
            };
            return p;
        }

        public static EnemyMovementPattern ResolveMovement(SpawnProfile profile)
        {
            if (profile.movement.pattern != EnemyMovementPattern.UseDefinition)
                return profile.movement.pattern;
            return MapLegacyBehavior(profile.enemy?.behavior ?? EnemyBehavior.DirectChaser);
        }

        public static EnemyAttackPattern ResolveAttack(SpawnProfile profile)
        {
            if (profile.attack.pattern != EnemyAttackPattern.UseDefinition)
                return profile.attack.pattern;
            var behavior = profile.enemy?.behavior ?? EnemyBehavior.DirectChaser;
            if (behavior == EnemyBehavior.SuicideApproach)
                return EnemyAttackPattern.SuicideExplosion;
            if (behavior == EnemyBehavior.MaintainRange || behavior == EnemyBehavior.FormationRanged)
                return EnemyAttackPattern.SingleShot;
            return EnemyAttackPattern.Contact;
        }

        static EnemyMovementPattern MapLegacyBehavior(EnemyBehavior behavior) =>
            behavior switch
            {
                EnemyBehavior.FlankingChaser => EnemyMovementPattern.Flank,
                EnemyBehavior.TelegraphedCharge => EnemyMovementPattern.TelegraphedCharge,
                EnemyBehavior.MaintainRange => EnemyMovementPattern.MaintainRange,
                EnemyBehavior.FormationRanged => EnemyMovementPattern.MaintainRange,
                EnemyBehavior.SuicideApproach => EnemyMovementPattern.DirectChase,
                EnemyBehavior.StrafeBurst => EnemyMovementPattern.Strafe,
                EnemyBehavior.FastSwarm => EnemyMovementPattern.Swarm,
                EnemyBehavior.DiveLunge => EnemyMovementPattern.DiveLunge,
                EnemyBehavior.DriftPulse => EnemyMovementPattern.Drift,
                EnemyBehavior.StationaryTurret => EnemyMovementPattern.Stationary,
                EnemyBehavior.TankChaser => EnemyMovementPattern.DirectChase,
                _ => EnemyMovementPattern.DirectChase
            };
    }
}
