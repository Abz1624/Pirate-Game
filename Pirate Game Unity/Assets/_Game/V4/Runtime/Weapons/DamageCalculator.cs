using UnityEngine;

namespace RoguePirates.V4.Weapons
{
    /// <summary>What kind of damage a roll is (V3 DamageProcKind): only direct weapon hits and whitelisted bridges crit.</summary>
    public enum WeaponDamageKind { DirectWeapon, WhitelistedBridge, Dot, Environment, Reflected, TriggeredItem }

    public readonly struct DamageRoll
    {
        public readonly float Amount;
        public readonly bool IsCrit;
        public DamageRoll(float amount, bool isCrit) { Amount = amount; IsCrit = isCrit; }
    }

    /// <summary>
    /// Port of V3 DamageSystem.RollDamageDetailed and GameMath.ComputeDamage: the same expression order (so the floats
    /// are identical) and the same combat-stream draw (one per roll, only when the kind can crit).
    /// </summary>
    public sealed class DamageCalculator
    {
        readonly IWeaponModifiers _mods;
        readonly ICombatRandom _random;

        public DamageCalculator(IWeaponModifiers modifiers, ICombatRandom random)
        {
            _mods = modifiers;
            _random = random;
        }

        public static bool AllowsCrit(WeaponDamageKind kind) => kind == WeaponDamageKind.DirectWeapon || kind == WeaponDamageKind.WhitelistedBridge;

        public DamageRoll Roll(float baseDamage, float weaponLevelMult, bool targetIsElite, float targetArmor, float procCoefficient,
            string weaponId, WeaponDamageKind kind = WeaponDamageKind.DirectWeapon)
        {
            var captainDamage = _mods.CaptainAdditiveDamage;
            var captainWeaponMult = weaponId != null ? _mods.CaptainWeaponMultiplier(weaponId) : 1f;
            var itemMult = _mods.ItemGlobalDamageMultiplier;
            var critBonus = _mods.ItemCritChanceBonus;
            var isCrit = AllowsCrit(kind) && _random.CombatFloat() < (_mods.CritChance + critBonus) * procCoefficient;
            var amount = ComputeDamage(
                baseDamage,
                weaponLevelMult,
                _mods.GlobalDamage - 1f + captainDamage,
                1f,
                isCrit,
                _mods.CritDamage,
                targetIsElite,
                _mods.EliteDamage,
                1f,
                targetArmor) * captainWeaponMult * itemMult;
            return new DamageRoll(amount, isCrit);
        }

        public float RollAmount(float baseDamage, float weaponLevelMult, bool targetIsElite, float targetArmor, float procCoefficient,
            string weaponId, WeaponDamageKind kind = WeaponDamageKind.DirectWeapon) =>
            Roll(baseDamage, weaponLevelMult, targetIsElite, targetArmor, procCoefficient, weaponId, kind).Amount;

        /// <summary>V3 GameMath.ComputeDamage.</summary>
        public static float ComputeDamage(float baseDamage, float weaponLevelDamage, float globalDamageBonus, float weaponDamageBonus,
            bool isCrit, float critDamage, bool isElite, float eliteDamageMult, float statusMult, float targetArmor)
        {
            var weaponRoll = baseDamage * weaponLevelDamage * (1f + globalDamageBonus) * weaponDamageBonus;
            if (isCrit) weaponRoll *= critDamage;
            if (isElite) weaponRoll *= eliteDamageMult;
            var raw = weaponRoll * statusMult;
            var armorMult = 100f / (100f + Mathf.Max(0f, targetArmor));
            return Mathf.Max(1f, raw * armorMult);
        }
    }
}
