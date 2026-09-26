using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    [Serializable]
    public sealed class ArchetypeStats
    {
        public float baseHP = 20f, baseDamage = 8f, moveSpeed = 4f, radius = 1f;
        public int threatCost = 1;
        public float attackCooldown = 1.5f, preferredRange = 8f;
    }

    [Serializable]
    public sealed class ArchetypeRewards
    {
        public int xpValue = 2, goldValue = 1;
    }

    [Serializable]
    public sealed class ArchetypeVisual
    {
        public GameObject visualPrefab;
        public float visualScale = 1f, visualYawOffset, visualHeightOffset;
        public Color placeholderColor = Color.red;
    }

    /// <summary>Mirror of the V3 EnemyCombatProfile registered for this enemy (registry values, before spawner overrides).</summary>
    [Serializable]
    public sealed class ArchetypeCombat
    {
        public bool registered;
        public ConfigEnums.SaltwindCombatArchetype archetype = ConfigEnums.SaltwindCombatArchetype.LegacyGeneric;
        public ConfigEnums.EnemyMovementPattern movementPattern = ConfigEnums.EnemyMovementPattern.UseDefinition;
        public ConfigEnums.EnemyAttackPattern attackPattern = ConfigEnums.EnemyAttackPattern.UseDefinition;
        public float preferredRange, attackRange, attackWindUp, attackCooldown;
        public int projectileCount = 1;
        public float spreadDegrees, projectileSpeed, chargeWindUp, chargeDuration, chargeCooldown, chargeSpeedMult;
        public bool contactDamageEnabled = true, eliteHammerheadSecondCharge, eliteKrakenSequentialTraps;
    }

    /// <summary>
    /// One enemy: identity, base stats, rewards, visuals, concurrency tags and its combat profile. Filled from the V3
    /// EnemyDefinition and EnemyCombatProfileRegistry. The legacy enemy world reads these from M2 (runtime database).
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Enemy Archetype", fileName = "EnemyArchetype")]
    public sealed class EnemyArchetypeConfig : V4ConfigAsset
    {
        [Tooltip("Enemy id used by legacy code, saves and telemetry.")] [ConfigIdentity] public string enemyId;
        public string displayName;
        public ConfigEnums.EnemyCategory category = ConfigEnums.EnemyCategory.Creature;
        public ConfigEnums.EnemyTier tier = ConfigEnums.EnemyTier.Soldier;
        public ConfigEnums.EnemyBehavior behavior = ConfigEnums.EnemyBehavior.DirectChaser;
        public string biomeTag = "emerald";
        public ArchetypeStats stats = new ArchetypeStats();
        public ArchetypeRewards rewards = new ArchetypeRewards();
        public ArchetypeVisual visual = new ArchetypeVisual();
        public string[] tags = Array.Empty<string>();
        [Tooltip("Concurrency tags used by timeline tag caps (ranged_gunner, area_denial, charge_windup).")]
        public string[] concurrencyTags = Array.Empty<string>();
        [Tooltip("Tier the timeline pools unlock by (DifficultyCurve tierUnlocks). V3 keeps it in a table by enemy id.")] public ConfigEnums.EnemyTier spawnTier = ConfigEnums.EnemyTier.Soldier;
        public bool isBoss, isMiniboss;
        [Tooltip("Off for enemies V3's database does not contain (drowned_pursuer/harrier exist only in the fallback database; V3 overtime spawns shark/cannonboat stats, register R-05).")]
        [AppliesFrom(0, "structural")] public bool inLegacyDatabase = true;
        [Tooltip("Copied into the legacy combat-profile registry at run start and on edits; reaches newly spawned enemies. Behaviors hard-coded in EnemyWorld move to data in M8.")] public ArchetypeCombat combat = new ArchetypeCombat();

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(enemyId)) report.Error(this, nameof(enemyId), "missing enemy id");
            report.Positive(this, "stats.baseHP", stats.baseHP);
            report.NotNegative(this, "stats.baseDamage", stats.baseDamage);
            report.Positive(this, "stats.radius", stats.radius);
            report.NotNegative(this, "stats.moveSpeed", stats.moveSpeed);
            report.NotNegative(this, "stats.attackCooldown", stats.attackCooldown);
        }
    }
}
