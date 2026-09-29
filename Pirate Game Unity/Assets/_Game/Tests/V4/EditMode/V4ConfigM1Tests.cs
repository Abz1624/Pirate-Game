using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Ports;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4ConfigM1Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        readonly List<Object> _created = new List<Object>();
        ConfigSet _set;

        [SetUp]
        public void SetUp()
        {
            _set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            Assert.IsNotNull(_set, "SaltwindDefault config set missing");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        T Clone<T>(T source) where T : Object { var c = Object.Instantiate(source); _created.Add(c); return c; }
        WaveTimelineConfig Timeline => _set.All<WaveTimelineConfig>().Single();
        DifficultyCurveConfig Difficulty => _set.All<DifficultyCurveConfig>().Single();

        // ---- the shared stage profile ------------------------------------------------------------------------

        /// <summary>
        /// M15 (replaces the retired V3 field-ownership and profile-equality tests): the stage profile the run builds from
        /// the V4 configs, under the v3-parity preset, has the content hash the M14 and M15 trace sets recorded in their
        /// headers. Any change to how a profile field is filled (or to a config value it reads) changes the hash.
        /// </summary>
        [Test]
        public void StageProfile_UnderTheParityPreset_KeepsTheTracedContentHash()
        {
            using var runtime = new ConfigSetRuntime(_set);
            var preset = File.ReadAllText(Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            var errors = new List<string>();
            Assert.IsTrue(PresetCodec.Apply(runtime, PresetCodec.FromJson(preset), 0f, errors), string.Join("\n", errors));
            using var port = new StageProfilePort(runtime);
            Assert.AreEqual("07c5c03eaa4afe98f0e3f4023b69980ee13ef279132b122f1666a60ebb17ab62", port.Profile.ToExport().ContentHash);
        }

        // ---- enum mirrors ----------------------------------------------------------------------------------

        [Test]
        public void ConfigEnums_MirrorTheV3Enums()
        {
            var pairs = new (Type mine, Type v3)[]
            {
                (typeof(ConfigEnums.EnemyBehavior), typeof(EnemyBehavior)), (typeof(ConfigEnums.EnemyCategory), typeof(EnemyCategory)),
                (typeof(ConfigEnums.EnemyTier), typeof(EnemyTier)), (typeof(ConfigEnums.EliteAffix), typeof(EliteAffix)),
                (typeof(ConfigEnums.WeaponPattern), typeof(WeaponPattern)), (typeof(ConfigEnums.TargetingMode), typeof(TargetingMode)),
                (typeof(ConfigEnums.UpgradeAxis), typeof(UpgradeAxis)), (typeof(ConfigEnums.SpawnMode), typeof(SpawnMode)),
                (typeof(ConfigEnums.SpawnGroupLayout), typeof(SpawnGroupLayout)), (typeof(ConfigEnums.SpawnFallbackPolicy), typeof(SpawnFallbackPolicy)),
                (typeof(ConfigEnums.EnemyMovementPattern), typeof(EnemyMovementPattern)), (typeof(ConfigEnums.EnemyAttackPattern), typeof(EnemyAttackPattern)),
                (typeof(ConfigEnums.EnemySpecialBehavior), typeof(EnemySpecialBehavior)), (typeof(ConfigEnums.SaltwindCombatArchetype), typeof(SaltwindCombatArchetype)),
            };
            foreach (var (mine, v3) in pairs)
            {
                var a = Enum.GetNames(mine).Select(n => $"{n}={(int)Enum.Parse(mine, n)}");
                var b = Enum.GetNames(v3).Select(n => $"{n}={(int)Enum.Parse(v3, n)}");
                CollectionAssert.AreEqual(b, a, $"{mine.Name} differs from V3");
            }
        }

        // ---- the v3-parity preset --------------------------------------------------------------------------

        static string ParityPresetPath => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "plan", "v4-verification", "presets", "v3-parity.json"));

        [Test]
        public void ParityPreset_PhaseIndices_TargetTheElitePhases()
        {
            // The preset addresses timeline phases by index; reordering the phases must not silently retarget it.
            var timeline = _set.All<WaveTimelineConfig>().Single();
            var json = File.ReadAllText(ParityPresetPath);
            var indices = System.Text.RegularExpressions.Regex.Matches(json, @"phases\[(\d+)\]\.focus\.variant")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => int.Parse(m.Groups[1].Value)).ToList();
            CollectionAssert.AreEquivalent(new[] { "elite_hammerhead", "elite_kraken" }, indices.Select(i => timeline.phases[i].phaseId));
            foreach (var i in indices) Assert.IsNotNull(timeline.phases[i].focus.variant, timeline.phases[i].phaseId + " spawns its elite variant by default (R-03)");
        }

        // ---- difficulty curve ------------------------------------------------------------------------------

        [Test]
        public void DifficultyDefaults_ReproduceTheV3Formulas()
        {
            var d = Difficulty;
            for (var minute = 0f; minute <= 20f; minute += 0.5f)
            {
                var hp = 20f * d.enemyHpPerMinute.Evaluate(minute) * d.hpMultipliers.tier[0] * d.hpMultipliers.stage[0] * d.globalHpMultiplier;
                Assert.AreEqual(GameMath.EnemyHP(20f, minute, 1, 0), hp, 1e-4f, $"hp at minute {minute}");
                var dmg = 8f * d.enemyDamagePerMinute.Evaluate(minute) * d.damageMultipliers.tier[0] * d.damageMultipliers.stage[0];
                Assert.AreEqual(GameMath.EnemyDamage(8f, minute, 1, 0), dmg, 1e-4f, $"damage at minute {minute}");
            }
        }

        // ---- console, presets, validation ---------------------------------------------------------------

        [Test]
        public void EveryConfigField_HasAConsoleRenderer()
        {
            var types = typeof(V4ConfigAsset).Assembly.GetTypes().Where(t => typeof(V4ConfigAsset).IsAssignableFrom(t) && !t.IsAbstract).ToList();
            Assert.GreaterOrEqual(types.Count, 7);
            // Any field Unity would serialize (public or [SerializeField], at any depth) that the walker cannot
            // handle would be missing from the hash, presets and console.
            var missing = new List<string>();
            var seen = new HashSet<Type>();
            void Check(Type t, string at)
            {
                if (!seen.Add(t)) return;
                for (var b = t; b != null && b != typeof(ScriptableObject) && b != typeof(object); b = b.BaseType)
                    foreach (var f in b.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        if (f.IsDefined(typeof(NonSerializedAttribute)) || (!f.IsPublic && !f.IsDefined(typeof(SerializeField)))) continue;
                        var kind = ConfigFieldWalker.KindOf(f.FieldType);
                        if (!kind.HasValue) { missing.Add($"{at}.{f.Name} ({f.FieldType.Name})"); continue; }
                        if (kind == ConfigFieldKind.Nested) Check(f.FieldType, $"{at}.{f.Name}");
                        if (kind == ConfigFieldKind.List && ConfigFieldWalker.KindOf(ConfigFieldWalker.ElementType(f.FieldType)) == ConfigFieldKind.Nested)
                            Check(ConfigFieldWalker.ElementType(f.FieldType), $"{at}.{f.Name}[]");
                    }
            }
            foreach (var type in types) Check(type, type.Name);
            CollectionAssert.IsEmpty(missing, "fields with no console renderer / hash support");
        }

        [Test]
        public void SaltwindSet_IsValid_AndPresetRoundTrips()
        {
            var report = ConfigValidator.Validate(_set);
            Assert.IsTrue(report.IsValid, report.ToString());
            string json, hash;
            using (var runtime = new ConfigSetRuntime(_set))
            {
                runtime.TryApplyEdit("timeline.saltwind", "phases[2].ratePerSecond", "4", 0f, out _);
                runtime.TryApplyEdit("spawner.saltwind_ambient_band_3", "placement.distanceMax", "20", 0f, out _);
                hash = runtime.Hash;
                json = PresetCodec.ToJson(PresetCodec.Capture(runtime, "band2x", runtime.Hash));
            }
            using (var fresh = new ConfigSetRuntime(_set))
            {
                var defaults = fresh.Hash;
                Assert.AreNotEqual(defaults, hash);
                Assert.IsTrue(PresetCodec.Apply(fresh, PresetCodec.FromJson(json), 0f, null));
                Assert.AreEqual(hash, fresh.Hash);
            }
        }

        [Test]
        public void Validator_TimelineRules()
        {
            var t = Clone(Timeline);
            t.reservedEncounterSlots = t.maxActiveEnemies;
            t.phases[0].rateMultiplier = 0f;
            t.phases.Add(new TimelinePhase { phaseId = "a", trigger = new PhaseTrigger { kind = PhaseTriggerKind.AfterPhaseCleared, afterPhaseId = "b" } });
            t.phases.Add(new TimelinePhase { phaseId = "b", trigger = new PhaseTrigger { kind = PhaseTriggerKind.AfterPhaseCleared, afterPhaseId = "a" } });
            t.phases.First(p => p.isOvertime).trigger.timeSeconds = 500f;
            t.phases[0].pools[0].entries.ForEach(e => e.weight = 0f);
            var report = new ConfigValidationReport();
            t.Validate(report);
            var text = report.ToString();
            StringAssert.Contains("reserved slots", text);
            StringAssert.Contains("rateMultiplier", text);
            StringAssert.Contains("trigger cycle", text);
            StringAssert.Contains("stage deadline", text);
            StringAssert.Contains("weights add up to 0", text);
        }

        [Test]
        public void Validator_RemainingTimelineRules()
        {
            var t = Clone(Timeline);
            t.phases.Add(new TimelinePhase { phaseId = "storm2", isStorm = true });
            t.storm.contractionEndSeconds = t.storm.contractionStartSeconds - 1f;
            t.phases[0].trigger.timeSeconds = -1f;
            t.phases[1].ratePerSecond = -1f;
            t.phases[2].warning.leadSeconds = -1f;
            var spawner = Clone(_set.All<EnemySpawnerConfig>().First());
            spawner.timing.maxActiveFromProfile = 2;
            t.phases[3].spawner = spawner;
            t.phases[3].burstCount = 5;
            var report = new ConfigValidationReport();
            t.Validate(report);
            var text = report.ToString();
            StringAssert.Contains("more than one storm phase", text);
            StringAssert.Contains("contraction ends before it starts", text);
            StringAssert.Contains("phases[0].trigger.timeSeconds", text);
            StringAssert.Contains("phases[1].ratePerSecond", text);
            StringAssert.Contains("phases[2].warning.leadSeconds", text);
            StringAssert.Contains("larger than the spawner cap", text);
        }

        [Test]
        public void MirrorCopies_LeaveNoFieldUnmatched()
        {
            var p = ScriptableObject.CreateInstance<SpawnProfile>();
            _created.Add(p);
            var c = ScriptableObject.CreateInstance<EnemySpawnerConfig>();
            _created.Add(c);
            foreach (var (v3, v4) in new (object, object)[] { (p.timing, c.timing), (p.placement, c.placement), (p.elite, c.elite), (p.movement, c.movement), (p.attack, c.attack) })
            {
                CollectionAssert.IsEmpty(FieldMirror.CopyFields(v3, v4), $"{v4.GetType().Name} has fields V3 lacks");
                CollectionAssert.IsEmpty(FieldMirror.CopyFields(v4, v3), $"{v3.GetType().Name} has fields the V4 mirror lacks");
            }
        }

        [Test]
        public void Validator_SpawnerWeaponDifficultyRules()
        {
            var s = Clone(_set.All<EnemySpawnerConfig>().First());
            s.anchor = null;
            s.timing.spawnIntervalSeconds = 0f;
            s.placement.distanceMax = s.placement.distanceMin - 1f;
            s.timing.maxActiveFromProfile = 2;
            s.timing.spawnCount = 5;
            var w = Clone(_set.All<WeaponConfig>().First());
            w.baseCooldown = 0f;
            foreach (var axis in w.upgradeAxes) axis.offerWeight = 0f;
            var d = Clone(Difficulty);
            d.pressureMax = d.pressureMin - 0.1f;
            var report = new ConfigValidationReport();
            s.Validate(report);
            w.Validate(report);
            d.Validate(report);
            var text = report.ToString();
            StringAssert.Contains("no anchor", text);
            StringAssert.Contains("spawnIntervalSeconds", text);
            StringAssert.Contains("below distanceMin", text);
            StringAssert.Contains("above the spawner cap", text);
            StringAssert.Contains("baseCooldown", text);
            StringAssert.Contains("below pressureMin", text);
        }
    }
}
