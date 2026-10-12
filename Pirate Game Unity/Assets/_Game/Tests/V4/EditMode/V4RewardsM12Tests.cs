using System.Collections.Generic;
using System.IO;
using System.Linq;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Editor;
using RoguePirates.V4.Events;
using RoguePirates.V4.Ports;
using RoguePirates.V4.Rewards;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>M12: Rum rows, settlement once per run id, unlocks and the results text.</summary>
    [Category("V4")]
    public sealed class V4RewardsM12Tests
    {
        const string SetPath = "Assets/_Game/V4/Data/ConfigSets/SaltwindDefault.asset";

        static ConfigSetRuntime Default() => new ConfigSetRuntime(AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath));

        sealed class Bank : IRumBank
        {
            public readonly HashSet<string> Receipts = new HashSet<string>();
            public int Treasury { get; set; }
            public bool TrySettle(string runId, int rum)
            {
                if (string.IsNullOrEmpty(runId) || !Receipts.Add(runId)) return false;
                Treasury += rum;
                return true;
            }
        }

        sealed class Rig
        {
            public CombatEvents Events = new CombatEvents();
            public Bank Bank = new Bank();
            public RewardModule Rum = new RewardModule();
            public float Time;
            int _tick;
            public void Publish(CombatEventKind kind, int a = 0, int b = 0, float z = 0f, string tag = null) =>
                Events.Publish(new CombatEvent(kind, _tick, Time, a, b, 0f, 0f, z, tag));
            public void Kill(float t, int unit = 0) { Time = t; Publish(CombatEventKind.Killed, unit, 0, t); }
            public void Flush() { Events.EndTick(); _tick++; }
        }

        static Rig Make(ConfigSetRuntime set)
        {
            var r = new Rig();
            var context = new RunContext(new RunClock(), r.Events, set);
            context.Register<IRumBank>(r.Bank);
            r.Rum.RunTime = () => r.Time;
            r.Rum.Initialize(context);
            r.Rum.BeginRun(new RunStart(1, false));
            return r;
        }

        /// <summary>The stage deadline overtime starts from (V5 M8z, 7 Oct, I8z.5: the timeline's, not V3's 10:00).</summary>
        static float Deadline(ConfigSetRuntime set) => set.Single<WaveTimelineConfig>().stageDeadlineSeconds;

        /// <summary>The overtime row's Rum for so many minutes (maxRum 0 = uncapped, D18 P2).</summary>
        static int OvertimeRum(RewardConfig c, int minutes)
        {
            var row = c.Row("overtime");
            var rum = minutes * row.rum;
            return row.maxRum > 0 ? Mathf.Min(rum, row.maxRum) : rum;
        }

        /// <summary>V3's approved fixture: 5 qualifying minutes, 2 special landmarks, 2 scheduled elites, the boss, 2 overtime minutes.</summary>
        static void PlayFixture(Rig r, float deadline)
        {
            for (var m = 0; m < 5; m++) for (var k = 0; k < 10; k++) r.Kill(m * 60f + 1f + k);
            r.Kill(5 * 60f + 1f); // minute 5: one kill, does not qualify
            r.Publish(CombatEventKind.LandmarkUsed, 3);
            r.Publish(CombatEventKind.LandmarkUsed, 7);
            r.Publish(CombatEventKind.LandmarkUsed, 7); // the same landmark again: once
            r.Publish(CombatEventKind.PhaseChanged, 101, 1, 0f, "elite_hammerhead");
            r.Publish(CombatEventKind.PhaseChanged, 102, 1, 0f, "elite_kraken");
            r.Flush();
            r.Kill(400f, 101);
            r.Kill(410f, 102);
            r.Publish(CombatEventKind.BossState, 2);
            r.Flush();
            for (var m = 0; m < 3; m++) for (var k = 0; k < 10; k++) r.Kill(deadline + 1f + m * 60f + k); // overtime minutes 0, 1, 2
            r.Flush();
            r.Time = deadline + 2 * 60f + 5f; // overtime minute 2 in progress: minutes 0 and 1 are paid
        }

        [Test]
        public void DefaultRows_AreTheRumEconomy()
        {
            using var set = Default();
            var c = set.Single<RewardConfig>();
            // V3's Saltwind Marks economy (its stage profile): 2 per minute of 10+ kills, 3 per special POI, 5 per
            // scheduled elite, 25 for the boss, 3 per overtime minute. V5 M8z (7 Oct, I8z.5): overtime Rum is uncapped
            // since the playtest (D18 P2, a recorded exception to D4).
            Assert.AreEqual(2, c.Row("minutes").rum);
            Assert.AreEqual(10, c.Row("minutes").minKills);
            Assert.AreEqual(3, c.Row("landmarks").rum);
            Assert.AreEqual(5, c.Row("elites").rum);
            Assert.AreEqual(2, c.Row("elites").maxCredits);
            Assert.AreEqual(25, c.Row("boss").rum);
            Assert.AreEqual(3, c.Row("overtime").rum);
            Assert.AreEqual(0, c.Row("overtime").maxRum, "D18 P2: no overtime cap");
            Assert.IsFalse(c.Row("chests").enabled, "the chest row is an extra, off by default");
            Assert.AreEqual("Rum", c.currencyName);
        }

        [Test]
        public void Fixture_PaysEachRowItsRum()
        {
            using var set = Default();
            var c = set.Single<RewardConfig>();
            var r = Make(set);
            PlayFixture(r, Deadline(set));
            var total = r.Rum.Settle("run-1", true);
            // V3's approved fixture breakdown (57 Rum at V3's values): 5 minutes, 2 POIs, 2 elites, the boss, 2 overtime minutes.
            var expected = 5 * c.Row("minutes").rum + 2 * c.Row("landmarks").rum + 2 * c.Row("elites").rum + c.Row("boss").rum + OvertimeRum(c, 2);
            Assert.AreEqual(expected, total);
            Assert.AreEqual(5 * c.Row("minutes").rum, r.Rum.SettledRumOf(RumSource.QualifyingMinute));
            Assert.AreEqual(2 * c.Row("landmarks").rum, r.Rum.SettledRumOf(RumSource.SpecialLandmark));
            Assert.AreEqual(2 * c.Row("elites").rum, r.Rum.SettledRumOf(RumSource.ScheduledElite));
            Assert.AreEqual(c.Row("boss").rum, r.Rum.SettledRumOf(RumSource.BossVictory));
            Assert.AreEqual(OvertimeRum(c, 2), r.Rum.SettledRumOf(RumSource.OvertimeMinute));
            Assert.IsTrue(r.Rum.SettlementApplied);
            Assert.AreEqual(expected, r.Bank.Treasury);
        }

        [Test]
        public void SettlesOncePerRunId_AndAFailedRunStillPaysWhatItEarned()
        {
            using var set = Default();
            var r = Make(set);
            for (var k = 0; k < 12; k++) r.Kill(10f + k);
            r.Flush();
            r.Time = 70f; // died in minute 1: minute 0 is closed
            Assert.AreEqual(2, r.Rum.Settle("run-A", true), "no boss, no objective: the minute still pays");
            Assert.AreEqual(2, r.Bank.Treasury);
            Assert.AreEqual(2, r.Rum.Settle("run-A", true), "a second death callback returns the first result");
            Assert.AreEqual(2, r.Bank.Treasury, "and pays nothing");

            var again = Make(set); // a new module, same run id (e.g. a duplicate settle path): the save's receipt blocks it
            again.Bank = r.Bank;
            var context = new RunContext(new RunClock(), again.Events, set);
            context.Register<IRumBank>(r.Bank);
            again.Rum = new RewardModule { RunTime = () => 70f };
            again.Rum.Initialize(context);
            again.Rum.BeginRun(new RunStart(1, false));
            again.Rum.Settle("run-A", true);
            Assert.IsFalse(again.Rum.SettlementApplied);
            Assert.AreEqual(2, r.Bank.Treasury);
        }

        [Test]
        public void MinuteRules_MatchV3()
        {
            using var set = Default();
            var d = Deadline(set); // V5 M8z (7 Oct, I8z.5): read from the timeline
            var r = Make(set);
            for (var k = 0; k < 10; k++) r.Kill(61f + k); // minute 1: 10 kills
            for (var k = 0; k < 9; k++) r.Kill(121f + k); // minute 2: 9 kills
            for (var k = 0; k < 15; k++) r.Kill(185f + k); // minute 3 (in progress at settlement)
            for (var k = 0; k < 10; k++) r.Kill(d - 5f + k * 0.4f); // the last pre-deadline minute
            r.Flush();
            r.Time = 199f;
            var live = r.Rum.Live.First(x => x.Source == RumSource.QualifyingMinute);
            Assert.AreEqual(1, live.Credits, "minute 1 only: minute 2 has 9 kills, minute 3 is still running");

            // The console jump closes skipped minutes with no kills; overtime kills never count as ordinary minutes.
            r.Time = d + 5f;
            live = r.Rum.Live.First(x => x.Source == RumSource.QualifyingMinute);
            Assert.AreEqual(3, live.Credits, "minutes 1, 3 and the last before the deadline");
            for (var k = 0; k < 20; k++) r.Kill(d + 5f + k);
            r.Flush();
            r.Time = d + 59f;
            Assert.AreEqual(0, r.Rum.Live.First(x => x.Source == RumSource.OvertimeMinute).Credits, "overtime minute 0 is paid only once minute 1 begins");
            r.Time = d + 61f;
            Assert.AreEqual(1, r.Rum.Live.First(x => x.Source == RumSource.OvertimeMinute).Credits);
        }

        [Test]
        public void Caps_EliteTwo_BossOnce_OvertimeByItsRow_AndUnregisteredElitesNeverPay()
        {
            using var set = Default();
            var d = Deadline(set); // V5 M8z (7 Oct, I8z.5): overtime starts at the timeline's deadline
            var r = Make(set);
            r.Publish(CombatEventKind.PhaseChanged, 5, 1, 0f, "elite_hammerhead");
            r.Publish(CombatEventKind.PhaseChanged, 6, 1, 0f, "elite_kraken");
            r.Publish(CombatEventKind.PhaseChanged, 7, 1, 0f, "reef_crab"); // not a scheduled elite (no index)
            r.Flush();
            r.Kill(200f, 7);
            r.Kill(201f, 99); // never registered
            r.Kill(202f, 5);
            r.Kill(203f, 5); // the same unit id again (reused): already paid
            r.Kill(204f, 6);
            r.Publish(CombatEventKind.BossState, 1); // summoned: not a victory
            r.Publish(CombatEventKind.BossState, 2);
            r.Publish(CombatEventKind.BossState, 2);
            for (var m = 0; m < 8; m++) for (var k = 0; k < 10; k++) r.Kill(d + 1f + m * 60f + k);
            r.Flush();
            r.Time = d + 8 * 60f + 1f;
            r.Rum.Settle("caps", true);
            var c = set.Single<RewardConfig>();
            Assert.AreEqual(c.Row("elites").maxCredits * c.Row("elites").rum, r.Rum.SettledRumOf(RumSource.ScheduledElite), "two elites");
            Assert.AreEqual(c.Row("boss").rum, r.Rum.SettledRumOf(RumSource.BossVictory), "once");
            Assert.AreEqual(OvertimeRum(c, 8), r.Rum.SettledRumOf(RumSource.OvertimeMinute), "8 qualifying overtime minutes, capped only when the row has a cap");
            c.Row("overtime").maxRum = 15; // a cap still caps
            var capped = Make(set);
            for (var m = 0; m < 8; m++) for (var k = 0; k < 10; k++) capped.Kill(d + 1f + m * 60f + k);
            capped.Flush();
            capped.Time = d + 8 * 60f + 1f;
            capped.Rum.Settle("capped", true);
            Assert.AreEqual(Mathf.Min(15, 8 * c.Row("overtime").rum), capped.Rum.SettledRumOf(RumSource.OvertimeMinute), "maxRum caps the row");
        }

        [Test]
        public void Settlement_ReadsEventsNotYetDelivered_AndTheChestRowShowsWhenOn()
        {
            using var set = Default();
            set.Single<RewardConfig>().Row("chests").enabled = true;
            var r = Make(set);
            r.Publish(CombatEventKind.ChestOpened);
            r.Publish(CombatEventKind.ChestOpened);
            r.Flush();
            r.Publish(CombatEventKind.BossState, 2); // the boss secured in the dying frame, not delivered yet
            r.Time = 30f;
            var total = r.Rum.Settle("pending", true);
            Assert.AreEqual(27, total, "boss 25 + 2 chests");
            Assert.AreEqual(2, r.Rum.SettledRows.First(x => x.RowId == "chests").Rum);
            r.Flush(); // delivered after settlement: the settled breakdown does not change
            Assert.AreEqual(27, r.Rum.SettledTotal);
            var text = RunResultsView.Format(r.Rum.SettledRows, r.Rum.SettledTotal, r.Bank.Treasury, "Rum");
            StringAssert.Contains("RUM EARNED  +27", text, "just the total (playtest round 1)");
            StringAssert.DoesNotContain("Chests opened", text, "no per-row breakdown on the results screen");
            StringAssert.Contains("Treasury: 27 Rum", text);
            StringAssert.DoesNotContain("Marks", text);
        }

        /// <summary>Playtest round 1: the gold left at death is banked as Rum (on a flattening curve); the row can be switched off.</summary>
        [Test]
        public void GoldLeftAtDeath_IsBankedAsRum()
        {
            using var set = Default();
            var r = Make(set);
            r.Rum.RunGold = () => 500;
            r.Time = 30f;
            r.Rum.Settle("gold", true);
            Assert.AreEqual(12, r.Rum.SettledRows.First(x => x.RowId == "gold").Rum, "500 gold → 60 × (1 − ½^(500/1500)) = 12 Rum");
            StringAssert.Contains("RUM EARNED  +12", RunResultsView.Format(r.Rum.SettledRows, r.Rum.SettledTotal, r.Bank.Treasury, "Rum"));

            // Playtest round 2: the curve flattens, so overtime-farmed gold cannot break the economy.
            var row = set.Single<RewardConfig>().Row("gold");
            // Round 3: scaled to effort — early gold pays about like active minutes (~1 Rum per 40 gold), ~1,500 gold
            // (15-25 minutes of strong play) about a boss victory, and a marathon at most a good run's other rows.
            Assert.AreEqual(60, row.maxRum);
            Assert.AreEqual(1500f, row.goldForHalf);
            Assert.AreEqual(0, RewardModule.GoldRum(0, 60, 1500f));
            Assert.AreEqual(2, RewardModule.GoldRum(100, 60, 1500f));
            Assert.AreEqual(17, RewardModule.GoldRum(765, 60, 1500f));
            Assert.AreEqual(30, RewardModule.GoldRum(1500, 60, 1500f), "goldForHalf pays half the cap");
            Assert.AreEqual(45, RewardModule.GoldRum(3000, 60, 1500f));
            Assert.AreEqual(59, RewardModule.GoldRum(10000, 60, 1500f));
            Assert.LessOrEqual(RewardModule.GoldRum(1000000, 60, 1500f), 60, "never more than the cap");
            var previousGain = 60;
            for (var g = 0; g < 12000; g += 1500)
            {
                Assert.LessOrEqual(RewardModule.GoldRum(g, 60, 1500f), RewardModule.GoldRum(g + 1500, 60, 1500f), "more gold never pays less");
                var gain = RewardModule.GoldRum(g + 1500, 60, 1500f) - RewardModule.GoldRum(g, 60, 1500f);
                Assert.LessOrEqual(gain, previousGain + 1, "each further 1,500 gold pays less (flattens)");
                previousGain = gain;
            }
            // V5 M8z (7 Oct, I8z.5): the switch-off check under the v3-parity preset was retired with the preset (D18).
            row.enabled = false;
            var off = Make(set);
            off.Rum.RunGold = () => 500;
            off.Time = 30f;
            off.Rum.Settle("gold-off", true);
            Assert.AreEqual(0, off.Rum.SettledTotal, "the row switched off banks nothing");
        }

        [Test]
        public void ModuleLab_NeverSettles()
        {
            using var set = Default();
            var r = Make(set);
            r.Rum.LabMode = true;
            r.Publish(CombatEventKind.BossState, 2);
            r.Flush();
            Assert.AreEqual(0, r.Rum.Settle("lab", true));
            Assert.AreEqual(0, r.Bank.Treasury);
            Assert.IsTrue(r.Rum.EndedByDeath, "the results headline still reads a death");
        }

        /// <summary>Every weapon's recorded hits count (orbit, cone, trail and area hits carry no weapon tag on the enemy).</summary>
        [Test]
        public void DamageShare_CountsEveryRecordedWeaponHit()
        {
            var share = new DamageShareTracker();
            share.Record("flying_cutlasses", 30f);
            share.Record("deck_cannons", 10f);
            share.Record("flying_cutlasses", 30f);
            share.Record(null, 99f);
            share.Record("mortar", 0f);
            var shares = new List<KeyValuePair<string, float>>();
            share.Shares(shares);
            Assert.AreEqual(2, shares.Count);
            Assert.AreEqual("flying_cutlasses", shares[0].Key);
            Assert.AreEqual(600f / 7f, shares[0].Value, 1e-3f);
            Assert.AreEqual(100f / 7f, shares[1].Value, 1e-3f);

            // Every weapon damage path in the weapon module records its hit (the ledger hook the share listens to).
            var weapons = File.ReadAllText(Path.Combine(Application.dataPath, "_Game/V4/Runtime/Weapons/WeaponCombat.cs"));
            StringAssert.Contains("Hooks.RecordHit(ledgerId", weapons);
        }

        /// <summary>
        /// M14.13: the menu's Captain locks. Adopted rule: Hank 100 and Sparky 140 Rum (buyable when the treasury holds
        /// it), Ember and Roxie until their weapon is owned, Richie until 5,000 lifetime gold. V3 rule (the V3Ownership
        /// config switch): Hank and Sparky by save ownership (Sparky also by a First Conquest), the rest open.
        /// V5 M8z (7 Oct, I8z.5): every weapon is a free starter for now (D18 P3), so Ember's and Roxie's weapon locks are
        /// always met in the shipped data; the test takes the Flamethrower out of the starters to check the rule.
        /// </summary>
        [Test]
        public void CaptainLocks_FollowTheAdoptedRule_AndV3sByItsSwitch()
        {
            using var set = Default();
            var starters = set.Single<UnlockConfig>().starterWeaponIds;
            // Owner disabled the Flamethrower 13 Oct (hackathon): out of the Saltwind pools, so no longer a starter (was D18 P3).
            Assert.IsFalse(starters.Contains("flamethrower"), "out of the pools (owner, 13 Oct)");
            set.Single<UnlockConfig>().starterWeaponIds = starters.Where(id => id != "flamethrower").ToArray();
            var save = new SaveService(new MemorySaveBackend());
            var lifetime = 0;
            var conquest = false;
            var locks = new CaptainLocks(() => set, () => save, () => conquest, () => lifetime);
            CaptainConfig C(string key) => set.All<CaptainConfig>().Single(c => c.captainKey == key);

            Assert.AreEqual(new[] { "rook", "brine", "richie", "flint", "ember", "roxie" }, locks.MenuOrder().Select(c => c.captainKey).ToArray(), "V3's menu order");
            Assert.IsFalse(locks.IsLocked(C("rook")));
            var hank = locks.StateOf(C("brine"));
            Assert.IsTrue(hank.Locked);
            Assert.AreEqual(100, hank.RumCost);
            Assert.IsFalse(hank.CanBuy, "no Rum yet");
            save.TrySettleRun("r1", 120);
            Assert.IsTrue(locks.StateOf(C("brine")).CanBuy, "100 Rum buys Hank");
            var unlocks = new UnlockService(() => save, () => set);
            StringAssert.StartsWith("Unlocked", unlocks.Purchase("brine"));
            Assert.IsFalse(locks.IsLocked(C("brine")));
            Assert.AreEqual(20, save.Meta.marks);
            Assert.AreEqual(140, locks.StateOf(C("flint")).RumCost);
            var ember = locks.StateOf(C("ember"));
            Assert.IsTrue(ember.Locked);
            StringAssert.Contains("first", ember.Reason, "Ember waits for her weapon");
            var richie = locks.StateOf(C("richie"));
            Assert.IsTrue(richie.Locked);
            StringAssert.Contains("5,000", richie.Reason);
            lifetime = 5000;
            Assert.IsFalse(locks.IsLocked(C("richie")), "5,000 lifetime gold unlocks Richie");

            Assert.IsTrue(set.TryApplyEdit(set.Single<UnlockConfig>().configId, "captainLockRule", "V3Ownership", 0f, out var error), error);
            var fresh = new SaveService(new MemorySaveBackend());
            var v3 = new CaptainLocks(() => set, () => fresh, () => conquest, () => 0);
            Assert.IsTrue(v3.IsLocked(C("brine")), "V3: Hank needs \"brine\" owned");
            Assert.IsTrue(v3.IsLocked(C("flint")));
            conquest = true;
            Assert.IsFalse(v3.IsLocked(C("flint")), "V3: a First Conquest unlocks Sparky");
            Assert.IsFalse(v3.IsLocked(C("ember")) || v3.IsLocked(C("roxie")) || v3.IsLocked(C("richie")), "V3: the rest are open");
        }

        /// <summary>
        /// M14.13 review fix: the menu's services apply the session's preset (to the Captain locks too), and the Set Sail
        /// button follows the lock state. V5 M8z (7 Oct, I8z.5): checked with a small preset of its own (the v3-parity
        /// preset is retired, D18).
        /// </summary>
        [Test]
        public void MenuServices_ApplyTheSessionPreset_ToTheCaptainLocks()
        {
            var set = AssetDatabase.LoadAssetAtPath<ConfigSet>(SetPath);
            using var defaults = RoguePirates.V4.Composition.MenuServices.ForConfig(set, null);
            CaptainConfig C(RoguePirates.V4.Composition.MenuServices m, string key) => m.Config.All<CaptainConfig>().Single(c => c.captainKey == key);
            Assert.AreEqual(CaptainLockRule.CaptainConfig, defaults.Config.Single<UnlockConfig>().captainLockRule);

            string preset;
            using (var edited = new ConfigSetRuntime(set))
            {
                Assert.IsTrue(edited.TryApplyEdit("captain.richie", "unlockLifetimeGold", "1234", 0f, out var error), error);
                Assert.IsTrue(edited.TryApplyEdit(edited.Single<UnlockConfig>().configId, "captainLockRule", "V3Ownership", 0f, out error), error);
                preset = PresetCodec.ToJson(PresetCodec.Capture(edited, "menu-test", edited.Hash));
            }
            var errors = new List<string>();
            using var withPreset = RoguePirates.V4.Composition.MenuServices.ForConfig(set, preset, errors);
            Assert.IsEmpty(errors, string.Join("\n", errors));
            Assert.AreEqual(1234, C(withPreset, "richie").unlockLifetimeGold, "the preset reaches the menu's configs");
            Assert.AreEqual(CaptainLockRule.V3Ownership, withPreset.Config.Single<UnlockConfig>().captainLockRule, "and the menu's Captain lock rule");
            Assert.AreEqual(5000, C(defaults, "richie").unlockLifetimeGold, "the defaults are untouched");

            Assert.AreEqual(("SET SAIL", true), V4MainMenuController.PlayButton(CaptainLockState.Open, "RUM"));
            Assert.AreEqual(("UNLOCK: 100 RUM", true), V4MainMenuController.PlayButton(new CaptainLockState(true, "100 Rum", 100, true), "RUM"));
            Assert.AreEqual(("LOCKED", false), V4MainMenuController.PlayButton(new CaptainLockState(true, "100 Rum", 100, false), "RUM"));
        }

        [Test]
        public void Unlocks_CostWhatV3Charged_AndSelectingNeverSpends()
        {
            using var set = Default();
            var save = new SaveService(new MemorySaveBackend());
            var unlocks = new UnlockService(() => save, () => set);
            // V3's Saltwind unlock prices (its stage profile), weapons in V3's goal order.
            // Owner disabled the Flamethrower 13 Oct (hackathon): out of the Saltwind pools, so it has no price (was 80).
            foreach (var (id, price) in new[] { ("lightning_mast", 60), ("mortar", 100), ("harpoon_gun", 100), ("brine", 100), ("flint", 140) })
                Assert.AreEqual(price, unlocks.CostOf(id), id);
            Assert.AreEqual("lightning_mast", unlocks.NextLockedWeapon(), "a fresh save's first locked weapon, as V3 picked it");

            save.TrySettleRun("r1", 70);
            var goal = unlocks.NextLockedWeapon();
            StringAssert.StartsWith("Unlock goal:", unlocks.SelectNextGoal());
            Assert.AreEqual(goal, save.Meta.selectedUnlockGoal);
            Assert.AreEqual(70, save.Meta.marks, "selecting a goal never spends Rum");
            StringAssert.Contains("70 / " + unlocks.CostOf(goal) + " Rum", unlocks.GoalProgress());
            var cost = unlocks.CostOf(goal);
            if (cost <= 70)
            {
                StringAssert.StartsWith("Unlocked", unlocks.PurchaseGoal());
                Assert.AreEqual(70 - cost, save.Meta.marks);
                StringAssert.Contains("owned", unlocks.GoalProgress());
            }
            else
            {
                Assert.AreEqual("Not enough Rum or already owned.", unlocks.PurchaseGoal());
                Assert.AreEqual(70, save.Meta.marks);
            }
        }

        /// <summary>V4 never pays through the V3 paths: no FinalizeRunOnDeath (Doubloons) in solo runs, no Marks tracker, no "Marks" text.</summary>
        [Test]
        public void NoV4Code_UsesTheV3MarksPaths_OrSaysMarks()
        {
            var root = Path.Combine(Application.dataPath, "_Game/V4");
            foreach (var f in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(f);
                var name = f.Replace('\\', '/');
                Assert.IsFalse(text.Contains("RunMarksTracker.Try") || text.Contains("RunMarksTracker.Register") || text.Contains("RunMarksTracker.BeginRun"), name + ": V3 Marks tracker");
                Assert.IsFalse(text.Contains("SaltwindRunSettlement.TrySettle") || text.Contains("SaltwindMarksUnlockLogic."), name + ": V3 settlement/unlocks");
                Assert.IsFalse(text.Contains("AddDoubloons"), name + ": Doubloons");
                Assert.IsFalse(text.Contains("FinalizeRunOnDeath("), name + ": V3 death finalize");
                if (name.Contains("/Presentation/"))
                    Assert.IsFalse(text.Contains("Marks"), name + ": player-facing text says Rum");
            }
        }
    }
}
