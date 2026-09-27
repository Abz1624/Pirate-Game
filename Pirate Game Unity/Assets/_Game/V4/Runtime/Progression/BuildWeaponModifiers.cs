using System;
using HighSeas.Core;
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

        public BuildWeaponModifiers(Func<PlayerStats> stats, CaptainModule captain, ItemModule items)
        {
            _stats = stats;
            _captain = captain;
            _items = items;
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
        public float CaptainWeaponMultiplier(string weaponId) => _captain.WeaponDamageMultiplier(weaponId);
        public float ItemGlobalDamageMultiplier => _items.GlobalDamageMultiplier();
        public float ItemCritChanceBonus => _items.CritChanceBonus();
        public float ItemAttackSpeedMultiplier => _items.AttackSpeedMultiplier();
        public float FlameRangeMultiplier => _captain.FlameRangeMultiplier;
        public float BurnDamageMultiplier => _captain.BurnDamageMultiplier;
        public float PowderKegBlastRadiusMultiplier => _captain.PowderKegBlastRadiusMultiplier;
    }
}
