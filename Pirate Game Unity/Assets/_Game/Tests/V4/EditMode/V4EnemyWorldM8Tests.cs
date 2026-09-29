using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Legacy;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>
    /// M8: the V4 enemy world against V3's EnemyWorld, run side by side from the same inputs (lockstep). Each build
    /// step adds its comparisons; step 1 covers slots, unit ids, spawning and despawning.
    /// </summary>
    [Category("V4")]
    public sealed class V4EnemyWorldM8Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        const string ProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";

        readonly List<Object> _created = new List<Object>();
        HashSet<GameObject> _rootsBefore;

        [SetUp]
        public void SetUp()
        {
            SaltwindCombatAgentStateStore.ClearAll();
            // V3's despawn calls Destroy (health bars, placeholder cubes), which edit mode refuses with an error log;
            // the objects are removed in TearDown instead.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            _rootsBefore = new HashSet<GameObject>(UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (!_rootsBefore.Contains(root)) Object.DestroyImmediate(root);
            _created.Clear();
            SaltwindCombatAgentStateStore.ClearAll();
            StageBalanceProfileContext.ResetForTests();
            _registry?.Dispose();
            _registry = null;
        }

        V3RegistryFromConfig _registry; // M14: V3's registry holds the config values while a test compares with it

        GameDatabase CatalogAppliedDatabase()
        {
            var db = RoguePirates.V4.Editor.V3ConfigMigrator.LoadCleanDatabase();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.enemies = db.enemies.Select(e => { if (e == null) return null; var c = Object.Instantiate(e); c.name = e.name; _created.Add(c); return c; }).ToArray();
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
            CombatWeaponCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            return clone;
        }

        /// <summary>V3's EnemyWorld and the V4 simulation over the same run database, each with its own view root.</summary>
        sealed class Pair
        {
            public EnemyWorld Legacy;
            public EnemySimulation V4;
            public EnemyDefinition[] Defs;
        }

        Pair Make(ConfigSetRuntime config)
        {
            _registry?.Dispose();
            _registry = new V3RegistryFromConfig(config);
            var runtime = new V4RuntimeDatabase(config);
            var db = runtime.Build(CatalogAppliedDatabase());
            _created.Add(db);

            var legacyRoot = new GameObject("legacy-world");
            _created.Add(legacyRoot);
            var legacy = legacyRoot.AddComponent<EnemyWorld>();
            typeof(EnemyWorld).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(legacy, null);
            legacy.SetDefinitions(db.enemies);

            var v4Root = new GameObject("v4-world");
            _created.Add(v4Root);
            var sim = new EnemySimulation(legacy.Agents.Length, new LegacyRunPort(), new V3EnemyViewSink(v4Root.transform),
                new EnemySpawnParamResolver(new EnemyCombatProfiles(config), config) { CombatEnabled = SaltwindCombatFeature.IsEnabled }, new EnemyWorldSettings { islandAvoidanceProbeDistance = 10f });
            sim.SetDefinitions(db.enemies, EnemySimulation.BuildRules(config, db.enemies));
            return new Pair { Legacy = legacy, V4 = sim, Defs = db.enemies };
        }

        static readonly FieldInfo[] AgentFields = typeof(EnemyAgent).GetFields(BindingFlags.Instance | BindingFlags.Public);

        static void AssertSame(Pair p, string step)
        {
            Assert.AreEqual(p.Legacy.ActiveCount, p.V4.ActiveCount, step + ": active count");
            Assert.AreEqual(p.Legacy.Agents.Length, p.V4.Agents.Length);
            var unitFields = typeof(EnemyUnit).GetFields(BindingFlags.Instance | BindingFlags.Public).ToDictionary(f => f.Name);
            // M11 adds one V4-only field (adaptive elites pay ordinary rewards); it is never set without the elite lever.
            Assert.AreEqual(AgentFields.Length + 1, unitFields.Count, "EnemyUnit mirrors EnemyAgent field for field (+ ordinaryRewards)");
            Assert.IsTrue(unitFields.ContainsKey(nameof(EnemyUnit.ordinaryRewards)));
            foreach (var unit in p.V4.Agents) Assert.IsFalse(unit.ordinaryRewards, step + ": no adaptive elites in lockstep runs");
            for (var i = 0; i < p.Legacy.Agents.Length; i++)
            {
                object a = p.Legacy.Agents[i];
                object b = p.V4.Agents[i];
                foreach (var f in AgentFields)
                {
                    var va = f.GetValue(a);
                    var vb = unitFields[f.Name].GetValue(b);
                    if (f.FieldType == typeof(Transform))
                    {
                        var ta = va as Transform;
                        var tb = vb as Transform;
                        Assert.AreEqual(ta != null, tb != null, $"{step}: slot {i} {f.Name} exists");
                        if (ta == null) continue;
                        Assert.AreEqual(ta.position, tb.position, $"{step}: slot {i} {f.Name} position");
                        Assert.AreEqual(ta.localScale, tb.localScale, $"{step}: slot {i} {f.Name} scale");
                        Assert.AreEqual(ta.rotation, tb.rotation, $"{step}: slot {i} {f.Name} rotation");
                        Assert.AreEqual(ta.gameObject.name, tb.gameObject.name, $"{step}: slot {i} {f.Name} model");
                        continue;
                    }
                    if (f.FieldType == typeof(EnemySpawnRuntimeParams))
                    {
                        Assert.AreEqual(JsonUtility.ToJson(va), JsonUtility.ToJson(vb), $"{step}: slot {i} patternParams");
                        continue;
                    }
                    Assert.AreEqual(va, vb, $"{step}: slot {i} {f.Name}");
                }
                var agent = p.Legacy.Agents[i];
                if (!agent.active) continue;
                var hasLegacy = SaltwindCombatAgentStateStore.TryGet(agent.id, out var ls);
                var hasV4 = p.V4.CombatStates.TryGet(agent.id, out var vs);
                Assert.AreEqual(hasLegacy, hasV4, $"{step}: slot {i} combat state exists");
                if (!hasLegacy) continue;
                Assert.AreEqual(ls.enemyId, vs.enemyId);
                Assert.AreEqual(ls.orbitSign, vs.orbitSign, $"{step}: slot {i} orbit sign");
                Assert.AreEqual(ls.phaseTimer, vs.phaseTimer, $"{step}: slot {i} initial phase timer");
                Assert.AreEqual(ls.phase, vs.phase);
            }
        }

        void RunSequence(Pair p, string label)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true; // see SetUp (reset by the runner before each test body)
            var defs = p.Defs;
            var profiles = AssetDatabase.FindAssets("t:SpawnProfile", new[] { "Assets/_Game/Data/SpawnProfiles/Saltwind" })
                .Select(g => AssetDatabase.LoadAssetAtPath<SpawnProfile>(AssetDatabase.GUIDToAssetPath(g))).Where(s => s != null && s.enemy != null).ToList();
            Assert.Greater(profiles.Count, 5);

            void Both(string step, System.Func<EnemyWorld, int> legacy, System.Func<EnemySimulation, int> v4)
            {
                var a = legacy(p.Legacy);
                var b = v4(p.V4);
                Assert.AreEqual(a, b, $"{label} {step}: returned slot");
                AssertSame(p, $"{label} {step}");
            }

            var position = new Vector3(12f, 0f, -7f);
            var affixes = new[] { EliteAffix.None, EliteAffix.Rich, EliteAffix.Armored, EliteAffix.Swift };
            for (var d = 0; d < defs.Length; d++)
            {
                if (defs[d] == null) continue;
                var index = d;
                var affix = affixes[d % affixes.Length];
                var pos = position + new Vector3(d * 3f, 0f, d);
                Both($"spawn {defs[d].id} {affix}", w => w.Spawn(index, pos, affix, 1.1f, 1.3f, 0.9f), s => s.Spawn(index, pos, affix, 1.1f, 1.3f, 0.9f));
            }
            Both("spawn out of range", w => w.Spawn(defs.Length + 3, position), s => s.Spawn(defs.Length + 3, position));
            for (var k = 0; k < profiles.Count; k++)
            {
                var profile = profiles[k];
                var index = System.Array.FindIndex(defs, e => e != null && e.id == profile.enemy.id);
                if (index < 0) continue;
                var role = (FormationRole)(k % 3);
                var pos = new Vector3(-20f + k * 4f, 0f, 30f);
                var instance = 100 + k;
                Both($"profile {profile.name}", w => w.SpawnFromProfile(index, pos, profile, instance, role, k, 7, 1.2f, 1.05f),
                    s => s.SpawnFromProfile(index, pos, profile, instance, role, k, 7, 1.2f, 1.05f));
            }
            var beat = System.Array.FindIndex(defs, e => e != null && e.id == "hammerhead");
            Both("pressure beat", w => w.SpawnPressureBeat(beat, position), s => s.SpawnPressureBeat(beat, position));

            // Free some slots: the next spawns must reuse the lowest free slots, with fresh unit ids.
            foreach (var slot in new[] { 3, 0, 7 })
            {
                p.Legacy.Despawn(slot);
                p.V4.Despawn(slot);
                AssertSame(p, $"{label} despawn {slot}");
            }
            Assert.IsTrue(p.Legacy.DespawnByUnitId(12) == p.V4.DespawnByUnitId(12));
            AssertSame(p, $"{label} despawn unit 12");
            var shark = System.Array.FindIndex(defs, e => e != null && e.id == "shark");
            for (var k = 0; k < 5; k++)
                Both($"reuse {k}", w => w.Spawn(shark, position), s => s.Spawn(shark, position));

            // Fill the world: failed spawns must not use up unit ids.
            var capacity = p.Legacy.Agents.Length;
            for (var k = 0; k < capacity + 3; k++)
            {
                var a = p.Legacy.Spawn(shark, position);
                var b = p.V4.Spawn(shark, position);
                Assert.AreEqual(a, b, $"{label} fill {k}");
            }
            AssertSame(p, $"{label} full");
            Assert.IsTrue(p.Legacy.TryDespawnOldestActive(out var oldA) & p.V4.TryDespawnOldestActive(out var oldB));
            Assert.AreEqual(oldA, oldB);
            Both("after full", w => w.Spawn(shark, position), s => s.Spawn(shark, position));
            Assert.AreEqual(p.Legacy.CullAllExceptAgent(20), p.V4.CullAllExceptAgent(20));
            AssertSame(p, $"{label} cull");
            p.Legacy.DespawnAllActive();
            p.V4.DespawnAllActive();
            AssertSame(p, $"{label} despawn all");
            Assert.AreEqual(0, p.V4.CombatStates.Count, "combat state cleared with the units");
        }

        [Test]
        public void Spawning_MatchesV3_SlotBySlot_WithoutTheCombatFeature()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var p = Make(config);
            Assert.IsFalse(SaltwindCombatFeature.IsEnabled);
            RunSequence(p, "feature off");
        }

        [Test]
        public void Spawning_MatchesV3_SlotBySlot_InASaltwindRun()
        {
            using var config = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            StageBalanceProfileContext.BeginRun(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath));
            Assert.IsTrue(SaltwindCombatFeature.IsEnabled, "Saltwind combat on: registered enemies get combat state and profile parameters");
            var p = Make(config);
            RunSequence(p, "saltwind");
        }

        [Test]
        public void RunScenes_CarryV3sEnemyWorldSceneValues_OnV4Game()
        {
            // V3's EnemyWorld component left the scenes in M8; its serialized values moved to V4Game.enemyWorld.
            foreach (var scene in new[] { "Assets/_Game/Scenes/V4/V4SaltwindRun.unity", "Assets/_Game/Scenes/V4/V4ModuleLab.unity" })
            {
                var text = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "..", scene)).Replace("\r\n", "\n");
                StringAssert.DoesNotContain("HighSeas.Enemies.EnemyWorld", text, scene + ": no V3 EnemyWorld");
                StringAssert.DoesNotContain("HighSeas.Combat.StatusController", text, scene + ": no V3 StatusController");
                StringAssert.Contains("  enemyWorld:\n    collideWithIslands: 1\n    islandAvoidanceProbeDistance: 6\n    islandCollisionPadding: 0.35\n    shipCollisionPadding: 0.55\n    enemySeparationPadding: 0.2", text,
                    scene + ": V3's scene values (the component's code default for the island probe was 10)");
            }
        }

        [Test]
        public void V3EnemyWorldTypes_OnlyInTheGoldenViewsAndTheSceneStep()
        {
            var types = new System.Text.RegularExpressions.Regex(@"\b(EnemyWorld|StatusController|HostileProjectileWorld|HellfirePatchWorld|CombatTelemetryDriver)\b");
            var allowed = new[] { "Runtime/Legacy/LegacyEnemyViews.cs", "Editor/V4SceneSetup.cs" };
            var root = System.IO.Path.Combine(Application.dataPath, "_Game", "V4");
            var offenders = new List<string>();
            foreach (var file in System.IO.Directory.GetFiles(root, "*.cs", System.IO.SearchOption.AllDirectories))
            {
                var rel = file.Substring(root.Length + 1).Replace('\\', '/');
                if (allowed.Contains(rel)) continue;
                var lines = System.IO.File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var code = lines[i];
                    var comment = code.IndexOf("//", System.StringComparison.Ordinal);
                    if (comment >= 0) code = code.Substring(0, comment);
                    code = System.Text.RegularExpressions.Regex.Replace(code, @"""(?:[^""\\]|\\.)*""", @""""""); // ignore string literals (tooltips)
                    if (types.IsMatch(code)) offenders.Add($"{rel}:{i + 1}");
                }
            }
            CollectionAssert.IsEmpty(offenders, "M8: V3's enemy world types only in the V3-scene golden views and the scene step");
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
