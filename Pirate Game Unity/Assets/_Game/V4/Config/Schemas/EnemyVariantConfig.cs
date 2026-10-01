using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    public enum VariantKind
    {
        [Tooltip("A separate enemy in the database (V3 specialist alias): its own id, some fields copied from the base.")]
        DatabaseAlias,
        [Tooltip("A combat-profile variant of the base enemy (V3 registry elite): same database enemy, own combat profile.")]
        RegistryElite
    }

    [Serializable] public sealed class FloatOverride { public bool enabled; public float value; }
    [Serializable] public sealed class IntOverride { public bool enabled; public int value; }
    [Serializable] public sealed class BehaviorOverride { public bool enabled; public ConfigEnums.EnemyBehavior value; }

    /// <summary>V5 M3d (owner, 1 Oct 2026): a variant that attacks differently from its base must look different.</summary>
    [Serializable]
    public sealed class VariantLook
    {
        [Tooltip("Its own model (empty = the base's, under inheritBaseVisual).")] public GameObject model;
        [Tooltip("The model's colours × this (white = the base's tint).")] public Color tint = Color.white;
        [Tooltip("The drawn size and the hitbox × this.")] [Min(0.1f)] public float viewSize = 1f;
    }

    /// <summary>
    /// An enemy built on a base archetype. Database aliases follow the V3 specialist rule
    /// (V3's specialist catalog; M16: the runtime database creates them): category, damage, radius, xp, gold, preferred range, attack cooldown
    /// and placeholder color come from the base; behavior, threat, HP and speed are overridden; tags are "specialist";
    /// everything else keeps EnemyDefinition defaults (so no visual prefab, register R-13). Registry elites
    /// (elite_hammerhead, elite_kraken_spawn) only carry their own combat profile.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Enemy Variant", fileName = "EnemyVariant")]
    public sealed class EnemyVariantConfig : V4ConfigAsset
    {
        [Tooltip("Legacy id: the alias enemy id, or the registry profile id for elites.")] [ConfigIdentity] public string variantId;
        [AppliesFrom(0, "structural: decides where the variant lives in the legacy data")] public VariantKind kind = VariantKind.DatabaseAlias;
        [AppliesFrom(0, "structural: the alias's slot in the runtime database")] [Tooltip("Database aliases: their order after the loaded enemies in the runtime database (M16: was V3's specialist catalog order).")]
        public int databaseSlotOrder;
        [AppliesFrom(0)] public EnemyArchetypeConfig baseArchetype;
        public string displayName;
        public BehaviorOverride behavior = new BehaviorOverride();
        public IntOverride threatCost = new IntOverride();
        public FloatOverride baseHP = new FloatOverride();
        public FloatOverride moveSpeed = new FloatOverride();
        public FloatOverride baseDamage = new FloatOverride();
        public string[] tags = Array.Empty<string>();
        [Tooltip("On (default, R-13 decision): the alias uses its base archetype's model (not its scale, which sets the hitbox) as a stand-in until it gets its own model. Off = V3 behavior (no visual prefab, rendered as a placeholder cube). Applies at run start: swapped models mid-run would skip the legacy NavMesh stripping.")]
        [AppliesFrom(0)] public bool inheritBaseVisual = true;
        [Tooltip("V5 M3d: its own look (model, tint, size), applied at run start.")] [AppliesFrom(0)] public VariantLook look = new VariantLook();
        [Tooltip("Concurrency tags the timeline's tag caps count (e.g. charge_windup).")] public string[] concurrencyTags = Array.Empty<string>();
        [Tooltip("Tier the timeline pools unlock by (DifficultyCurve tierUnlocks).")] public ConfigEnums.EnemyTier spawnTier = ConfigEnums.EnemyTier.Soldier;
        [Tooltip("Aliases: copied into the legacy combat-profile registry at run start and on edits; reaches newly spawned enemies. Registry elites: the elite's own definition (appended to the run database) reads this profile when the timeline spawns the elite (R-03). V3's enemy-id behaviours are data in the world block (M8).")] public ArchetypeCombat combat = new ArchetypeCombat();
        [Tooltip("The variant's own id-matched rules (V3 matched the exact enemy id, so they are not inherited from the base).")] public ArchetypeWorldRules world = new ArchetypeWorldRules();

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(variantId)) report.Error(this, nameof(variantId), "missing variant id");
            if (baseArchetype == null) report.Error(this, nameof(baseArchetype), "no base archetype");
            if (baseHP.enabled) report.Positive(this, "baseHP.value", baseHP.value);
            if (moveSpeed.enabled) report.NotNegative(this, "moveSpeed.value", moveSpeed.value);
        }
    }
}
