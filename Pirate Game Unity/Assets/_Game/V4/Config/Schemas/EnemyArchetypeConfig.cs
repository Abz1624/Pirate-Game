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
        [Tooltip("The model is a stand-in until the enemy gets its own art (future content; e.g. the Drowned enemies, R-05).")]
        public bool placeholderModel;
        public GameObject visualPrefab;
        public float visualScale = 1f, visualYawOffset, visualHeightOffset;
        public Color placeholderColor = Color.red;
        [Tooltip("Views of this model to prewarm in the V4 view pool (0 = the pool catalog's default for its kind). Read by the pool catalog (M4); V4 enemy views use it from M8.")]
        [AppliesFrom(8)] public int poolPrewarm;
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
    /// Per-enemy rules the V3 enemy world hard-coded by enemy id (EnemyWorld, SaltwindCombatBehaviorSystem,
    /// SaltwindSpecialistCombat, SaltwindCombatAgentStateStore). Not inherited by variants: V3 matched the exact id.
    /// </summary>
    [Serializable]
    public sealed class ArchetypeWorldRules
    {
        [Tooltip("Charge timer at spawn instead of the rule (telegraphed chargers 1.5 s, their minibosses 1.15 s, others 2 s). V3: swordfish 0.")]
        [AppliesFrom(8)] public FloatOverride initialChargeTimer = new FloatOverride();
        [Tooltip("First attack delay for ships and ranged enemies instead of the rule (half the attack cooldown, or 1 s). V3: cannonboat 1.1 s, drowned_harrier 0.9 s.")]
        [AppliesFrom(8)] public FloatOverride initialAttackCooldown = new FloatOverride();
        [Tooltip("Combat phase timer at spawn (V3: powder_barge 2 s before its first fuse).")]
        [AppliesFrom(8)] public float initialCombatPhaseTimer;
        [Tooltip("Swordfish style (V3: swordfish): the model springs forward while dashing; without a combat profile it moves with the spring-dash pattern; fast dashes may reach 2.6x its speed.")]
        [AppliesFrom(8)] public bool swordfishSpringDash;
        [Tooltip("Repair Tenders never heal this enemy (V3: repair_tender itself).")]
        [AppliesFrom(8)] public bool excludedFromRepair;
        [Tooltip("The first elite of this enemy to tick gives every enemy of its combat profile the elite second charge for the rest of the run (V3 quirk: hammerhead, register R-100).")]
        [AppliesFrom(8)] public bool eliteLatchesSecondChargeForAll;
        [Tooltip("Uses ranged attacks whatever its category and behaviour (V3: cannonboat, gunner_cog, bomb_boat, manta_ray, drowned_harrier).")]
        [AppliesFrom(8)] public bool alwaysRanged;

        [Header("Ranged path without a combat profile (V3's pre-Saltwind path; every V3 enemy but the boss has a profile)")]
        [Tooltip("Never uses the profileless ranged path at all (V3: reef_crab_miniboss returned before both of its branches).")]
        [AppliesFrom(8)] public bool noProfilelessRangedAttack;
        [Tooltip("Fires only beyond this fraction of the preferred range (rule 0.7; V3: cannonboat 0.8).")]
        [AppliesFrom(8)] public FloatOverride rangedMinRangeFactor = new FloatOverride();
        [Tooltip("Fires only within this fraction of the preferred range (rule 1.25; V3: cannonboat 1.15).")]
        [AppliesFrom(8)] public FloatOverride rangedMaxRangeFactor = new FloatOverride();
        [Tooltip("Recovery after a shot instead of the rule (attack cooldown or 2.4 s, ships at least 2.5 s; V3: cannonboat 2 s).")]
        [AppliesFrom(8)] public FloatOverride rangedRecovery = new FloatOverride();
        [Tooltip("Also fires two flanking shots at 85 % damage (V3: manta_ray).")]
        [AppliesFrom(8)] public bool rangedFlankShots;
        [Tooltip("Own shot profile instead of the rule (ships 9 m/s, 1.35 s, hit 0.22, model 0.13; others 1.3 s). V3: drowned_harrier 10/1.45/0.24/0.14, cannonboat 9/1.55/0.38/0.16.")]
        [AppliesFrom(8)] public bool ownShotProfile;
        [AppliesFrom(8)] public float shotSpeed = 9f, shotLife = 1.3f, shotHitRadius = 0.22f, shotVisualRadius = 0.13f;
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
        public ArchetypeWorldRules world = new ArchetypeWorldRules();

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(enemyId)) report.Error(this, nameof(enemyId), "missing enemy id");
            report.Positive(this, "stats.baseHP", stats.baseHP);
            report.NotNegative(this, "stats.baseDamage", stats.baseDamage);
            report.Positive(this, "stats.radius", stats.radius);
            if (visual != null && visual.poolPrewarm < 0) report.Error(this, "visual.poolPrewarm", "negative prewarm");
            report.NotNegative(this, "stats.moveSpeed", stats.moveSpeed);
            report.NotNegative(this, "stats.attackCooldown", stats.attackCooldown);
        }
    }
}
