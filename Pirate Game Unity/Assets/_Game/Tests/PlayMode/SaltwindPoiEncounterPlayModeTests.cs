using System.Collections;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.POI;
using HighSeas.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HighSeas.Tests
{
    public sealed class SaltwindPoiEncounterPlayModeTests
    {
        PlayModeSaveIsolation _saveIsolation;
        SaltwindPlayModeTestSupport _harness;
        POIManager _poi;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveIsolation = PlayModeSaveIsolation.Begin();
            _harness = SaltwindPlayModeTestSupport.Create(99001);
            _poi = _harness.CreatePoiManager();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _poi = null;
            _harness?.Dispose();
            _harness = null;
            _saveIsolation?.Dispose();
            _saveIsolation = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator BossBlockedThenRetry_SpawnsTrackedBoss()
        {
            var cap = _harness.Spawner.GetEnemyCapForDebug();
            _harness.FillActiveEnemiesToCount(cap);

            var failed = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            var profile = _harness.CreateEncounterProfile(1);
            profile.enemy.isBoss = true;
            _harness.Spawner.TriggerProfile(profile, 1, failed.EncounterId);
            RunEncounterTracker.MarkFailed(failed.EncounterId);
            _harness.Spawner.CancelPendingEncounter(failed.EncounterId);
            while (_harness.EnemyWorld.ActiveCount >= cap)
                Assert.IsTrue(_harness.EnemyWorld.TryDespawnOldestActive(out _), "Need a free hard-cap slot before boss retry spawn.");

            var boss = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            var spawned = _harness.Spawner.TriggerProfileImmediate(profile, 1, boss.EncounterId);
            Assert.Greater(spawned, 0);
            boss = RunEncounterTracker.Get(boss.EncounterId);
            var agentId = FirstAgent(boss);
            Assert.IsTrue(RunEncounterTracker.RegisterAgentKill(agentId));
            Assert.IsTrue(RunEncounterTracker.BossObjectiveSecured);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PendingBossQueue_DoesNotSpawnAfterEncounterFailed()
        {
            var encounter = RunEncounterTracker.Request(RunEncounterKind.Boss, 1);
            var profile = _harness.CreateEncounterProfile(1);
            _harness.Spawner.TriggerProfile(profile, 1, encounter.EncounterId);
            RunEncounterTracker.MarkFailed(encounter.EncounterId);

            var processed = _harness.Spawner.ProcessPendingEncountersNow();
            Assert.AreEqual(0, processed);
            encounter = RunEncounterTracker.Get(encounter.EncounterId);
            Assert.AreEqual(RunEncounterState.Failed, encounter.State);
            Assert.AreEqual(0, encounter.SpawnedCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PowderCache_RepeatedCompleteAttempt_KeepsEncounterId()
        {
            var db = GameDatabaseLoader.Load();
            var layout = new StageLayout
            {
                playerSpawn = Vector3.zero,
                pois = { new POISlot { type = POIType.MerchantSkiff, position = new Vector3(4f, 0f, 0f) } }
            };
            var schedule = ScriptableObject.CreateInstance<EncounterSpawnSchedule>();
            schedule.powderCacheAmbush = _harness.CreateEncounterProfile(3);
            _harness.Spawner.SetSchedule(schedule);
            _poi.Initialize(layout, db);

            _poi.TickPowderCacheProximityAt(new Vector3(4f, 0f, 0f), 0);
            var firstId = _poi.DebugPowderCacheEncounterId;
            Assert.Greater(firstId, 0);

            _poi.TickPowderCacheProximityAt(new Vector3(4f, 0f, 0f), 0);
            Assert.AreEqual(firstId, _poi.DebugPowderCacheEncounterId);

            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerPrefsMeta_UnchangedAfterFixtures()
        {
            var before = PlayModeSaveIsolation.SnapshotProductionMetaJson();
            yield return null;
            Assert.AreEqual(before, PlayModeSaveIsolation.SnapshotProductionMetaJson());
        }

        static int FirstAgent(RunEncounterRecord record)
        {
            foreach (var id in record.SpawnedAgentIds)
                return id;
            return -1;
        }
    }
}
