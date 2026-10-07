using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Playtest (3 Oct 2026, owner): level-up cards read at a glance: no descriptions, only what changes, from the current
    /// value to the next ("Damage 10.4 → 12.0", "Max Hull 100 → 106"). Reads the run's live loadout and stats.
    /// V5 M8z (7 Oct): through the run views (IRunViews: the loadout's weapon modifiers, the progression's stats), no
    /// longer the game object and the run manager.
    /// </summary>
    public static class CardStatLines
    {
        const string Arrow = " → ";

        /// <summary>One hit's damage (what the damage numbers show): base x level x the behaviour's per-hit multiplier.</summary>
        static float WeaponDamage(WeaponConfig c, int level, float damageBonus) =>
            c.baseDamage * (c.levelDamageFactor ? damageBonus * level * 0.1f + 1f : damageBonus) * HitMultiplier(c);

        static float HitMultiplier(WeaponConfig c)
        {
            switch (c.behavior)
            {
                case ConfigEnums.WeaponBehavior.Projectile: return c.lane != null && c.lane.enabled ? 1f : c.projectile.damageMultiplier;
                case ConfigEnums.WeaponBehavior.Fan: return c.fan.damageMultiplier;
                case ConfigEnums.WeaponBehavior.Orbit: return c.orbit.damageMultiplier;
                case ConfigEnums.WeaponBehavior.Trail: return c.trail.damageMultiplier;
                case ConfigEnums.WeaponBehavior.Mine: return c.mine.blastDamageMultiplier;
                case ConfigEnums.WeaponBehavior.Aura: return c.aura.damageMultiplier;
                default: return 1f;
            }
        }

        /// <summary>A weapon upgrade: damage (the level raises it on every upgrade) and each upgraded axis, current to next.</summary>
        public static string[] WeaponUpgrade(WeaponConfig c, IReadOnlyList<UpgradeLine> lines, int nextLevel, IRunViews views)
        {
            var loadout = views?.Loadout;
            if (c == null || loadout == null || !loadout.TryWeaponModifiers(c.weaponId, out var w)) return null;
            var result = new List<string>();
            float damageNext = w.DamageBonus, cooldownNext = w.CooldownMult, speedNext = w.SpeedMult, sizeNext = w.SizeMult, durationNext = w.DurationMult;
            var quantityNext = w.QuantityBonus;
            var slowNext = w.SlowBonus;
            var other = new List<string>();
            foreach (var l in lines ?? new UpgradeLine[0])
                switch (l.Axis)
                {
                    case ConfigEnums.UpgradeAxis.Damage: damageNext *= 1f + l.Value; break;
                    case ConfigEnums.UpgradeAxis.Cooldown: cooldownNext *= 1f - Mathf.Clamp01(l.Value); break;
                    case ConfigEnums.UpgradeAxis.Quantity: quantityNext += Mathf.Max(1, Mathf.RoundToInt(l.Value)); break;
                    case ConfigEnums.UpgradeAxis.Speed: speedNext *= 1f + l.Value; break;
                    case ConfigEnums.UpgradeAxis.Size: sizeNext *= 1f + l.Value; break;
                    case ConfigEnums.UpgradeAxis.Duration: durationNext *= 1f + l.Value; break;
                    case ConfigEnums.UpgradeAxis.Slow: slowNext += l.Value; break; // V5 M8z (7 Oct, D21)
                    default: other.Add($"{l.Axis} +{l.Value * 100f:0.#}%"); break;
                }
            var dNow = WeaponDamage(c, w.Level, w.DamageBonus);
            var dNext = WeaponDamage(c, Mathf.Min(c.maxLevel, Mathf.Max(w.Level + 1, nextLevel)), damageNext);
            result.Add($"Damage {dNow:0.#}{Arrow}{dNext:0.#}");
            if (!Mathf.Approximately(cooldownNext, w.CooldownMult)) result.Add($"Reload {c.baseCooldown * w.CooldownMult:0.##}s{Arrow}{c.baseCooldown * cooldownNext:0.##}s");
            if (quantityNext != w.QuantityBonus) result.Add($"Shots {c.baseQuantity + w.QuantityBonus}{Arrow}{c.baseQuantity + quantityNext}");
            if (!Mathf.Approximately(speedNext, w.SpeedMult)) result.Add($"Shot Speed {w.SpeedMult * 100f:0}%{Arrow}{speedNext * 100f:0}%");
            if (!Mathf.Approximately(sizeNext, w.SizeMult)) result.Add($"Size {w.SizeMult * 100f:0}%{Arrow}{sizeNext * 100f:0}%");
            if (!Mathf.Approximately(durationNext, w.DurationMult)) result.Add($"Duration {w.DurationMult * 100f:0}%{Arrow}{durationNext * 100f:0}%");
            if (!Mathf.Approximately(slowNext, w.SlowBonus))
            {
                var slow = BaseSlow(c);
                result.Add($"Slow {(slow + w.SlowBonus) * 100f:0}%{Arrow}{(slow + slowNext) * 100f:0}%");
            }
            // V5 M8z (7 Oct, D21, Q20): a rank milestone the upgrade reaches.
            var reached = Mathf.Min(c.maxLevel, Mathf.Max(w.Level + 1, nextLevel));
            foreach (var m in c.milestones ?? new WeaponMilestone[0])
                if (m != null && m.level > w.Level && m.level <= reached && !string.IsNullOrEmpty(m.description)) result.Add($"Rank {m.level}: {m.description}");
            result.AddRange(other);
            return result.ToArray();
        }

        /// <summary>V5 M8z (7 Oct, D21): the weapon's own slow share (its slowing block's), 0 when it does not slow.</summary>
        static float BaseSlow(WeaponConfig c) =>
            Mathf.Max(c.predictedArea.slowFraction, Mathf.Max(c.aura.slowFraction, c.projectile.slowFraction));

        /// <summary>A new weapon: its level 1 numbers.</summary>
        public static string[] NewWeapon(WeaponConfig c)
        {
            var lines = new List<string> { $"Damage {WeaponDamage(c, 1, 1f):0.#}", $"Reload {c.baseCooldown:0.##}s" };
            if (c.baseQuantity > 1) lines.Add($"Shots {c.baseQuantity}");
            if (BaseSlow(c) > 0f) lines.Add($"Slow {BaseSlow(c) * 100f:0}%"); // V5 M8z (7 Oct, D21)
            return lines.ToArray();
        }

        /// <summary>A Logbook: its stat, current to next (flat stats as numbers, chances as percentages, the rest against their base).</summary>
        public static string[] Logbook(LogbookConfig logbook, float bonus, IRunViews views)
        {
            if (logbook == null) return null;
            var stat = (StatId)logbook.stat;
            var name = StatDisplay.GetDisplayName(stat);
            var stats = views?.Progression;
            var now = stats != null ? stats.StatValue(stat) : 0f;
            var next = (now + bonus) * Mathf.Max(0.0001f, logbook.perLevelMultiplicative);
            switch (stat)
            {
                case StatId.Hull: case StatId.Shield: case StatId.Armor:
                    return new[] { $"{name} {now:0}{Arrow}{next:0}" };
                case StatId.HullRegen:
                    return new[] { $"{name} {now:0.#}/s{Arrow}{next:0.#}/s" };
                case StatId.Quantity:
                    return new[] { $"{name} +{now:0}{Arrow}+{next:0}" };
                case StatId.CritChance: case StatId.Evasion: case StatId.Luck:
                    return new[] { $"{name} {now * 100f:0.#}%{Arrow}{next * 100f:0.#}%" };
                default:
                {
                    var b = stats != null ? stats.StatBase(stat) : 0f;
                    if (b <= 0f) b = 1f;
                    return new[] { $"{name} {now / b * 100f:0.#}%{Arrow}{next / b * 100f:0.#}%" };
                }
            }
        }
    }
}
