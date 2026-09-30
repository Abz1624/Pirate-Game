using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Config;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Balance
{
    /// <summary>What a weapon is for, by its firing behaviour (V5 M3 parity bands compare weapons within a role).</summary>
    public enum WeaponRole { SingleTarget, Crowd, Zone }

    /// <summary>One weapon at one level: its median damage per second on one target and on a crowd (× its behaviour's coverage).</summary>
    public struct WeaponLevelPower
    {
        public int Level;
        public float SingleDps, CrowdDps;
        public float RolePower(WeaponRole role) => role == WeaponRole.SingleTarget ? SingleDps : CrowdDps;
    }

    public sealed class WeaponPowerCurve
    {
        public WeaponConfig Weapon;
        public WeaponRole Role;
        public readonly List<WeaponLevelPower> Levels = new List<WeaponLevelPower>();
        public WeaponLevelPower At(int level) => Levels.First(l => l.Level == Mathf.Clamp(level, 1, Levels.Count));
    }

    /// <summary>
    /// V5 M3 (I3.1): each weapon's power per level. The upgrade path is the game's own: rarities from
    /// <see cref="OfferGenerator.RollRarity"/> (luck 0), cards from <see cref="OfferGenerator.WeaponUpgradeCard"/>, applied by
    /// <see cref="ProgressionLoadout.ApplyUpgradePackage"/> with the card's levels; damage, cooldown and volley from
    /// <see cref="WeaponStatResolver"/> with neutral player stats. Crowd power multiplies by the behaviour's coverage
    /// (<see cref="DirectorConfig.behaviorCoverage"/>), the size upgrades, and for streams, trails, auras and mines the
    /// duration upgrades. Evolutions are excluded (they add effects the numbers do not show).
    /// The medians of many seeded paths make the curve.
    /// </summary>
    public static class WeaponModel
    {
        public static WeaponRole RoleOf(ConfigEnums.WeaponBehavior b)
        {
            switch (b)
            {
                case ConfigEnums.WeaponBehavior.Projectile: return WeaponRole.SingleTarget;
                case ConfigEnums.WeaponBehavior.Fan:
                case ConfigEnums.WeaponBehavior.Orbit:
                case ConfigEnums.WeaponBehavior.ConeStream:
                case ConfigEnums.WeaponBehavior.Aura: return WeaponRole.Crowd;
                default: return WeaponRole.Zone; // Mine, Trail, AreaStrike, PredictedArea, Summon
            }
        }

        sealed class Rng : IOfferRandom
        {
            readonly System.Random _r;
            public Rng(int seed) => _r = new System.Random(seed);
            public float LootFloat() => (float)_r.NextDouble();
        }

        /// <summary>Neutral player stats (no Logbooks, items or Captain): the weapon alone.</summary>
        sealed class NeutralMods : IWeaponModifiers
        {
            public float AttackSpeed => 1f;
            public float ProjectileSpeed => 1f;
            public float Size => 1f;
            public float Duration => 1f;
            public float Quantity => 0f;
            public float CritChance => 0.05f;
            public float CritDamage => 1.5f;
            public float EliteDamage => 1f;
            public float GlobalDamage => 1f;
            public float CaptainAdditiveDamage => 0f;
            public float CaptainWeaponMultiplier(string weaponId) => 1f;
            public float ItemGlobalDamageMultiplier => 1f;
            public float ItemCritChanceBonus => 0f;
            public float ItemAttackSpeedMultiplier => 1f;
            public float FlameRangeMultiplier => 1f;
            public float BurnDamageMultiplier => 1f;
            public float PowderKegBlastRadiusMultiplier => 1f;
            public float ExplosionDamageMultiplier => 1f;
            public void ExplosionResolved() { }
            public int ExtraLightningJumps => 0;
        }

        public static List<WeaponPowerCurve> Curves(SimContent c, int paths = 300, int seed = 1) =>
            c.WeaponsInOrder.Select(w => CurveFor(c, w, paths, seed)).ToList();

        public static WeaponPowerCurve CurveFor(SimContent c, WeaponConfig w, int paths = 300, int seed = 1)
        {
            var generator = new OfferGenerator(() => c.Progression, () => c.OfferWeights, () => c.WeaponsInOrder, () => c.LogbooksInOrder, () => c.Evolutions, () => false);
            var mods = new NeutralMods();
            var coverage = c.Director != null ? c.Director.behaviorCoverage : null;
            var max = Mathf.Max(1, w.maxLevel);
            var single = new List<float>[max + 1];
            var crowd = new List<float>[max + 1];
            for (var l = 1; l <= max; l++) { single[l] = new List<float>(paths); crowd[l] = new List<float>(paths); }
            var cover = coverage != null && (int)w.behavior < coverage.Length ? coverage[(int)w.behavior] : 1f;
            for (var path = 0; path < paths; path++)
            {
                var rng = new Rng(SimBatch.RunSeed(seed, w.weaponId, path));
                var loadout = new ProgressionLoadout(id => id == w.weaponId ? w : null, _ => null) { NewWeaponDamageBonus = c.Progression.newWeaponDamageBonus };
                loadout.EnsureSlotCapacity(1);
                loadout.Equip(w.weaponId);
                var lw = loadout.Weapons[0];
                var recorded = 0;
                var guard = 0;
                while (recorded < max && guard++ < 100)
                {
                    // Record this state for every level reached since the last record (multi-level cards skip levels).
                    var dps = Dps(lw, mods);
                    // Crowd power: coverage widens with size (more targets in the cone, blast or orbit), and streams, trails,
                    // auras and mines hit for longer with duration (a model assumption; single-target power ignores both).
                    var area = cover * lw.SizeMult * (LastsLonger(w.behavior) ? lw.DurationMult : 1f);
                    while (recorded < lw.Level) { recorded++; single[recorded].Add(dps); crowd[recorded].Add(dps * area); }
                    if (lw.Level >= max) break;
                    var rarity = generator.RollRarity(rng, 0f);
                    var card = generator.WeaponUpgradeCard(w, lw.Level, rarity, rng);
                    loadout.ApplyUpgradePackage(0, card.Lines, Mathf.Max(1, card.NextLevel - card.CurrentLevel));
                }
            }
            var curve = new WeaponPowerCurve { Weapon = w, Role = RoleOf(w.behavior) };
            for (var l = 1; l <= max; l++)
                curve.Levels.Add(new WeaponLevelPower { Level = l, SingleDps = SimBatch.Percentile(single[l], 0.5f), CrowdDps = SimBatch.Percentile(crowd[l], 0.5f) });
            return curve;
        }

        /// <summary>One weapon's retune: base damage × DamageScale; every non-volley upgrade increment and cap × GrowthScale.</summary>
        public struct ParityTune
        {
            public string WeaponId;
            public float DamageScale, GrowthScale, WorstBefore, WorstAfter;
        }

        static readonly float[] GrowthGrid = { 0.5f, 0.6f, 0.7f, 0.8f, 0.9f, 1f, 1.15f, 1.3f, 1.5f, 1.75f, 2f };
        /// <summary>Growth changes stay within ×0.5–×2 of the original in total, so every upgrade card still feels like one.</summary>
        const float MinGrowth = 0.5f, MaxGrowth = 2f;

        static void ScaleGrowth(WeaponConfig w, float factor)
        {
            if (w.upgradeAxes == null) return;
            foreach (var a in w.upgradeAxes)
            {
                if (a == null || a.axis == ConfigEnums.UpgradeAxis.Quantity) continue;
                a.baseIncrement *= factor;
                if (a.axis == ConfigEnums.UpgradeAxis.Cooldown) a.baseIncrement = Mathf.Min(a.baseIncrement, 0.3f);
                if (a.maxAccumulated > 0f) a.maxAccumulated *= factor;
            }
        }

        static float Worst(IReadOnlyList<WeaponPowerCurve> curves, WeaponPowerCurve w) =>
            ParityLevels.Max(l => Mathf.Abs(Mathf.Log(Ratio(curves, w, l))));

        /// <summary>
        /// V5 M3 (G3.2): brings every weapon toward its role's target at levels 1, 4 and 12 by searching its growth (the
        /// upgrade increments) and then setting its base damage to centre it, a few rounds (the targets move as weapons
        /// do). Edits the weapon configs of <paramref name="c"/> (a run's copies); the Editor writes them to the assets.
        /// </summary>
        public static List<ParityTune> TuneParity(SimContent c, int paths = 150, int rounds = 4)
        {
            var tunes = c.WeaponsInOrder.ToDictionary(w => w.weaponId, w => new ParityTune { WeaponId = w.weaponId, DamageScale = 1f, GrowthScale = 1f });
            var before = Curves(c, paths);
            foreach (var w in before) { var t = tunes[w.Weapon.weaponId]; t.WorstBefore = Worst(before, w); tunes[w.Weapon.weaponId] = t; }
            for (var round = 0; round < rounds; round++)
            {
                var curves = Curves(c, paths);
                foreach (var cur in curves)
                {
                    var w = cur.Weapon;
                    var others = curves.Where(x => x != cur).ToList();
                    var bestG = 1f;
                    var bestScore = float.MaxValue;
                    var bestF = 1f;
                    var sofar = tunes[w.weaponId].GrowthScale;
                    foreach (var g in GrowthGrid)
                    {
                        if (sofar * g < MinGrowth - 1e-4f || sofar * g > MaxGrowth + 1e-4f) continue;
                        ScaleGrowth(w, g);
                        var trial = CurveFor(c, w, paths);
                        ScaleGrowth(w, 1f / g);
                        var all = others.Append(trial).ToList();
                        var logs = ParityLevels.Select(l => Mathf.Log(Ratio(all, trial, l))).ToList();
                        var f = Mathf.Exp(-logs.Average());
                        var score = logs.Max(x => Mathf.Abs(x + Mathf.Log(f)));
                        if (score < bestScore - 1e-4f) { bestScore = score; bestG = g; bestF = f; }
                    }
                    ScaleGrowth(w, bestG);
                    w.baseDamage *= bestF;
                    var t = tunes[w.weaponId];
                    t.GrowthScale *= bestG;
                    t.DamageScale *= bestF;
                    tunes[w.weaponId] = t;
                }
            }
            var after = Curves(c, paths);
            foreach (var w in after) { var t = tunes[w.Weapon.weaponId]; t.WorstAfter = Worst(after, w); tunes[w.Weapon.weaponId] = t; }
            return tunes.Values.ToList();
        }

        static bool LastsLonger(ConfigEnums.WeaponBehavior b) =>
            b == ConfigEnums.WeaponBehavior.ConeStream || b == ConfigEnums.WeaponBehavior.Trail || b == ConfigEnums.WeaponBehavior.Aura || b == ConfigEnums.WeaponBehavior.Mine;

        static float Dps(LoadoutWeapon lw, IWeaponModifiers m)
        {
            var slot = new WeaponSlot
            {
                Config = lw.Config, Level = lw.Level, DamageBonus = lw.DamageBonus, QuantityBonus = lw.QuantityBonus, CooldownMult = lw.CooldownMult,
                SpeedMult = lw.SpeedMult, SizeMult = lw.SizeMult, DurationMult = lw.DurationMult, KnockbackMult = lw.KnockbackMult,
            };
            var damage = lw.Config.baseDamage * WeaponStatResolver.LevelMult(slot);
            var crit = 1f + Mathf.Clamp01(m.CritChance) * Mathf.Max(0f, m.CritDamage - 1f);
            return damage * crit * WeaponStatResolver.ProjectileQuantity(slot, m) / Mathf.Max(0.05f, WeaponStatResolver.Cooldown(slot, m, 1f));
        }

        /// <summary>A role's target at a level: the geometric mean of its weapons' role power.</summary>
        public static float RoleTarget(IEnumerable<WeaponPowerCurve> curves, WeaponRole role, int level)
        {
            var values = curves.Where(x => x.Role == role).Select(x => x.At(level).RolePower(role)).Where(v => v > 0f).ToList();
            return values.Count == 0 ? 0f : Mathf.Exp(values.Average(v => Mathf.Log(v)));
        }

        /// <summary>A weapon's power at a level against its role's target (1 = on target).</summary>
        public static float Ratio(IReadOnlyList<WeaponPowerCurve> curves, WeaponPowerCurve w, int level)
        {
            var target = RoleTarget(curves, w.Role, level);
            return target > 0f ? w.At(level).RolePower(w.Role) / target : float.NaN;
        }

        public static readonly int[] ParityLevels = { 1, 4, 6, 8, 12 }; // 6: where evolution unlocks since the demo-baseline retune; 8: in M3 (review)

        /// <summary>The Markdown parity table (plan/v5/phase-03-builds/weapon-parity.md).</summary>
        public static string Report(IReadOnlyList<WeaponPowerCurve> curves, float band = 0.2f)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("| Weapon | Role | " + string.Join(" | ", ParityLevels.Select(l => $"Lv {l} power (× role)")) + " | In ±" + (band * 100f).ToString("0") + "% |");
            sb.AppendLine("|---|---|" + string.Concat(ParityLevels.Select(_ => "---|")) + "---|");
            foreach (var w in curves.OrderBy(x => x.Role).ThenBy(x => x.Weapon.weaponId))
            {
                var cells = ParityLevels.Select(l => $"{w.At(l).RolePower(w.Role):0.0} (×{Ratio(curves, w, l):0.00})");
                var ok = ParityLevels.All(l => Mathf.Abs(Ratio(curves, w, l) - 1f) <= band + 1e-4f);
                sb.AppendLine($"| {w.Weapon.weaponId} | {w.Role} | {string.Join(" | ", cells)} | {(ok ? "✓" : "✗")} |");
            }
            return sb.ToString();
        }
    }
}
