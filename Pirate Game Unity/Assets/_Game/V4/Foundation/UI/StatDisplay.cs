using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.UI
{
    /// <summary>Friendly names and formatting for level-up / stat lines.</summary>
    public static class StatDisplay
    {
        public static string GetDisplayName(StatId stat) => stat switch
        {
            StatId.Hull => "Max Hull",
            StatId.HullRegen => "Hull Regen",
            StatId.Damage => "Damage",
            StatId.AttackSpeed => "Fire Rate",
            StatId.Quantity => "Projectiles",
            StatId.Size => "Shot Size",
            StatId.MoveSpeed => "Sail Speed",
            StatId.XPGain => "XP Gain",
            StatId.GoldGain => "Gold Gain",
            StatId.PickupRadius => "Pickup Range",
            StatId.Luck => "Luck",
            StatId.CritChance => "Crit Chance",
            StatId.CritDamage => "Crit Damage",
            StatId.ProjectileSpeed => "Projectile Speed",
            StatId.Knockback => "Knockback",
            StatId.Armor => "Armor",
            StatId.Evasion => "Evasion",
            StatId.Shield => "Shield",
            StatId.Duration => "Duration",
            StatId.EliteDamage => "Elite Damage",
            StatId.DoubloonGain => "Doubloon Gain",
            _ => stat.ToString()
        };

        /// <summary>Format a logbook per-level bonus (additive value before rarity mult).</summary>
        public static string FormatLogbookBonus(StatId stat, float value)
        {
            if (stat == StatId.Quantity)
                return $"+{Mathf.Max(1, Mathf.RoundToInt(value))} {GetDisplayName(stat)}";

            if (UsesFlatAdditive(stat, value))
                return $"+{FormatFlat(value)} {GetDisplayName(stat)}";

            return $"+{value * 100f:0.#}% {GetDisplayName(stat)}";
        }

        public static bool UsesFlatAdditive(StatId stat, float value)
        {
            if (stat == StatId.Hull)
                return true;
            if (stat == StatId.HullRegen)
                return value >= 0.15f;
            if (stat == StatId.CritChance && value <= 0.25f)
                return true;
            if (stat == StatId.Luck && value <= 0.2f)
                return true;
            if (stat == StatId.Shield)
                return value >= 1f;
            return false;
        }

        static string FormatFlat(float value)
        {
            if (Mathf.Approximately(value, Mathf.Round(value)))
                return Mathf.RoundToInt(value).ToString();
            return value.ToString("0.#");
        }
    }
}
