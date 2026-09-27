using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Legacy;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    [Category("V4")]
    public sealed class V4EnemyM2Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        readonly List<Object> _created = new List<Object>();
        ConfigSet _set;

        [SetUp]
        public void SetUp() => _set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
        }

        /// <summary>The database a V3 run uses: the loaded one after the catalogs (applied here to a deep clone).</summary>
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

        /// <summary>
        /// V3 visuals for the specialist aliases (no model, placeholder cube). The default gives them their template's
        /// model instead (R-13 decision); the v3-parity preset carries the same edits.
        /// </summary>
        static void UseV3AliasVisuals(ConfigSetRuntime config)
        {
            foreach (var v in config.All<EnemyVariantConfig>().Where(v => v.kind == VariantKind.DatabaseAlias))
                Assert.IsTrue(config.TryApplyEdit(v.configId, "inheritBaseVisual", "false", 0f, out var error), error);
        }

        [Test]
        public void RuntimeDatabase_ReproducesTheCatalogAppliedV3Enemies_SlotForSlot()
        {
            var v3 = CatalogAppliedDatabase();
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                UseV3AliasVisuals(config);
                var db = runtime.Build(v3);
                CollectionAssert.IsEmpty(runtime.Unconfigured, "every enemy in the live database has a V4 config");
                // V3's slots first (null slots included); V4-only enemies (Drowned, registry elites) are appended after them.
                Assert.AreEqual(v3.enemies.Length + runtime.V4OnlyIds.Count, db.enemies.Length, "V3's layout, then the V4-only enemies");
                CollectionAssert.AreEquivalent(new[] { "drowned_harrier", "drowned_pursuer", "elite_hammerhead", "elite_kraken_spawn" }, runtime.V4OnlyIds);
                for (var i = v3.enemies.Length; i < db.enemies.Length; i++)
                    CollectionAssert.IsEmpty(db.enemies[i].visualPrefab != null ? db.enemies[i].visualPrefab.GetComponentsInChildren<Collider>(true) : new Collider[0], db.enemies[i].id + ": collider-free model (R-81)");
                var compared = 0;
                for (var i = 0; i < v3.enemies.Length; i++)
                {
                    Assert.AreEqual(v3.enemies[i] == null, db.enemies[i] == null, $"slot {i} null-ness");
                    if (v3.enemies[i] == null) continue;
                    Assert.AreNotSame(v3.enemies[i], db.enemies[i], "runtime definitions are per-run copies");
                    Assert.AreEqual(JsonUtility.ToJson(v3.enemies[i]), JsonUtility.ToJson(db.enemies[i]), $"slot {i} ({v3.enemies[i].id})");
                    compared++;
                }
                Assert.AreEqual(22, compared, "14 authored enemies + 8 specialist aliases");
                Assert.AreEqual(v3.weapons.Length, db.weapons.Length);
                for (var i = 0; i < v3.weapons.Length; i++)
                    Assert.AreSame(v3.weapons[i], db.weapons[i], "weapons stay on the loaded database's assets until M5");
            }
        }

        [Test]
        public void ConfigAlone_ReproducesEveryV3Enemy_OnAFreshDefinition()
        {
            // The runtime database starts from copies of the V3 definitions, so a field Apply forgot would still match
            // there. Writing onto an empty definition proves every value comes from config.
            var v3 = CatalogAppliedDatabase();
            using (var config = new ConfigSetRuntime(_set))
            {
                UseV3AliasVisuals(config);
                var compared = 0;
                foreach (var legacy in v3.enemies.Where(e => e != null))
                {
                    var archetype = config.All<EnemyArchetypeConfig>().FirstOrDefault(a => a.enemyId == legacy.id);
                    var variant = config.All<EnemyVariantConfig>().FirstOrDefault(v => v.variantId == legacy.id && v.kind == VariantKind.DatabaseAlias);
                    Assert.IsTrue(archetype != null || variant != null, legacy.id);
                    var fresh = ScriptableObject.CreateInstance<EnemyDefinition>();
                    _created.Add(fresh);
                    V4RuntimeDatabase.Apply(archetype != null ? EnemyStatResolver.Resolve(archetype) : EnemyStatResolver.Resolve(variant), fresh);
                    Assert.AreEqual(JsonUtility.ToJson(legacy), JsonUtility.ToJson(fresh), legacy.id);
                    compared++;
                }
                Assert.AreEqual(22, compared);
            }
        }

        [Test]
        public void SpecialistAliases_UseTheirTemplateModel_ByDefault()
        {
            // R-13 decision: stand-in models until the specialists get their own.
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var db = runtime.Build(CatalogAppliedDatabase());
                var aliases = config.All<EnemyVariantConfig>().Where(v => v.kind == VariantKind.DatabaseAlias).ToList();
                Assert.AreEqual(8, aliases.Count);
                foreach (var v in aliases)
                {
                    Assert.IsTrue(v.inheritBaseVisual, v.variantId);
                    var alias = db.GetEnemy(v.variantId);
                    var template = db.GetEnemy(v.baseArchetype.enemyId);
                    Assert.IsNotNull(template.visualPrefab, v.baseArchetype.enemyId + " has a model");
                    Assert.IsNotNull(alias.visualPrefab, v.variantId);
                    Assert.AreEqual(template.visualPrefab.name, alias.visualPrefab.name, v.variantId + " uses the template model");
                    Assert.AreEqual(template.visualPrefab.GetComponentsInChildren<Renderer>(true).Length, alias.visualPrefab.GetComponentsInChildren<Renderer>(true).Length, v.variantId);
                    CollectionAssert.IsEmpty(alias.visualPrefab.GetComponentsInChildren<Collider>(true), v.variantId + ": no colliders, like V3's cube (R-81)");
                    Assert.AreEqual(1f, alias.visualScale, v.variantId + " keeps the V3 scale, so its hitbox is unchanged");
                    Assert.AreEqual(template.visualHeightOffset, alias.visualHeightOffset, v.variantId);
                    Assert.AreEqual(v.baseHP.value, alias.baseHP, v.variantId + " keeps its own stats");
                }
            }
        }

        [Test]
        public void FallbackOnlyArchetypes_EqualV3FallbackDefinitions()
        {
            var fallback = (EnemyDefinition[])typeof(GameDatabaseLoader).GetMethod("CreateEnemies", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            foreach (var d in fallback) _created.Add(d);
            var drowned = _set.All<EnemyArchetypeConfig>().Where(a => !a.inLegacyDatabase).ToList();
            CollectionAssert.AreEquivalent(new[] { "drowned_pursuer", "drowned_harrier" }, drowned.Select(a => a.enemyId));
            foreach (var archetype in drowned)
            {
                var legacy = fallback.Single(d => d.id == archetype.enemyId);
                var fresh = ScriptableObject.CreateInstance<EnemyDefinition>();
                _created.Add(fresh);
                V4RuntimeDatabase.Apply(EnemyStatResolver.Resolve(archetype), fresh);
                // R-05: V4 gives them placeholder models (V3's fallback defines none); everything else is V3's.
                Assert.IsTrue(archetype.visual.placeholderModel, archetype.enemyId + " is marked as a placeholder model");
                fresh.visualPrefab = null;
                fresh.visualScale = legacy.visualScale;
                // Base HP/damage come from the V3 overtime enemies (shark, cannonboat), not V3's never-used fallback.
                Assert.AreEqual(_set.All<EnemyArchetypeConfig>().Single(a => a.enemyId == (archetype.enemyId == "drowned_pursuer" ? "shark" : "cannonboat")).stats.baseDamage, fresh.baseDamage, archetype.enemyId + " damage");
                fresh.baseHP = legacy.baseHP;
                fresh.baseDamage = legacy.baseDamage;
                Assert.AreEqual(JsonUtility.ToJson(legacy), JsonUtility.ToJson(fresh), archetype.enemyId);
            }
        }

        [Test]
        public void EditingAnArchetype_RewritesItsDefinitionAndItsAliases()
        {
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var db = runtime.Build(CatalogAppliedDatabase());
                Assert.IsTrue(config.TryApplyEdit("enemy.rammer_raft", "stats.baseDamage", "99", 0f, out var error), error);
                Assert.AreEqual(99f, db.GetEnemy("rammer_raft").baseDamage);
                Assert.AreEqual(99f, db.GetEnemy("ram_raider").baseDamage, "aliases copy the template's damage");
                Assert.IsTrue(config.TryApplyEdit("variant.ram_raider", "baseHP.value", "50", 0f, out error), error);
                Assert.AreEqual(50f, db.GetEnemy("ram_raider").baseHP);
                Assert.AreNotEqual(50f, db.GetEnemy("rammer_raft").baseHP);
            }
        }

        /// <summary>
        /// The registry as V3 builds it. The live one is shared and mutated at runtime (the elite hammerhead flag), so
        /// comparing against it would depend on what ran earlier in the session.
        /// </summary>
        static Dictionary<string, EnemyCombatProfile> Pristine() =>
            (Dictionary<string, EnemyCombatProfile>)typeof(EnemyCombatProfileRegistry).GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        [Test]
        public void CombatBlocks_EqualTheRegistry_ForArchetypesVariantsAndElites()
        {
            var checkedIds = new List<string>();
            var pristine = Pristine();
            foreach (var (id, combat) in _set.All<EnemyArchetypeConfig>().Select(a => (a.enemyId, a.combat))
                         .Concat(_set.All<EnemyVariantConfig>().Select(v => (v.variantId, v.combat))))
            {
                if (!pristine.TryGetValue(id, out var profile)) { Assert.IsFalse(combat.registered, id); continue; }
                var expected = new ArchetypeCombat();
                LegacyConfigMapping.CopyFields(profile, expected);
                expected.registered = true;
                // R-77: the elite Kraken keeps the plain Kraken's no-contact rule (V3's elite entry left the default on).
                if (id == "elite_kraken_spawn") expected.contactDamageEnabled = false;
                Assert.AreEqual(JsonUtility.ToJson(expected), JsonUtility.ToJson(combat), id);
                checkedIds.Add(id);
            }
            CollectionAssert.IsSubsetOf(new[] { "elite_hammerhead", "elite_kraken_spawn", "drowned_pursuer", "drowned_harrier", "needle_sloop" }, checkedIds);
            Assert.GreaterOrEqual(checkedIds.Count, 24);
        }

        [Test]
        public void CombatOverride_WritesConfigValues_AndRestoresTheRegistry()
        {
            Assert.IsTrue(EnemyCombatProfileRegistry.TryGet("hammerhead", out var live));
            var before = Snapshot(live);
            try
            {
                using (var config = new ConfigSetRuntime(_set))
                {
                    config.TryApplyEdit("enemy.hammerhead", "combat.chargeCooldown", "9", 0f, out _);
                    var over = new LegacyCombatProfileOverride(config);
                    over.ApplyAll();
                    Assert.AreEqual(9f, live.chargeCooldown, "config value written into the registry");
                    config.TryApplyEdit("enemy.hammerhead", "combat.chargeCooldown", "7", 0f, out _);
                    Assert.AreEqual(7f, live.chargeCooldown, "live edits reach the registry");
                    over.Dispose();
                }
                Assert.AreEqual(JsonUtility.ToJson(before), JsonUtility.ToJson(Snapshot(live)), "registry restored when the run ends");
            }
            finally
            {
                LegacyConfigMapping.CopyFields(before, live); // never leave the shared registry changed
            }
        }

        static ArchetypeCombat Snapshot(EnemyCombatProfile p)
        {
            var c = new ArchetypeCombat();
            LegacyConfigMapping.CopyFields(p, c);
            return c;
        }

        [Test]
        public void Resolver_RecordsWhichLayerSetEachValue()
        {
            var alias = _set.All<EnemyVariantConfig>().Single(v => v.variantId == "needle_sloop");
            var r = EnemyStatResolver.Resolve(alias);
            Assert.AreEqual(StatLayer.Archetype, r.Provenance["baseDamage"], "aliases copy damage from the template");
            Assert.AreEqual(StatLayer.Variant, r.Provenance["baseHP"]);
            Assert.AreEqual(StatLayer.Variant, r.Provenance["visualPrefab"], "aliases take the template model by default (R-13)");
            Assert.AreEqual("needle_sloop", r.id);
            var elite = EnemyStatResolver.Resolve(_set.All<EnemyVariantConfig>().Single(v => v.variantId == "elite_hammerhead"));
            Assert.AreEqual("elite_hammerhead", elite.id, "registry elites have their own definition (R-03), so their combat profile applies to them alone");
            Assert.AreEqual(_set.All<EnemyArchetypeConfig>().Single(a => a.enemyId == "hammerhead").stats.baseHP, elite.baseHP, "with the base enemy's stats");
            Assert.AreEqual(StatLayer.Variant, elite.Provenance["combat"]);
            Assert.IsTrue(elite.combat.eliteHammerheadSecondCharge);
        }

        /// <summary>
        /// Content-id literals outside Legacy/ that are not comparisons: the trace harness credits its forced kills to
        /// a weapon (as V3's trace did), and the migrator names the miniboss beat's pool.
        /// </summary>
        static readonly HashSet<string> AllowedLiterals = new HashSet<string>
        {
            "DevTools/Trace/RunTraceRecorder.cs:deck_cannons",
            "Editor/V3ConfigMigrator.cs:reef_crab_miniboss",
            // The playtest-decision data step names the elite variants and Drowned enemies it wires up (R-03, R-05).
            "Editor/V3ConfigMigrator.cs:elite_hammerhead",
            "Editor/V3ConfigMigrator.cs:elite_kraken_spawn",
            "Editor/V3ConfigMigrator.cs:drowned_pursuer",
            "Editor/V3ConfigMigrator.cs:drowned_harrier",
            // ...and the V3 overtime enemies whose base HP/damage the Drowned take (playtest follow-up).
            "Editor/V3ConfigMigrator.cs:shark",
            "Editor/V3ConfigMigrator.cs:cannonboat",
            // M5: the weapon data step maps V3 WeaponController's weapon-id branches onto config (behaviors, blocks, models).
            "Editor/V3ConfigMigrator.cs:swivel_guns",
            "Editor/V3ConfigMigrator.cs:deck_cannons",
            "Editor/V3ConfigMigrator.cs:long_nine",
            "Editor/V3ConfigMigrator.cs:harpoon_gun",
            "Editor/V3ConfigMigrator.cs:broadside_battery",
            "Editor/V3ConfigMigrator.cs:flamethrower",
            // M6: the weapon visuals step maps V3's juice controller and aim overlay weapon-id branches onto config.
            "Editor/V3ConfigMigrator.cs:mortar",
            "Editor/V3ConfigMigrator.cs:lightning_mast",
            "Editor/V3ConfigMigrator.cs:flying_cutlasses",
            "Editor/V3ConfigMigrator.cs:powder_kegs",
            // M8: the world-rules data step maps V3 EnemyWorld's enemy-id branches onto archetype/variant data.
            "Editor/V3ConfigMigrator.cs:swordfish",
            "Editor/V3ConfigMigrator.cs:powder_barge",
            "Editor/V3ConfigMigrator.cs:repair_tender",
            "Editor/V3ConfigMigrator.cs:hammerhead",
            "Editor/V3ConfigMigrator.cs:gunner_cog",
            "Editor/V3ConfigMigrator.cs:bomb_boat",
            "Editor/V3ConfigMigrator.cs:manta_ray",
        };

        [Test]
        public void NoV4CodeOutsideLegacy_UsesContentIds()
        {
            // Any string literal equal to a content id counts (==, case, switch arms, is, Equals, StartsWith, Contains,
            // collection literals...), not only the comparison forms.
            var ids = new HashSet<string>(_set.All<EnemyArchetypeConfig>().Select(a => a.enemyId)
                .Concat(_set.All<EnemyVariantConfig>().Select(v => v.variantId))
                .Concat(_set.All<WeaponConfig>().Select(w => w.weaponId)));
            var literal = new Regex("\"([a-z0-9_]+)\"");
            var root = Path.Combine(Application.dataPath, "_Game", "V4");
            var offenders = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var rel = file.Substring(root.Length + 1).Replace('\\', '/');
                if (rel.StartsWith("Runtime/Legacy/")) continue; // the name-keyed bridge (plan D3)
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                    foreach (Match m in literal.Matches(lines[i]))
                        if (ids.Contains(m.Groups[1].Value) && !AllowedLiterals.Contains(rel + ":" + m.Groups[1].Value))
                            offenders.Add($"{rel}:{i + 1} \"{m.Groups[1].Value}\"");
            }
            CollectionAssert.IsEmpty(offenders, "content ids outside Legacy/");
        }
    }
}
