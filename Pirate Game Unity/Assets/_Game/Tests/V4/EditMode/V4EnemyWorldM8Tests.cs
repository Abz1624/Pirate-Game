using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Ports;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M8: the V4 enemy world's scene values, flame-kill hook and per-id world rules.
    /// </summary>
    [Category("V4")]
    public sealed class V4EnemyWorldM8Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        readonly List<Object> _created = new List<Object>();
        HashSet<GameObject> _rootsBefore;

        [SetUp]
        public void SetUp()
        {
            _rootsBefore = new HashSet<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (!_rootsBefore.Contains(root)) Object.DestroyImmediate(root);
            _created.Clear();
            StageBalanceProfileContext.ResetForTests();
        }

        GameDatabase CatalogAppliedDatabase()
        {
            // After a Play Mode session the cached experiment profile can be a destroyed object (register R-82): re-resolve it.
            if (CombatExperimentContext.Active == null) CombatExperimentContext.ResetForTests();
            var db = GameDatabaseLoader.Load();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.enemies = db.enemies.Select(e => { if (e == null) return null; var c = Object.Instantiate(e); c.name = e.name; _created.Add(c); return c; }).ToArray();
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
            CombatWeaponCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            return clone;
        }

        /// <summary>The V4 simulation over the run database.</summary>
        sealed class Pair
        {
            public EnemySimulation V4;
            public EnemyDefinition[] Defs;
        }

        Pair Make(ConfigSetRuntime config)
        {
            var runtime = new V4RuntimeDatabase(config);
            var db = runtime.Build(CatalogAppliedDatabase());
            _created.Add(db);

            var sim = new EnemySimulation(RoguePirates.V4.Foundation.Platform.QualityProfile.Current.enemyCap, new RunPort(), new EnemyViewForwarder(null),
                new EnemySpawnParamResolver(new EnemyCombatProfiles(config), config) { CombatEnabled = SaltwindCombatFeature.IsEnabled }, new EnemyWorldSettings { islandAvoidanceProbeDistance = 10f });
            sim.SetDefinitions(db.enemies, EnemySimulation.BuildRules(config, db.enemies));
            return new Pair { V4 = sim, Defs = db.enemies };
        }

        [Test]
        public void RunScenes_CarryV3sEnemyWorldSceneValues_OnV4Game()
        {
            // V3's EnemyWorld component left the scenes in M8; its serialized values moved to V4Game.enemyWorld.
            foreach (var scene in new[] { "Assets/_Game/Scenes/V4/V4SaltwindRun.unity", "Assets/_Game/Scenes/V4/V4ModuleLab.unity" })
            {
                var text = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", scene)).Replace("\r\n", "\n");
                StringAssert.Contains("  enemyWorld:\n    collideWithIslands: 1\n    islandAvoidanceProbeDistance: 6\n    islandCollisionPadding: 0.35\n    shipCollisionPadding: 0.55\n    enemySeparationPadding: 0.2", text,
                    scene + ": V3's scene values (the component's code default for the island probe was 10)");
            }
        }

        sealed class OnlyHellfire : IHellfireEnvironment
        {
            public bool RunActive => true;
            public float RollTickDamage(bool elite) => 1f;
            public bool IsHellfireWeapon(string weaponId) => weaponId == "evolved_flamer";
            public void OilDropped(Vector3 position, float radius) { }
        }

        [Test]
        public void FlameKillHook_SpawnsV4HellfirePatches_OnlyForTheHellfireEvolution()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var p = Make(config);
            var patches = new HellfirePatches(p.V4, new EnemyStatuses(), new OnlyHellfire());
            var hooks = new RoguePirates.V4.Weapons.WeaponCombatHooks(null, patches);
            hooks.SpawnFlamePatch(Vector3.zero, "deck_cannons");
            Assert.AreEqual(0, patches.ActiveCount, "no patch for other weapons");
            hooks.SpawnFlamePatch(Vector3.zero, "evolved_flamer");
            Assert.AreEqual(1, patches.ActiveCount, "the weapon hook reaches the V4 patches (the Hellfire Nozzle gate decides)");
        }

        [Test]
        public void IdRules_ComeFromTheExactArchetypeOrVariant()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var runtime = new V4RuntimeDatabase(config);
            var db = runtime.Build(CatalogAppliedDatabase());
            _created.Add(db);
            var rules = EnemySimulation.BuildRules(config, db.enemies);
            ArchetypeWorldRules Of(string id) => rules[System.Array.FindIndex(db.enemies, e => e != null && e.id == id)];
            Assert.IsTrue(Of("swordfish").initialChargeTimer.enabled);
            Assert.AreEqual(0f, Of("swordfish").initialChargeTimer.value);
            Assert.IsTrue(Of("swordfish").swordfishSpringDash);
            Assert.AreEqual(1.1f, Of("cannonboat").initialAttackCooldown.value);
            Assert.AreEqual(2f, Of("powder_barge").initialCombatPhaseTimer);
            Assert.IsTrue(Of("repair_tender").excludedFromRepair);
            Assert.IsFalse(Of("hammerhead").eliteLatchesSecondChargeForAll, "R-100 fix: no shared latch (the v3-parity preset turns V3's back on)");
            Assert.IsTrue(Of("hammerhead").eliteChargesTwice, "R-100 fix: an elite hammerhead charges twice itself");
            Assert.IsFalse(Of("elite_hammerhead").eliteLatchesSecondChargeForAll, "V3 matched the exact id");
            foreach (var id in new[] { "cannonboat", "gunner_cog", "bomb_boat", "manta_ray" }) Assert.IsTrue(Of(id).alwaysRanged, id);
            Assert.AreEqual(0.9f, Of("drowned_harrier").initialAttackCooldown.value);
            Assert.IsTrue(Of("drowned_harrier").alwaysRanged);
            Assert.IsTrue(Of("reef_crab_miniboss").noProfilelessRangedAttack);
            Assert.AreEqual(0.8f, Of("cannonboat").rangedMinRangeFactor.value);
            Assert.AreEqual(1.15f, Of("cannonboat").rangedMaxRangeFactor.value);
            Assert.AreEqual(2f, Of("cannonboat").rangedRecovery.value);
            Assert.IsTrue(Of("manta_ray").rangedFlankShots);
            Assert.IsTrue(Of("cannonboat").ownShotProfile && Of("drowned_harrier").ownShotProfile);
            Assert.AreEqual(new[] { 9f, 1.55f, 0.38f, 0.16f }, new[] { Of("cannonboat").shotSpeed, Of("cannonboat").shotLife, Of("cannonboat").shotHitRadius, Of("cannonboat").shotVisualRadius });
            Assert.AreEqual(new[] { 10f, 1.45f, 0.24f, 0.14f }, new[] { Of("drowned_harrier").shotSpeed, Of("drowned_harrier").shotLife, Of("drowned_harrier").shotHitRadius, Of("drowned_harrier").shotVisualRadius });
            var ruled = new[] { "swordfish", "cannonboat", "drowned_harrier", "powder_barge", "repair_tender", "hammerhead", "gunner_cog", "bomb_boat", "manta_ray", "reef_crab_miniboss" };
            for (var i = 0; i < db.enemies.Length; i++)
            {
                if (db.enemies[i] == null || ruled.Contains(db.enemies[i].id)) continue;
                Assert.AreEqual(JsonUtility.ToJson(new ArchetypeWorldRules()), JsonUtility.ToJson(rules[i] ?? new ArchetypeWorldRules()), db.enemies[i].id + ": no id rules (V3 had none)");
            }
        }
    }
}
