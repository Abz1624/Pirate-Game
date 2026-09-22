using UnityEngine;

namespace HighSeas.Enemies
{
    /// <summary>Deterministic schedule helpers shared by EncounterSpawner and EditMode tests.</summary>
    public static class EncounterScheduleRunLogic
    {
        public struct LoopState
        {
            public float ScheduledStartSeconds;
            public float Elapsed;
            public int Spawned;
            public int LoopCycleIndex;
            public float NextSpawnAt;
            public float NextAttemptAt;
            public bool Complete;
        }

        public static bool IsProfileRunComplete(SpawnProfile profile, float elapsedSeconds, int spawnedCount)
        {
            if (profile == null) return true;
            var timing = profile.timing;
            if (profile.spawnMode == SpawnMode.Continuous)
            {
                if (timing.durationSeconds <= 0f) return false;
                return elapsedSeconds >= timing.durationSeconds;
            }

            return spawnedCount >= timing.spawnCount;
        }

        public static float GetContinuousSpawnInterval(
            SpawnProfile profile,
            float globalMinGap,
            float spawnRateMultiplier)
        {
            if (profile == null) return globalMinGap;
            var mult = Mathf.Max(0.1f, spawnRateMultiplier);
            return Mathf.Max(globalMinGap, profile.timing.spawnIntervalSeconds / mult);
        }

        /// <summary>
        /// Restarts a looping schedule entry. Returns false if loop is disabled or the cycle window has expired.
        /// </summary>
        public static bool TryRestartLoopCycle(
            ScheduledSpawnEntry entry,
            SpawnProfile profile,
            float runTime,
            ref LoopState state)
        {
            if (entry == null || profile == null || !entry.loop || entry.loopIntervalSeconds <= 0f)
                return false;

            state.LoopCycleIndex++;
            var cycleStart = state.ScheduledStartSeconds + state.LoopCycleIndex * entry.loopIntervalSeconds;
            if (runTime > cycleStart + entry.loopIntervalSeconds)
                return false;

            state.Complete = false;
            state.Spawned = 0;
            state.Elapsed = 0f;
            state.ScheduledStartSeconds = cycleStart;
            state.NextSpawnAt = cycleStart + profile.timing.startDelaySeconds;
            state.NextAttemptAt = state.NextSpawnAt;
            return true;
        }
    }
}
