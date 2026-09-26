using RoguePirates.V4.Config;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Weapons
{
    /// <summary>
    /// The weapon module's targeting as the aim overlay reads it (M6): the same target pick and cluster prediction the
    /// weapons use, without handing presentation the targeting service, enemy query or modifier sources themselves.
    /// Target resolution is pure (no random draws, no state change).
    /// </summary>
    public sealed class WeaponAimView : IWeaponAimView
    {
        readonly WeaponCombat _combat;

        public WeaponAimView(WeaponCombat combat) => _combat = combat;

        public bool TryResolveTarget(WeaponConfig weapon, out Vector3 targetPosition, out Vector3 aimDirection)
        {
            targetPosition = default;
            aimDirection = default;
            var targeting = _combat?.Targeting;
            if (targeting == null || weapon == null || !targeting.TryResolveTarget(weapon, out var index, out aimDirection)) return false;
            var enemies = _combat.Enemies;
            if (enemies == null) return false;
            targetPosition = enemies.Position(index);
            return true;
        }

        public Vector3 PredictClusterGround(float range) =>
            _combat?.Targeting != null ? _combat.Targeting.PredictClusterGround(range) : Vector3.zero;

        public bool DirectionalTargeting
        {
            get
            {
                var targeting = _combat?.Targeting;
                return targeting == null || targeting.DirectionalTargeting == null || targeting.DirectionalTargeting();
            }
        }

        public float ArcDegrees(WeaponConfig weapon) => WeaponTargetingService.ResolveArcDegrees(weapon);

        public float SizeMultiplier => _combat?.Mods != null ? _combat.Mods.Size : 1f;
    }
}
