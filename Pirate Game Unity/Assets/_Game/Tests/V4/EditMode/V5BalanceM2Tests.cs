using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using RoguePirates.V4.Editor;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M2 balance data: the personalities and targets data and the v3-parity preset restoring the pre-M2 values.
    /// V5 M8z (I8z.7, owner 7 Oct 2026: one simulator): the in-engine simulator's tests (its math, determinism,
    /// personalities, the autopilot calibration, the baseline targets, its spawn rules) were retired with it; the balance
    /// model is the JavaScript Monte Carlo (plan/playtest/montecarlo), checked by <see cref="V5MonteCarloExportTests"/>.
    /// </summary>
    [Category("V5")]
    public sealed class V5BalanceM2Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        // ---- Data ---------------------------------------------------------------------------------------------------

        [Test]
        public void PersonalitiesAndTargets_AreInTheSet_AndValidate()
        {
            using var rt = new ConfigSetRuntime(Set);
            var personalities = rt.Single<PlayerPersonalityConfig>();
            var targets = rt.Single<BalanceTargetsConfig>();
            Assert.IsNotNull(personalities);
            Assert.IsNotNull(targets);
            var report = new ConfigValidationReport();
            personalities.Validate(report);
            targets.Validate(report);
            Assert.IsTrue(report.IsValid, report.ToString());
            Assert.AreEqual(10, personalities.personalities.Count);
            Assert.AreEqual(1f, personalities.personalities.Sum(p => p.share), 0.001f);
            foreach (var p in personalities.personalities) Assert.IsNotNull(targets.For(p.personalityId), "targets for " + p.personalityId);
            Assert.IsNotNull(personalities.Find(targets.baselinePersonalityId), "the baseline personality exists");
        }

        [Test]
        public void Personalities_MatchThePersonalitiesDoc()
        {
            var ps = V5BalanceDefaults.Personalities();
            var first = ps.Single(p => p.personalityId == "first_timer");
            Assert.AreEqual((0.15f, 0.35f, 0.30f, 6f), (first.share, first.dodgeSkill, first.contactAvoidance, first.engageDistance));
            Assert.AreEqual((PersonalityMovement.StopGo, PersonalityBossTiming.Never, PersonalityCards.Shiny, PersonalityActive.Forgets), (first.movement, first.bossTiming, first.cards, first.activeUse));
            var steady = ps.Single(p => p.personalityId == "steady_captain");
            Assert.AreEqual((0.20f, 0.70f, 0.65f, 10f, 0.5f), (steady.share, steady.dodgeSkill, steady.contactAvoidance, steady.engageDistance, steady.pickupGreed));
            Assert.AreEqual((PersonalityBossTiming.Minute, 6f, PersonalityCards.Balanced), (steady.bossTiming, steady.bossMinute, steady.cards));
            var speed = ps.Single(p => p.personalityId == "speedrunner");
            Assert.AreEqual((PersonalityMovement.Beeline, PersonalityChests.None, PersonalityStop.AfterBoss, 3f), (speed.movement, speed.chests, speed.stop, speed.bossMinute));
        }

        [Test]
        public void Targets_ValidationCatchesBrokenData()
        {
            var t = ScriptableObject.CreateInstance<BalanceTargetsConfig>();
            try
            {
                t.configId = "t";
                t.levelUpGapTo10 = new TargetBand(20f, 10f);
                t.minutes.Add(new MinuteTargets { minute = 3 });
                t.minutes.Add(new MinuteTargets { minute = 3 });
                t.calibration.movementRetreat = new float[2];
                var report = new ConfigValidationReport();
                t.Validate(report);
                var text = report.ToString();
                StringAssert.Contains("levelUpGapTo10", text);
                StringAssert.Contains("minutes[1].minute", text);
                StringAssert.Contains("movementRetreat", text);
            }
            finally { Object.DestroyImmediate(t); }
        }

        [Test]
        public void TargetBand_NaNIsUnchecked()
        {
            Assert.IsTrue(new TargetBand(float.NaN, 5f).Contains(-100f));
            Assert.IsFalse(new TargetBand(float.NaN, 5f).Contains(5.1f));
            Assert.IsTrue(new TargetBand(2f, float.NaN).Contains(1e6f));
            Assert.IsFalse(new TargetBand(2f, 3f).Contains(1.9f));
        }

        [Test]
        public void Retune_IsInTheData_AndTheParityPresetRestoresIt()
        {
            using var rt = new ConfigSetRuntime(Set);
            StringAssert.StartsWith("saltwind-v5-", rt.Single<ProgressionConfig>().balanceVersion, "retuned in V5 (M2, M3, then the demo baseline)");
            Assert.AreEqual(3, rt.Single<ProgressionConfig>().levelZeroToOneXp);
            Assert.AreEqual(0f, rt.Single<DifficultyCurveConfig>().ordinaryHp.c1, 1e-6f, "M2's 0.08, × 0.55 by the demo-baseline retune, then flat in M3d (owner note h)");
            Assert.AreEqual("0.044", CountPressure.Edits.Single(e => e.path == "ordinaryHp.c1").pre, "M3d keeps the retune's value as its value before");
            var parity = new ConfigSetRuntime(Set);
            try
            {
                var errors = new List<string>();
                var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
                Assert.IsTrue(PresetCodec.Apply(parity, PresetCodec.FromJson(preset), 0f, errors), string.Join("\n", errors));
                var p = parity.Single<ProgressionConfig>();
                Assert.AreEqual((5, 5f, 5.325000286102295, 1.065000057220459), (p.levelZeroToOneXp, p.xpCurveOffset, p.xpCurveMultiplier, p.xpCurveExponent));
                var d = parity.Single<DifficultyCurveConfig>();
                Assert.AreEqual((0.11f, 0.022f, 0.14f, 0.028f, 0.055f, 2f, 0.5f), (d.ordinaryHp.c1, d.ordinaryHp.c2, d.ordinaryDamage.c1, d.ordinaryDamage.c2, d.enemyDamagePerMinute.c1, d.overtimeHpMultiplierBase, d.overtimeHpMultiplierExponentPerMinute));
                var t = parity.Single<WaveTimelineConfig>();
                var bands = t.phases.Where(x => x.ratePerSecond > 0f && !x.isOvertime).OrderBy(x => x.trigger.timeSeconds).Select(x => x.ratePerSecond).ToList();
                CollectionAssert.AreEqual(new[] { 0.6f, 1.2f, 1.8f, 2.4f, 3f }, bands);
                foreach (var x in t.phases.Where(x => x.focus != null && x.focus.enabled))
                    if (x.focus.scheduledEliteIndex < 0) Assert.AreEqual((4f, 1.15f), (x.focus.hpMultiplier, x.focus.damageMultiplier));
                    else Assert.AreEqual(1f, x.spawner.elite.hpMultiplier);
                Assert.AreEqual(1f, t.phases.Single(x => x.isBoss).spawner.elite.hpMultiplier);
                Assert.AreEqual(800f, t.phases.Single(x => x.isBoss).spawner.anchor.stats.baseHP, "the Barnacle King's pre-M2 base HP");
            }
            finally { parity.Dispose(); }
        }
    }
}
