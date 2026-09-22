using System.Collections;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Save;
using HighSeas.V3.MultiShip;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    /// <summary>Codex R10: runtime lifecycle integration (boss, cache retry, pause, settlement, mode).</summary>
    public sealed class SaltwindLifecyclePlayModeTests
    {
        SaltwindPlayModeTestSupport _harness;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _harness = SaltwindPlayModeTestSupport.Create(88001);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            V3RunSession.Reset();
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
            _harness?.Dispose();
            _harness = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossSpawnKill_SecuresObjective_ThenDeathSettlesOnce()
        {
            var marksBefore = AppRoot.Instance.Save.Meta.marks;
            var rm = _harness.RunManager;
            RunMarksTracker.BeginRun(rm.RunId);

            var boss = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            var profile = _harness.CreateEncounterProfile(1);
            var spawned = _harness.Spawner.TriggerProfileImmediate(profile, 1, boss.EncounterId);
            Assert.Greater(spawned, 0, "Boss encounter should spawn at least one agent in PlayMode.");

            boss = RunEncounterTracker.Get(boss.EncounterId);
            var agentId = boss.SpawnedCount > 0
                ? FirstSpawnedAgentId(boss)
                : FindFirstActiveEnemyUnitId(_harness.EnemyWorld);
            Assert.Greater(agentId, 0);
            if (!boss.SpawnedAgentIds.Contains(agentId))
                RunEncounterTracker.RegisterSpawnedAgent(boss.EncounterId, agentId);

            Assert.IsTrue(RunEncounterTracker.RegisterAgentKill(agentId));
            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured);
            rm.SecureObjective();
            RunMarksTracker.TryRegisterBossVictory();

            rm.SetState(RunState.PlayerDead);
            rm.FinalizeRunOnDeath();

            Assert.IsTrue(rm.ObjectiveSecured);
            Assert.IsTrue(SaltwindRunSettlement.LastSettlementApplied);
            Assert.Greater(SaltwindRunSettlement.LastBreakdown.BossVictoryCredits, 0);
            Assert.AreEqual(marksBefore + SaltwindRunSettlement.LastMarksAwarded, AppRoot.Instance.Save.Meta.marks);

            var treasuryAfterFirst = AppRoot.Instance.Save.Meta.marks;
            rm.FinalizeRunOnDeath();
            Assert.AreEqual(treasuryAfterFirst, AppRoot.Instance.Save.Meta.marks);

            yield return null;
        }

        [UnityTest]
        public IEnumerator PartialCache_AtCap_SingleKill_DoesNotCompleteEncounter()
        {
            var cap = _harness.Spawner.GetEnemyCapForDebug();
            _harness.FillActiveEnemiesToCount(cap - 1);

            var cache = RunEncounterTracker.Request(RunEncounterKind.PowderCache, 3);
            var profile = _harness.CreateEncounterProfile(3);
            _harness.Spawner.TriggerProfile(profile, 3, cache.EncounterId);
            _harness.Spawner.ProcessPendingEncountersNow();

            cache = RunEncounterTracker.Get(cache.EncounterId);
            Assert.Less(cache.SpawnedCount, 3);
            var agentId = FirstSpawnedAgentId(cache);
            if (agentId > 0)
            {
                RunEncounterTracker.RegisterAgentKill(agentId);
                cache = RunEncounterTracker.Get(cache.EncounterId);
                Assert.IsTrue(cache.KilledAgentIds.Contains(agentId));
                Assert.AreNotEqual(RunEncounterState.Completed, cache.State);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator PowderCache_PartialSpawn_RetriesAfterCapacityFrees()
        {
            var rm = _harness.RunManager;
            var cap = _harness.Spawner.GetEnemyCapForDebug();
            _harness.FillActiveEnemiesToCount(cap - 1);

            var cache = RunEncounterTracker.Request(RunEncounterKind.PowderCache, 3);
            var profile = _harness.CreateEncounterProfile(3);
            _harness.Spawner.TriggerProfile(profile, 3, cache.EncounterId);

            Assert.AreEqual(1, _harness.Spawner.PendingTriggeredEncounterCount);
            _harness.Spawner.ProcessPendingEncountersNow();

            cache = RunEncounterTracker.Get(cache.EncounterId);
            Assert.Less(cache.SpawnedCount, 3);
            Assert.Greater(_harness.Spawner.PendingTriggeredEncounterCount, 0);

            Assert.IsTrue(_harness.EnemyWorld.TryDespawnOldestActive(out _));
            rm.RestoreSuspendSnapshot(new RunSnapshotData
            {
                elapsedSeconds = rm.ElapsedSeconds + 0.5f
            });

            var secondPass = _harness.Spawner.ProcessPendingEncountersNow();
            Assert.Greater(secondPass, 0);
            cache = RunEncounterTracker.Get(cache.EncounterId);
            Assert.GreaterOrEqual(cache.SpawnedCount, 2);

            yield return null;
        }

        [UnityTest]
        public IEnumerator RunManager_Pause_BlocksElapsedAdvance()
        {
            var rm = _harness.RunManager;
            rm.EnterPlaying();
            var start = rm.ElapsedSeconds;
            rm.SetPause(PauseReason.LevelUp);
            yield return new WaitForSeconds(0.25f);
            Assert.That(rm.ElapsedSeconds, Is.EqualTo(start).Within(0.01f));
            rm.SetPause(PauseReason.None);
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(rm.ElapsedSeconds, start);
        }

        [UnityTest]
        public IEnumerator LateBossVictory_AfterDeathFlag_StillSecuresObjectiveForSettlement()
        {
            var rm = _harness.RunManager;
            RunMarksTracker.BeginRun(rm.RunId);
            rm.SetState(RunState.PlayerDead);
            rm.SecureObjective();
            RunMarksTracker.TryRegisterBossVictory();
            rm.FinalizeRunOnDeath();

            Assert.IsTrue(rm.ObjectiveSecured);
            Assert.IsTrue(SaltwindRunSettlement.LastSettlementApplied);
            Assert.Greater(SaltwindRunSettlement.LastBreakdown.BossVictoryCredits, 0);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreeRetries_IncrementGeneration_AndClearBossEncounter()
        {
            var start = SaltwindRunResetLogic.RetryGeneration;
            for (var i = 0; i < 3; i++)
            {
                RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
                SaltwindRunResetLogic.NotifyRetryStarting();
                RunEncounterTracker.ResetForNewRun();
                RunMarksTracker.Reset();
                SaltwindRunResetLogic.AssertEncountersClearedForTests();
                yield return null;
            }

            Assert.AreEqual(start + 3, SaltwindRunResetLogic.RetryGeneration);
        }

        [UnityTest]
        public IEnumerator LocalMultiplayerSession_SkipsStageBalanceProfileGate()
        {
            V3RunSession.Configure(V3RunSessionKind.PvP);
            Assert.IsFalse(SaltwindRunResetLogic.ShouldUseStageBalanceProfile());
            V3RunSession.Reset();
            Assert.IsTrue(SaltwindRunResetLogic.ShouldUseStageBalanceProfile());
            yield return null;
        }

        [UnityTest]
        public IEnumerator DamageProcPolicy_DotDamage_NeverCritsInPlayModeRolls()
        {
            var rm = _harness.RunManager;
            var stats = new PlayerStats();
            stats.AddModifier(new StatModifier(StatId.CritChance, 0.95f));
            var crits = 0;
            for (var i = 0; i < 64; i++)
            {
                var roll = DamageSystem.RollDamageDetailed(
                    10f, 1f, stats, false, 0f, rm.Rng, 1f, "deck_cannons", DamageProcKind.Dot);
                if (roll.isCrit) crits++;
            }

            Assert.AreEqual(0, crits);
            yield return null;
        }

        static int FirstSpawnedAgentId(RunEncounterRecord record)
        {
            foreach (var id in record.SpawnedAgentIds)
                return id;
            return -1;
        }

        static int FindFirstActiveEnemyUnitId(EnemyWorld world)
        {
            if (world?.Agents == null) return -1;
            for (var i = 0; i < world.Agents.Length; i++)
            {
                if (world.Agents[i].active)
                    return world.Agents[i].id;
            }

            return -1;
        }
    }
}
