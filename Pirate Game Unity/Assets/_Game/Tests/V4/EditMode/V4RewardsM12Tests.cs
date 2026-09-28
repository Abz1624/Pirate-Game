using System.Collections.Generic;
using System.IO;
using System.Linq;
using HighSeas.Balance;
using HighSeas.Core;
using NUnit.Framework;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Legacy;
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
        const string ProfilePath = "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset";

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

        /// <summary>V3's approved fixture: 5 qualifying minutes, 2 special landmarks, 2 scheduled elites, the boss, 2 overtime minutes.</summary>
        static void PlayFixture(Rig r)
        {
            for (var m = 0; m < 5; m++) for (var k = 0; k < 10; k++) r.Kill(m * 60f + 1f + k);
            r.Kill(5 * 60f + 1f); // minute 5: one kill, does not qualify
            r.Publish(CombatEventKind.LandmarkUsed, 3);
            r.Publish(CombatEventKind.LandmarkUsed, 7);
            r.Publish(CombatEventKind.LandmarkUsed, 7); // the same landmark again: once
            r.Publish(CombatEventKind.PhaseChanged, 101, 1, 0f, LegacyConfigMapping.EliteA);
            r.Publish(CombatEventKind.PhaseChanged, 102, 1, 0f, LegacyConfigMapping.EliteB);
            r.Flush();
            r.Kill(400f, 101);
            r.Kill(410f, 102);
            r.Publish(CombatEventKind.BossState, 2);
            r.Flush();
            for (var m = 0; m < 3; m++) for (var k = 0; k < 10; k++) r.Kill(601f + m * 60f + k); // overtime minutes 0, 1, 2
            r.Flush();
            r.Time = 600f + 2 * 60f + 5f; // overtime minute 2 in progress: minutes 0 and 1 are paid
        }

        [Test]
        public void DefaultRows_AreV3sMarksEconomy()
        {
            using var set = Default();
            var c = set.Single<RewardConfig>();
            var p = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath);
            Assert.AreEqual(p.marksPerQualifyingMinute, c.Row("minutes").rum);
            Assert.AreEqual(p.marksPerQualifyingMinuteMinKills, c.Row("minutes").minKills);
            Assert.AreEqual(p.marksPerSpecialPoi, c.Row("landmarks").rum);
            Assert.AreEqual(p.marksPerScheduledElite, c.Row("elites").rum);
            Assert.AreEqual(2, c.Row("elites").maxCredits);
            Assert.AreEqual(p.marksBossVictory, c.Row("boss").rum);
            Assert.AreEqual(p.marksPerOvertimeMinute, c.Row("overtime").rum);
            Assert.AreEqual(p.marksOvertimeCap, c.Row("overtime").maxRum);
            Assert.IsFalse(c.Row("chests").enabled, "the chest row is an extra, off by default");
            Assert.AreEqual("Rum", c.currencyName);
        }

        [Test]
        public void Fixture_Pays57_TheSameBreakdownAsV3()
        {
            using var set = Default();
            var r = Make(set);
            PlayFixture(r);
            var total = r.Rum.Settle("run-1", true);
            var v3 = SaltwindStageSettlementLogic.ComputeBreakdown(AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath).ToExport(),
                SaltwindStageSettlementLogic.BuildFixtureRecord());
            Assert.AreEqual(SaltwindStageSettlementLogic.ApprovedFixtureTotalMarks, total);
            Assert.AreEqual(57, total);
            Assert.AreEqual(v3.QualifyingOrdinaryMinutes, r.Rum.SettledRumOf(RumSource.QualifyingMinute));
            Assert.AreEqual(v3.SpecialPoiCredits, r.Rum.SettledRumOf(RumSource.SpecialLandmark));
            Assert.AreEqual(v3.ScheduledEliteCredits, r.Rum.SettledRumOf(RumSource.ScheduledElite));
            Assert.AreEqual(v3.BossVictoryCredits, r.Rum.SettledRumOf(RumSource.BossVictory));
            Assert.AreEqual(v3.OvertimeMinuteCredits, r.Rum.SettledRumOf(RumSource.OvertimeMinute));
            Assert.IsTrue(r.Rum.SettlementApplied);
            Assert.AreEqual(57, r.Bank.Treasury);
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
            var r = Make(set);
            for (var k = 0; k < 10; k++) r.Kill(61f + k); // minute 1: 10 kills
            for (var k = 0; k < 9; k++) r.Kill(121f + k); // minute 2: 9 kills
            for (var k = 0; k < 15; k++) r.Kill(185f + k); // minute 3 (in progress at settlement)
            for (var k = 0; k < 10; k++) r.Kill(595f + k * 0.4f); // minute 9, the last pre-deadline minute
            r.Flush();
            r.Time = 199f;
            var live = r.Rum.Live.First(x => x.Source == RumSource.QualifyingMinute);
            Assert.AreEqual(1, live.Credits, "minute 1 only: minute 2 has 9 kills, minute 3 is still running");

            // The console jump closes skipped minutes with no kills; overtime kills never count as ordinary minutes.
            r.Time = 605f;
            live = r.Rum.Live.First(x => x.Source == RumSource.QualifyingMinute);
            Assert.AreEqual(3, live.Credits, "minutes 1, 3 and 9");
            for (var k = 0; k < 20; k++) r.Kill(605f + k);
            r.Flush();
            r.Time = 659f;
            Assert.AreEqual(0, r.Rum.Live.First(x => x.Source == RumSource.OvertimeMinute).Credits, "overtime minute 0 is paid only once minute 1 begins");
            r.Time = 661f;
            Assert.AreEqual(1, r.Rum.Live.First(x => x.Source == RumSource.OvertimeMinute).Credits);
        }

        [Test]
        public void Caps_EliteTwo_BossOnce_OvertimeFifteen_AndUnregisteredElitesNeverPay()
        {
            using var set = Default();
            var r = Make(set);
            r.Publish(CombatEventKind.PhaseChanged, 5, 1, 0f, LegacyConfigMapping.EliteA);
            r.Publish(CombatEventKind.PhaseChanged, 6, 1, 0f, LegacyConfigMapping.EliteB);
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
            for (var m = 0; m < 8; m++) for (var k = 0; k < 10; k++) r.Kill(601f + m * 60f + k);
            r.Flush();
            r.Time = 600f + 8 * 60f + 1f;
            r.Rum.Settle("caps", true);
            Assert.AreEqual(10, r.Rum.SettledRumOf(RumSource.ScheduledElite), "two elites × 5");
            Assert.AreEqual(25, r.Rum.SettledRumOf(RumSource.BossVictory), "once");
            Assert.AreEqual(15, r.Rum.SettledRumOf(RumSource.OvertimeMinute), "8 qualifying overtime minutes, capped at 15");
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

        /// <summary>Playtest round 1: the gold left at death is banked as Rum (1 per 10 gold); the v3-parity preset turns it off.</summary>
        [Test]
        public void GoldLeftAtDeath_IsBankedAsRum()
        {
            using var set = Default();
            var r = Make(set);
            r.Rum.RunGold = () => 257;
            r.Time = 30f;
            r.Rum.Settle("gold", true);
            Assert.AreEqual(25, r.Rum.SettledRows.First(x => x.RowId == "gold").Rum, "257 gold → 25 Rum");
            StringAssert.Contains("RUM EARNED  +25", RunResultsView.Format(r.Rum.SettledRows, r.Rum.SettledTotal, r.Bank.Treasury, "Rum"));
            var preset = File.ReadAllText(Path.Combine(Application.dataPath, "../../plan/v4-verification/presets/v3-parity.json"));
            var errors = new List<string>();
            Assert.IsTrue(PresetCodec.Apply(set, PresetCodec.FromJson(preset), 0f, errors), string.Join("; ", errors));
            Assert.IsFalse(set.Single<RewardConfig>().Row("gold").enabled, "V3 banked nothing: off under the parity preset");
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

        [Test]
        public void Unlocks_CostWhatV3Charged_AndSelectingNeverSpends()
        {
            using var set = Default();
            var export = AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(ProfilePath).ToExport();
            var save = new SaveService(new MemorySaveBackend());
            var unlocks = new UnlockService(() => save, () => set);
            foreach (var w in export.WeaponUnlocks) Assert.AreEqual(w.MarksCost, unlocks.CostOf(w.ContentId), w.ContentId);
            foreach (var c in export.CaptainUnlocks) Assert.AreEqual(c.MarksCost, unlocks.CostOf(c.ContentId), c.ContentId);
            Assert.AreEqual(SaltwindMarksUnlockLogic.GetNextLockedMarksWeapon(export, save), unlocks.NextLockedWeapon());

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
                if (!name.EndsWith("/Legacy/LegacyAdapters.cs"))
                    Assert.IsFalse(text.Contains("FinalizeRunOnDeath("), name + ": V3 death finalize (only the kept PvP branch may call it)");
                if (name.Contains("/Presentation/"))
                    Assert.IsFalse(text.Contains("Marks"), name + ": player-facing text says Rum");
            }
        }
    }
}
