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
        FallbackHeal,
        FallbackDamage,
        FallbackMoveSpeed,
        Continue
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
        public WeaponUpgradeLine[] upgradeLines;

        public string LevelLabel =>
            isWeapon ? $"Lv {currentLevel} → {nextLevel}" : currentLevel > 0 ? $"Lv {currentLevel} → {nextLevel}" : "New";
    }
}
