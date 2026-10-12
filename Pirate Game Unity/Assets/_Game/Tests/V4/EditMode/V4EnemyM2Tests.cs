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

        /// <summary>The database a run starts from: the loaded one, as a deep clone (M16: V3's catalogs are gone).</summary>
        GameDatabase LoadedDatabase()
        {
            // After a Play Mode session the cached experiment profile can be a destroyed object (register R-82): re-resolve it.
            if (CombatExperimentContext.Active == null) CombatExperimentContext.ResetForTests();
            var db = GameDatabaseLoader.Load();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.enemies = db.enemies.Select(e => { if (e == null) return null; var c = Object.Instantiate(e); c.name = e.name; _created.Add(c); return c; }).ToArray();
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
            return clone;
        }

        GameDatabase CloneOf(GameDatabase db)
        {
            if (CombatExperimentContext.Active == null) CombatExperimentContext.ResetForTests();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.enemies = db.enemies.Select(e => { if (e == null) return null; var c = Object.Instantiate(e); c.name = e.name; _created.Add(c); return c; }).ToArray();
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
            return clone;
        }

        /// <summary>M16: the runtime database appends V3's eight specialist aliases right after the loaded slots, in V3's
        /// catalog order (proved equal to the catalog path, field for field, before the catalogs were deleted), for the
        /// Editor database and the player (Resources) one.</summary>
        [TestCase(true)]
        [TestCase(false)]
        public void RuntimeDatabase_AppendsTheAliases_InV3sOrder(bool editorDatabase)
        {
            var db = editorDatabase ? AssetDatabase.LoadAssetAtPath<GameDatabase>(GameDatabaseLoader.AssetPath) : Resources.Load<GameDatabase>("GameDatabase");
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var loaded = CloneOf(db);
                var built = runtime.Build(loaded).enemies;
                var order = new[] { "needle_sloop", "ram_raider", "bomb_ketch", "net_cutter", "repair_tender", "powder_barge", "shield_frigate", "treasure_raider" };
                for (var i = 0; i < order.Length; i++)
                {
                    var d = built[loaded.enemies.Length + i];
                    Assert.AreEqual(order[i], d.id, "slot " + (loaded.enemies.Length + i));
                    CollectionAssert.Contains(d.tags, "specialist");
                    Assert.AreEqual(string.Empty, d.name, "nameless, as the catalog's definitions");
                }
            }
        }

        /// <summary>M15 (the V4 half of the retired V3 slot-for-slot test): every enemy the loaded database holds has a
        /// V4 config, the V4-only enemies are appended after the loaded slots, and their models carry no colliders (R-81).</summary>
        [Test]
        public void RuntimeDatabase_ConfiguresEveryEnemy_AndAppendsTheV4OnlyOnes()
        {
            var loaded = LoadedDatabase();
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var db = runtime.Build(loaded);
                CollectionAssert.IsEmpty(runtime.Unconfigured, "every enemy in the live database has a V4 config");
                CollectionAssert.AreEquivalent(new[] { "drowned_harrier", "drowned_pursuer", "elite_hammerhead", "elite_kraken_spawn" }, runtime.V4OnlyIds);
                Assert.AreEqual(loaded.enemies.Length + 8 + runtime.V4OnlyIds.Count, db.enemies.Length, "the loaded layout, the 8 specialist aliases (M16: created here), then the V4-only enemies");
                Assert.AreEqual(14, loaded.enemies.Count(e => e != null), "14 authored enemies in the loaded database");
                Assert.AreEqual(22 + runtime.V4OnlyIds.Count, db.enemies.Count(e => e != null), "14 authored + 8 aliases + the V4-only enemies");
                for (var i = 0; i < loaded.enemies.Length; i++)
                {
                    Assert.AreEqual(loaded.enemies[i] == null, db.enemies[i] == null, $"slot {i} null-ness");
                    if (loaded.enemies[i] != null) Assert.AreNotSame(loaded.enemies[i], db.enemies[i], "runtime definitions are per-run copies");
                }
                for (var i = loaded.enemies.Length; i < db.enemies.Length; i++)
                    CollectionAssert.IsEmpty(db.enemies[i].visualPrefab != null ? db.enemies[i].visualPrefab.GetComponentsInChildren<Collider>(true) : new Collider[0], db.enemies[i].id + ": collider-free model (R-81)");
            }
        }

        /// <summary>M16 (V3's proxy-model table removed): every runtime enemy has its model, and the archetypes carry the
        /// proxy models and scales V3's table gave the eight ship enemies without art.</summary>
        [Test]
        public void EveryRuntimeEnemy_HasAModel_AndTheArchetypesCarryV3sProxies()
        {
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var db = runtime.Build(LoadedDatabase());
                foreach (var d in db.enemies.Where(e => e != null))
                    Assert.IsNotNull(d.visualPrefab, d.id + " has a model");
                // V5 M8z (7 Oct, I8z.5): the playtest resized the ships on purpose; the sizes are the config's, so only the
                // proxy models are pinned and every size must be drawable.
                var proxies = new (string id, string model)[]
                {
                    ("cutthroat_skiff", "SmuggleBoat"), ("deckhand_dinghy", "SmuggleBoat"), ("gunner_cog", "CannonboatNavy"), // V5 M3d tells it apart by tint and size
                    ("bomb_boat", "FireBoat"), ("rammer_raft", "RamShip"), ("boarding_barge", "RamShip"),
                    ("drowned_pursuer", "SmuggleBoat"), ("drowned_harrier", "CannonboatNavy"),
                };
                foreach (var (id, model) in proxies)
                    Assert.AreEqual(model, db.GetEnemy(id).visualPrefab.name, id);
                foreach (var d in db.enemies.Where(e => e != null))
                    Assert.Greater(d.visualScale, 0f, d.id + " has a drawable size");
            }
        }

        /// <summary>M16: the spawn-warning tier comes from config; equal to V3's id table for every runtime enemy.</summary>
        [Test]
        public void WarningTiers_FromConfig_EqualV3sTable()
        {
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var db = runtime.Build(LoadedDatabase());
                foreach (var d in db.enemies.Where(e => e != null))
                {
                    var expected = d.isBoss ? EnemyTier.Boss : d.isMiniboss ? EnemyTier.General : V3Tier(d.id);
                    var fromConfig = d.isBoss ? EnemyTier.Boss : d.isMiniboss ? EnemyTier.General : d.tier;
                    Assert.AreEqual(expected, fromConfig, d.id);
                    Assert.AreEqual(fromConfig, RoguePirates.V4.Foundation.Balance.EnemyTierLogic.ResolveTier(d), d.id + " (the warning reads it)");
                }
            }
        }

        /// <summary>V3's tier table by enemy id (EnemyTierLogic.ResolveTierFromId, deleted in M16).</summary>
        static EnemyTier V3Tier(string id) => id switch
        {
            "barnacle_king" => EnemyTier.Boss,
            "reef_crab_miniboss" or "kraken_spawn" or "boarding_barge" => EnemyTier.General,
            "shark" or "cannonboat" or "gunner_cog" or "bomb_boat" or "rammer_raft" or "manta_ray" or "needle_sloop" or "ram_raider" => EnemyTier.Captain,
            _ => EnemyTier.Soldier
        };

        [Test]
        public void SpecialistAliases_UseTheirTemplateModel_ByDefault()
        {
            // R-13 decision: stand-in models until the specialists get their own.
            using (var config = new ConfigSetRuntime(_set))
            using (var runtime = new V4RuntimeDatabase(config))
            {
                var db = runtime.Build(LoadedDatabase());
                var aliases = config.All<EnemyVariantConfig>().Where(v => v.kind == VariantKind.DatabaseAlias).ToList();
                Assert.AreEqual(8, aliases.Count);
                foreach (var v in aliases)
                {
                    Assert.IsTrue(v.inheritBaseVisual, v.variantId);
                    var alias = db.GetEnemy(v.variantId);
                    var template = db.GetEnemy(v.baseArchetype.enemyId);
                    Assert.IsNotNull(template.visualPrefab, v.baseArchetype.enemyId + " has a model");
                    Assert.IsNotNull(alias.visualPrefab, v.variantId);
                    var expected = v.look?.model != null ? v.look.model.name : template.visualPrefab.name; // V5 M3d: a variant may wear its own model
                    Assert.AreEqual(expected, alias.visualPrefab.name, v.variantId + " uses the template model or its own look");
                    if (v.look?.model == null) Assert.AreEqual(template.visualPrefab.GetComponentsInChildren<Renderer>(true).Length, alias.visualPrefab.GetComponentsInChildren<Renderer>(true).Length, v.variantId);
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
                var db = runtime.Build(LoadedDatabase());
                Assert.IsTrue(config.TryApplyEdit("enemy.rammer_raft", "stats.baseDamage", "99", 0f, out var error), error);
                Assert.AreEqual(99f, db.GetEnemy("rammer_raft").baseDamage);
                Assert.AreEqual(99f, db.GetEnemy("ram_raider").baseDamage, "aliases copy the template's damage");
                Assert.IsTrue(config.TryApplyEdit("variant.ram_raider", "baseHP.value", "50", 0f, out error), error);
                Assert.AreEqual(50f, db.GetEnemy("ram_raider").baseHP);
                Assert.AreNotEqual(50f, db.GetEnemy("rammer_raft").baseHP);
            }
        }

        [Test]
        public void CombatProfiles_AreTheRunsCopies_OfTheConfig()
        {
            // M14: per-run profiles replace V3's static registry (M16: the registry is deleted).
            using (var config = new ConfigSetRuntime(_set))
            using (var profiles = new EnemyCombatProfiles(config))
            {
                Assert.IsTrue(profiles.TryGet("hammerhead", out var run));
                using (var other = new EnemyCombatProfiles(config))
                {
                    Assert.IsTrue(other.TryGet("hammerhead", out var second));
                    Assert.AreNotSame(run, second, "each run builds its own copies");
                }
                config.TryApplyEdit("enemy.hammerhead", "combat.chargeCooldown", "9", 0f, out _);
                Assert.AreEqual(9f, run.chargeCooldown, "live edits reach the run's profile");
                run.eliteHammerheadSecondCharge = true; // the R-100 latch changes the run's copy only
                var registered = config.All<EnemyArchetypeConfig>().Count(a => a.combat.registered) + config.All<EnemyVariantConfig>().Count(v => v.combat.registered);
                Assert.AreEqual(registered, profiles.Count, "one profile per registered combat block");
            }
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
            // Hackathon (12 Oct): the PvP practice bot's fallback draft when the match names none (a copy of the server's
            // practice bot draft; every id is still checked against the config set before it is used).
            "Runtime/Pvp/PvpBotArsenal.cs:deck_cannons",
            "Runtime/Pvp/PvpBotArsenal.cs:mortar",
            "Runtime/Pvp/PvpBotArsenal.cs:chainshot",
            "Runtime/Pvp/PvpBotArsenal.cs:swivel_guns",
            "Runtime/Pvp/PvpBotArsenal.cs:harpoon_gun",
            "Runtime/Pvp/PvpBotArsenal.cs:long_nine",
        };

        /// <summary>
        /// Foundation files allowed to key data by content id. M15 listed 17 (V3's tiers, combat profiles, weapon and
        /// specialist catalogs, database fallbacks, unlock pools); M16 moved all of them onto config. Keep it empty.
        /// </summary>
        static readonly HashSet<string> NameKeyedFoundationFiles = new HashSet<string>
        {
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
