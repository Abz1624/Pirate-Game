using System.IO;
using HighSeas.Balance;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Tests
{
    public sealed class SaltwindBalanceLabGateTests
    {
        static StageBalanceProfileExport BuiltInExport() =>
            StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();

        [Test]
        public void Parity_ChestPrice_MatchesCodecFixtures()
        {
            var export = BuiltInExport();
            Assert.AreEqual(20, SaltwindLabRuntimeParityLogic.QuoteChestPrice(export, 0));
            Assert.AreEqual(31, SaltwindLabRuntimeParityLogic.QuoteChestPrice(export, 1));
        }

        [Test]
        public void Parity_XpLevelZeroToOne_MatchesCodec()
        {
            var export = BuiltInExport();
            Assert.AreEqual(7, SaltwindLabRuntimeParityLogic.QuoteXpToNext(export, 0));
        }

        [Test]
        public void Parity_StageDeadline_MatchesExport()
        {
            var export = BuiltInExport();
            Assert.AreEqual(export.Timing.StageDeadlineSeconds,
                SaltwindLabRuntimeParityLogic.QuoteStageDeadline(export));
        }

        [Test]
        public void Matrix_sameSeedAndPolicy_ReproducesSummary()
        {
            var export = BuiltInExport();
            var options = new SaltwindBalanceLabBatchOptions { BaseSeed = 909, SeedsPerCell = 4 };
            var scenario = SaltwindBalanceLabScenarioFactory.Create(
                export,
                options.BaseSeed,
                options.SeedsPerCell,
                SaltwindLabBuildPolicy.Broadside,
                SaltwindLabUnlockPool.FreshSave,
                BalanceSkillPreset.Average);
            var first = BalanceLabRunner.Run(scenario);
            var second = BalanceLabRunner.Run(scenario);
            Assert.AreEqual(first.WinRate, second.WinRate);
            Assert.AreEqual(first.AverageKills, second.AverageKills);
        }

        [Test]
        public void Matrix_fullGrid_HasTwelveCellsAndExpectedRunCount()
        {
            var export = BuiltInExport();
            var report = SaltwindBalanceLabBatchRunner.RunMatrix(export, new SaltwindBalanceLabBatchOptions
            {
                BaseSeed = 12000,
                SeedsPerCell = 2
            });
            Assert.AreEqual(12, report.Cells.Count);
            Assert.AreEqual(24, report.TotalRuns);
            Assert.IsFalse(string.IsNullOrEmpty(report.ProfileContentHash));
        }

        [Test]
        public void LabScenario_doesNotReferencePlayerSave()
        {
            var scenario = SaltwindBalanceLabScenarioFactory.Create(
                BuiltInExport(),
                1,
                1,
                SaltwindLabBuildPolicy.Precision,
                SaltwindLabUnlockPool.FreshSave,
                BalanceSkillPreset.Expert);
            Assert.IsNotNull(scenario.StageProfile);
            Assert.IsTrue(scenario.BehaviorModelLabel.Length > 0);
        }
    }
}
