using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Enemies;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Spawning;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>M14 switch-over: the seeded pickup stream and the reused adaptive-elite copy.</summary>
    [Category("V4")]
    public sealed class V4SwitchoverM14Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        static ConfigSetRuntime Default() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        /// <summary>14.8 (intended change): pickup draws come from a seed-derived stream that Unity's global random state —
        /// which damage numbers, camera shake and other VFX also consume — can no longer move.</summary>
        [Test]
        public void PickupStream_IsSeeded_AndIndependentOfUnitysRandom()
        {
            var a = new RoguePirates.V4.Progression.SeededPickupRandom(101 ^ 0x71C0B5);
            var b = new RoguePirates.V4.Progression.SeededPickupRandom(101 ^ 0x71C0B5);
            for (var i = 0; i < 200; i++)
            {
                Random.InitState(i * 31); // presentation drawing from the global state in between
                _ = Random.value;
                Assert.AreEqual(a.Range(-18f, 18f), b.Range(-18f, 18f));
                Assert.AreEqual(a.Range(6, 11), b.Range(6, 11));
                Assert.AreEqual(a.InsideUnitSphere(), b.InsideUnitSphere());
            }
            var c = new RoguePirates.V4.Progression.SeededPickupRandom(202 ^ 0x71C0B5);
            Assert.AreNotEqual(new RoguePirates.V4.Progression.SeededPickupRandom(101 ^ 0x71C0B5).Range(0f, 1f), c.Range(0f, 1f), "the run seed decides the stream");
        }

        /// <summary>14.4: the adaptive-elite copy is one reused object per profile, current at each call (no per-spawn create).</summary>
        [Test]
        public void EliteCopy_IsReused_AndCurrent()
        {
            using var set = Default();
            var v4 = new SpawnerProfiles(set);
            try
            {
                v4.Build();
                var spawner = set.All<EnemySpawnerConfig>().First(s => s.legacyProfileId.Contains("ambient"));
                var source = v4.For(spawner);
                var d = set.Single<DirectorConfig>();
                var a = v4.EliteCopy(source, d);
                Assert.IsTrue(a.elite.isElite);
                Assert.AreEqual(Mathf.Max(0.1f, d.eliteHpMultiplier), a.elite.hpMultiplier);
                Assert.AreSame(source.movement, a.movement);
                Assert.AreSame(source.enemy, a.enemy);
                Assert.IsFalse(source.elite.isElite, "the source stays as it was");
                d.eliteHpMultiplier = 2.25f;
                var b = v4.EliteCopy(source, d);
                Assert.AreSame(a, b, "one reusable copy");
                Assert.AreEqual(2.25f, b.elite.hpMultiplier, "current at each call");
            }
            finally { v4.Dispose(); }
        }
    }
}
