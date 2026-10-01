using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Presentation.Effects;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// V5 M3e (owner notes d, f, i): the per-frame paths the profiler named allocate nothing in a steady state and return
    /// what they returned before; the heavy models have Mesh LODs.
    /// </summary>
    public sealed class V5PerformanceTests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        /// <summary>Unity's GC allocation constraint (the GC.Alloc profiler counter; review M3e: the thread byte counter
        /// can read 0 on Unity's runtime). The first call may build caches, so it runs once before.</summary>
        static void NoAllocation(TestDelegate action, string message)
        {
            action();
            Assert.That(action, UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(), message);
        }

        static object _sink;

        [Test]
        public void TheAllocationCheck_CatchesAnAllocation()
        {
            Assert.That(() => { _sink = new byte[256]; }, UnityEngine.TestTools.Constraints.Is.AllocatingGCMemory(), "the check can fail");
        }

        [Test]
        public void ConfigLookups_ReturnWhatLinqReturns_AndAllocateNothing()
        {
            using var rt = new ConfigSetRuntime(Set);
            Assert.AreSame(rt.Configs.OfType<WaveTimelineConfig>().First(), rt.Single<WaveTimelineConfig>());
            CollectionAssert.AreEqual(rt.Configs.OfType<EnemyArchetypeConfig>().ToList(), rt.All<EnemyArchetypeConfig>().ToList(), "same configs, same order");
            NoAllocation(() => { rt.Single<WaveTimelineConfig>(); rt.Single<DifficultyCurveConfig>(); rt.Single<ExperimentConfig>(); }, "Single<T> allocates nothing");
            var timeline = rt.Single<WaveTimelineConfig>();
            foreach (var p in timeline.phases) Assert.AreSame(p, timeline.Phase(p.phaseId));
            Assert.IsNull(timeline.Phase("no_such_phase"));
            NoAllocation(() => timeline.Phase("band_4"), "Phase(id) allocates nothing");
        }

        [Test]
        public void SpatialHash_ReusesItsCells_AndQueriesTheSame()
        {
            var hash = new SpatialHash(4f);
            var positions = Enumerable.Range(0, 80).Select(i => new Vector3((i * 7.3f) % 60f, 0f, (i * 3.1f) % 60f)).ToArray();
            void Build() { hash.Clear(); for (var i = 0; i < positions.Length; i++) hash.Insert(i, positions[i]); }
            Build();
            var first = hash.Query(new Vector3(30f, 0f, 30f), 10f).ToList();
            NoAllocation(() => Build(), "a rebuild reuses its cell lists");
            CollectionAssert.AreEqual(first, hash.Query(new Vector3(30f, 0f, 30f), 10f).ToList(), "same neighbours, same order");
        }

        [Test]
        public void ShipWake_SwitchesOnMovement_AndAllocatesNothingWhileItHolds()
        {
            var views = new EnemyViewPresenter(null, null);
            var ship = new GameObject("ship");
            try
            {
                var wake = new GameObject("Boat Wake (Trail based)");
                wake.transform.SetParent(ship.transform, false);
                wake.SetActive(false);
                var trail = wake.AddComponent<TrailRenderer>();
                views.TickWake(ship.transform, RoguePirates.V4.Foundation.Core.EnemyCategory.Ship, new Vector3(3f, 0f, 0f));
                Assert.IsTrue(wake.activeSelf, "moving: the wake part is on");
                Assert.IsTrue(trail.emitting);
                NoAllocation(() => views.TickWake(ship.transform, RoguePirates.V4.Foundation.Core.EnemyCategory.Ship, new Vector3(3f, 0f, 0f)), "no search while it keeps moving");
                views.TickWake(ship.transform, RoguePirates.V4.Foundation.Core.EnemyCategory.Ship, Vector3.zero);
                Assert.IsFalse(trail.emitting, "still: the trail stops");
                views.TickWake(ship.transform, RoguePirates.V4.Foundation.Core.EnemyCategory.Creature, new Vector3(3f, 0f, 0f));
                Assert.IsFalse(trail.emitting, "creatures have no wake");

                // A new life (review M3e): a trail attached since (a VFX-profile trail) is found again, not a stale list.
                var attached = new GameObject("profile trail").AddComponent<TrailRenderer>();
                attached.transform.SetParent(ship.transform, false);
                attached.emitting = false;
                views.OnViewReused(ship.transform);
                views.TickWake(ship.transform, RoguePirates.V4.Foundation.Core.EnemyCategory.Ship, new Vector3(3f, 0f, 0f));
                Assert.IsTrue(attached.emitting, "the reused view drives the trail it has now");
            }
            finally
            {
                Object.DestroyImmediate(ship);
                views.Dispose();
            }
        }

        sealed class FakeLoadout : RoguePirates.V4.Views.ILoadoutView
        {
            public int WeaponSlotCap { get; set; } = 6;
            public List<(string id, string name, int level)> Weapons = new List<(string, string, int)>();
            public List<(string id, string name, int stacks)> Items = new List<(string, string, int)>();
            public int WeaponCount => Weapons.Count;
            public string WeaponId(int i) => Weapons[i].id;
            public string WeaponName(int i) => Weapons[i].name;
            public string WeaponIconName(int i) => Weapons[i].name;
            public int WeaponLevel(int i) => Weapons[i].level;
            public int ItemCount => Items.Count;
            public string ItemId(int i) => Items[i].id;
            public string ItemName(int i) => Items[i].name;
            public int ItemStacks(int i) => Items[i].stacks;
        }

        [Test]
        public void TheLoadoutHud_RebuildsWhenAnythingItShowsChanges()
        {
            var l = new FakeLoadout();
            l.Weapons.Add(("deck_cannons", "Deck Cannons", 3));
            l.Items.Add(("powder_horn", "Powder Horn", 1));
            int Sig() => RoguePirates.V4.Presentation.Hud.LoadoutHudPresenter.Signature(l, null, l.WeaponSlotCap);
            var seen = new HashSet<int> { Sig() };
            l.Weapons[0] = ("deck_cannons", "Deck Cannons", 4); Assert.IsTrue(seen.Add(Sig()), "a level");
            l.Weapons[0] = ("deck_cannons", "Grand Battery", 4); Assert.IsTrue(seen.Add(Sig()), "an evolved name");
            l.Items[0] = ("powder_horn", "Powder Horn", 2); Assert.IsTrue(seen.Add(Sig()), "a stack");
            l.Items[0] = ("powder_horn", "Powder Horn Plus", 2); Assert.IsTrue(seen.Add(Sig()), "an item name");
            l.Weapons.Add(("mortar", "Mortar", 1)); Assert.IsTrue(seen.Add(Sig()), "a new weapon");
            l.WeaponSlotCap = 5; Assert.IsTrue(seen.Add(Sig()), "the slot cap");
            Assert.AreEqual(Sig(), Sig(), "unchanged: no rebuild");
        }

        [Test]
        public void VfxProfileEffects_ArePrewarmedAtBind()
        {
            var host = new GameObject("vfx test");
            var effect = new GameObject("test_hit_effect");
            var library = ScriptableObject.CreateInstance<VfxProfileLibrary>();
            try
            {
                var profile = new VfxProfile { subject = VfxSubject.Enemy, id = "shark" };
                profile.hit.prefab = effect;
                profile.trail.prefab = effect;
                library.profiles = new[] { profile };
                var pools = new RoguePirates.V4.Presentation.Pooling.PoolService(host.transform);
                var player = host.AddComponent<VfxProfilePlayer>();
                player.Bind(pools, library, () => null);
                var stats = pools.Stats().ToList();
                Assert.AreEqual(VfxProfilePlayer.PrewarmPerEffect, stats.Single(s => s.Key == "vfxprofile-fx/test_hit_effect").Prewarmed, "the hit effect is made at bind");
                Assert.AreEqual(VfxProfilePlayer.PrewarmPerEffect, stats.Single(s => s.Key == "vfxprofile/test_hit_effect").Prewarmed, "the trail too");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(effect);
                Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void HeavyModels_HaveMeshLods()
        {
            foreach (var path in V5MeshLods.Models)
            {
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.IsNotNull(importer, path);
                Assert.IsTrue(importer.generateMeshLods, path);
            }
            CollectionAssert.IsEmpty(V5MeshLods.HeavyModelsNotListed(), "every game model over the limit is listed");
        }
    }
}
