using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Enemies
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
        public static EnemySpawnRuntimeParams Resolve(
            SpawnProfile profile,
            EnemyDefinition spawnedEnemy,
            int profileInstanceId,
            FormationRole role,
            int slot,
            int leaderId)
        {
            var identityEnemy = spawnedEnemy ?? profile?.enemy;
            EnemySpawnRuntimeParams p;
            if (SaltwindCombatFeature.IsEnabled && identityEnemy != null)
                p = EnemyCombatProfileRegistry.ResolveRuntimeParams(identityEnemy, profile);
            else
                p = BuildFromProfileAnchor(profile, identityEnemy);

            p.profileInstanceId = profileInstanceId;
            p.formationRole = role;
            p.formationSlot = slot;
            p.formationLeaderAgentId = leaderId;
            if (profile != null)
            {
                p.specialBehavior = profile.specialBehavior;
                p.formation = profile.formation;
                p.summon = profile.summon;
                p.tether = profile.tether;
                p.supportAura = profile.supportAura;
                p.phaseDash = profile.phaseDash;
                p.terrainAmbush = profile.terrainAmbush;
                p.adaptive = profile.adaptive;
                p.adaptiveSpeedMult = 1f;
                p.ambushHidden = profile.specialBehavior == EnemySpecialBehavior.TerrainAmbush
                    && profile.terrainAmbush.trigger != TerrainAmbushTrigger.OnSpawn;
                p.dashCooldownTimer = profile.phaseDash.dashCooldownSeconds * 0.5f;
                p.summonTimer = profile.summon.spawnIntervalSeconds;
            }

            return p;
        }

        public static EnemySpawnRuntimeParams Resolve(
            SpawnProfile profile,
            int profileInstanceId,
            FormationRole role,
            int slot,
            int leaderId) =>
            Resolve(profile, profile?.enemy, profileInstanceId, role, slot, leaderId);

        public static EnemySpawnRuntimeParams ResolveForDefinition(EnemyDefinition spawnedEnemy) =>
            Resolve(null, spawnedEnemy, 0, FormationRole.Auto, 0, -1);

        public static void ApplyEncounterOverrides(
            ref EnemySpawnRuntimeParams p,
            SpawnProfile profile,
            EnemyDefinition spawnedEnemy)
        {
            if (profile == null) return;
            if (profile.enemy != null && spawnedEnemy != null && profile.enemy.id != spawnedEnemy.id)
                return;
            if (profile.movement.pattern != EnemyMovementPattern.UseDefinition)
                p.movementPattern = profile.movement.pattern;
            if (profile.attack.pattern != EnemyAttackPattern.UseDefinition)
                p.attackPattern = profile.attack.pattern;
            if (profile.movement.preferredRange > 0f)
                p.preferredRange = profile.movement.preferredRange;
            if (profile.movement.chargeWindUpSeconds > 0f)
                p.chargeWindUp = profile.movement.chargeWindUpSeconds;
            if (profile.movement.chargeDurationSeconds > 0f)
                p.chargeDuration = profile.movement.chargeDurationSeconds;
            if (profile.movement.chargeCooldownSeconds > 0f)
                p.chargeCooldown = profile.movement.chargeCooldownSeconds;
            if (profile.movement.chargeSpeedMultiplier > 0f)
                p.chargeSpeedMult = profile.movement.chargeSpeedMultiplier;
            if (profile.attack.attackRange > 0f)
                p.attackRange = profile.attack.attackRange;
            if (profile.attack.windUpSeconds > 0f)
                p.attackWindUp = profile.attack.windUpSeconds;
            if (profile.attack.cooldownSeconds > 0f)
                p.attackCooldown = profile.attack.cooldownSeconds;
        }

        public static EnemySpawnRuntimeParams ResolveLegacyFromDefinition(EnemyDefinition def)
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.enemy = def;
            return BuildFromProfileAnchor(profile, def);
        }

        static EnemySpawnRuntimeParams BuildFromProfileAnchor(SpawnProfile profile, EnemyDefinition identityEnemy)
        {
            profile ??= ScriptableObject.CreateInstance<SpawnProfile>();
            if (identityEnemy != null)
                profile.enemy = identityEnemy;

            var p = new EnemySpawnRuntimeParams
            {
                movementPattern = ResolveMovement(profile),
                attackPattern = ResolveAttack(profile),
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
                adaptiveSpeedMult = 1f
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
            var enemy = profile.enemy;
            var behavior = enemy?.behavior ?? EnemyBehavior.DirectChaser;
            if (enemy != null && enemy.id == "cannonboat")
                return EnemyAttackPattern.SingleShot;
            if (!EnemyRangedAttackRules.SupportsRangedAttacks(enemy))
                return EnemyAttackPattern.Contact;
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
