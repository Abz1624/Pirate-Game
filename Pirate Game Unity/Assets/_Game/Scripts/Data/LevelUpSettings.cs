using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "LevelUpSettings", menuName = "HighSeas/Level Up Settings")]
    public sealed class LevelUpSettings : ScriptableObject
    {
        [Header("Cards")]
        [Min(1)] public int cardsPerLevel = 3;
        [Range(0f, 1f)] public float weaponOfferWeight = 0.55f;
        [Min(1f)] public float newWeaponWeightMultiplier = 2.5f;
        public bool excludeMaxedWeapons = true;
        public bool preventDuplicateCards = true;

        [Header("First Level Up")]
        public bool scriptFirstLevelUp = true;
        public string startingWeaponId = "swivel_guns";

        [Header("Demo")]
        [Tooltip("When enabled, every level-up includes a new weapon card if any unowned weapons remain.")]
        public bool alwaysIncludeNewWeapon = true;

        [Header("Upgrade Package Size By Rarity")]
        [Min(1)] public int commonStatLines = 1;
        [Min(1)] public int uncommonStatLines = 1;
        [Min(1)] public int rareStatLines = 2;
        [Min(1)] public int epicStatLines = 2;
        [Min(1)] public int legendaryStatLines = 3;

        [Header("Rarity Power")]
        [Range(1f, 2f)] public float upgradePowerMultiplier = 1.2f;

        [Header("Modal")]
        [Min(0)] public int refreshesPerRun = 2;

        public int StatLinesForRarity(Core.Rarity rarity) => rarity switch
        {
            Core.Rarity.Uncommon => uncommonStatLines,
            Core.Rarity.Rare => rareStatLines,
            Core.Rarity.Epic => epicStatLines,
            Core.Rarity.Legendary => legendaryStatLines,
            _ => commonStatLines
        };
    }
}
