using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Ports;
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

        /// <summary>The database a run starts from: the loaded one after the catalogs (applied here to a deep clone).</summary>
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

        /// <summary>M15 (the V4 half of the retired V3 slot-for-slot test): every enemy the loaded database holds has a
        /// V4 config, the V4-only enemies are appended after the loaded slots, and their models carry no colliders (R-81).</summary>
        [Test]
        public void RuntimeDatabase_ConfiguresEveryEnemy_AndAppendsTheV4OnlyOnes()
        {
            var loaded = CatalogAppliedDatabase();
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var db = runtime.Build(loaded);
                CollectionAssert.IsEmpty(runtime.Unconfigured, "every enemy in the live database has a V4 config");
                CollectionAssert.AreEquivalent(new[] { "drowned_harrier", "drowned_pursuer", "elite_hammerhead", "elite_kraken_spawn" }, runtime.V4OnlyIds);
                Assert.AreEqual(loaded.enemies.Length + runtime.V4OnlyIds.Count, db.enemies.Length, "the loaded layout, then the V4-only enemies");
                Assert.AreEqual(22, loaded.enemies.Count(e => e != null), "14 authored enemies + 8 specialist aliases");
                for (var i = 0; i < loaded.enemies.Length; i++)
                {
                    Assert.AreEqual(loaded.enemies[i] == null, db.enemies[i] == null, $"slot {i} null-ness");
                    if (loaded.enemies[i] != null) Assert.AreNotSame(loaded.enemies[i], db.enemies[i], "runtime definitions are per-run copies");
                }
                for (var i = loaded.enemies.Length; i < db.enemies.Length; i++)
                    CollectionAssert.IsEmpty(db.enemies[i].visualPrefab != null ? db.enemies[i].visualPrefab.GetComponentsInChildren<Collider>(true) : new Collider[0], db.enemies[i].id + ": collider-free model (R-81)");
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

        [Test]
        public void CombatProfiles_AreTheRunsCopies_OfTheConfig_AndNeverTouchTheV3Registry()
        {
            // M14: per-run profiles replace the registry override (which wrote V3's static registry and restored it).
            Assert.IsTrue(EnemyCombatProfileRegistry.TryGet("hammerhead", out var registry));
            var before = JsonUtility.ToJson(Snapshot(registry));
            using (var config = new ConfigSetRuntime(_set))
            using (var profiles = new EnemyCombatProfiles(config))
            {
                Assert.IsTrue(profiles.TryGet("hammerhead", out var run));
                Assert.AreNotSame(registry, run);
                config.TryApplyEdit("enemy.hammerhead", "combat.chargeCooldown", "9", 0f, out _);
                Assert.AreEqual(9f, run.chargeCooldown, "live edits reach the run's profile");
                run.eliteHammerheadSecondCharge = true; // the R-100 latch changes the run's copy only
                var registered = config.All<EnemyArchetypeConfig>().Count(a => a.combat.registered) + config.All<EnemyVariantConfig>().Count(v => v.combat.registered);
                Assert.AreEqual(registered, profiles.Count, "one profile per registered combat block");
            }
            Assert.AreEqual(before, JsonUtility.ToJson(Snapshot(registry)), "V3's registry untouched");
        }

        static ArchetypeCombat Snapshot(EnemyCombatProfile p)
        {
            var c = new ArchetypeCombat();
            FieldMirror.CopyFields(p, c);
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
        /// Content-id literals that are not comparisons: the trace harness credits its forced kills to a weapon (as V3's
        /// trace did); the stage-profile port keys the shared profile's elite beat by its timeline phase id, which equals
        /// the elite Hammerhead's variant id.
        /// </summary>
        static readonly HashSet<string> AllowedLiterals = new HashSet<string>
        {
            "DevTools/Trace/RunTraceRecorder.cs:deck_cannons",
            "Runtime/Ports/StageProfilePort.cs:elite_hammerhead",
        };

        /// <summary>
        /// M15: shared code moved into V4/Foundation that still keys V3's data tables by content id (tiers, combat
        /// profiles, weapon and specialist catalogs, database fallbacks, unlock pools). Replacing them with config is
        /// follow-up work outside M15 (a behaviour-neutral move); this list may only shrink.
        /// </summary>
        static readonly HashSet<string> NameKeyedFoundationFiles = new HashSet<string>
        {
            "Foundation/Balance/BossStatExportLogic.cs",
            "Foundation/Balance/EnemyTierLogic.cs",
            "Foundation/Balance/StageBalanceProfile.cs",
            "Foundation/Balance/WeaponStatExportLogic.cs",
            "Foundation/Combat/CombatSpecialistEnemyCatalog.cs",
            "Foundation/Combat/CombatWeaponCatalog.cs",
            "Foundation/Combat/EnemyCombatProfileRegistry.cs",
            "Foundation/Combat/WeaponDebugPalette.cs",
            "Foundation/Combat/WeaponDisplayNames.cs",
            "Foundation/Combat/WeaponEvolutionCatalog.cs",
            "Foundation/Core/GameDatabaseLoader.cs",
            "Foundation/Data/ShipDefinition.cs",
            "Foundation/Enemies/EnemyRangedAttackRules.cs",
            "Foundation/Enemies/EnemySpawnRuntimeParams.cs",
            "Foundation/Enemies/EnemyVisualFallbackResolver.cs",
            "Foundation/Progression/ContentUnlocks.cs",
            "Foundation/VFX/VfxParticleUtil.cs",
        };

        [Test]
        public void NoV4Code_UsesContentIds()
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
                if (NameKeyedFoundationFiles.Contains(rel)) continue;
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                    foreach (Match m in literal.Matches(lines[i]))
                        if (ids.Contains(m.Groups[1].Value) && !AllowedLiterals.Contains(rel + ":" + m.Groups[1].Value))
                            offenders.Add($"{rel}:{i + 1} \"{m.Groups[1].Value}\"");
            }
            CollectionAssert.IsEmpty(offenders, "content ids in V4 code");
            // The allowlists may only shrink: an entry whose file no longer holds a content id must be removed.
            var stale = NameKeyedFoundationFiles.Where(f => !File.Exists(Path.Combine(root, f))
                    || !literal.Matches(File.ReadAllText(Path.Combine(root, f))).Cast<Match>().Any(m => ids.Contains(m.Groups[1].Value)))
                .Concat(AllowedLiterals.Where(a =>
                {
                    var i = a.LastIndexOf(':');
                    var file = Path.Combine(root, a.Substring(0, i));
                    return !File.Exists(file) || !File.ReadAllText(file).Contains("\"" + a.Substring(i + 1) + "\"") || !ids.Contains(a.Substring(i + 1));
                }))
                .ToList();
            CollectionAssert.IsEmpty(stale, "allowlist entries that no longer apply");
        }
    }
}
