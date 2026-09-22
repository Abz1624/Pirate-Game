using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Save;
using HighSeas.V3.MultiShip;
using NUnit.Framework;

namespace HighSeas.Tests
{
    public sealed class SaltwindResetGateTests
    {
        [TearDown]
        public void TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            V3RunSession.Reset();
        }

        [Test]
        public void ThreeRetries_IncrementGeneration_AndClearEncounters()
        {
            var start = SaltwindRunResetLogic.RetryGeneration;
            for (var i = 0; i < 3; i++)
            {
                RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
                SaltwindRunResetLogic.NotifyRetryStarting();
                RunEncounterTracker.ResetForNewRun();
                RunMarksTracker.Reset();
                SaltwindRunResetLogic.AssertEncountersClearedForTests();
            }

            Assert.AreEqual(start + 3, SaltwindRunResetLogic.RetryGeneration);
        }

        [Test]
        public void StageBalanceProfile_SkippedForLocalMultiplayer()
        {
            V3RunSession.Configure(V3RunSessionKind.PvP);
            Assert.IsFalse(SaltwindRunResetLogic.ShouldUseStageBalanceProfile());
        }

        [Test]
        public void SnapshotRestore_RejectsBossSummonedWithZeroSpawns()
        {
            var data = new RunSnapshotData
            {
                schemaVersion = 2,
                bossSummoned = true,
                bossDefeated = false,
                activeBossEncounterId = 3,
                activeBossSpawnedCount = 0,
                activeBossState = (int)RunEncounterState.Requested
            };
            Assert.AreEqual(RunSnapshotRestoreVerdict.RejectIncompleteActiveEncounter,
                RunSnapshotRestoreLogic.Evaluate(data));
        }

        [Test]
        public void SnapshotRestore_AllowsLegacySchemaWithoutEncounterFields()
        {
            var data = new RunSnapshotData { schemaVersion = 1, runSeed = 7, gold = 10 };
            Assert.AreEqual(RunSnapshotRestoreVerdict.OkLoadoutSuspend,
                RunSnapshotRestoreLogic.Evaluate(data));
        }

        [Test]
        public void SnapshotRestore_RejectsFutureSchema()
        {
            var data = new RunSnapshotData { schemaVersion = 99 };
            Assert.AreEqual(RunSnapshotRestoreVerdict.RejectUnsupportedSchema,
                RunSnapshotRestoreLogic.Evaluate(data));
        }

        [Test]
        public void ResetPhaseCount_MatchesDocumentedOrder()
        {
            Assert.AreEqual(8, SaltwindRunResetLogic.PhaseCount);
        }
    }
}
