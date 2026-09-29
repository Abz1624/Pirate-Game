using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Which layer set a resolved value (shown by the console's enemy inspector).</summary>
    public enum StatLayer { Default, Archetype, Variant, Stage, Spawner, Experiment, Runtime }

    /// <summary>The enemy values the game uses, after layering, with the layer that set each one.</summary>
    public sealed class ResolvedEnemyStats
    {
        public string id, displayName;
        public ConfigEnums.EnemyCategory category;
        public ConfigEnums.EnemyTier tier;
        public ConfigEnums.EnemyBehavior behavior;
        public string biomeTag;
        public float baseHP, baseDamage, moveSpeed, radius, attackCooldown, preferredRange;
        public int threatCost, xpValue, goldValue;
        public GameObject visualPrefab;
        public float visualScale, visualYawOffset, visualHeightOffset;
        public Color placeholderColor;
        public string[] tags;
        public bool isBoss, isMiniboss;
        public ArchetypeCombat combat;
        public readonly Dictionary<string, StatLayer> Provenance = new Dictionary<string, StatLayer>();

        internal void Set<T>(ref T field, T value, string name, StatLayer layer) { field = value; Provenance[name] = layer; }
    }

    /// <summary>
    /// Resolves an enemy's values from its layers. M2 uses layers 1-2 (archetype, variant); stage, spawner/phase,
    /// experiment and runtime-director layers are added by M3 and M10. Pure: no Unity or legacy state.
    /// </summary>
    public static class EnemyStatResolver
    {
        public static ResolvedEnemyStats Resolve(EnemyArchetypeConfig archetype)
        {
            if (archetype == null) throw new ArgumentNullException(nameof(archetype));
            var r = new ResolvedEnemyStats();
            const StatLayer a = StatLayer.Archetype;
            r.Set(ref r.id, archetype.enemyId, "id", a);
            r.Set(ref r.displayName, archetype.displayName, "displayName", a);
            r.Set(ref r.category, archetype.category, "category", a);
            // M16: the warning tier is config (V3 looked it up in a table by id): the archetype's own tier, else its spawn tier.
            r.Set(ref r.tier, archetype.tier != ConfigEnums.EnemyTier.Soldier ? archetype.tier : archetype.spawnTier, "tier", a);
            r.Set(ref r.behavior, archetype.behavior, "behavior", a);
            r.Set(ref r.biomeTag, archetype.biomeTag, "biomeTag", a);
            r.Set(ref r.baseHP, archetype.stats.baseHP, "baseHP", a);
            r.Set(ref r.baseDamage, archetype.stats.baseDamage, "baseDamage", a);
            r.Set(ref r.moveSpeed, archetype.stats.moveSpeed, "moveSpeed", a);
            r.Set(ref r.radius, archetype.stats.radius, "radius", a);
            r.Set(ref r.threatCost, archetype.stats.threatCost, "threatCost", a);
            r.Set(ref r.attackCooldown, archetype.stats.attackCooldown, "attackCooldown", a);
            r.Set(ref r.preferredRange, archetype.stats.preferredRange, "preferredRange", a);
            r.Set(ref r.xpValue, archetype.rewards.xpValue, "xpValue", a);
            r.Set(ref r.goldValue, archetype.rewards.goldValue, "goldValue", a);
            r.Set(ref r.visualPrefab, archetype.visual.visualPrefab, "visualPrefab", a);
            r.Set(ref r.visualScale, archetype.visual.visualScale, "visualScale", a);
            r.Set(ref r.visualYawOffset, archetype.visual.visualYawOffset, "visualYawOffset", a);
            r.Set(ref r.visualHeightOffset, archetype.visual.visualHeightOffset, "visualHeightOffset", a);
            r.Set(ref r.placeholderColor, archetype.visual.placeholderColor, "placeholderColor", a);
            r.Set(ref r.tags, archetype.tags, "tags", a);
            r.Set(ref r.isBoss, archetype.isBoss, "isBoss", a);
            r.Set(ref r.isMiniboss, archetype.isMiniboss, "isMiniboss", a);
            r.Set(ref r.combat, archetype.combat, "combat", a);
            return r;
        }

        /// <summary>
        /// A database alias: V3's specialist rule. Fields not copied from the base keep EnemyDefinition's defaults,
        /// exactly as V3's specialist catalog created them (the runtime database creates them since M16).
        /// </summary>
        public static ResolvedEnemyStats Resolve(EnemyVariantConfig variant)
        {
            if (variant == null) throw new ArgumentNullException(nameof(variant));
            if (variant.kind == VariantKind.RegistryElite)
            {
                // The base enemy's stats with the elite's own id (its combat profile is keyed by it, R-03) and combat.
                var elite = Resolve(variant.baseArchetype);
                elite.Set(ref elite.id, variant.variantId, "id", StatLayer.Variant);
                elite.Set(ref elite.displayName, string.IsNullOrEmpty(variant.displayName) ? "Elite " + elite.displayName : variant.displayName, "displayName", StatLayer.Variant);
                elite.Set(ref elite.combat, variant.combat, "combat", StatLayer.Variant);
                // V3's tier table did not list the registry elites, so they read as Soldier.
                elite.Set(ref elite.tier, ConfigEnums.EnemyTier.Soldier, "tier", StatLayer.Variant);
                return elite;
            }

            var b = variant.baseArchetype;
            var r = new ResolvedEnemyStats();
            const StatLayer d = StatLayer.Default, a = StatLayer.Archetype, v = StatLayer.Variant;
            // EnemyDefinition defaults for everything the alias does not set.
            r.Set(ref r.tier, variant.spawnTier, "tier", v); // M16: V3's tier table by id, now the variant's spawn tier
            r.Set(ref r.biomeTag, "emerald", "biomeTag", d);
            r.Set(ref r.visualScale, 1f, "visualScale", d);
            r.Set(ref r.visualYawOffset, 0f, "visualYawOffset", d);
            r.Set(ref r.visualHeightOffset, 0f, "visualHeightOffset", d);
            r.Set(ref r.visualPrefab, null, "visualPrefab", d);
            r.Set(ref r.isBoss, false, "isBoss", d);
            r.Set(ref r.isMiniboss, false, "isMiniboss", d);
            // Copied from the template.
            r.Set(ref r.category, b.category, "category", a);
            r.Set(ref r.baseDamage, b.stats.baseDamage, "baseDamage", a);
            r.Set(ref r.radius, b.stats.radius, "radius", a);
            r.Set(ref r.xpValue, b.rewards.xpValue, "xpValue", a);
            r.Set(ref r.goldValue, b.rewards.goldValue, "goldValue", a);
            r.Set(ref r.preferredRange, b.stats.preferredRange, "preferredRange", a);
            r.Set(ref r.attackCooldown, b.stats.attackCooldown, "attackCooldown", a);
            r.Set(ref r.placeholderColor, b.visual.placeholderColor, "placeholderColor", a);
            // Base values the alias overrides when the override is on.
            r.Set(ref r.behavior, b.behavior, "behavior", a);
            r.Set(ref r.threatCost, b.stats.threatCost, "threatCost", a);
            r.Set(ref r.baseHP, b.stats.baseHP, "baseHP", a);
            r.Set(ref r.moveSpeed, b.stats.moveSpeed, "moveSpeed", a);
            // The variant's own values.
            r.Set(ref r.id, variant.variantId, "id", v);
            r.Set(ref r.displayName, variant.displayName, "displayName", v);
            r.Set(ref r.tags, variant.tags, "tags", v);
            r.Set(ref r.combat, variant.combat, "combat", v);
            if (variant.behavior.enabled) r.Set(ref r.behavior, variant.behavior.value, "behavior", v);
            if (variant.threatCost.enabled) r.Set(ref r.threatCost, variant.threatCost.value, "threatCost", v);
            if (variant.baseHP.enabled) r.Set(ref r.baseHP, variant.baseHP.value, "baseHP", v);
            if (variant.moveSpeed.enabled) r.Set(ref r.moveSpeed, variant.moveSpeed.value, "moveSpeed", v);
            if (variant.baseDamage.enabled) r.Set(ref r.baseDamage, variant.baseDamage.value, "baseDamage", v);
            if (variant.inheritBaseVisual)
            {
                // The model only (R-13). visualScale stays the alias's own: the legacy enemy world sizes the gameplay
                // radius from it (EnemyWorld.Spawn), so copying the template's scale would change the hitbox.
                r.Set(ref r.visualPrefab, b.visual.visualPrefab, "visualPrefab", v);
                r.Set(ref r.visualYawOffset, b.visual.visualYawOffset, "visualYawOffset", v);
                r.Set(ref r.visualHeightOffset, b.visual.visualHeightOffset, "visualHeightOffset", v);
            }
            return r;
        }
    }
}
