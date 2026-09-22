using System.Collections;
using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.POI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    /// <summary>Codex R10: same-tick boss secure + live proc budget callbacks.</summary>
    public sealed class SaltwindAdvancedPlayModeTests
    {
        SaltwindPlayModeTestSupport _harness;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _harness = SaltwindPlayModeTestSupport.Create(99001);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
            _harness?.Dispose();
            _harness = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameTickBossKill_OnPlayerDeathPath_SecuresObjectiveAndSettlesBossMarks()
        {
            var rm = _harness.RunManager;
            RunMarksTracker.BeginRun(rm.RunId);
            var poi = _harness.CreatePoiManager();

            var boss = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            poi.ConfigureTrackedBossEncounterForTests(boss.EncounterId);
            RunEncounterTracker.RegisterSpawnedAgent(boss.EncounterId, 501);
            Assert.IsTrue(RunEncounterTracker.RegisterAgentKill(501));
            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured);
            Assert.IsFalse(rm.ObjectiveSecured);

            var marksBefore = AppRoot.Instance.Save.Meta.marks;
            var player = _harness.CreatePlayerDamageReceiver(10f);
            player.TakeDamage(999f, Vector3.zero, false);
            Assert.IsFalse(player.IsAlive);

            poi.TrySecureIfBossDead();
            rm.SetState(RunState.PlayerDead);
            rm.FinalizeRunOnDeath();

            Assert.IsTrue(rm.ObjectiveSecured);
            Assert.IsTrue(SaltwindRunSettlement.LastSettlementApplied);
            Assert.Greater(SaltwindRunSettlement.LastBreakdown.BossVictoryCredits, 0);
            Assert.Greater(AppRoot.Instance.Save.Meta.marks, marksBefore);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SameTickBossKill_PostCombatFrame_ResolvesAfterRunEnded()
        {
            var rm = _harness.RunManager;
            var poi = _harness.CreatePoiManager();
            var boss = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            poi.ConfigureTrackedBossEncounterForTests(boss.EncounterId);
            RunEncounterTracker.RegisterSpawnedAgent(boss.EncounterId, 502);
            RunEncounterTracker.RegisterAgentKill(502);

            _harness.CreatePlayerDamageReceiver(5f).TakeDamage(99f, Vector3.zero, false);
            rm.SetState(RunState.PlayerDead);
            rm.FinalizeRunOnDeath();
            Assert.IsFalse(rm.ObjectiveSecured);

            SaltwindSameTickPostCombatFrame.Resolve(poi);
            Assert.IsTrue(rm.ObjectiveSecured);
            Assert.IsTrue(poi.BossDefeated);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CombatProcRuntime_CapsTriggeredApplicationsAtPolicyMax()
        {
            _harness.FillActiveEnemiesToCount(20);
            var agents = _harness.EnemyWorld.Agents;
            var firstIndex = -1;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                firstIndex = i;
                break;
            }

            Assert.GreaterOrEqual(firstIndex, 0);
            var hpBefore = agents[firstIndex].hp;

            CombatProcRuntime.BeginWeaponDamageResolution();
            var applied = 0;
            for (var i = 0; i < 20; i++)
            {
                if (CombatProcRuntime.TryApplyTriggeredEnemyDamage(_harness.EnemyWorld, firstIndex, 1f, "powder_kegs"))
                    applied++;
            }

            Assert.AreEqual(DamageProcPolicy.MaxProcEventsPerResolution, applied);
            Assert.AreEqual(DamageProcPolicy.MaxProcEventsPerResolution, DamageProcPolicy.ProcEventsThisResolutionForTests);
            Assert.Less(agents[firstIndex].hp, hpBefore);
            yield return null;
        }
    }

    /// <summary>Minimal stand-in for V3RunBootstrap post-combat same-tick hook.</summary>
    static class SaltwindSameTickPostCombatFrame
    {
        public static void Resolve(POIManager poi)
        {
            if (RunManager.Instance == null || !RunManager.Instance.IsRunEnded) return;
            poi?.TrySecureIfBossDead();
        }
    }
}
