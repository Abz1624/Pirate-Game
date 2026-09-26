using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    [Serializable]
    public sealed class WeaponUpgradeAxis
    {
        public ConfigEnums.UpgradeAxis axis = ConfigEnums.UpgradeAxis.Damage;
        public float baseIncrement, offerWeight = 1f, maxAccumulated;
        public string displayFormat = string.Empty;
    }

    /// <summary>
    /// One weapon, filled from the V3 WeaponDefinition after CombatWeaponCatalog's runtime retunes. Pattern-specific
    /// constants (swivel bursts, keg arming, cutlass orbit...) are added with the Weapon Manager in M5.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Weapon", fileName = "Weapon")]
    public sealed class WeaponConfig : V4ConfigAsset
    {
        [ConfigIdentity] public string weaponId;
        public string displayName;
        [AppliesFrom(5)] public ConfigEnums.WeaponPattern pattern = ConfigEnums.WeaponPattern.AutoProjectile;
        [AppliesFrom(5)] public ConfigEnums.TargetingMode targeting = ConfigEnums.TargetingMode.Nearest;
        [AppliesFrom(5)] public string[] tags = Array.Empty<string>();
        [AppliesFrom(5)] public float baseDamage = 10f, baseCooldown = 1.2f, range = 14f;
        [AppliesFrom(5)] public int baseQuantity = 1;
        [AppliesFrom(5)] public float projectileSpeed = 16f, duration = 2f, size = 1f, knockback = 1f;
        [AppliesFrom(5)] public float fanSpreadDegrees = 50f, targetArcDegrees = 120f;
        [AppliesFrom(5)] public bool preferElite, preferHighestHull;
        [AppliesFrom(5)] public int maxLevel = 12;
        [AppliesFrom(5)] public ConfigEnums.UpgradeAxis[] validUpgradeAxes = Array.Empty<ConfigEnums.UpgradeAxis>();
        [AppliesFrom(5)] public WeaponUpgradeAxis[] upgradeAxes = Array.Empty<WeaponUpgradeAxis>();
        [AppliesFrom(5)] public float procCoefficient = 1f;
        [AppliesFrom(5)] public Color projectileColor = Color.white;
        [Tooltip("True for weapons created at runtime by CombatWeaponCatalog (no V3 asset).")] public bool runtimeCatalogWeapon;

        public override void Validate(ConfigValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(weaponId)) report.Error(this, nameof(weaponId), "missing weapon id");
            report.Positive(this, nameof(baseCooldown), baseCooldown);
            report.Positive(this, nameof(range), range);
            report.NotNegative(this, nameof(baseDamage), baseDamage);
            if (maxLevel < 1) report.Error(this, nameof(maxLevel), "must be at least 1");
            report.WeightsNotZero(this, nameof(upgradeAxes), Array.ConvertAll(upgradeAxes, a => a.offerWeight));
        }
    }
}
