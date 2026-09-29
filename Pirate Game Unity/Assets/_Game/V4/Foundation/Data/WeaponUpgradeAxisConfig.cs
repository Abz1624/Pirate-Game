using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    [System.Serializable]
    public struct WeaponUpgradeAxisConfig
    {
        public UpgradeAxis axis;
        [Tooltip("Additive percent for damage/cooldown/speed/size/duration/knockback. Use 1 for flat quantity.")]
        public float baseIncrement;
        public float offerWeight;
        [Tooltip("0 = no cap.")]
        public float maxAccumulated;
        [Tooltip("Optional override. Leave empty for default formatting.")]
        public string displayFormat;

        public string FormatValue(float rolledValue)
        {
            if (!string.IsNullOrEmpty(displayFormat))
                return string.Format(displayFormat, rolledValue);

            return axis switch
            {
                UpgradeAxis.Damage => $"+{rolledValue * 100f:0}% Damage",
                UpgradeAxis.Cooldown => $"+{rolledValue * 100f:0}% Attack Speed",
                UpgradeAxis.Quantity => $"+{Mathf.RoundToInt(rolledValue)} Projectiles",
                UpgradeAxis.Speed => $"+{rolledValue * 100f:0}% Projectile Speed",
                UpgradeAxis.Size => $"+{rolledValue * 100f:0}% Size",
                UpgradeAxis.Duration => $"+{rolledValue * 100f:0}% Duration",
                UpgradeAxis.Knockback => $"+{rolledValue * 100f:0}% Knockback",
                UpgradeAxis.Crit => $"+{rolledValue * 100f:0}% Crit Chance",
                UpgradeAxis.CritDamage => $"+{rolledValue * 100f:0}% Crit Damage",
                UpgradeAxis.Pierce => $"+{Mathf.RoundToInt(rolledValue)} Pierce",
                _ => $"+{rolledValue:0.##} {axis}"
            };
        }
    }
}
