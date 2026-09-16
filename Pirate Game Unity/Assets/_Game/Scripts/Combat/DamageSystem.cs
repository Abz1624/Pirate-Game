using HighSeas.Core;
using HighSeas.V3;

namespace HighSeas.Combat
{
    public static class DamageSystem
    {
        public readonly struct DamageRoll
        {
            public readonly float amount;
            public readonly bool isCrit;

            public DamageRoll(float amount, bool isCrit)
            {
                this.amount = amount;
                this.isCrit = isCrit;
            }
        }

        public static DamageRoll RollDamageDetailed(
            float baseDamage,
            float weaponLevelMult,
            PlayerStats stats,
            bool targetIsElite,
            float targetArmor,
            RunRng rng,
            float procCoefficient = 1f,
            string weaponId = null,
            DamageProcKind procKind = DamageProcKind.DirectWeapon)
        {
            var captain = V3CaptainController.Instance;
            var captainDamage = captain != null ? captain.GetAdditiveDamageBonus() : 0f;
            var captainWeaponMult = weaponId != null && captain != null
                ? captain.GetWeaponDamageMultiplier(weaponId)
                : 1f;
            var itemEffects = V3RunItemEffects.Instance;
            var itemMult = itemEffects != null ? itemEffects.GetGlobalDamageMultiplier(null) : 1f;
            var critBonus = itemEffects != null ? itemEffects.GetCritChanceBonus() : 0f;
            var isCrit = DamageProcPolicy.AllowsCrit(procKind)
                && rng.CombatFloat() < (stats.CritChance + critBonus) * procCoefficient;
            var amount = GameMath.ComputeDamage(
                baseDamage,
                weaponLevelMult,
                stats.GlobalDamage - 1f + captainDamage,
                1f,
                isCrit,
                stats.CritDamage,
                targetIsElite,
                stats.EliteDamage,
                1f,
                targetArmor) * captainWeaponMult * itemMult;
            return new DamageRoll(amount, isCrit);
        }

        public static float RollDamage(
            float baseDamage,
            float weaponLevelMult,
            PlayerStats stats,
            bool targetIsElite,
            float targetArmor,
            RunRng rng,
            float procCoefficient = 1f,
            string weaponId = null,
            DamageProcKind procKind = DamageProcKind.DirectWeapon)
        {
            return RollDamageDetailed(
                baseDamage,
                weaponLevelMult,
                stats,
                targetIsElite,
                targetArmor,
                rng,
                procCoefficient,
                weaponId,
                procKind).amount;
        }
    }
}
