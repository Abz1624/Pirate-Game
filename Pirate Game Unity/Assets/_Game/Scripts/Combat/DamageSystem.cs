using HighSeas.Core;

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
            float procCoefficient = 1f)
        {
            var isCrit = rng.CombatFloat() < stats.CritChance * procCoefficient;
            return GameMath.ComputeDamage(
                baseDamage,
                weaponLevelMult,
                stats.GlobalDamage - 1f,
                1f,
                isCrit,
                stats.CritDamage,
                targetIsElite,
                stats.EliteDamage,
                1f,
                targetArmor);
        }
    }
}
