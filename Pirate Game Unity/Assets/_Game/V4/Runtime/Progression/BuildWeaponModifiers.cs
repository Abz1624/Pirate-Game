using System;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Weapons;

namespace RoguePirates.V4.Progression
{
    /// <summary>
    /// What the weapons read from the build (M9): the run stats (the store the stat resolver writes), the Captain's
    /// passives and actives, and the items' triggered effects, read at each use as V3's DamageSystem did.
    /// Replaces LegacyWeaponModifiers, which read V3's Captain and item singletons.
    /// </summary>
    public sealed class BuildWeaponModifiers : IWeaponModifiers
    {
        readonly Func<PlayerStats> _stats;
        readonly CaptainModule _captain;
        readonly ItemModule _items;

        readonly Func<string, bool> _isLightning;

        /// <param name="isLightning">The weapon is a lightning weapon (its config's area-strike behavior).</param>
        public BuildWeaponModifiers(Func<PlayerStats> stats, CaptainModule captain, ItemModule items, Func<string, bool> isLightning = null)
        {
            _stats = stats;
            _captain = captain;
            _items = items;
            _isLightning = isLightning;
        }

        PlayerStats Stats => _stats();
        public float AttackSpeed => Stats.AttackSpeed;
        public float ProjectileSpeed => Stats.ProjectileSpeed;
        public float Size => Stats.Size;
        public float Duration => Stats.Duration;
        public float Quantity => Stats.Get(StatId.Quantity);
        public float CritChance => Stats.CritChance;
        public float CritDamage => Stats.CritDamage;
        public float EliteDamage => Stats.EliteDamage;
        public float GlobalDamage => Stats.GlobalDamage;

        public float CaptainAdditiveDamage => _captain.AdditiveDamageBonus();
        /// <summary>Running Fuse, and with R-49's switch Roxie's lightning ×1.1 on area-strike (lightning) weapons.</summary>
        public float CaptainWeaponMultiplier(string weaponId)
        {
            var mult = _captain.WeaponDamageMultiplier(weaponId);
            var lightning = _captain.LightningDamageMultiplier;
            if (lightning != 1f && _isLightning != null && _isLightning(weaponId)) mult *= lightning;
            return mult;
        }
        public float ItemGlobalDamageMultiplier => _items.GlobalDamageMultiplier();
        public float ItemCritChanceBonus => _items.CritChanceBonus();
        public float ItemAttackSpeedMultiplier => _items.AttackSpeedMultiplier();
        public float FlameRangeMultiplier => _captain.FlameRangeMultiplier;
        public float BurnDamageMultiplier => _captain.BurnDamageMultiplier;
        public float PowderKegBlastRadiusMultiplier => _captain.PowderKegBlastRadiusMultiplier;
        public float ExplosionDamageMultiplier => _items.ExplosionDamageMultiplier();
        public void ExplosionResolved() => _items.NotifyExplosionResolved();
        public int ExtraLightningJumps => _items.ExtraLightningJumps();
    }
}
