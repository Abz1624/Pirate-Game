using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using HighSeas.V3;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Tests
{
    /// <summary>M9 step 9.6: the V4 offer generator deals V3's level-up cards (lockstep on the same Loot stream).</summary>
    [Category("V4")]
    public sealed class V4ProgressionM9Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            StageBalanceProfileContext.ResetForTests();
            WeaponEvolutionRuntime.Reset();
        }

        sealed class Loot : IOfferRandom
        {
            readonly RunRng _rng;
            public Loot(RunRng rng) { _rng = rng; }
            public float LootFloat() => _rng.LootFloat();
        }

        sealed class Ctx : IOfferContext
        {
            public Func<string, bool> Items = _ => false;
            public bool IsWeaponOfferable(string weaponId) => WeaponFeatureFlags.IsWeaponEnabled(weaponId);
            public bool HasItem(string itemId) => Items(itemId);
            public bool IsEvolved(string weaponId) => WeaponEvolutionRuntime.IsEvolved(weaponId);
        }

        sealed class State : IOfferState
        {
            public List<string> Weapons = new List<string>(), Logbooks = new List<string>();
            public Dictionary<string, int> WeaponLevels = new Dictionary<string, int>(), LogbookLevels = new Dictionary<string, int>();
            public IReadOnlyList<string> OwnedWeapons => Weapons;
            public IReadOnlyList<string> OwnedLogbooks => Logbooks;
            public int WeaponLevel(string id) => WeaponLevels.TryGetValue(id, out var l) ? l : 0;
            public int LogbookLevel(string id) => LogbookLevels.TryGetValue(id, out var l) ? l : 0;
        }

        GameDatabase RunDatabase()
        {
            var db = V3ConfigMigrator.LoadCleanDatabase();
            var clone = Object.Instantiate(db);
            _created.Add(clone);
            clone.weapons = db.weapons.Select(w => { if (w == null) return null; var c = Object.Instantiate(w); c.name = w.name; _created.Add(c); return c; }).ToArray();
            CombatWeaponCatalog.ApplyToDatabase(clone, CombatExperimentContext.Active);
            return clone;
        }

        static OfferGenerator Generator(ConfigSet set) => new OfferGenerator(
            () => set.configs.OfType<ProgressionConfig>().Single(),
            () => set.configs.OfType<OfferWeightsConfig>().Single(),
            () => OfferGenerator.InDatabaseOrder(set.configs.OfType<WeaponConfig>()),
            () => OfferGenerator.InDatabaseOrder(set.configs.OfType<LogbookConfig>()),
            () => set.configs.OfType<WeaponEvolutionConfig>().ToList(),
            () => CombatExperimentContext.Active.weaponEvolutionsEnabled);

        static void AssertSame(List<LevelOffer> v3, List<Offer> v4, string tag)
        {
            Assert.AreEqual(v3.Count, v4.Count, tag + " card count");
            for (var i = 0; i < v3.Count; i++)
            {
                var a = v3[i];
                var b = v4[i];
                var t = $"{tag} card {i} ({a.kind} {a.id})";
                Assert.AreEqual((int)a.kind, (int)b.Kind, t + " kind");
                Assert.AreEqual(a.id, b.Id, t + " id");
                Assert.AreEqual(a.evolutionId, b.EvolutionId, t + " evolution");
                Assert.AreEqual((int)a.rarity, b.Rarity, t + " rarity");
                Assert.AreEqual(a.currentLevel, b.CurrentLevel, t + " current level");
                Assert.AreEqual(a.nextLevel, b.NextLevel, t + " next level");
                var la = a.upgradeLines ?? Array.Empty<WeaponUpgradeLine>();
                var lb = b.Lines ?? Array.Empty<UpgradeLine>();
                Assert.AreEqual(la.Length, lb.Length, t + " lines");
                for (var k = 0; k < la.Length; k++)
                {
                    Assert.AreEqual((int)la[k].axis, (int)lb[k].Axis, t + $" line {k} axis");
                    Assert.AreEqual(la[k].value, lb[k].Value, t + $" line {k} value");
                }
            }
        }

        [Test]
        public void Offers_EqualV3s_OnTheSameLootStream_ForManyBuilds()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile); // Saltwind: fill-state weights, the Saltwind first level-up and Logbook pool
            var settings = AssetDatabase.LoadAssetAtPath<LevelUpSettings>("Assets/_Game/Data/LevelUpSettings.asset");
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(V3ConfigMigrator.PacingPath);
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            var gen = Generator(set);
            var db = RunDatabase();
            var weaponIds = db.weapons.Where(w => w != null).Select(w => w.id).ToArray();
            var logbookIds = db.logbooks.Where(l => l != null).Select(l => l.id).ToArray();
            var p = set.configs.OfType<ProgressionConfig>().Single();
            var scenario = new System.Random(4242);
            var kinds = new HashSet<LevelOfferKind>();
            var inventory = Inventory();
            var items = (List<string>)typeof(V3RunItemInventory).GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(inventory);

            for (var seed = 1; seed <= 600; seed++)
            {
                var state = new State();
                var weaponCount = scenario.Next(1, 7);
                foreach (var id in weaponIds.OrderBy(_ => scenario.Next()).Take(weaponCount))
                {
                    state.Weapons.Add(id);
                    state.WeaponLevels[id] = scenario.Next(0, 5) == 0 ? 12 : scenario.Next(1, 12);
                }
                var logbookCount = scenario.Next(0, 5);
                foreach (var id in logbookIds.OrderBy(_ => scenario.Next()).Take(logbookCount))
                {
                    state.Logbooks.Add(id);
                    state.LogbookLevels[id] = scenario.Next(0, 4) == 0 ? db.GetLogbook(id).maxLevel : scenario.Next(1, 8);
                }
                if (seed % 25 == 0)
                {
                    // Everything capped: only fallback cards (or Continue) are left.
                    state = new State();
                    foreach (var id in weaponIds.Take(p.weaponSlots)) { state.Weapons.Add(id); state.WeaponLevels[id] = db.GetWeapon(id).maxLevel; }
                    foreach (var id in logbookIds.Take(p.logbookSlots)) { state.Logbooks.Add(id); state.LogbookLevels[id] = db.GetLogbook(id).maxLevel; }
                }
                var isFirst = seed % 9 == 0 && seed % 25 != 0;
                var luck = (float)scenario.NextDouble() * 0.6f;
                var level = scenario.Next(1, 25);
                var hull = scenario.Next(0, 3) == 0 ? 100f : (float)scenario.NextDouble() * 100f;
                var stats = new PlayerStats();
                if (scenario.Next(0, 6) == 0) stats.AddModifier(new StatModifier(StatId.Damage, 10f)); // damage fallback capped
                var itemsOwned = scenario.Next(0, 2) == 0 && seed % 25 != 0;

                // V3's evolution check reads the item inventory and the evolved set.
                WeaponEvolutionRuntime.Reset();
                var v3Items = itemsOwned ? new HashSet<string>(set.configs.OfType<WeaponEvolutionConfig>().Select(e => e.requiredItemId)) : new HashSet<string>();
                if (scenario.Next(0, 5) == 0 && state.Weapons.Count > 0)
                {
                    var evo = set.configs.OfType<WeaponEvolutionConfig>().FirstOrDefault(e => e.baseWeapon != null && e.baseWeapon.weaponId == state.Weapons[0]);
                    if (evo != null) WeaponEvolutionRuntime.Apply(state.Weapons[0], evo.evolutionId);
                }
                inventory.ClearForNewRun();
                items.AddRange(v3Items);

                var v3Rng = new RunRng(seed);
                var v4Rng = new RunRng(seed);
                var v3 = LevelOfferGenerator.Generate(db, settings, pacing, v3Rng, luck, state.Weapons, state.WeaponLevels, state.Logbooks, state.LogbookLevels,
                    p.weaponSlots, p.logbookSlots, isFirst, stats, hull, 100f, level, useFillStateWeighting: true);
                var v4 = gen.Generate(new Loot(v4Rng), new Ctx { Items = v3Items.Contains }, state, luck, isFirst,
                    new OfferPlayer(hull, 100f, stats.GlobalDamage, stats.MoveSpeed), level);
                AssertSame(v3, v4, $"seed {seed}");
                Assert.AreEqual(v3Rng.LootFloat(), v4Rng.LootFloat(), $"seed {seed}: same number of Loot draws");
                foreach (var o in v3) kinds.Add(o.kind);
            }
            // The scenarios reach every Saltwind card kind.
            foreach (var k in new[] { LevelOfferKind.WeaponUpgrade, LevelOfferKind.NewWeapon, LevelOfferKind.Logbook, LevelOfferKind.WeaponEvolution, LevelOfferKind.FallbackHeal })
                CollectionAssert.Contains(kinds, k, k + " covered");
        }

        /// <summary>V3's evolution check asks V3RunItemInventory.Instance.HasItem: a live inventory (items set per scenario).</summary>
        V3RunItemInventory Inventory()
        {
            var inv = V3RunItemInventory.Instance != null ? V3RunItemInventory.Instance : V3RunItemInventory.Ensure();
            _created.Add(inv.gameObject);
            return inv;
        }

        [Test]
        public void WeaponOfferData_EqualsV3sRunDatabase()
        {
            var db = RunDatabase();
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            foreach (var w in set.configs.OfType<WeaponConfig>())
            {
                var def = db.weapons[w.databaseOrder];
                Assert.AreEqual(def.id, w.weaponId, "database order");
                Assert.AreEqual(def.maxLevel, w.maxLevel, w.weaponId);
                var v3 = def.GetUpgradeConfigs();
                Assert.AreEqual(v3.Count, w.upgradeAxes.Length, w.weaponId + " axes (V3's pattern defaults where the asset has none)");
                for (var i = 0; i < v3.Count; i++)
                {
                    Assert.AreEqual((int)v3[i].axis, (int)w.upgradeAxes[i].axis);
                    Assert.AreEqual(v3[i].baseIncrement, w.upgradeAxes[i].baseIncrement);
                    Assert.AreEqual(v3[i].offerWeight, w.upgradeAxes[i].offerWeight);
                }
            }
        }
    }
}
