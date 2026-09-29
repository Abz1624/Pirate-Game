using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4PoolM4Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        struct Rec { public int Value; }

        // ---- Slot pools ----------------------------------------------------------------------------------------

        [Test]
        public void SlotPool_AllocatesUntilFull_ThenRefuses()
        {
            var pool = new SlotPool<Rec>(3);
            Assert.AreEqual(0, pool.Allocate(), "low indices first");
            Assert.AreEqual(1, pool.Allocate());
            Assert.AreEqual(2, pool.Allocate());
            Assert.IsTrue(pool.IsFull);
            Assert.AreEqual(-1, pool.Allocate());
            Assert.AreEqual(1, pool.Refused);
            Assert.AreEqual(3, pool.Count);
            Assert.AreEqual(3, pool.Peak);
        }

        [Test]
        public void SlotPool_FreeReusesTheSlot_ResetsIt_AndBumpsTheGeneration()
        {
            var pool = new SlotPool<Rec>(4);
            var a = pool.Allocate();
            pool[a].Value = 42;
            var generation = pool.Generation(a);
            Assert.IsTrue(pool.Free(a));
            Assert.IsFalse(pool.Free(a), "double free is ignored");
            Assert.IsFalse(pool.Free(99), "out of range is ignored");
            var b = pool.Allocate();
            Assert.AreEqual(a, b, "the freed slot comes back first");
            Assert.AreEqual(0, pool[b].Value, "reset to default");
            Assert.AreNotEqual(generation, pool.Generation(b), "a stored (index, generation) can tell the slot was reused");
        }

        [Test]
        public void SlotPool_ActiveIteration_CoversExactlyTheActiveSlots()
        {
            var pool = new SlotPool<Rec>(16);
            var live = new HashSet<int>();
            for (var i = 0; i < 12; i++) live.Add(pool.Allocate());
            foreach (var i in new[] { 3, 0, 11, 7 }) { pool.Free(i); live.Remove(i); }
            live.Add(pool.Allocate());
            var seen = new HashSet<int>();
            for (var i = 0; i < pool.Count; i++) seen.Add(pool.ActiveAt(i));
            CollectionAssert.AreEquivalent(live, seen);
            foreach (var i in live) Assert.IsTrue(pool.IsActive(i));
            pool.Clear();
            Assert.AreEqual(0, pool.Count);
            Assert.IsFalse(pool.IsActive(live.First()));
        }

        [Test]
        public void SlotPoolCapacities_FollowTheQualityTier()
        {
            var low = SlotPoolCapacities.For(QualityTier.Low);
            var mid = SlotPoolCapacities.For(QualityTier.Mid);
            var high = SlotPoolCapacities.For(QualityTier.High);
            Assert.AreEqual((160, 280, 96, 180), (low.Enemies, low.PlayerProjectiles, low.HostileProjectiles, low.Pickups));
            Assert.AreEqual((260, 500, 96, 180), (mid.Enemies, mid.PlayerProjectiles, mid.HostileProjectiles, mid.Pickups));
            Assert.AreEqual((420, 900, 96, 180), (high.Enemies, high.PlayerProjectiles, high.HostileProjectiles, high.Pickups));
        }

        // ---- View pool -----------------------------------------------------------------------------------------

        sealed class Hooks : MonoBehaviour, IPoolable
        {
            public int Acquired, Released;
            public void OnAcquired() => Acquired++;
            public void OnReleased() => Released++;
        }

        GameObject _prefab, _root;
        PoolService _pools;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("TestPoolPrefab");
            _prefab.AddComponent<Hooks>();
            _root = new GameObject("TestPoolRoot");
            _pools = new PoolService(_root.transform);
        }

        [TearDown]
        public void TearDown()
        {
            _pools.Dispose();
            Object.DestroyImmediate(_root);
            Object.DestroyImmediate(_prefab);
        }

        void Register(string key, int prewarm, int cap, ConfigEnums.PoolOverflowPolicy policy) =>
            _pools.Register(new PoolDefinition(key, _prefab, prewarm, cap, policy, "test"));

        PoolStats Stat(string key) => _pools.Stats().Single(s => s.Key == key);

        [Test]
        public void Prewarm_CreatesInactiveInstances_AndAcquiringWithinItCreatesNothing()
        {
            Register("a", 4, 8, ConfigEnums.PoolOverflowPolicy.Grow);
            _pools.PrewarmAll();
            Assert.AreEqual(4, _root.GetComponentsInChildren<Hooks>(true).Length);
            Assert.AreEqual(4, Stat("a").Total);
            Assert.IsTrue(_root.GetComponentsInChildren<Hooks>(true).All(h => !h.gameObject.activeSelf));
            var got = Enumerable.Range(0, 4).Select(i => _pools.Acquire("a", new Vector3(i, 0, 0), Quaternion.identity)).ToList();
            Assert.IsTrue(got.All(g => g != null && g.activeSelf));
            Assert.AreEqual(new Vector3(2, 0, 0), got[2].transform.position);
            Assert.AreEqual(0, _pools.TotalCreatedAfterPrewarm);
            Assert.AreEqual((4, 0, 4), (Stat("a").InUse, Stat("a").Available, Stat("a").Peak));
            Assert.IsTrue(got.All(g => g.GetComponent<Hooks>().Acquired == 1), "IPoolable hooks run");
        }

        [Test]
        public void Grow_PastPrewarmAndCap_CreatesAndCountsIt()
        {
            Register("g", 2, 2, ConfigEnums.PoolOverflowPolicy.Grow);
            _pools.PrewarmAll();
            for (var i = 0; i < 5; i++) Assert.IsNotNull(_pools.Acquire("g", Vector3.zero, Quaternion.identity));
            Assert.AreEqual(3, Stat("g").CreatedAfterPrewarm);
            Assert.AreEqual(3, _pools.TotalCreatedAfterPrewarm);
            Assert.AreEqual(5, Stat("g").Peak);
        }

        [Test]
        public void RecycleOldest_AtTheCap_ReusesTheFirstAcquired()
        {
            Register("r", 3, 3, ConfigEnums.PoolOverflowPolicy.RecycleOldest);
            _pools.PrewarmAll();
            var first = _pools.Acquire("r", Vector3.zero, Quaternion.identity);
            _pools.Acquire("r", Vector3.zero, Quaternion.identity);
            _pools.Acquire("r", Vector3.zero, Quaternion.identity);
            var fourth = _pools.Acquire("r", Vector3.one, Quaternion.identity);
            Assert.AreSame(first, fourth, "the oldest in use comes back");
            Assert.AreEqual(Vector3.one, fourth.transform.position);
            Assert.AreEqual((1, 0, 3), (Stat("r").Recycled, Stat("r").CreatedAfterPrewarm, Stat("r").InUse));
            Assert.AreEqual(1, first.GetComponent<Hooks>().Released, "the recycled view was released first");
        }

        [Test]
        public void Reject_AtTheCap_ReturnsNull()
        {
            Register("x", 1, 1, ConfigEnums.PoolOverflowPolicy.Reject);
            _pools.PrewarmAll();
            Assert.IsNotNull(_pools.Acquire("x", Vector3.zero, Quaternion.identity));
            Assert.IsNull(_pools.Acquire("x", Vector3.zero, Quaternion.identity));
            Assert.AreEqual((1, 0), (Stat("x").Rejected, Stat("x").CreatedAfterPrewarm));
            Assert.IsNull(_pools.Acquire("unknown", Vector3.zero, Quaternion.identity), "unknown keys give nothing");
        }

        [Test]
        public void Release_ReturnsTheInstance_AndRefusesForeignObjects()
        {
            Register("a", 1, 0, ConfigEnums.PoolOverflowPolicy.Grow);
            _pools.PrewarmAll();
            var go = _pools.Acquire("a", Vector3.zero, Quaternion.identity);
            go.transform.SetParent(null);
            Assert.IsTrue(_pools.Release(go));
            Assert.IsFalse(go.activeSelf);
            Assert.AreSame(_root.transform, go.transform.parent, "back under the pool root");
            Assert.IsFalse(_pools.Release(go), "already released");
            Assert.IsTrue(_pools.IsPooled(go));
            Assert.IsTrue(_pools.CreatedByPool(go.GetInstanceID()));
            var foreign = new GameObject("Foreign");
            try
            {
                UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("was not acquired from a pool"));
                Assert.IsFalse(_pools.Release(foreign));
                Assert.IsFalse(_pools.CreatedByPool(foreign.GetInstanceID()));
            }
            finally { Object.DestroyImmediate(foreign); }
            Assert.AreSame(go, _pools.Acquire("a", Vector3.zero, Quaternion.identity), "reused, no new instance");
            Assert.AreEqual(0, _pools.TotalCreatedAfterPrewarm);
            _pools.ReleaseAll();
            Assert.AreEqual(0, Stat("a").InUse);
        }

        // ---- Catalog -------------------------------------------------------------------------------------------

        [Test]
        public void Catalog_DerivesOnePoolPerReachableEnemyModel_NotPrewarmedUntilM8()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            using var config = new ConfigSetRuntime(set);
            var catalog = config.All<PoolCatalogConfig>().Single();
            var defs = PoolCatalog.Build(config, moduleLab: false);
            Assert.IsFalse(defs.Any(d => d.Key.StartsWith("lab/")), "Module Lab pools only in the lab");
            var derived = defs.Where(d => d.Key.StartsWith(PoolCatalog.EnemyKeyPrefix)).ToList();
            Assert.Greater(derived.Count, 5);
            Assert.AreEqual(derived.Count, derived.Select(d => d.Key).Distinct().Count(), "one pool per model");
            // M14.10: the enemy view presenter renders through these pools, so the catalog prewarms them (the switch below).
            Assert.IsTrue(catalog.prewarmEnemyViews, "derived enemy pools prewarm by default since M14.10");
            Assert.IsTrue(config.TryApplyEdit(catalog.configId, "prewarmEnemyViews", "false", 0f, out var off), off);
            Assert.IsTrue(PoolCatalog.Build(config, moduleLab: false).Where(d => d.Key.StartsWith(PoolCatalog.EnemyKeyPrefix)).All(d => d.Prewarm == 0),
                "switched off, nothing is created up front");

            var reachable = PoolCatalog.ReachableEnemies(config, catalog);
            var ids = reachable.Select(r => r.id).ToList();
            CollectionAssert.IsSubsetOf(new[] { "hammerhead", "reef_crab_miniboss", "elite_kraken_spawn", "drowned_pursuer", "drowned_harrier" }, ids);
            foreach (var (id, model, _) in reachable.Where(r => r.model != null))
                Assert.IsTrue(derived.Any(d => d.Prefab == model && d.Source.Contains(id)), id + " has a pool for its model");

            Assert.IsTrue(config.TryApplyEdit(catalog.configId, "prewarmEnemyViews", "true", 0f, out var e), e);
            var warm = PoolCatalog.Build(config, moduleLab: false).Where(d => d.Key.StartsWith(PoolCatalog.EnemyKeyPrefix)).ToList();
            var reefModel = reachable.Single(r => r.id == "reef_crab_miniboss").model;
            Assert.AreEqual(catalog.bossViewPrewarm, warm.Single(d => d.Prefab == reefModel).Prewarm, "miniboss models prewarm the boss count");
            var hammerheadModel = reachable.Single(r => r.id == "hammerhead").model;
            Assert.AreEqual(catalog.enemyViewPrewarm, warm.Single(d => d.Prefab == hammerheadModel).Prewarm, "other models prewarm the enemy count");
            // M14.15: an archetype's own visual.poolPrewarm wins (the common SmuggleBoat enemies prewarm 60).
            var own = reachable.Where(r => r.model != null && r.prewarm != catalog.enemyViewPrewarm && r.prewarm != catalog.bossViewPrewarm).Select(r => r.model).ToList();
            var odd = warm.Where(d => !own.Contains(d.Prefab) && d.Prewarm != catalog.enemyViewPrewarm && d.Prewarm != catalog.bossViewPrewarm).Select(d => $"{d.Key}={d.Prewarm}").ToList();
            Assert.IsEmpty(odd, "boss and miniboss models (Reef Crab, Barnacle King) use the boss count");
            Assert.IsTrue(warm.Where(d => own.Contains(d.Prefab)).All(d => d.Prewarm == reachable.Where(r => r.model == d.Prefab).Max(r => r.prewarm)), "an archetype's own prewarm, the largest per model");

            var lab = PoolCatalog.Build(config, moduleLab: true);
            Assert.AreEqual(2, lab.Count(d => d.Key.StartsWith("lab/")));
        }

        [Test]
        public void CatalogValidator_RejectsDuplicateKeys_AndPrewarmAboveCap()
        {
            var catalog = ScriptableObject.CreateInstance<PoolCatalogConfig>();
            try
            {
                catalog.configId = "pools.test";
                catalog.entries.Add(new PoolCatalogEntry { key = "a", prefab = _prefab, prewarm = 4, cap = 8 });
                catalog.entries.Add(new PoolCatalogEntry { key = "a", prefab = _prefab, prewarm = 9, cap = 8 });
                catalog.entries.Add(new PoolCatalogEntry { key = "b", prefab = null });
                var report = new ConfigValidationReport();
                catalog.Validate(report);
                var text = report.ToString();
                StringAssert.Contains("duplicate key 'a'", text);
                StringAssert.Contains("prewarm 9 above cap 8", text);
                StringAssert.Contains("missing prefab", text);
            }
            finally { Object.DestroyImmediate(catalog); }
        }
    }
}
