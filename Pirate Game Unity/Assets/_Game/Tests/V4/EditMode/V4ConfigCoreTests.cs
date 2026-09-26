using System;
using System.Collections.Generic;
using NUnit.Framework;
using RoguePirates.V4.Config;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4ConfigCoreTests
    {
        readonly List<Object> _created = new List<Object>();

        ProbeConfig Make(string id)
        {
            var c = ScriptableObject.CreateInstance<ProbeConfig>();
            c.configId = id;
            c.name = id;
            _created.Add(c);
            return c;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        [Test]
        public void Hash_IsStable_AndChangesWithAnyValue()
        {
            var a = Make("p");
            var b = Make("p");
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
            b.entries[1].range.max = 3f;
            Assert.AreNotEqual(a.ComputeHash(), b.ComputeHash());
            b.entries[1].range.max = a.entries[1].range.max;
            b.ignored = 99f; // not serialized, not hashed
            Assert.AreEqual(a.ComputeHash(), b.ComputeHash());
            StringAssert.Contains("\"rate\":0.6", CanonicalConfigWriter.ToJson(a));
        }

        [Test]
        public void SetHash_IgnoresOrder_ButNotValues()
        {
            var a = Make("a");
            var b = Make("b");
            Assert.AreEqual(ConfigSet.HashOf(new V4ConfigAsset[] { a, b }), ConfigSet.HashOf(new V4ConfigAsset[] { b, a }));
            var before = ConfigSet.HashOf(new V4ConfigAsset[] { a, b });
            b.cap = 80;
            Assert.AreNotEqual(before, ConfigSet.HashOf(new V4ConfigAsset[] { a, b }));
        }

        [Test]
        public void Runtime_EditsCopies_LogsChanges_AndResets()
        {
            var source = Make("p");
            using (var runtime = new ConfigSetRuntime("set", new V4ConfigAsset[] { source }))
            {
                var changes = new List<ConfigChange>();
                runtime.ConfigChanged += changes.Add;
                Assert.IsTrue(runtime.TryApplyEdit("p", "entries[1].range.min", "4.5", 12f, out var error), error);
                Assert.IsTrue(runtime.TryApplyEdit("p", "mode", "Off", 13f, out error), error);
                var copy = runtime.Get<ProbeConfig>("p");
                Assert.AreEqual(4.5f, copy.entries[1].range.min);
                Assert.AreEqual(ProbeMode.Off, copy.mode);
                Assert.AreEqual(0f, source.entries[1].range.min, "asset must not change");
                Assert.AreEqual(2, changes.Count);
                Assert.AreEqual("entries[1].range.min", changes[0].path);
                Assert.AreEqual(12f, changes[0].runTime);
                Assert.IsFalse(runtime.TryApplyEdit("p", "rate", "fast", 0f, out error));
                Assert.IsTrue(runtime.ResetField("p", "mode", 14f, out error), error);
                Assert.AreEqual(ProbeMode.On, copy.mode);
            }
        }

        [Test]
        public void Runtime_RemapsReferencesToTheCopies()
        {
            var a = Make("a");
            var b = Make("b");
            a.other = b;
            using (var runtime = new ConfigSetRuntime("set", new V4ConfigAsset[] { a, b }))
            {
                Assert.AreSame(runtime.Get<ProbeConfig>("b"), runtime.Get<ProbeConfig>("a").other);
                Assert.AreSame(b, a.other, "asset reference unchanged");
            }
        }

        [Test]
        public void Preset_RoundTrip_ReproducesTheEditedSet()
        {
            var source = Make("p");
            string json, hash;
            using (var runtime = new ConfigSetRuntime("set", new V4ConfigAsset[] { source }))
            {
                runtime.TryApplyEdit("p", "rate", "1.8", 0f, out _);
                runtime.TryApplyEdit("p", "shares[1]", "0.5", 0f, out _);
                PresetCodec.TryResize(runtime, "p", "entries", "3", 0f, out _);
                runtime.TryApplyEdit("p", "entries[2].id", "c", 0f, out _);
                hash = runtime.Hash;
                json = PresetCodec.ToJson(PresetCodec.Capture(runtime, "test", "base"));
            }

            using (var fresh = new ConfigSetRuntime("set", new V4ConfigAsset[] { source }))
            {
                var errors = new List<string>();
                Assert.IsTrue(PresetCodec.Apply(fresh, PresetCodec.FromJson(json), 0f, errors), string.Join("\n", errors));
                Assert.AreEqual(hash, fresh.Hash);
                Assert.AreEqual(3, fresh.Get<ProbeConfig>("p").entries.Count);
                Assert.AreEqual("c", fresh.Get<ProbeConfig>("p").entries[2].id);
            }
        }

        [Test]
        public void Preset_FromOlderSchemaWithoutMigration_IsRejected()
        {
            var source = Make("p");
            using (var runtime = new ConfigSetRuntime("set", new V4ConfigAsset[] { source }))
            {
                var preset = new ConfigPreset();
                preset.configs.Add(new ConfigPresetEntry { configId = "p", schemaVersion = source.schemaVersion - 1 });
                var errors = new List<string>();
                Assert.IsFalse(PresetCodec.Apply(runtime, preset, 0f, errors));
                StringAssert.Contains("schema", errors[0]);
            }
        }

        [Test]
        public void ResetConfig_RestoresListLengths_AndPresetsApplyOnDefaults()
        {
            var source = Make("p");
            var defaults = source.ComputeHash();
            using (var runtime = new ConfigSetRuntime("set", new V4ConfigAsset[] { source }))
            {
                PresetCodec.TryResize(runtime, "p", "shares", "3", 0f, out _);
                PresetCodec.TryResize(runtime, "p", "entries", "1", 0f, out _);
                runtime.TryApplyEdit("p", "rate", "9", 0f, out _);
                runtime.ResetConfig("p", 0f);
                Assert.AreEqual(defaults, runtime.Get<ProbeConfig>("p").ComputeHash(), "reset must restore list lengths and values");

                // A preset applied after a resize must equal defaults + preset (Apply resets first).
                var preset = new ConfigPreset();
                preset.configs.Add(new ConfigPresetEntry { configId = "p", schemaVersion = source.schemaVersion,
                    edits = { new ConfigPresetEdit { path = "cap", value = "70" } } });
                PresetCodec.TryResize(runtime, "p", "entries", "5", 0f, out _);
                Assert.IsTrue(PresetCodec.Apply(runtime, preset, 0f, null));
                var copy = runtime.Get<ProbeConfig>("p");
                Assert.AreEqual(2, copy.entries.Count);
                Assert.AreEqual(70, copy.cap);
            }
        }

        [Test]
        public void Edits_RejectEnumNumbersThatNameNoMember()
        {
            var source = Make("p");
            using (var runtime = new ConfigSetRuntime("set", new V4ConfigAsset[] { source }))
            {
                Assert.IsFalse(runtime.TryApplyEdit("p", "mode", "7", 0f, out _));
                Assert.IsTrue(runtime.TryApplyEdit("p", "mode", "0", 0f, out var error), error);
                Assert.AreEqual(ProbeMode.Off, runtime.Get<ProbeConfig>("p").mode);
            }
        }

        [Test]
        public void Validator_StatIdHelper_RejectsIdsOutsideTheStatTable()
        {
            var c = Make("p");
            var report = new ConfigValidationReport();
            report.StatId(c, "stat", 21);
            report.StatId(c, "stat", -1);
            report.StatId(c, "stat", 20);
            Assert.AreEqual(2, System.Linq.Enumerable.Count(report.Errors));
        }

        [Test]
        public void Validator_ReportsIds_References_AndConfigRules()
        {
            var a = Make("a");
            var dup = Make("a");
            var noId = Make("");
            var outside = Make("outside");
            a.other = outside;
            a.entries.ForEach(e => e.weight = 0f);
            a.rate = -1f;
            var report = ConfigValidator.Validate(new V4ConfigAsset[] { a, dup, noId });
            Assert.IsFalse(report.IsValid);
            var text = report.ToString();
            StringAssert.Contains("duplicate id", text);
            StringAssert.Contains("missing id", text);
            StringAssert.Contains("not in this config set", text);
            StringAssert.Contains("weights add up to 0", text);
            StringAssert.Contains("must not be negative", text);
        }
    }
}
