using HighSeas.Enemies;
using NUnit.Framework;
using UnityEngine;

namespace HighSeas.Tests
{
    public sealed class EncounterScheduleRunLogicTests
    {
        [Test]
        public void ContinuousProfile_CompletesOnElapsedDuration_NotDensityScaled()
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.spawnMode = SpawnMode.Continuous;
            profile.timing.durationSeconds = 10f;

            Assert.IsFalse(EncounterScheduleRunLogic.IsProfileRunComplete(profile, 9.9f, 0));
            Assert.IsTrue(EncounterScheduleRunLogic.IsProfileRunComplete(profile, 10f, 0));
        }

        [Test]
        public void BurstProfile_CompletesOnActualSpawnCount()
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.spawnMode = SpawnMode.Burst;
            profile.timing.spawnCount = 10;

            Assert.IsFalse(EncounterScheduleRunLogic.IsProfileRunComplete(profile, 0f, 3));
            Assert.IsTrue(EncounterScheduleRunLogic.IsProfileRunComplete(profile, 0f, 10));
        }

        [Test]
        public void ContinuousInterval_DensitySlowsCadenceOnly()
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.spawnMode = SpawnMode.Continuous;
            profile.timing.spawnIntervalSeconds = 2f;

            var normal = EncounterScheduleRunLogic.GetContinuousSpawnInterval(profile, 0.3f, 1f);
            var dense = EncounterScheduleRunLogic.GetContinuousSpawnInterval(profile, 0.3f, 2f);

            Assert.AreEqual(2f, normal, 0.001f);
            Assert.AreEqual(1f, dense, 0.001f);
        }

        [Test]
        public void LoopCycle_RestartsWithinWindow_SkipsExpiredCycle()
        {
            var profile = ScriptableObject.CreateInstance<SpawnProfile>();
            profile.timing.startDelaySeconds = 0f;
            var entry = new ScheduledSpawnEntry
            {
                profile = profile,
                startTimeSeconds = 0f,
                loop = true,
                loopIntervalSeconds = 30f
            };

            var state = new EncounterScheduleRunLogic.LoopState
            {
                ScheduledStartSeconds = 0f,
                LoopCycleIndex = 0,
                Spawned = 5,
                Elapsed = 30f,
                Complete = true
            };

            Assert.IsTrue(EncounterScheduleRunLogic.TryRestartLoopCycle(entry, profile, 25f, ref state));
            Assert.AreEqual(1, state.LoopCycleIndex);
            Assert.AreEqual(0, state.Spawned);
            Assert.IsFalse(state.Complete);

            state.LoopCycleIndex = 0;
            state.ScheduledStartSeconds = 0f;
            state.Complete = true;
            Assert.IsFalse(EncounterScheduleRunLogic.TryRestartLoopCycle(entry, profile, 95f, ref state));
        }
    }
}
