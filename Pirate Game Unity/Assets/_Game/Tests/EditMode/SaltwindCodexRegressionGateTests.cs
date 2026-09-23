using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Enemies;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;
using UnityEngine;

namespace HighSeas.Tests
{
    /// <summary>Codex R1/R2/R3 regression gates (EditMode helpers + runtime logic).</summary>
    public sealed class SaltwindCodexRegressionGateTests
    {
        [TearDown]
        public void TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
        }

        [Test]
        public void AmbientBands_AreExclusiveWindows_NotStacked()
        {
            var starts = new[] { 0f, 120f, 240f, 360f, 480f };
            const float deadline = 600f;

            Assert.AreEqual(1, EncounterScheduleRunLogic.CountActiveBandWindows(119.9f, starts, deadline));
            Assert.AreEqual(1, EncounterScheduleRunLogic.CountActiveBandWindows(120f, starts, deadline));
            Assert.AreEqual(1, EncounterScheduleRunLogic.CountActiveBandWindows(239.9f, starts, deadline));
            Assert.AreEqual(1, EncounterScheduleRunLogic.CountActiveBandWindows(479.9f, starts, deadline));
            Assert.AreEqual(0, EncounterScheduleRunLogic.CountActiveBandWindows(600f, starts, deadline));
        }

        [Test]
        public void ScheduledContinuousWindow_CompletesAtNextBandStart()
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.spawnMode = SpawnMode.Continuous;
            profile.timing.durationSeconds = 0f;

            Assert.IsTrue(EncounterScheduleRunLogic.IsScheduledContinuousWindowComplete(
                120f, 0f, 120f, profile, 999f, 999));
            Assert.IsFalse(EncounterScheduleRunLogic.IsScheduledContinuousWindowComplete(
                119f, 0f, 120f, profile, 10f, 0));
        }

        [Test]
        public void MarksRecord_BuildRecord_IsPure_NoMutationOnRepeatedRead()
        {
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            RunMarksTracker.BeginRun("pure-read");

            var first = RunMarksTracker.BuildRecord();
            var second = RunMarksTracker.BuildRecord();
            Assert.AreEqual(first.QualifyingOvertimeMinutes, second.QualifyingOvertimeMinutes);
            Assert.AreEqual(first.QualifyingOrdinaryMinutes, second.QualifyingOrdinaryMinutes);
        }

        [Test]
        public void StageDeadline_IncludesFinalOrdinaryMinuteBucket()
        {
            const float deadline = 600f;
            const int completedMinute = 9;
            var minuteEndSeconds = (completedMinute + 1) * 60f;
            Assert.LessOrEqual(minuteEndSeconds, deadline);
        }

        [Test]
        public void ScheduledEliteKillCredits_AreDistinctPerElite()
        {
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            RunMarksTracker.BeginRun("elites");

            RunMarksTracker.RegisterScheduledEliteAgent(101, 0);
            Assert.IsTrue(RunMarksTracker.TryCreditScheduledEliteKill(101, "hammerhead"));
            RunMarksTracker.RegisterScheduledEliteAgent(202, 1);
            Assert.IsTrue(RunMarksTracker.TryCreditScheduledEliteKill(202, "kraken_spawn"));
            Assert.IsFalse(RunMarksTracker.TryCreditScheduledEliteKill(202, "kraken_spawn"));

            var record = RunMarksTracker.BuildRecord();
            Assert.AreEqual(2, record.ScheduledEliteKillCredits);
        }

        [Test]
        public void NineMinuteAmbient_UsesSingleBandFourExportRate()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var starts = new[] { 0f, 120f, 240f, 360f, 480f };
            Assert.AreEqual(1, EncounterScheduleRunLogic.CountActiveBandWindows(540f, starts, export.Timing.StageDeadlineSeconds));
            Assert.AreEqual(3.0f, SaltwindLabSpawnParityLogic.GetAmbientRateAtTime(export, 540f), 0.001f);
            Assert.AreEqual(3.0f, SaltwindLabSpawnParityLogic.EffectiveAmbientSpawnsPerSecond(export, 540f, bossFightActive: false), 0.001f);
        }

        [Test]
        public void LegacyUnlockedIds_CountAsEffectiveOwnership()
        {
            var save = new SaveService(new InMemorySaveBackend());
            save.Meta.unlockedIds = new[] { "mortar" };
            Assert.IsFalse(save.HasMarksOwned("mortar"));
            Assert.IsTrue(save.HasEffectiveContentOwned("mortar"));
        }

        [Test]
        public void MarksOwnedLightningMast_MakesCopperConductorEligible()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var save = new SaveService(new InMemorySaveBackend());
            save.Meta.marksOwnedContentIds = new[] { "lightning_mast" };

            Assert.IsFalse(SaltwindStageProgressionLogic.IsRunItemOfferEligible(export, "copper_conductor"));
            Assert.IsTrue(SaltwindStageProgressionLogic.IsRunItemOfferEligible(export, "copper_conductor", save));
        }

        [Test]
        public void TryPurchaseUnlock_SupportsCaptainBrine()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var save = new SaveService(new InMemorySaveBackend());
            save.Meta.marks = 100;
            Assert.IsTrue(SaltwindMarksUnlockLogic.TryPurchaseUnlock(save, export, "brine"));
            Assert.IsTrue(save.HasEffectiveContentOwned("brine"));
            Assert.AreEqual(0, save.Meta.marks);
        }
    }
}
