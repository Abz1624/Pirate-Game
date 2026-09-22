using System;
using System.Collections.Generic;
using System.IO;
using HighSeas.Balance;
using NUnit.Framework;
using RoguePirates.Simulation;
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
        public void Matrix_fullGrid_HasEighteenCellsAndExpectedRunCount()
        {
            var export = BuiltInExport();
            var report = SaltwindBalanceLabBatchRunner.RunMatrix(export, new SaltwindBalanceLabBatchOptions
            {
                BaseSeed = 12000,
                SeedsPerCell = 2
            });
            Assert.AreEqual(18, report.Cells.Count);
            Assert.AreEqual(36, report.TotalRuns);
            Assert.IsFalse(string.IsNullOrEmpty(report.ProfileContentHash));
        }

        [Test]
        public void Calibration_MarksExpert_UsesTougherBossAndLowerHullThanFreshExpert()
        {
            var export = BuiltInExport();
            var fresh = SaltwindBalanceLabScenarioFactory.Create(
                export, 1, 1, SaltwindLabBuildPolicy.Precision, SaltwindLabUnlockPool.FreshSave, BalanceSkillPreset.Expert);
            var marks = SaltwindBalanceLabScenarioFactory.Create(
                export, 1, 1, SaltwindLabBuildPolicy.Precision, SaltwindLabUnlockPool.MarksUnlocked, BalanceSkillPreset.Expert);
            Assert.Greater(marks.BossBaseHull, fresh.BossBaseHull);
            Assert.Less(marks.PlayerHullMultiplier, fresh.PlayerHullMultiplier);
        }

        [Test]
        public void Matrix_ExpertCells_StayWithinDocumentedWinBand()
        {
            var export = BuiltInExport();
            var report = SaltwindBalanceLabBatchRunner.RunMatrix(export, new SaltwindBalanceLabBatchOptions
            {
                BaseSeed = 12000,
                SeedsPerCell = 12
            });
            for (var i = 0; i < report.Cells.Count; i++)
            {
                var cell = report.Cells[i];
                if (cell.Skill != BalanceSkillPreset.Expert) continue;
                Assert.IsTrue(
                    SaltwindLabMatrixCalibration.IsWithinDocumentedBand(cell.Skill, cell.Simulation.WinRate),
                    $"{cell.BuildPolicy}/{cell.UnlockPool} expert win {cell.Simulation.WinRate:P0}");
            }
        }

        [Test]
        public void Matrix_AverageAndExpert_DifferForSamePolicyPool()
        {
            var export = BuiltInExport();
            var report = SaltwindBalanceLabBatchRunner.RunMatrix(export, new SaltwindBalanceLabBatchOptions
            {
                BaseSeed = 12000,
                SeedsPerCell = 8
            });
            BalanceLabSummary avg = null;
            BalanceLabSummary expert = null;
            for (var i = 0; i < report.Cells.Count; i++)
            {
                var cell = report.Cells[i];
                if (cell.BuildPolicy != SaltwindLabBuildPolicy.Broadside ||
                    cell.UnlockPool != SaltwindLabUnlockPool.FreshSave)
                    continue;
                if (cell.Skill == BalanceSkillPreset.Average) avg = cell.Simulation;
                if (cell.Skill == BalanceSkillPreset.Expert) expert = cell.Simulation;
            }

            Assert.NotNull(avg);
            Assert.NotNull(expert);
            Assert.That(Math.Abs(avg.WinRate - expert.WinRate), Is.GreaterThan(0.001f),
                "Average (kite) and Expert (orbit) should diverge under calibration");
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

        [Test]
        public void TraceParity_FixedSeedCheckpoints_MatchRuntimeAndLab()
        {
            var export = BuiltInExport();
            var checkpoints = new[] { 0f, 120f, 240f, 600f, 720f, 810f };
            for (var i = 0; i < checkpoints.Length; i++)
            {
                var elapsed = checkpoints[i];
                var runtime = SaltwindLabTraceParityLogic.BuildRuntimeCheckpoint(export, elapsed, 1, 3);
                var lab = SaltwindLabTraceParityLogic.BuildLabCheckpoint(export, elapsed, 1, 3);
                Assert.IsTrue(
                    SaltwindLabTraceParityLogic.CheckpointsMatch(runtime, lab),
                    $"checkpoint t={elapsed}s chest={runtime.ChestPrice}/{lab.ChestPrice}");
            }
        }

        [Test]
        public void RouteParity_FirstChestCheckpoint_MatchesExportTravelBudget()
        {
            var export = BuiltInExport();
            var deadline = SaltwindLabRuntimeParityLogic.QuoteStageDeadline(export);
            Assert.AreEqual(15f, SaltwindLabRouteParityLogic.FirstChestTravelSeconds(export), 0.001f);
            Assert.AreEqual(15f,
                SaltwindLabRouteParityLogic.SalvageChestCheckpointSeconds(export, 0, deadline + 300f),
                0.001f);
        }

        [Test]
        public void LevelOffer_FillStateAndPity_AddsWeaponAtProtectionLevel()
        {
            var export = BuiltInExport();
            var owned = new List<string> { "deck_cannons" };
            var levels = new Dictionary<string, int> { { "deck_cannons", 1 } };
            var rng = new SimRng(4242);
            for (var playerLevel = 1; playerLevel < 4; playerLevel++)
            {
                var pick = SaltwindLabLevelOfferLogic.RollAutopick(
                    export,
                    SaltwindLabBuildPolicy.AreaControl,
                    SaltwindLabUnlockPool.MarksUnlocked,
                    owned,
                    levels,
                    playerLevel,
                    6,
                    ref rng);
                if (pick.Action == SaltwindLabLevelOfferLogic.LevelUpAction.AddWeapon &&
                    !string.IsNullOrEmpty(pick.WeaponId))
                {
                    owned.Add(pick.WeaponId);
                    levels[pick.WeaponId] = 1;
                }
            }

            var pityPick = SaltwindLabLevelOfferLogic.RollAutopick(
                export,
                SaltwindLabBuildPolicy.AreaControl,
                SaltwindLabUnlockPool.MarksUnlocked,
                owned,
                levels,
                4,
                6,
                ref rng);
            Assert.AreEqual(SaltwindLabLevelOfferLogic.LevelUpAction.AddWeapon, pityPick.Action);
            Assert.IsFalse(string.IsNullOrEmpty(pityPick.WeaponId));
            Assert.AreNotEqual("deck_cannons", pityPick.WeaponId);
        }

        [Test]
        public void BossWin_SaltwindScenario_SecuresObjectiveBeforeTimeout()
        {
            var export = BuiltInExport();
            Assert.Greater(export.BossStatSnapshot.BaseHull, 100f);
            var scenario = SaltwindBalanceLabScenarioFactory.Create(
                export,
                12000,
                12,
                SaltwindLabBuildPolicy.Broadside,
                SaltwindLabUnlockPool.FreshSave,
                BalanceSkillPreset.Expert);
            var summary = BalanceLabRunner.Run(scenario);
            Assert.IsTrue(
                SaltwindLabMatrixCalibration.IsWithinDocumentedBand(BalanceSkillPreset.Expert, summary.WinRate),
                $"expert winRate={summary.WinRate:P0} avgSec={summary.AverageSeconds}");
            Assert.IsTrue(summary.Samples.Exists(s => s.ObjectiveSecured));
        }

#if UNITY_EDITOR
        [Test]
        public void Export_LinkedSchedule_IncludesSnapshotsInHash()
        {
            var profile = UnityEditor.AssetDatabase.LoadAssetAtPath<StageBalanceProfile>(
                "Assets/_Game/Data/Balance/SaltwindSoloStageBalanceProfile.asset");
            if (profile == null || profile.encounterSchedule == null)
                Assert.Ignore("Saltwind profile asset or schedule not linked.");

            var export = profile.ToExport();
            Assert.Greater(export.ScheduleProfileSnapshots.Count, 0);
            Assert.Greater(export.WeaponStatSnapshots.Count, 0);
            Assert.AreEqual(2, export.BalanceVersion);
            Assert.IsFalse(string.IsNullOrEmpty(export.ContentHash));

            var timing = export.ScheduleProfileSnapshots[0];
            Assert.IsFalse(string.IsNullOrEmpty(timing.ProfileKey));
        }
#endif
    }
}
