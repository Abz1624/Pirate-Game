using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "WeaponDefinition", menuName = "HighSeas/Weapon Definition")]
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
        public int maxLevel = 12;
        public UpgradeAxis[] validUpgradeAxes;
        public float procCoefficient = 1f;
        public Color projectileColor = Color.white;
    }
}
