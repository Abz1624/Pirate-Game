using HighSeas.Encounters;
using NUnit.Framework;

namespace HighSeas.Tests
{
    public sealed class RunEncounterTrackerTests
    {
        [TearDown]
        public void TearDown() => RunEncounterTracker.ResetForTests();

        [Test]
        public void BossEncounter_NoSpawn_NoFalseComplete()
        {
            var enc = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            Assert.AreEqual(RunEncounterState.Requested, enc.State);
            Assert.IsFalse(RunEncounterTracker.BossObjectiveSecured);
        }

        [Test]
        public void BossEncounter_DespawnWithoutKill_DoesNotComplete()
        {
            var enc = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            RunEncounterTracker.RegisterSpawnedAgent(enc.EncounterId, 42);
            Assert.IsFalse(RunEncounterTracker.RegisterAgentKill(99));
            Assert.IsFalse(RunEncounterTracker.BossObjectiveSecured);
        }

        [Test]
        public void BossEncounter_TrackedKill_SecuresOnce()
        {
            var enc = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            RunEncounterTracker.RegisterSpawnedAgent(enc.EncounterId, 7);
            Assert.IsTrue(RunEncounterTracker.RegisterAgentKill(7));
            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured);
            Assert.IsFalse(RunEncounterTracker.RegisterAgentKill(7));
        }

        [Test]
        public void FailedSummon_DoesNotSecureBoss()
        {
            var enc = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            RunEncounterTracker.MarkFailed(enc.EncounterId);
            Assert.AreEqual(RunEncounterState.Failed, enc.State);
            Assert.IsFalse(RunEncounterTracker.BossObjectiveSecured);
        }

        [Test]
        public void PartialCacheSpawn_DoesNotCompleteUntilAllKilled()
        {
            var enc = RunEncounterTracker.Request(RunEncounterKind.PowderCache, 3);
            RunEncounterTracker.RegisterSpawnedAgent(enc.EncounterId, 1);
            RunEncounterTracker.RegisterSpawnedAgent(enc.EncounterId, 2);
            Assert.AreEqual(RunEncounterState.Spawned, enc.State);
            RunEncounterTracker.RegisterAgentKill(1);
            Assert.AreNotEqual(RunEncounterState.Completed, enc.State);
            RunEncounterTracker.RegisterSpawnedAgent(enc.EncounterId, 3);
            RunEncounterTracker.RegisterAgentKill(2);
            RunEncounterTracker.RegisterAgentKill(3);
            Assert.AreEqual(RunEncounterState.Completed, enc.State);
        }

        [Test]
        public void PooledAgentId_AfterDespawnMapping_DoesNotSecureBoss()
        {
            var enc = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            RunEncounterTracker.RegisterSpawnedAgent(enc.EncounterId, 7);
            RunEncounterTracker.ReleaseAgentMapping(7);
            Assert.IsFalse(RunEncounterTracker.RegisterAgentKill(7));
            Assert.IsFalse(RunEncounterTracker.BossObjectiveSecured);
        }

        [Test]
        public void ResetForNewRun_ClearsOutstandingEncounters()
        {
            RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            RunEncounterTracker.ResetForNewRun();
            Assert.IsNull(RunEncounterTracker.ActiveBossEncounter);
            Assert.IsFalse(RunEncounterTracker.BossObjectiveSecured);
        }
    }
}
