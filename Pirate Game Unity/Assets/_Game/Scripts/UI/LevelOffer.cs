using System;
using HighSeas.Core;
using HighSeas.Data;

namespace HighSeas.UI
{
    public enum LevelOfferKind
    {
        WeaponUpgrade,
        NewWeapon,
        Logbook,
        NewRunItem,
        FallbackHeal,
        FallbackDamage,
        FallbackMoveSpeed,
        Continue,
        WeaponEvolution
    }

    [Serializable]
    public struct WeaponUpgradeLine
    {
        public UpgradeAxis axis;
        public float value;
        public string displayText;
    }

    [Serializable]
    public struct LevelOffer
    {
        public LevelOfferKind kind;
        public string id;
        public string title;
        public string categoryLabel;
        public string description;
        public Rarity rarity;
        public int currentLevel;
        public int nextLevel;
        public bool isWeapon;
        public bool isNew;
        public string evolutionId;
        public WeaponUpgradeLine[] upgradeLines;
        public string[] effectLines;
        public string behaviorLine;
        public bool hideRarity;

        public string LevelLabel =>
            isWeapon ? $"Lv {currentLevel} → {nextLevel}" : currentLevel > 0 ? $"Lv {currentLevel} → {nextLevel}" : "New";
    }
}
