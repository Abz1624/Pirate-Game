using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
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
    /// <summary>M9 step 9.1: the build configs equal their V3 sources (the v3-parity preset restores V3 where D6 differs).</summary>
    [Category("V4")]
    public sealed class V4BuildM9Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";
        readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _created) if (o != null) Object.DestroyImmediate(o);
            _created.Clear();
            StageBalanceProfileContext.ResetForTests();
        }

        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        static ConfigSetRuntime Parity()
        {
            var runtime = new ConfigSetRuntime(Set);
            var errors = new List<string>();
            var preset = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            Assert.IsTrue(PresetCodec.Apply(runtime, PresetCodec.FromJson(preset), 0f, errors), string.Join("\n", errors));
            return runtime;
        }

        [Test]
        public void TheSetHoldsEveryBuildConfig_AndValidates()
        {
            var set = Set;
            Assert.AreEqual(1, set.configs.OfType<ProgressionConfig>().Count());
            Assert.AreEqual(1, set.configs.OfType<OfferWeightsConfig>().Count());
            Assert.AreEqual(10, set.configs.OfType<LogbookConfig>().Count());
            Assert.AreEqual(19, set.configs.OfType<ItemConfig>().Count());
            Assert.AreEqual(1, set.configs.OfType<PowerupConfig>().Count());
            Assert.AreEqual(6, set.configs.OfType<CaptainConfig>().Count());
            Assert.AreEqual(1, set.configs.OfType<UnlockConfig>().Count());
            Assert.AreEqual(1, set.configs.OfType<PickupConfig>().Count());
            var report = ConfigValidator.Validate(set);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void XpCurve_EqualsV3sStageCodec()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile);
            var export = StageBalanceProfileContext.Export;
            var p = Set.configs.OfType<ProgressionConfig>().Single();
            for (var level = 0; level <= 80; level++)
                Assert.AreEqual(RoguePirates.Simulation.Balance.StageBalanceProfileExportCodec.XpToNextLevel(export, level), ProgressionMath.XpToNext(p, level), "level " + level);
            Assert.AreEqual(profile.weaponSlotCap, p.weaponSlots);
            Assert.AreEqual(profile.logbookSlotCap, p.logbookSlots);
            CollectionAssert.AreEqual(profile.newWeaponOfferProtectionLevels, p.newWeaponProtectionLevels);
            CollectionAssert.AreEqual(GameConstants.RarityMultipliers, p.rarityMultipliers);
        }

        [Test]
        public void OfferWeights_EqualV3sSaltwindTable()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            StageBalanceProfileContext.BeginRun(profile); // Saltwind ("mixed") weights
            var method = typeof(HighSeas.UI.LevelOfferGenerator).GetMethod("GetFillStateWeights", BindingFlags.Static | BindingFlags.NonPublic);
            var weights = Set.configs.OfType<OfferWeightsConfig>().Single();
            for (var n = 0; n <= 8; n++)
            {
                var v3 = method.Invoke(null, new object[] { n });
                float F(string field) => (float)v3.GetType().GetField(field).GetValue(v3);
                var row = weights.RowFor(n);
                Assert.AreEqual(F("equippedWeaponUpgrade"), row.weaponUpgrade, $"{n} weapons");
                Assert.AreEqual(F("newWeapon"), row.newWeapon, $"{n} weapons");
                Assert.AreEqual(F("equippedLogbookUpgrade"), row.logbookUpgrade, $"{n} weapons");
                Assert.AreEqual(F("newLogbook"), row.newLogbook, $"{n} weapons");
                Assert.AreEqual(F("newRunItem"), row.runItem, $"{n} weapons");
            }
        }

        [Test]
        public void Logbooks_EqualV3sDatabase_UnderTheParityPreset_AndPointAtTheIntendedStatsByDefault()
        {
            var db = V3ConfigMigrator.LoadCleanDatabase();
            using var parity = Parity();
            foreach (var l in parity.All<LogbookConfig>())
            {
                var v3 = db.logbooks[l.databaseOrder];
                Assert.AreEqual(v3.id, l.logbookId);
                var v3Stat = (int)v3.primaryStat;
                Assert.AreEqual(v3Stat >= 0 && v3Stat <= 20 ? v3Stat : -1, l.stat, l.logbookId + " (V3's stat 22 did nothing: -1)");
                Assert.AreEqual(v3.perLevelAdditive, l.perLevelAdditive, l.logbookId);
                Assert.AreEqual(v3.perLevelMultiplicative, l.perLevelMultiplicative, l.logbookId);
                Assert.AreEqual(v3.maxLevel, l.maxLevel, l.logbookId);
                Assert.AreEqual(V3LogbookCatalog.IsSaltwindPoolId(v3.id), l.inSaltwindPool, l.logbookId);
            }
            var byId = Set.configs.OfType<LogbookConfig>().ToDictionary(l => l.logbookId);
            Assert.AreEqual((int)StatId.PickupRadius, byId["scavengers_net"].stat, "D6");
            Assert.AreEqual((int)StatId.XPGain, byId["cartographers_folio"].stat, "D6");
            Assert.AreEqual((int)StatId.GoldGain, byId["quartermasters_ledger"].stat, "D6");
            Assert.AreEqual((int)StatId.MoveSpeed, byId["navigators_charts"].stat, "D6");
            Assert.AreEqual(0.02f, byId["lookouts_glass"].perLevelAdditive, "R-34 fix (approved at the M9 gate)");
            using (var v3 = Parity())
                Assert.AreEqual(2f, v3.All<LogbookConfig>().Single(l => l.logbookId == "lookouts_glass").perLevelAdditive, "the v3-parity preset restores V3's +2.0");
        }

        [Test]
        public void Items_StatModifiers_EqualV3sApplyItemEffect_TwoStacksEach()
        {
            var host = new GameObject("run");
            _created.Add(host);
            var rm = host.AddComponent<RunManager>();
            typeof(RunManager).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(rm, null);
            var inv = host.AddComponent<V3RunItemInventory>();
            var apply = typeof(V3RunItemInventory).GetMethod("ApplyItemEffect", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var item in Set.configs.OfType<ItemConfig>())
            {
                rm.BeginRun(1);
                apply.Invoke(inv, new object[] { item.itemId });
                apply.Invoke(inv, new object[] { item.itemId });
                var mine = new PlayerStats();
                for (var s = 0; s < 2; s++)
                    foreach (var m in item.statModifiers) mine.AddModifier(new StatModifier((StatId)m.stat, m.additive, m.multiplicative));
                foreach (StatId stat in Enum.GetValues(typeof(StatId)))
                    Assert.AreEqual(rm.Stats.Get(stat), mine.Get(stat), $"{item.itemId} {stat}");
            }
        }

        [Test]
        public void Items_CatalogData_EqualsV3()
        {
            var items = Set.configs.OfType<ItemConfig>().ToDictionary(i => i.itemId);
            CollectionAssert.AreEquivalent(V3ItemCatalog.PlaytestItemOrder, items.Keys);
            for (var c = 0; c < V3ItemCatalog.ChestPool.Count; c++)
            {
                var (id, rarity) = V3ItemCatalog.ChestPool[c];
                Assert.IsTrue(items[id].inChestPool, id);
                Assert.AreEqual(c, items[id].chestPoolOrder, id);
                Assert.AreEqual((int)rarity, items[id].rarity, id);
            }
            foreach (var id in V3ItemCatalog.InteractionItemIds) Assert.IsTrue(items[id].inLevelUpPool, id);
            foreach (var i in items.Values)
            {
                Assert.AreEqual(V3ItemCatalog.GetDisplayName(i.itemId), i.displayName);
                Assert.AreEqual(V3ItemCatalog.GetDescription(i.itemId), i.description);
            }
            Assert.AreEqual(12f, items["captains_bell"].effects.Single().threshold, "Captain's Bell every 12 kills");
            Assert.AreEqual(0.88f, items["reinforced_keel"].effects.Single().value);
            foreach (var id in new[] { "black_powder_keg", "powder_fuse", "sea_witchs_eye", "leviathan_tooth", "stormglass", "cursed_compass" })
            {
                Assert.IsTrue(items[id].effects.Single().active, id + ": R-40 fix (approved at the M9 gate)");
                using var v3 = Parity();
                Assert.IsFalse(v3.All<ItemConfig>().Single(i => i.itemId == id).effects.Single().active, id + ": the v3-parity preset restores V3 (never read)");
            }
        }

        [Test]
        public void Captains_EqualV3sProfiles_WithTheAdoptedStartingWeapons()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            var captains = Set.configs.OfType<CaptainConfig>().ToDictionary(c => (V3CaptainId)c.legacyId);
            foreach (V3CaptainId id in Enum.GetValues(typeof(V3CaptainId)))
            {
                var v3 = V3CaptainProfile.Get(id);
                var c = captains[id];
                Assert.AreEqual(v3.BaseHull, c.baseHull, id.ToString());
                Assert.AreEqual(v3.BaseMoveSpeed, c.baseMoveSpeed, id.ToString());
                Assert.AreEqual(v3.StartingWeaponId, c.v3StartingWeaponId, id.ToString());
                Assert.AreEqual(V3CaptainCatalog.GetMenuLabel(id), c.displayName);
                Assert.AreEqual(Array.IndexOf(V3CaptainCatalog.MenuOrder, id), c.menuOrder);
                var unlock = profile.captainUnlocks.FirstOrDefault(u => u.contentId == c.captainKey);
                Assert.AreEqual(unlock != null ? unlock.marksCost : 0, c.rumCost, id.ToString());
            }
            Assert.AreEqual("deck_cannons", captains[V3CaptainId.Brine].startingWeaponId, "D6: Hank");
            Assert.AreEqual("lightning_mast", captains[V3CaptainId.Roxie].startingWeaponId, "D6: Roxie");
            Assert.AreEqual("flamethrower", captains[V3CaptainId.Ember].prerequisiteWeaponId, "D6: Ember needs the Flamethrower");
            Assert.AreEqual(100, captains[V3CaptainId.Brine].rumCost);
            Assert.AreEqual(140, captains[V3CaptainId.Flint].rumCost);
            Assert.AreEqual(20f, captains[V3CaptainId.Brine].active.cooldownSeconds, "Brace!");
            Assert.AreEqual(0.4f, captains[V3CaptainId.Brine].active.braceIncomingDamageMultiplier);
        }

        [Test]
        public void PowerupsPickupsAndUnlocks_EqualV3()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(V3ConfigMigrator.ProfilePath);
            var pacing = AssetDatabase.LoadAssetAtPath<DemoPacingSettings>(V3ConfigMigrator.PacingPath);
            var pw = Set.configs.OfType<PowerupConfig>().Single();
            Assert.AreEqual(profile.randomPowerupChancePerKill, pw.dropChancePerKill);
            Assert.AreEqual(profile.randomPowerupMinIntervalSeconds, pw.minIntervalSeconds);
            Assert.AreEqual(V3FloorPowerupWorld.MaxPerStage, pw.maxPerStage);
            Assert.AreEqual(V3FloorPowerupWorld.LifetimeSeconds, pw.lifetimeSeconds);
            Assert.AreEqual(V3FloorPowerupWorld.CollectRadius, pw.collectRadius);
            Assert.AreEqual(profile.powderRushDurationSeconds, pw.powderRushSeconds);
            Assert.AreEqual(profile.deadCalmSlowFraction, pw.deadCalmSlowFraction);
            Assert.IsFalse(pw.powderRushIsDamageInBattleSignalSlot, "R-43 fix: attack speed in its own slot (approved at the M9 gate)");
            Assert.IsTrue(pw.fullSailOwnSlot, "R-43 fix: Full Sail stacks with Tailwind");
            using (var v3 = Parity())
            {
                var v3pw = v3.Single<PowerupConfig>();
                Assert.IsTrue(v3pw.powderRushIsDamageInBattleSignalSlot, "the v3-parity preset restores V3's shared slot");
                Assert.IsFalse(v3pw.fullSailOwnSlot);
            }
            CollectionAssert.AreEqual(new[] { PowerupKind.RepairSupplies, PowerupKind.PowderRush, PowerupKind.MagneticTide, PowerupKind.DeadCalm }, pw.pool.Select(e => e.kind));
            Assert.AreEqual((int)V3FloorPowerupKind.DeadCalm, (int)PowerupKind.DeadCalm, "enum order mirrors V3");
            var pk = Set.configs.OfType<PickupConfig>().Single();
            Assert.AreEqual(pacing.healthDropChance, pk.healthDropChance);
            Assert.AreEqual(pacing.xpAutoPickupRadius, pk.xpAutoPickupRadius);
            Assert.AreEqual(pacing.barrelBurstVisualMax, pk.burstCountMax);
            var un = Set.configs.OfType<UnlockConfig>().Single();
            CollectionAssert.AreEqual(profile.starterWeaponIds, un.starterWeaponIds);
            Assert.AreEqual(StartingWeaponRule.CaptainWeapon, un.startingWeaponRule, "D6");
        }

        // ---- 9.2 Stat resolver -----------------------------------------------------------------------------------------

        [Test]
        public void StatResolver_GivesPlayerStatsNumbers_AndRemovesBySource()
        {
            var stats = new PlayerStats();
            var v3 = new PlayerStats();
            var resolver = new StatResolver(() => stats);
            var changed = new List<int>();
            resolver.Changed += changed.Add;

            // The same changes in the same order as V3 applies them directly.
            resolver.SetBase((int)StatId.Hull, 110f); v3.SetBase(StatId.Hull, 110f);
            resolver.Add("item:grapeshot_pouch", (int)StatId.Damage, 0f, 1.1f); v3.AddModifier(new StatModifier(StatId.Damage, 0f, 1.1f));
            resolver.Add("logbook:gunnery_manual", (int)StatId.Damage, 0.08f * 1.25f); v3.AddModifier(new StatModifier(StatId.Damage, 0.08f * 1.25f));
            resolver.Add("powerup:battle_signal", (int)StatId.Damage, 0f, 1.25f); v3.AddModifier(new StatModifier(StatId.Damage, 0f, 1.25f));
            resolver.Add("item:grapeshot_pouch", (int)StatId.Damage, 0f, 1.1f); v3.AddModifier(new StatModifier(StatId.Damage, 0f, 1.1f));
            foreach (StatId s in Enum.GetValues(typeof(StatId))) Assert.AreEqual(v3.Get(s), resolver.Get((int)s), s.ToString());
            Assert.AreEqual(2, resolver.CountFor("item:grapeshot_pouch"));

            resolver.RemoveSource("powerup:battle_signal"); v3.RemoveModifier(new StatModifier(StatId.Damage, 0f, 1.25f));
            Assert.AreEqual(v3.Get(StatId.Damage), resolver.Get((int)StatId.Damage), "only the powerup's change goes");
            Assert.AreEqual(0, resolver.CountFor("powerup:battle_signal"));
            Assert.AreEqual(2, resolver.CountFor("item:grapeshot_pouch"));

            resolver.Add("logbook:navigators_charts", 22, 0.03f); // V3's missing stat: ignored, as PlayerStats never matched it
            Assert.AreEqual(v3.Get(StatId.MoveSpeed), resolver.Get((int)StatId.MoveSpeed));
            CollectionAssert.Contains(changed, (int)StatId.Damage);
            CollectionAssert.Contains(changed, (int)StatId.Hull);
            CollectionAssert.DoesNotContain(changed, 22);
        }

        [Test]
        public void ParityPreset_RestoresV3ForEveryAdoptedDecision()
        {
            using var parity = Parity();
            Assert.AreEqual(StartingWeaponRule.V3AlwaysFirstConquestWeapon, parity.Single<UnlockConfig>().startingWeaponRule);
            Assert.IsFalse(parity.Single<ExperimentConfig>().captainActivesEnabled, "V3: actives off");
            Assert.IsTrue(Set.configs.OfType<ExperimentConfig>().Single().captainActivesEnabled, "D6: actives on");
        }
    }
}
