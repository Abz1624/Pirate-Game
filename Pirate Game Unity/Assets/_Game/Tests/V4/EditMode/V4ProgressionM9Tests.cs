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
using HighSeas.Weapons;
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
            public bool IsWeaponUnlocked(string weaponId) => V3ContentUnlocks.IsWeaponUnlocked(weaponId);
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

        static OfferGenerator Generator(ConfigSetRuntime set) => new OfferGenerator(
            () => set.Single<ProgressionConfig>(),
            () => set.Single<OfferWeightsConfig>(),
            () => OfferGenerator.InDatabaseOrder(set.All<WeaponConfig>()),
            () => OfferGenerator.InDatabaseOrder(set.All<LogbookConfig>()),
            () => set.All<WeaponEvolutionConfig>().ToList(),
            () => CombatExperimentContext.Active.weaponEvolutionsEnabled);

        /// <summary>The config set under the v3-parity preset (V3's Logbook stats and the other V3 values).</summary>
        static ConfigSetRuntime Parity()
        {
            var runtime = new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));
            var errors = new List<string>();
            var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            Assert.IsTrue(PresetCodec.Apply(runtime, PresetCodec.FromJson(preset), 0f, errors), string.Join("; ", errors));
            return runtime;
        }

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
            using var set = Parity();
            var gen = Generator(set);
            var db = RunDatabase();
            var weaponIds = db.weapons.Where(w => w != null).Select(w => w.id).ToArray();
            var logbookIds = db.logbooks.Where(l => l != null).Select(l => l.id).ToArray();
            var p = set.Single<ProgressionConfig>();
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
                var v3Items = itemsOwned ? new HashSet<string>(set.All<WeaponEvolutionConfig>().Select(e => e.requiredItemId)) : new HashSet<string>();
                if (scenario.Next(0, 5) == 0 && state.Weapons.Count > 0)
                {
                    var evo = set.All<WeaponEvolutionConfig>().FirstOrDefault(e => e.baseWeapon != null && e.baseWeapon.weaponId == state.Weapons[0]);
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

        // ---- The module (V3 LevelSystem) ------------------------------------------------------------------------------

        sealed class Run : IProgressionRun
        {
            public bool Exists => true;
            public bool IsRunEnded => false;
            public void SetPause(PauseReason reason) { }
            public void SetState(RunState state) { }
            public void EnterPlaying() { }
        }

        static T Awake<T>(GameObject host) where T : MonoBehaviour
        {
            var c = host.AddComponent<T>();
            typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(c, null);
            return c;
        }

        [Test]
        public void Module_EqualsV3sLevelSystem_OverXpGrantsAndCardChoices()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile);
            var settings = AssetDatabase.LoadAssetAtPath<LevelUpSettings>("Assets/_Game/Data/LevelUpSettings.asset");
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(V3ConfigMigrator.PacingPath);
            using var set = Parity();
            var db = RunDatabase();
            var experiment = CombatExperimentContext.Active;
            var inventory = Inventory();
            var starter = "swivel_guns";
            var choose = BindingFlags.Instance | BindingFlags.NonPublic;

            for (var seed = 1; seed <= 12; seed++)
            {
                // V3: RunManager, the switched-off weapon record, LevelSystem set up as V3RunBootstrap / LegacyRunShell do.
                var host = new GameObject("v3-run");
                _created.Add(host);
                var rm = Awake<RunManager>(host);
                rm.BeginRun(seed);
                inventory.ClearForNewRun();
                WeaponEvolutionRuntime.Reset();
                var recordHost = new GameObject("record");
                _created.Add(recordHost);
                var record = Awake<WeaponController>(recordHost);
                record.ApplyCombatExperiment(experiment);
                record.EnsureWeaponSlotCapacity(profile.weaponSlotCap);
                record.Initialize(db, starter);
                var levelHost = new GameObject("levels");
                _created.Add(levelHost);
                var levels = levelHost.AddComponent<LevelSystem>();
                typeof(LevelSystem).GetField("levelUpSettings", choose).SetValue(levels, settings);
                typeof(LevelSystem).GetField("demoPacing", choose).SetValue(levels, pacing);
                typeof(LevelSystem).GetMethod("Awake", choose).Invoke(levels, null);
                levels.ApplyStageBalanceProgression();
                levels.ConfigureV3();
                levels.ApplyCombatExperiment(experiment);
                levels.PrepareForNewRun(db, starter);
                levels.SyncWeaponControllerFromOwnedLoadout(record);

                // V4: the module on its own Loot stream and stats.
                var v4Stats = new PlayerStats();
                var resolver = new StatResolver(() => v4Stats);
                var v4Rng = new RunRng(seed);
                var module = new ProgressionModule(
                    () => set.Single<ProgressionConfig>(),
                    id => set.All<WeaponConfig>().FirstOrDefault(w => w.weaponId == id),
                    id => set.All<LogbookConfig>().FirstOrDefault(l => l.logbookId == id),
                    Generator(set), new Loot(v4Rng), new Ctx(), resolver, resolver, new Run(), null);
                module.BeginRun(starter, experiment.weaponSlotCap, pacing.startingDamageMultiplier);

                var scenario = new System.Random(seed * 31);
                for (var step = 0; step < 40; step++)
                {
                    var xp = scenario.Next(1, 30);
                    levels.AddXP(xp);
                    module.AddXp(xp);
                    var tag = $"seed {seed} step {step}";
                    while (levels.PendingLevelUps > 0)
                    {
                        var v3Offers = (List<LevelOffer>)typeof(LevelSystem).GetMethod("GenerateOffers", choose).Invoke(levels, null);
                        var v4Offers = module.GenerateOffers();
                        AssertSame(v3Offers, v4Offers, tag);
                        var action = scenario.Next(0, 10);
                        if (action == 0 && levels.RefreshesRemaining > 0)
                        {
                            typeof(LevelSystem).GetMethod("OnLevelUpRefresh", choose).Invoke(levels, null);
                            module.Refresh();
                            continue;
                        }
                        if (action == 1)
                        {
                            typeof(LevelSystem).GetMethod("OnLevelUpSkipped", choose).Invoke(levels, null);
                            module.Skip();
                            continue;
                        }
                        var pick = scenario.Next(0, v3Offers.Count);
                        typeof(LevelSystem).GetMethod("OnOfferSelected", choose).Invoke(levels, new object[] { v3Offers[pick] });
                        module.Select(v4Offers[pick]);
                    }

                    Assert.AreEqual(levels.Level, module.Level, tag + " level");
                    Assert.AreEqual(levels.CurrentXP, module.CurrentXp, tag + " xp");
                    Assert.AreEqual(levels.XPToNext, (float)module.XpToNext, tag + " xp to next");
                    Assert.AreEqual(levels.PendingLevelUps, module.PendingLevelUps, tag + " pending");
                    Assert.AreEqual(levels.RefreshesRemaining, module.RefreshesRemaining, tag + " refreshes");
                    CollectionAssert.AreEqual(levels.OwnedWeapons, module.OwnedWeapons, tag + " weapons");
                    CollectionAssert.AreEqual(levels.OwnedLogbooks, module.OwnedLogbooks, tag + " logbooks");
                    foreach (var id in levels.OwnedWeapons) Assert.AreEqual(levels.GetWeaponLevel(id), module.WeaponLevel(id), tag + " level of " + id);
                    foreach (var id in levels.OwnedLogbooks) Assert.AreEqual(levels.GetLogbookLevel(id), module.LogbookLevel(id), tag + " rank of " + id);
                    Assert.AreEqual(record.Weapons.Count, module.Loadout.Count, tag + " loadout size");
                    for (var i = 0; i < record.Weapons.Count; i++)
                    {
                        var a = record.Weapons[i];
                        var b = module.Loadout.Get(i);
                        var t = $"{tag} slot {i} ({a.def.id})";
                        Assert.AreEqual(a.def.id, b.WeaponId, t);
                        Assert.AreEqual(a.level, b.Level, t + " level");
                        Assert.AreEqual(a.damageBonus, b.DamageBonus, t + " damage");
                        Assert.AreEqual(a.quantityBonus, b.QuantityBonus, t + " quantity");
                        Assert.AreEqual(a.cooldownMult, b.CooldownMult, t + " cooldown");
                        Assert.AreEqual(a.speedMult, b.SpeedMult, t + " speed");
                        Assert.AreEqual(a.sizeMult, b.SizeMult, t + " size");
                        Assert.AreEqual(a.durationMult, b.DurationMult, t + " duration");
                        Assert.AreEqual(a.knockbackMult, b.KnockbackMult, t + " knockback");
                        WeaponEvolutionRuntime.TryGetEvolutionId(a.def.id, out var evo);
                        Assert.AreEqual(evo, b.EvolutionId, t + " evolution");
                    }
                    foreach (StatId s in Enum.GetValues(typeof(StatId))) Assert.AreEqual(rm.Stats.Get(s), v4Stats.Get(s), $"{tag} {s}");
                }
                Assert.AreEqual(rm.Rng.LootFloat(), v4Rng.LootFloat(), $"seed {seed}: same Loot draws");
                Object.DestroyImmediate(levelHost);
                Object.DestroyImmediate(recordHost);
                Object.DestroyImmediate(host);
            }
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
