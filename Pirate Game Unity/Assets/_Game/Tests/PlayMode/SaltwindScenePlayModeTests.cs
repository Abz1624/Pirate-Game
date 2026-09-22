using System.Collections;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Encounters;
using HighSeas.POI;
using HighSeas.Player;
using HighSeas.V3;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    /// <summary>Codex R10: authored V3SaltwindRun scene + bootstrap/POI wiring.</summary>
    public sealed class SaltwindScenePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StageBalanceProfileContext.ResetForTests();
            RunMarksTracker.Reset();
            RunEncounterTracker.ResetForTests();
            SaltwindRunSettlement.ClearLastResults();
            yield return SceneManager.LoadSceneAsync(V3SceneNames.Boot, LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator V3SaltwindRunScene_BootstrapWiresRunManagerPoiAndSpawner()
        {
            yield return SaltwindScenePlayModeSupport.LoadSaltwindRunUntilPlaying();

            Assert.NotNull(Object.FindFirstObjectByType<V3RunBootstrap>(), "V3RunBootstrap missing from scene.");
            var poi = Object.FindFirstObjectByType<POIManager>();
            var spawner = Object.FindFirstObjectByType<Enemies.EncounterSpawner>();
            Assert.NotNull(poi, "POIManager not initialized in Saltwind scene.");
            Assert.NotNull(spawner, "EncounterSpawner missing from Saltwind scene.");
            Assert.IsTrue(poi.enabled, "POIManager should be enabled for solo Saltwind.");
            Assert.IsTrue(StageBalanceProfileContext.IsActive);
            Assert.IsFalse(string.IsNullOrEmpty(RunManager.Instance.RunId));
            Assert.IsNotNull(RunMarksTracker.BuildRecord().RunId);
        }

        [UnityTest]
        public IEnumerator V3SaltwindRunScene_PoiDirectorAcceptsBossSummonRequest()
        {
            yield return SaltwindScenePlayModeSupport.LoadSaltwindRunUntilPlaying();

            var poi = Object.FindFirstObjectByType<POIManager>();
            Assert.NotNull(poi);
            var before = poi.BossSummoned;
            var requested = poi.TrySummonBoss();
            if (requested)
            {
                Assert.IsTrue(poi.BossSummoned);
                Assert.Greater(RunEncounterTracker.ActiveBossEncounter?.EncounterId ?? 0, 0);
            }
            else
            {
                Assert.AreEqual(before, poi.BossSummoned);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator V3SaltwindRunScene_BossCombatKill_SecuresObjectiveViaPoiDirector()
        {
            yield return SaltwindScenePlayModeSupport.LoadSaltwindRunUntilPlaying();

            var poi = Object.FindFirstObjectByType<POIManager>();
            var enemyWorld = Object.FindFirstObjectByType<Enemies.EnemyWorld>();
            Assert.NotNull(poi);
            Assert.NotNull(enemyWorld);

            var summon = new BossSummonAttemptResult();
            yield return SaltwindScenePlayModeSupport.TrySummonBossWithRetries(poi, summon);
            Assert.IsTrue(summon.Summoned, "Boss summon did not succeed after retries (placement/cap/schedule).");

            var wait = new BossAgentWaitResult();
            yield return SaltwindScenePlayModeSupport.WaitForActiveBossAgent(enemyWorld, wait);
            var bossIndex = wait.AgentIndex;
            Assert.GreaterOrEqual(bossIndex, 0, "No active boss agent found after summon.");

            var encounter = RunEncounterTracker.ActiveBossEncounter;
            Assert.NotNull(encounter);
            Assert.Greater(encounter.SpawnedCount, 0, "Boss encounter has no registered spawns.");

            var bossUnitId = enemyWorld.Agents[bossIndex].id;
            Assert.IsTrue(encounter.SpawnedAgentIds.Contains(bossUnitId),
                "Spawned boss agent is not tracked on the active encounter.");

            SaltwindScenePlayModeSupport.ApplyLethalWeaponDamage(enemyWorld, bossIndex);
            yield return null;

            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured, "Boss kill did not complete tracked encounter.");
            Assert.IsTrue(poi.BossDefeated, "POIManager did not record boss defeated.");
            Assert.IsTrue(RunManager.Instance.ObjectiveSecured, "RunManager objective was not secured.");
            Assert.IsTrue(RunMarksTracker.BuildRecord().BossVictoryCredited, "Boss victory Marks credit missing.");
        }

        [UnityTest]
        public IEnumerator V3SaltwindRunScene_BossVictoryThenPlayerDeath_SettlesOnce()
        {
            yield return SaltwindScenePlayModeSupport.LoadSaltwindRunUntilPlaying();

            AppRoot.EnsureInitialized();
            var marksBefore = AppRoot.Instance.Save.Meta.marks;

            var poi = Object.FindFirstObjectByType<POIManager>();
            var enemyWorld = Object.FindFirstObjectByType<Enemies.EnemyWorld>();
            var player = Object.FindFirstObjectByType<PlayerShipController>();
            Assert.NotNull(poi);
            Assert.NotNull(enemyWorld);
            Assert.NotNull(player);

            var summon = new BossSummonAttemptResult();
            yield return SaltwindScenePlayModeSupport.TrySummonBossWithRetries(poi, summon);
            Assert.IsTrue(summon.Summoned);

            var wait = new BossAgentWaitResult();
            yield return SaltwindScenePlayModeSupport.WaitForActiveBossAgent(enemyWorld, wait);
            SaltwindScenePlayModeSupport.ApplyLethalWeaponDamage(enemyWorld, wait.AgentIndex);
            yield return null;

            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured);
            Assert.IsTrue(RunManager.Instance.ObjectiveSecured);

            player.Damage.TakeDamage(99999f, Vector3.zero, false);
            var deadline = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (RunManager.Instance.IsRunEnded && SaltwindRunSettlement.LastSettlementApplied)
                    break;
                yield return null;
            }

            Assert.IsTrue(RunManager.Instance.IsRunEnded);
            Assert.IsTrue(SaltwindRunSettlement.LastSettlementApplied);
            Assert.Greater(SaltwindRunSettlement.LastBreakdown.BossVictoryCredits, 0);
            Assert.AreEqual(marksBefore + SaltwindRunSettlement.LastMarksAwarded, AppRoot.Instance.Save.Meta.marks);

            var marksAfterFirst = AppRoot.Instance.Save.Meta.marks;
            RunManager.Instance.FinalizeRunOnDeath();
            Assert.AreEqual(marksAfterFirst, AppRoot.Instance.Save.Meta.marks);
        }
    }
}
