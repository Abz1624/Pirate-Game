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
        [Tooltip("V5 M5 (Jolly Roger): every threshold kills add a stack of + value damage, at most cap stacks (+ seconds per extra copy, at most 40); a hit on the ship removes half.")] KillStreakDamage = 13,
        [Tooltip("V5 M5 (Captain's Last Rum): once a run, a fatal hit restores value of max hull, threshold seconds of immunity and + cap damage for seconds.")] CheatDeath = 14,
        [Tooltip("V5 M5 (Leviathan Heart): every threshold seconds a value-damage pulse of radius cap on the densest crowd after a seconds-long telegraph; × min(2, 1 + 0.2 per extra copy).")] PeriodicPulse = 15,
        [Tooltip("V5 M5 (Skeleton Key): a chest is free with chance 1 - (1 - value)^copies, at most cap.")] FreeChestChance = 16,
        [Tooltip("V5 M5 (Smuggler's Ledger): chest prices × value (once); non-Common chest rarity weights × (1 + threshold per copy), at most cap.")] ChestPriceAndRarity = 17,
        [Tooltip("V5 M5 (Barnacle Crown): with at least seconds enemies within threshold m, a stack of + value damage a second, at most cap (+2 per extra copy, at most 20); it loses 2 stacks a second otherwise.")] SurroundedDamage = 18,
        [Tooltip("V5 M5 (Shark Tooth Necklace): + value damage per copy (at most cap) against enemies above threshold of their hull.")] HealthyTargetDamage = 19,
        [Tooltip("V5 M5 (Black Spot): elite kills drop + value per copy (at most cap) more gold and XP.")] EliteRewards = 20,
        [Tooltip("Hackathon (Devil's Match): a weapon hit sets the enemy burning with chance value; a burning enemy that dies explodes for threshold damage in a radius-m blast.")] IgniteOnHit = 21,
        [Tooltip("Hackathon (Hammerhead Helm): knockback the ship receives x (1 - value per copy), at most cap off.")] ReceivedKnockback = 22,
        [Tooltip("Hackathon (Hammerhead Helm): sailing faster than threshold m/s into an enemy below full hull deals value damage, once every seconds per enemy.")] RamDamage = 23,
        [Tooltip("Hackathon (Queen Anne's Fuse): a keg blast sets off a blast at the nearest enemy it missed within threshold m of its edge after seconds, at value of its damage; one generation per copy, at most cap.")] KegChainBlast = 24,
        [Tooltip("Hackathon (Storm in a Bottle): every threshold chain-lightning hits, count strikes of value damage (radius m) over spreadSeconds at enemies within cap m; seconds cooldown.")] LightningBurst = 25,
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
        [Tooltip("Hackathon: a blast or strike radius in metres (Devil's Match, Storm in a Bottle).")] public float radius;
        [Tooltip("Hackathon: how many strikes (Storm in a Bottle).")] public int count;
        [Tooltip("Hackathon: the seconds the strikes are spread over (Storm in a Bottle).")] public float spreadSeconds;
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
