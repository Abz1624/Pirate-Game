using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.DevTools.Balance;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 after the demo (1 Oct 2026): the demo's tuning is the baseline and a retune went around its slower levelling.
    /// Every retuned value is in the data, the demo's values are in the data, and the v3-parity preset still takes all of
    /// them back to V3 (review of the balance re-check, findings 2 and 9).
    /// </summary>
    public sealed class V5DemoBaselineTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        static string Value(ConfigSetRuntime rt, string configId, string path) => ConfigFieldWalker.Format(ConfigPath.Get(rt.Get<V4ConfigAsset>(configId), path));

        static string Normal(ConfigSetRuntime rt, string configId, string path, string text)
        {
            Assert.IsTrue(ConfigFieldWalker.TryParse(ConfigPath.TypeAt(rt.Get<V4ConfigAsset>(configId), path), text, out var parsed), $"{configId}.{path}: '{text}'");
            return ConfigFieldWalker.Format(parsed);
        }

        [Test]
        public void EveryRetunedValue_AndTheDirectorCurve_AreInTheData()
        {
            using var rt = new ConfigSetRuntime(Set);
            var wrong = DemoBaseline.Retune
                .Where(e => Value(rt, e.configId, e.path) != Normal(rt, e.configId, e.path, e.value))
                .Select(e => $"{e.configId}.{e.path} = {Value(rt, e.configId, e.path)} (retune {e.value})").ToList();
            CollectionAssert.IsEmpty(wrong);
            var curve = rt.Single<DirectorConfig>().onTrackGrowth;
            foreach (var (minute, growth) in DemoBaseline.OnTrackGrowth)
                Assert.AreEqual(growth, curve.Evaluate(minute), 1e-3f, $"director growth at {minute}:00");
            Assert.IsTrue(rt.Configs.Where(c => DemoBaseline.Retune.Any(e => e.configId == c.configId)).All(c => c.balanceVersion == "saltwind-v5-demo"), "retuned configs carry the balance version");
        }

        [Test]
        public void TheDemosValues_AreInTheData_AndDifferFromBefore()
        {
            using var rt = new ConfigSetRuntime(Set);
            var unchanged = DemoBaseline.PreDemo.Where(e => Value(rt, e.configId, e.path) == Normal(rt, e.configId, e.path, e.value)).Select(e => e.configId + "." + e.path).ToList();
            CollectionAssert.IsEmpty(unchanged, "every listed demo change is still in the data");
            Assert.AreEqual((0.5f, 1.625f), (rt.Single<PickupConfig>().xpPickupMultiplier, rt.Single<RunConfig>().shipSpeedMultiplier));
            var w = rt.All<WeaponConfig>().ToDictionary(c => c.weaponId);
            Assert.AreEqual((0.86f, 0.33f), (w["swivel_guns"].projectile.lifeSeconds, w["broadside_battery"].fan.lifeSeconds), "the shorter shot lifetimes of the shorter ranges");
            Assert.AreEqual((16f, 12f, 13f), (w["deck_cannons"].range, w["swivel_guns"].range, w["lightning_mast"].range));
        }

        [Test]
        public void TheParityPreset_TakesEveryDemoValueBackToV3()
        {
            var preset = PresetCodec.FromJson(System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json")));
            var edits = preset.configs.SelectMany(c => c.edits.Select(e => (c.configId, e.path, e.value))).ToList();
            var missing = DemoBaseline.PreDemo.Where(p => !edits.Any(e => e.configId == p.configId && e.path == p.path && e.value == p.value)).Select(p => $"{p.configId}.{p.path} = {p.value}").ToList();
            CollectionAssert.IsEmpty(missing, "the pre-demo value (V3's, as the demo changed only V3-valued fields) is in the preset");
            var retuned = DemoBaseline.Retune.Where(e => e.configId != "balance_targets.saltwind")
                .Where(e => !edits.Any(x => x.configId == e.configId && x.path == e.path)).Select(e => e.configId + "." + e.path).ToList();
            CollectionAssert.IsEmpty(retuned, "every retuned game value has its V3 value in the preset");
            using var parity = V4DifficultyM10Tests.Parity();
            Assert.AreEqual((1f, 1f), (parity.Single<PickupConfig>().xpPickupMultiplier, parity.Single<RunConfig>().shipSpeedMultiplier));
            var d = parity.Single<DifficultyCurveConfig>();
            Assert.AreEqual((0.09f, 0.018f), (d.enemyHpPerMinute.c1, d.enemyHpPerMinute.c2));
        }

        [Test]
        public void XpPace_AndShipSpeed_MustBePositive()
        {
            using var rt = new ConfigSetRuntime(Set);
            rt.Single<PickupConfig>().xpPickupMultiplier = 0f;
            rt.Single<RunConfig>().shipSpeedMultiplier = -1f;
            var report = new ConfigValidationReport();
            rt.Single<PickupConfig>().Validate(report);
            rt.Single<RunConfig>().Validate(report);
            var text = report.ToString();
            StringAssert.Contains("xpPickupMultiplier", text);
            StringAssert.Contains("shipSpeedMultiplier", text);
        }
    }
}
