using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    /// <summary>A stat change per stack of an item (V3 V3RunItemInventory.ApplyItemEffect).</summary>
    [Serializable]
    public sealed class ItemStatModifier
    {
        [Tooltip("GameEnums StatId 0–20.")] public int stat;
        public float additive;
        public float multiplicative = 1f;
    }

    /// <summary>What an item's triggered effect does (V3 V3RunItemEffects).</summary>
    public enum ItemEffectKind
    {
        None = 0,
        [Tooltip("Turning by threshold° gives × value attack speed for seconds (Gunner's Gloves).")] AttackSpeedAfterTurn = 1,
        [Tooltip("A near miss gives + value crit chance for seconds (Ghost Lantern; V3 never detects near misses).")] CritAfterNearMiss = 2,
        [Tooltip("At threshold m/s or faster, incoming damage × value (Reinforced Keel).")] IncomingDamageAtSpeed = 3,
        [Tooltip("Every threshold kills, every weapon fires once (Captain's Bell).")] FireAllEveryKills = 4,
        [Tooltip("Damage × (1 + min(cap, value × distinct weapon families)) (Admiral's Ledger).")] DamagePerWeaponFamily = 5,
        [Tooltip("Damage × (1 + min(cap, gold / threshold)) (Golden Figurehead).")] DamagePerGold = 6,
        [Tooltip("Explosion damage × value (Black Powder Keg; V3 computes it, nothing reads it).")] ExplosionDamage = 7,
        [Tooltip("After threshold m without being hit, explosion damage × value until the next explosion (Powder Fuse; never read in V3).")] ExplosionDamageAfterDistance = 8,
        [Tooltip("Status damage × value (Sea Witch's Eye; never read in V3).")] StatusDamage = 9,
        [Tooltip("Damage to controlled targets × value (Leviathan Tooth; never read in V3).")] ControlledTargetDamage = 10,
        [Tooltip("+ value lightning jumps (Stormglass; never read in V3).")] ExtraLightningJumps = 11,
        [Tooltip("Enemy spawn density × value (Cursed Compass; never read in V3).")] SpawnDensity = 12,
    }

    /// <summary>One triggered effect of an item. Effects are per item, not per stack (V3 checked HasItem).</summary>
    [Serializable]
    public sealed class ItemEffect
    {
        public ItemEffectKind kind;
        public float value = 1f;
        public float threshold;
        public float seconds;
        public float cap;
        [Tooltip("The effect acts. Off for the effects V3 computes but never reads (register R-40): they stay inert until decided.")]
        public bool active = true;
    }

    /// <summary>
    /// A run item (M9): its stat changes per stack and its triggered effects, from V3's V3ItemCatalog,
    /// V3RunItemInventory and V3RunItemEffects. Evolutions reference the item that unlocks them.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Item", fileName = "Item")]
    public sealed class ItemConfig : V4ConfigAsset
    {
        [ConfigIdentity] public string itemId;
        public string displayName;
        [TextArea] public string description;
        [Tooltip("0 Common, 1 Uncommon, 2 Rare, 3 Epic, 4 Legendary.")] public int rarity;
        [Tooltip("In the chest loot pool.")] public bool inChestPool = true;
        [Tooltip("In the level-up item pool (V3 InteractionItemIds; Saltwind level-ups offer no items).")] public bool inLevelUpPool;
        [Tooltip("Order in V3's chest pool (loot rolls index it).")] public int chestPoolOrder = -1;
        [Tooltip("Order in V3's collection list.")] public int collectionOrder;
        [Tooltip("Each stack adds these.")] public ItemStatModifier[] statModifiers = Array.Empty<ItemStatModifier>();
        public ItemEffect[] effects = Array.Empty<ItemEffect>();

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(itemId)) report.Error(this, nameof(itemId), "missing id");
            if (rarity < 0 || rarity > 4) report.Error(this, nameof(rarity), "must be 0-4");
            if (statModifiers != null)
                for (var i = 0; i < statModifiers.Length; i++)
                    if (statModifiers[i] != null) report.StatId(this, $"statModifiers[{i}].stat", statModifiers[i].stat);
            if (effects != null)
                for (var i = 0; i < effects.Length; i++)
                {
                    var e = effects[i];
                    if (e == null) continue;
                    if (e.kind == ItemEffectKind.FireAllEveryKills && e.threshold < 1f) report.Error(this, $"effects[{i}].threshold", "needs at least 1 kill");
                    if (e.kind == ItemEffectKind.DamagePerGold && !(e.threshold > 0f)) report.Error(this, $"effects[{i}].threshold", "the gold divisor must be greater than 0");
                    report.NotNegative(this, $"effects[{i}].seconds", e.seconds);
                }
        }
    }
}
