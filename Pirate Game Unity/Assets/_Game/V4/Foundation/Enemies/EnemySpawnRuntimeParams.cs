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

}
