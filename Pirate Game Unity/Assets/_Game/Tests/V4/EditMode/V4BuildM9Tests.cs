using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Pickups;
using RoguePirates.V4.Foundation.Progression;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>M9 step 9.1: the build configs hold the adopted (D6) values. V5 M8z (7 Oct, I8z.5): the halves that checked
    /// the v3-parity preset restoring V3 were retired with the preset (D18).</summary>
    [Category("V4")]
    public sealed class V4BuildM9Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        static ConfigSet Set => AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);

        [Test]
        public void TheSetHoldsEveryBuildConfig_AndValidates()
        {
            var set = Set;
            Assert.AreEqual(1, set.configs.OfType<ProgressionConfig>().Count());
            Assert.AreEqual(1, set.configs.OfType<OfferWeightsConfig>().Count());
            Assert.AreEqual(15, set.configs.OfType<LogbookConfig>().Count(), "V3's ten and M5's five");
            Assert.AreEqual(47, set.configs.OfType<ItemConfig>().Count(), "V3's 19, V5 M4's eight, M4b's five, M5's fifteen");
            Assert.AreEqual(1, set.configs.OfType<PowerupConfig>().Count());
            Assert.AreEqual(6, set.configs.OfType<CaptainConfig>().Count());
            Assert.AreEqual(1, set.configs.OfType<UnlockConfig>().Count());
            Assert.AreEqual(1, set.configs.OfType<PickupConfig>().Count());
            var report = ConfigValidator.Validate(set);
            Assert.IsTrue(report.IsValid, report.ToString());
        }

        [Test]
        public void Logbooks_PointAtTheIntendedStatsByDefault()
        {
            var byId = Set.configs.OfType<LogbookConfig>().ToDictionary(l => l.logbookId);
            Assert.AreEqual((int)StatId.PickupRadius, byId["scavengers_net"].stat, "D6");
            Assert.AreEqual((int)StatId.XPGain, byId["cartographers_folio"].stat, "D6");
            Assert.AreEqual((int)StatId.GoldGain, byId["quartermasters_ledger"].stat, "D6");
            Assert.AreEqual((int)StatId.MoveSpeed, byId["navigators_charts"].stat, "D6");
            // V5 M8z (7 Oct, I8z.5): the playtest retuned the value; R-34's rule is a fraction per level, not V3's +2.0.
            Assert.That(byId["lookouts_glass"].perLevelAdditive, Is.GreaterThan(0f).And.LessThan(1f), "R-34 fix (approved at the M9 gate)");
        }

        [Test]
        public void Items_HoldTheAdoptedEffects()
        {
            var items = Set.configs.OfType<ItemConfig>().ToDictionary(i => i.itemId);
            Assert.AreEqual(12f, items["captains_bell"].effects.Single().threshold, "Captain's Bell every 12 kills");
            Assert.AreEqual(0.88f, items["reinforced_keel"].effects.Single().value);
            foreach (var id in new[] { "black_powder_keg", "powder_fuse", "sea_witchs_eye", "leviathan_tooth", "stormglass", "cursed_compass" })
            {
                Assert.IsTrue(items[id].effects.Single().active, id + ": R-40 fix (approved at the M9 gate)");
            }
        }

        [Test]
        public void Captains_HoldTheAdoptedStartingWeapons()
        {
            var captains = Set.configs.OfType<CaptainConfig>().ToDictionary(c => (CaptainId)c.legacyId);
            Assert.AreEqual("deck_cannons", captains[CaptainId.Brine].startingWeaponId, "D6: Hank");
            Assert.AreEqual("lightning_mast", captains[CaptainId.Roxie].startingWeaponId, "D6: Roxie");
            Assert.AreEqual("flamethrower", captains[CaptainId.Ember].prerequisiteWeaponId, "D6: Ember needs the Flamethrower");
            Assert.AreEqual(100, captains[CaptainId.Brine].rumCost);
            Assert.AreEqual(140, captains[CaptainId.Flint].rumCost);
            Assert.AreEqual(20f, captains[CaptainId.Brine].active.cooldownSeconds, "Brace!");
            Assert.AreEqual(0.4f, captains[CaptainId.Brine].active.braceIncomingDamageMultiplier);
        }

        [Test]
        public void PowerupsAndUnlocks_HoldTheAdoptedRules()
        {
            var pw = Set.configs.OfType<PowerupConfig>().Single();
            Assert.IsFalse(pw.powderRushIsDamageInBattleSignalSlot, "R-43 fix: attack speed in its own slot (approved at the M9 gate)");
            Assert.IsTrue(pw.fullSailOwnSlot, "R-43 fix: Full Sail stacks with Tailwind");
            // V5 M5 (D34): the four designed kinds wait for a rebalance.
            CollectionAssert.AreEqual(new[] { PowerupKind.RepairSupplies, PowerupKind.PowderRush, PowerupKind.MagneticTide, PowerupKind.DeadCalm }, pw.pool.Select(e => e.kind));
            Assert.AreEqual((int)FloorPowerupKind.DeadCalm, (int)PowerupKind.DeadCalm, "enum order mirrors V3");
            var un = Set.configs.OfType<UnlockConfig>().Single();
            Assert.AreEqual(StartingWeaponRule.CaptainWeapon, un.startingWeaponRule, "D6");
            Assert.IsTrue(Set.configs.OfType<ExperimentConfig>().Single().captainActivesEnabled, "D6: actives on");
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
    }
}
