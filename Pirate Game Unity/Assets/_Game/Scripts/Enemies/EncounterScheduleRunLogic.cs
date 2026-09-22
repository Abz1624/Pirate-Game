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

        /// <summary>
        /// Saltwind ambient bands: each schedule entry is active in [start, nextStart) or until stage deadline.
        /// </summary>
        public static float ResolveScheduleEntryEndSeconds(
            ScheduledSpawnEntry[] entries,
            int entryIndex,
            float stageDeadlineSeconds)
        {
            if (entries == null || entryIndex < 0)
                return stageDeadlineSeconds;

            for (var j = entryIndex + 1; j < entries.Length; j++)
            {
                var next = entries[j];
                if (next?.profile != null)
                    return next.startTimeSeconds;
            }

            return stageDeadlineSeconds;
        }

        public static bool IsWithinScheduleWindow(float runTimeSeconds, float windowStart, float windowEnd) =>
            runTimeSeconds >= windowStart && runTimeSeconds < windowEnd;

        public static bool IsScheduledContinuousWindowComplete(
            float runTimeSeconds,
            float windowStart,
            float windowEnd,
            SpawnProfile profile,
            float elapsedInWindow,
            int spawnedCount)
        {
            if (runTimeSeconds >= windowEnd)
                return true;

            if (profile == null)
                return true;

            if (profile.spawnMode != SpawnMode.Continuous)
                return IsProfileRunComplete(profile, elapsedInWindow, spawnedCount);

            if (profile.timing.durationSeconds > 0f)
                return elapsedInWindow >= profile.timing.durationSeconds;

            return false;
        }

        /// <summary>How many authored ambient band windows contain runTime (expect 0 or 1 when windows are exclusive).</summary>
        public static int CountActiveBandWindows(float runTimeSeconds, float[] bandStartSeconds, float stageDeadlineSeconds)
        {
            if (bandStartSeconds == null || bandStartSeconds.Length == 0)
                return 0;

            var count = 0;
            for (var i = 0; i < bandStartSeconds.Length; i++)
            {
                var end = i + 1 < bandStartSeconds.Length
                    ? bandStartSeconds[i + 1]
                    : stageDeadlineSeconds;
                if (IsWithinScheduleWindow(runTimeSeconds, bandStartSeconds[i], end))
                    count++;
            }

            return count;
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
