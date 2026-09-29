using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "RoguePirates/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        public WeaponPattern pattern;
        public TargetingMode targeting;
        public string[] tags;
        public float baseDamage = 10f;
        public float baseCooldown = 1.2f;
        public float range = 14f;
        public int baseQuantity = 1;
        public float projectileSpeed = 16f;
        public float duration = 2f;
        public float size = 1f;
        public float knockback = 1f;
        [Tooltip("Total yaw arc in degrees for FanVolley weapons (e.g. Broadside Battery).")]
        [Range(4f, 90f)] public float fanSpreadDegrees = 50f;
        [Tooltip("Forward/lateral arc used by directional targeting profiles.")]
        [Range(0f, 360f)] public float targetArcDegrees = 120f;
        public bool preferElite;
        public bool preferHighestHull;
        public int maxLevel = 12;
        public UpgradeAxis[] validUpgradeAxes;
        public WeaponUpgradeAxisConfig[] upgradeAxisConfigs;
        public float procCoefficient = 1f;
        public Color projectileColor = Color.white;

        public IReadOnlyList<WeaponUpgradeAxisConfig> GetUpgradeConfigs()
        {
            if (upgradeAxisConfigs != null && upgradeAxisConfigs.Length > 0)
                return upgradeAxisConfigs;
            return BuildDefaultUpgradeConfigs();
        }

        public bool TryGetAxisConfig(UpgradeAxis axis, out WeaponUpgradeAxisConfig config)
        {
            var configs = GetUpgradeConfigs();
            for (var i = 0; i < configs.Count; i++)
            {
                if (configs[i].axis == axis)
                {
                    config = configs[i];
                    return true;
                }
            }

            config = default;
            return false;
        }

        WeaponUpgradeAxisConfig[] BuildDefaultUpgradeConfigs()
        {
            return pattern switch
            {
                WeaponPattern.AutoProjectile => new[]
                {
                    Axis(UpgradeAxis.Damage, 0.08f, 1.2f, 2f),
                    Axis(UpgradeAxis.Cooldown, 0.05f, 1f, 0.6f),
                    Axis(UpgradeAxis.Quantity, 1f, 0.8f, 4f),
                    Axis(UpgradeAxis.Speed, 0.06f, 0.7f, 1.5f),
                    Axis(UpgradeAxis.Knockback, 0.05f, 0.6f, 1.5f)
                },
                WeaponPattern.FanVolley => new[]
                {
                    Axis(UpgradeAxis.Damage, 0.07f, 1.1f, 1.8f),
                    Axis(UpgradeAxis.Cooldown, 0.05f, 1f, 0.6f),
                    Axis(UpgradeAxis.Quantity, 1f, 1.2f, 5f),
                    Axis(UpgradeAxis.Size, 0.06f, 0.8f, 1.5f)
                },
                WeaponPattern.MineDrop => new[]
                {
                    Axis(UpgradeAxis.Damage, 0.1f, 1.2f, 2.5f),
                    Axis(UpgradeAxis.Cooldown, 0.04f, 0.9f, 0.5f),
                    Axis(UpgradeAxis.Size, 0.08f, 1f, 2f),
                    Axis(UpgradeAxis.Knockback, 0.06f, 0.7f, 1.5f)
                },
                WeaponPattern.ChainLightning => new[]
                {
                    Axis(UpgradeAxis.Damage, 0.08f, 1.1f, 2f),
                    Axis(UpgradeAxis.Cooldown, 0.05f, 1f, 0.6f),
                    Axis(UpgradeAxis.Quantity, 1f, 1.1f, 4f),
                    Axis(UpgradeAxis.Knockback, 0.04f, 0.6f, 1f)
                },
                WeaponPattern.Orbit => new[]
                {
                    Axis(UpgradeAxis.Damage, 0.07f, 1f, 1.8f),
                    Axis(UpgradeAxis.Cooldown, 0.05f, 1f, 0.6f),
                    Axis(UpgradeAxis.Quantity, 1f, 1.2f, 4f),
                    Axis(UpgradeAxis.Size, 0.07f, 0.9f, 1.8f)
                },
                WeaponPattern.TrailZone => new[]
                {
                    Axis(UpgradeAxis.Damage, 0.08f, 1.1f, 2f),
                    Axis(UpgradeAxis.Duration, 0.1f, 1f, 2f),
                    Axis(UpgradeAxis.Size, 0.08f, 1.1f, 2f)
                },
                WeaponPattern.AuraPulse => new[]
                {
                    Axis(UpgradeAxis.Damage, 0.07f, 1f, 1.8f),
                    Axis(UpgradeAxis.Cooldown, 0.05f, 1f, 0.6f),
                    Axis(UpgradeAxis.Size, 0.07f, 0.9f, 1.8f)
                },
                _ => new[] { Axis(UpgradeAxis.Damage, 0.08f, 1f, 2f), Axis(UpgradeAxis.Cooldown, 0.05f, 1f, 0.6f) }
            };
        }

        static WeaponUpgradeAxisConfig Axis(UpgradeAxis axis, float increment, float weight, float cap) => new()
        {
            axis = axis,
            baseIncrement = increment,
            offerWeight = weight,
            maxAccumulated = cap
        };
    }
}
