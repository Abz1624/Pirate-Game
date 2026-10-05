using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>
    /// V5 after the demo (30 Sep 2026): the owner kept the demo's tuning as the new baseline. These are the demo's balance
    /// changes with their values before it (V5 M3), so a runtime copy can be put back to the pre-demo game: the balance
    /// re-check compares the two, and the calibration against the autopilot's real (pre-demo) runs plays the old values.
    /// </summary>
    public static class DemoBaseline
    {
        public static readonly (string configId, string path, string value)[] PreDemo =
        {
            ("pickups.saltwind", "xpPickupMultiplier", "1"),
            ("run", "shipSpeedMultiplier", "1"),
            ("timeline.saltwind", "phases[0].pools[0].share", "1"),
            ("difficulty.saltwind", "tierUnlocks[0].minMinutes", "1.5"),
            ("enemy.hammerhead", "combat.chargeWindUp", "1.2"),
            ("enemy.hammerhead", "combat.chargeCooldown", "2"),
            ("enemy.rammer_raft", "combat.chargeWindUp", "1.4"),
            ("enemy.rammer_raft", "combat.chargeCooldown", "2.2"),
            ("spawner.saltwind_elite_kraken", "placement.requireOutsidePlayerFov", "true"),
            ("weapon.long_nine", "range", "24"),
            ("weapon.deck_cannons", "range", "22"),
            ("weapon.mortar", "range", "18"),
            ("weapon.harpoon_gun", "range", "16"),
            ("weapon.lightning_mast", "range", "16"),
            ("weapon.swivel_guns", "range", "16"),
            ("weapon.broadside_battery", "range", "14"),
            ("weapon.deck_cannons", "projectile.lifeSeconds", "3"),
            ("weapon.harpoon_gun", "projectile.lifeSeconds", "3"),
            ("weapon.swivel_guns", "projectile.lifeSeconds", "2.4"),
            ("weapon.broadside_battery", "fan.lifeSeconds", "1.2"),
        };

        /// <summary>
        /// The retune around the demo's slower levelling (owner, 30 Sep 2026: keep XP ×0.5 and retune): enemies grow
        /// gentler (HP growth ×0.55, damage growth ×0.7), the evolution card comes from weapon level 6 (was 8) so focused
        /// builds still evolve in most runs (the Lightning Mast 2% weaker to stay in parity there), and the level and level-up
        /// pacing targets follow the slower pace. Each entry
        /// holds its value before the retune (V5 M3). Written by Editor ▸ RoguePirates ▸ V5 ▸ Write demo-baseline retune.
        /// </summary>
        public static readonly (string configId, string path, string value, string pre)[] Retune =
        {
            ("difficulty.saltwind", "enemyHpPerMinute.c1", "0.0495", "0.09"),
            ("difficulty.saltwind", "enemyHpPerMinute.c2", "0.0099", "0.018"),
            ("difficulty.saltwind", "ordinaryHp.c1", "0.044", "0.08"),
            ("difficulty.saltwind", "ordinaryHp.c2", "0.0055", "0.01"),
            ("difficulty.saltwind", "enemyDamagePerMinute.c1", "0.0175", "0.025"),
            ("difficulty.saltwind", "ordinaryDamage.c1", "0.042", "0.06"),
            ("difficulty.saltwind", "ordinaryDamage.c2", "0.007", "0.01"),
            ("progression.saltwind", "evolutionUnlockLevel", "6", "8"),
            // Weapon parity is now also checked at level 6 (the evolution level): the Lightning Mast's extra strike put it at
            // ×1.20 of its role there; 2% off its base damage keeps every level in ±20%.
            ("weapon.lightning_mast", "baseDamage", "10.56", "10.775537"),
            ("balance_targets.saltwind", "levelUpGapMinute1.min", "12", "4"), // ±20% around the chosen 15 s (owner, 1 Oct)
            ("balance_targets.saltwind", "levelUpGapMinute1.max", "18", "12"),
            ("balance_targets.saltwind", "levelUpGapTo10.min", "20", "13"), // ±20% around the chosen 25 s
            ("balance_targets.saltwind", "levelUpGapTo10.max", "30", "20"),
            ("balance_targets.saltwind", "personalities[3].levelAt10.min", "21", "30"),
            ("balance_targets.saltwind", "personalities[3].levelAt10.max", "35", "46"),
            ("balance_targets.saltwind", "minutes[6].level.min", "15", "18"),
            ("balance_targets.saltwind", "minutes[6].level.max", "26", "29"),
            ("balance_targets.saltwind", "minutes[7].level.min", "17", "21"),
            ("balance_targets.saltwind", "minutes[7].level.max", "29", "32"),
            ("balance_targets.saltwind", "minutes[8].level.min", "18", "23"),
            ("balance_targets.saltwind", "minutes[8].level.max", "31", "35"),
            ("balance_targets.saltwind", "minutes[9].level.min", "20", "25"),
            ("balance_targets.saltwind", "minutes[9].level.max", "33", "38"),
            ("balance_targets.saltwind", "minutes[10].level.min", "21", "28"),
            ("balance_targets.saltwind", "minutes[10].level.max", "35", "42"),
            ("balance_targets.saltwind", "minutes[11].level.min", "22", "30"),
            ("balance_targets.saltwind", "minutes[11].level.max", "37", "45"),
            ("balance_targets.saltwind", "minutes[12].level.min", "23", "32"),
            ("balance_targets.saltwind", "minutes[12].level.max", "39", "48"),
        };

        /// <summary>
        /// The director's on-track offense growth after the retune (SimBatch.OnTrackGrowth: the Steady Captain in Observe
        /// mode, 60 runs, seed 11, on the retuned data, re-measured after the review's XP-order fix; 15:00 extrapolated from
        /// 12:00 by M2's ratio ×1.39).
        /// </summary>
        public static readonly (float minute, float growth)[] OnTrackGrowth =
        {
            (0f, 1f), (1f, 4.2f), (2f, 5.7f), (3f, 7.3f), (4f, 9.7f), (5f, 13.1f), (6f, 16.8f), (7f, 20.2f), (8f, 25.9f), (9f, 31f), (10f, 33.8f), (12f, 47.6f), (15f, 66.2f),
        };

        /// <summary>The director's on-track growth before the retune (V5 M2).</summary>
        public static readonly (float minute, float growth)[] OnTrackGrowthM2 =
        {
            (0f, 1f), (1f, 5.4f), (2f, 8.3f), (3f, 11f), (4f, 14.7f), (5f, 17.7f), (6f, 23.3f), (7f, 29f), (8f, 35f), (9f, 40.8f), (10f, 48.8f), (12f, 57.4f), (15f, 80f),
        };

        /// <summary>The retune's values before it (V5 M3) on a runtime copy, the director's curve included.</summary>
        public static bool RevertRetune(ConfigSetRuntime rt, List<string> errors)
        {
            var ok = true;
            foreach (var (id, path, _, pre) in Retune)
            {
                if (rt.TryApplyEdit(id, path, pre, 0f, out var error)) continue;
                errors?.Add($"{id}.{path}: {error}");
                ok = false;
            }
            var director = rt.Single<DirectorConfig>();
            if (director != null) director.onTrackGrowth = DirectorConfig.LinearCurve(OnTrackGrowthM2);
            return ok;
        }

        /// <summary>
        /// Puts the pre-demo values back on a runtime copy (all of them, or those <paramref name="only"/> picks). Edits
        /// the copies in place; unlike PresetCodec.Apply it keeps any other change already made to the copy.
        /// </summary>
        public static bool Revert(ConfigSetRuntime rt, List<string> errors, Func<string, string, bool> only = null)
        {
            var ok = true;
            foreach (var (id, path, value) in PreDemo.Where(e => only == null || only(e.configId, e.path)))
            {
                if (rt.TryApplyEdit(id, path, value, 0f, out var error)) continue;
                errors?.Add($"{id}.{path}: {error}");
                ok = false;
            }
            return ok;
        }
    }
}
