using HighSeas.Core;
using HighSeas.V3;

namespace HighSeas.Combat
{
    public static class DamageSystem
    {
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
            var captain = V3CaptainController.Instance;
            var captainDamage = captain != null ? captain.GetAdditiveDamageBonus() : 0f;
            var captainWeaponMult = weaponId != null && captain != null
                ? captain.GetWeaponDamageMultiplier(weaponId)
                : 1f;
            var isCrit = DamageProcPolicy.AllowsCrit(procKind)
                && rng.CombatFloat() < stats.CritChance * procCoefficient;
            return GameMath.ComputeDamage(
                baseDamage,
                weaponLevelMult,
                stats.GlobalDamage - 1f + captainDamage,
                1f,
                isCrit,
                stats.CritDamage,
                targetIsElite,
                stats.EliteDamage,
                1f,
                targetArmor) * captainWeaponMult;
        }
    }
}
