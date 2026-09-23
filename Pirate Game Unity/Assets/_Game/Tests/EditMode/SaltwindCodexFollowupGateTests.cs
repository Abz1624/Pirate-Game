using HighSeas.Balance;
using HighSeas.Core;
using NUnit.Framework;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Tests
{
    public sealed class SaltwindCodexFollowupGateTests
    {
        [TearDown]
        public void TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
        }

        [Test]
        public void CannonboatCap_RejectsFifthThroughOrdinaryPool()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            for (var seed = 0; seed < 64; seed++)
            {
                var rng = new RunRng(seed);
                if (!SaltwindAmbientCompositionLogic.TryPickEnemyId(
                        1,
                        rng,
                        id => id == "cannonboat" ? 4 : 0,
                        export,
                        out var picked))
                    continue;
                Assert.AreNotEqual("cannonboat", picked, $"Seed {seed} selected a fifth cannonboat.");
            }
        }

        [Test]
        public void ScheduledEliteKill_RequiresRegisteredAgent()
        {
            StageBalanceProfileContext.BeginRun(StageBalanceProfile.CreateBuiltInSaltwindDefaults());
            RunMarksTracker.BeginRun("agent-elite");
            Assert.IsFalse(RunMarksTracker.TryCreditScheduledEliteKill(999, "hammerhead"));
            RunMarksTracker.RegisterScheduledEliteAgent(999, 0);
            Assert.IsTrue(RunMarksTracker.TryCreditScheduledEliteKill(999, "hammerhead"));
            Assert.IsFalse(RunMarksTracker.TryCreditScheduledEliteKill(999, "hammerhead"));
        }

        [Test]
        public void SwarmPulse_SkippedPermanentlyWhenBossActiveAtBeatTime()
        {
            var profile = StageBalanceProfile.CreateBuiltInSaltwindDefaults();
            var pulseA = SaltwindPressureBeatLogic.GetSwarmStartSeconds(profile, 0);
            Assert.IsTrue(SaltwindPressureBeatLogic.ShouldSkipScheduledEventForBoss(pulseA + 5f, pulseA, bossFightActive: true));
            Assert.IsFalse(SaltwindPressureBeatLogic.ShouldStartSwarmPulse(pulseA + 60f, profile, 0, bossFightActive: false, alreadyStarted: true));
        }

        [Test]
        public void LegacyOwnedContent_BlocksMarksPurchase()
        {
            var save = new SaveService(new MemorySaveBackend());
            save.Meta.unlockedIds = new[] { "lightning_mast" };
            save.Meta.marks = 100;
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            Assert.IsFalse(SaltwindMarksUnlockLogic.TryPurchaseUnlock(save, export, "lightning_mast"));
            Assert.AreNotEqual("lightning_mast", SaltwindMarksUnlockLogic.GetNextLockedMarksWeapon(export, save));
            Assert.AreEqual(100, save.Meta.marks);
        }

        [Test]
        public void ConservativeBossHull_V6_IsHigherThanFreshExpert()
        {
            var export = StageBalanceProfile.CreateBuiltInSaltwindDefaults().ToExport();
            var conservative = SaltwindBalanceLabScenarioFactory.Create(
                export, 1, 1, SaltwindLabBuildPolicy.Broadside, SaltwindLabUnlockPool.FreshSave, BalanceSkillPreset.Conservative);
            var expert = SaltwindBalanceLabScenarioFactory.Create(
                export, 1, 1, SaltwindLabBuildPolicy.Broadside, SaltwindLabUnlockPool.FreshSave, BalanceSkillPreset.Expert);
            Assert.Greater(conservative.BossBaseHull, expert.BossBaseHull);
        }
    }
}
