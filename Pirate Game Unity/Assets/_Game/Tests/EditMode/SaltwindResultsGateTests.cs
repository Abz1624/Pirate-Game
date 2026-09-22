using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.V3;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Tests
{
    public sealed class SaltwindResultsGateTests
    {
        [TearDown]
        public void TearDown() => StageBalanceProfileContext.ResetForTests();

        [Test]
        public void Headline_KeepsVictoryAfterDeath()
        {
            var headline = SaltwindRunResultsLogic.BuildHeadline(objectiveSecured: true, runEndedAfterDeath: true);
            Assert.IsTrue(headline.Contains("VICTORY"));
        }

        [Test]
        public void WeaponContribution_ZeroDamage_ReturnsEmpty()
        {
            var lines = SaltwindRunResultsLogic.BuildWeaponContributionLines(
                new Dictionary<string, WeaponContributionStats>
                {
                    ["deck_cannons"] = new WeaponContributionStats { totalDamage = 0f }
                });
            Assert.AreEqual(0, lines.Count);
        }

        [Test]
        public void WeaponContribution_UsesDisplayNames_And100Percent()
        {
            WeaponDamageLedger.Reset();
            WeaponDamageLedger.RecordHit("flamethrower", 50f, false, false, false);
            WeaponDamageLedger.RecordHit("deck_cannons", 50f, false, false, false);
            var lines = SaltwindRunResultsLogic.BuildWeaponContributionLines(WeaponDamageLedger.Snapshot());
            Assert.AreEqual(2, lines.Count);
            var totalPct = 0f;
            foreach (var line in lines)
            {
                totalPct += line.DamageSharePercent;
                Assert.IsFalse(line.DisplayName.Contains("_"));
            }

            Assert.AreEqual(100f, totalPct, 0.1f);
            var hasFlamethrower = false;
            foreach (var line in lines)
            {
                if (line.DisplayName == "Flamethrower") hasFlamethrower = true;
            }

            Assert.IsTrue(hasFlamethrower);
        }

        [Test]
        public void MarksUi_MatchesLedgerFixture()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var record = SaltwindStageSettlementLogic.BuildFixtureRecord();
            var breakdown = SaltwindStageSettlementLogic.ComputeBreakdown(export, record);
            Assert.IsTrue(SaltwindRunResultsLogic.MarksUiMatchesLedger(breakdown, breakdown.TotalMarks));
            Assert.AreEqual(57, breakdown.TotalMarks);
        }

        [Test]
        public void UnlockGoal_SelectDoesNotPurchase()
        {
            var save = new SaveService(new InMemorySaveBackend());
            save.Meta.marks = 200;
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            var export = StageBalanceProfileContext.Export;

            Assert.IsTrue(SaltwindMarksUnlockLogic.TrySetUnlockGoal(save, "mortar"));
            Assert.IsFalse(save.HasMarksOwned("mortar"));
            Assert.AreEqual(200, save.Meta.marks);
            Assert.IsTrue(SaltwindMarksUnlockLogic.TryPurchaseUnlock(save, export, "mortar"));
            Assert.IsTrue(save.HasMarksOwned("mortar"));
        }

        [Test]
        public void UnlockGoalProgress_ShowsFriendlyName()
        {
            var save = new SaveService(new InMemorySaveBackend());
            save.Meta.marks = 12;
            SaltwindMarksUnlockLogic.TrySetUnlockGoal(save, "lightning_mast");
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            var text = SaltwindMarksUnlockLogic.FormatSelectedGoalProgress(save, StageBalanceProfileContext.Export);
            Assert.IsTrue(text.Contains("Lightning Mast"));
            Assert.IsFalse(text.Contains("lightning_mast"));
        }
    }
}
