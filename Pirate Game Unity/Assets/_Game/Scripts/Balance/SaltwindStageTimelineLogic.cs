using System;
using UnityEngine;

namespace HighSeas.Balance
{
    /// <summary>Deterministic Saltwind solo timeline helpers (profile fields + schedule deadline).</summary>
    public static class SaltwindStageTimelineLogic
    {
        public static int GetAmbientBandIndex(float elapsedSeconds, float bandDurationSeconds, int bandCount)
        {
            if (bandCount <= 0 || bandDurationSeconds <= 0f)
                return 0;
            var band = Mathf.FloorToInt(elapsedSeconds / bandDurationSeconds);
            return Mathf.Clamp(band, 0, bandCount - 1);
        }

        public static float GetAmbientRateForBand(StageBalanceProfile profile, int bandIndex)
        {
            if (profile?.ambientRatePerSecond == null || profile.ambientRatePerSecond.Length == 0)
                return 0f;
            bandIndex = Mathf.Clamp(bandIndex, 0, profile.ambientRatePerSecond.Length - 1);
            return profile.ambientRatePerSecond[bandIndex];
        }

        public static float GetAmbientRateAtTime(StageBalanceProfile profile, float elapsedSeconds)
        {
            if (profile == null) return 0f;
            var band = GetAmbientBandIndex(
                elapsedSeconds,
                profile.ambientBandDurationSeconds,
                profile.ambientRatePerSecond.Length);
            return GetAmbientRateForBand(profile, band);
        }

        public static bool ShouldTickAmbientSchedule(
            float elapsedSeconds,
            bool isOvertime,
            float stageDeadlineSeconds)
        {
            if (isOvertime) return false;
            return elapsedSeconds < stageDeadlineSeconds;
        }

        public static float GetEffectiveAmbientRateMultiplier(
            StageBalanceProfile profile,
            float elapsedSeconds,
            bool bossFightActive,
            float externalModifier)
        {
            var mult = Mathf.Max(0.1f, externalModifier);
            if (bossFightActive && profile != null)
                mult *= profile.bossAmbientRateMultiplier;
            return mult;
        }

        public static bool IsScheduledEliteMinute(float elapsedSeconds, StageBalanceProfile profile, int eliteIndex)
        {
            if (profile == null) return false;
            var target = eliteIndex == 0 ? profile.scheduledEliteSecondsA : profile.scheduledEliteSecondsB;
            return Mathf.Abs(elapsedSeconds - target) < 0.5f ||
                   (elapsedSeconds >= target && elapsedSeconds < target + 1f);
        }

        public static bool IsSwarmPulseStart(float elapsedSeconds, StageBalanceProfile profile, int pulseIndex)
        {
            if (profile == null) return false;
            var target = pulseIndex == 0 ? profile.swarmStartSecondsA : profile.swarmStartSecondsB;
            return elapsedSeconds >= target && elapsedSeconds < target + 1f;
        }

        public static int CountTimelineCheckpointsPassed(float elapsedSeconds, ReadOnlySpan<float> checkpoints)
        {
            var count = 0;
            for (var i = 0; i < checkpoints.Length; i++)
            {
                if (elapsedSeconds + 0.001f >= checkpoints[i])
                    count++;
            }

            return count;
        }
    }
}
