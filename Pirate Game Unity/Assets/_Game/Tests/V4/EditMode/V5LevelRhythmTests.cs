using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M3c (owner, 1 Oct 2026: "1 1 2 3 5"): fewer level-ups whose gaps grow: the first level with the first kill,
    /// then 10, 10, 20, 30, 50 s, then 60 s, through a fitted XP table; weapon cards worth more levels (evolutions stay
    /// reachable) and an interim flat enemy scale (every personality's run length and boss wins held until M3d).
    /// V5 M8z (I8z.7): the three checks that ran the in-engine simulator (level times on another seed, every personality's
    /// run length, the per-minute level targets) were retired with it; the JavaScript model is checked on the owner's
    /// recorded sessions instead (plan/playtest/montecarlo/compare_session.mjs).
    /// </summary>
    public sealed class V5LevelRhythmTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        [Test]
        public void TargetGaps_FollowTheFibonacciSeries_ThenGrowTo60()
        {
            var gaps = Enumerable.Range(2, 9).Select(XpTableTuner.TargetGap).ToArray();
            CollectionAssert.AreEqual(new[] { 10f, 10f, 20f, 30f, 50f, 60f, 60f, 60f, 60f }, gaps);
            Assert.AreEqual(XpTableTuner.FirstKillSeconds, XpTableTuner.TargetGap(1), "the first level with the first kill");
            Assert.AreEqual(4, Enumerable.Range(1, 40).Count(l => XpTableTuner.TargetTime(l) < 60f), "4 level-ups in the first minute, as before");
            var levelsBy10 = Enumerable.Range(1, 40).Count(l => XpTableTuner.TargetTime(l) <= 600f);
            Assert.That(levelsBy10, Is.InRange(12, 14), "about 13 level-ups by 10:00 (was ~24)");
        }

        [Test]
        public void TheTable_ReplacesTheCurve_AndExtendsByItsLastStep()
        {
            var c = ScriptableObject.CreateInstance<ProgressionConfig>();
            try
            {
                var formula = ProgressionMath.XpToNext(c, 3);
                c.xpToNextTable = new[] { 1, 3, 7, 12 };
                Assert.AreEqual(1, ProgressionMath.XpToNext(c, 0));
                Assert.AreEqual(12, ProgressionMath.XpToNext(c, 3));
                Assert.AreEqual(16, ProgressionMath.XpToNext(c, 4), "past the end: the average step of the last levels ((12 - 1) / 3 ≈ 4)");
                Assert.AreEqual(24, ProgressionMath.XpToNext(c, 6));
                c.xpToNextTable = System.Array.Empty<int>();
                Assert.AreEqual(formula, ProgressionMath.XpToNext(c, 3), "empty: the curve");
                c.xpToNextTable = new[] { 0 };
                var report = new ConfigValidationReport();
                c.Validate(report);
                StringAssert.Contains("xpToNextTable", report.ToString(), "entries must be at least 1");
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void TheData_HoldsTheFittedTable_TheCardLevels_AndThePaceTargets()
        {
            using var rt = new ConfigSetRuntime(Set);
            var p = rt.Single<ProgressionConfig>();
            Assert.GreaterOrEqual(p.xpToNextTable.Length, 12, "the fitted table (measured levels; past it the average last step)");
            Assert.AreEqual(1, p.xpToNextTable[0], "the first level with the first kill");
            for (var i = 2; i < p.xpToNextTable.Length; i++) Assert.GreaterOrEqual(p.xpToNextTable[i], p.xpToNextTable[i - 1], $"never cheaper later (level {i})");
            CollectionAssert.AreEqual(new[] { 2, 2, 3, 3, 4 }, p.levelsPerRarity, "fewer picks, each worth more levels");
            Assert.AreEqual(104f, rt.All<CaptainConfig>().Single(c => c.captainKey == "roxie").baseHull, "Roxie's hull (Captains within ±10%)");
            Assert.AreEqual(5, p.evolutionUnlockLevel, "the evolution card from weapon level 5 (focused builds evolve in most runs)");
            var e = rt.Single<ExperimentConfig>();
            Assert.AreEqual((1f, 1f), (e.enemyHpMultiplier, e.enemyDamageMultiplier), "M3c's interim ×0.85 enemy scale, replaced by M3d's fixed per-type stats");
            Assert.AreEqual("saltwind-v5-m3c", p.balanceVersion);
            var t = rt.Get<V4ConfigAsset>("balance_targets.saltwind");
            var wrong = XpTableTuner.TargetEdits
                .Where(e => ConfigFieldWalker.Format(ConfigPath.Get(t, e.path)) != Parse(t, e.path, e.value))
                .Select(e => e.path).ToList();
            CollectionAssert.IsEmpty(wrong, "the pace targets for the rhythm");
        }

        static string Parse(object root, string path, string text)
        {
            ConfigFieldWalker.TryParse(ConfigPath.TypeAt(root, path), text, out var v);
            return ConfigFieldWalker.Format(v);
        }

        [Test]
        public void TheParityPreset_RestoresTheCurve_AndOneLevelPerCard()
        {
            using var parity = V4DifficultyM10Tests.Parity();
            var p = parity.Single<ProgressionConfig>();
            Assert.AreEqual(0, p.xpToNextTable.Length, "V3: the curve");
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1 }, p.levelsPerRarity);
            Assert.AreEqual(88f, parity.All<CaptainConfig>().Single(c => c.captainKey == "roxie").baseHull);
            Assert.AreEqual((1f, 1f), (parity.Single<ExperimentConfig>().enemyHpMultiplier, parity.Single<ExperimentConfig>().enemyDamageMultiplier));
        }

    }
}
