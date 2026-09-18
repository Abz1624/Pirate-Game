using System;
using UnityEngine;

namespace HighSeas.Data
{
    [Serializable]
    public sealed class WeaponJuiceVfxBinding
    {
        public string weaponId;
        [Tooltip("Projectile mesh / trail prefab for this weapon.")]
        public GameObject projectilePrefab;
        [Tooltip("Muzzle burst prefab (often a gun prefab with CannonSmoke child).")]
        public GameObject muzzleVfxPrefab;
        [Tooltip("Impact / explosion prefab for this weapon.")]
        public GameObject impactVfxPrefab;
        [Min(0.1f)] public float projectileScaleMult = 1f;
        [Min(0.1f)] public float muzzleScaleMult = 1f;
        [Min(0.1f)] public float impactScaleMult = 1f;
        [Min(0.1f)] public float projectileLifeMult = 1f;
    }
}
