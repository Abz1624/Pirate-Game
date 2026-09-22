using UnityEngine;

namespace HighSeas.Balance
{
    /// <summary>Profile-driven Saltwind pressure beats (elites + swarm pulses).</summary>
    public static class SaltwindPressureBeatLogic
    {
        public const float SpawnRetryWindowSeconds = 20f;

        public static float GetEliteBeatSeconds(StageBalanceProfile profile, int eliteIndex)
        {
            if (profile == null) return eliteIndex == 0 ? 180f : 480f;
            return eliteIndex == 0 ? profile.scheduledEliteSecondsA : profile.scheduledEliteSecondsB;
        }

        public static float GetSwarmStartSeconds(StageBalanceProfile profile, int pulseIndex)
        {
            if (profile == null) return pulseIndex == 0 ? 240f : 420f;
            return pulseIndex == 0 ? profile.swarmStartSecondsA : profile.swarmStartSecondsB;
        }

        public static float GetSwarmDurationSeconds(StageBalanceProfile profile) =>
            profile != null ? profile.swarmDurationSeconds : 30f;

        public static float GetSwarmMultiplier(StageBalanceProfile profile) =>
            profile != null ? profile.swarmIntensityMultiplier : 1.25f;

        public static float GetWarningSeconds(StageBalanceProfile profile) =>
            profile != null ? profile.eventWarningSeconds : 5f;

        public static bool ShouldShowEventWarning(float elapsed, float eventTime, float warningSeconds) =>
            elapsed >= eventTime - warningSeconds && elapsed < eventTime;

        /// <summary>Boss conflict: skip the scheduled event permanently (no post-boss defer).</summary>
        public static bool ShouldSkipScheduledEventForBoss(float elapsed, float beatTime, bool bossFightActive) =>
            bossFightActive && elapsed >= beatTime;

        public static bool ShouldStartSwarmPulse(
            float elapsed,
            StageBalanceProfile profile,
            int pulseIndex,
            bool bossFightActive,
            bool alreadyStarted)
        {
            if (alreadyStarted || bossFightActive) return false;
            return elapsed >= GetSwarmStartSeconds(profile, pulseIndex);
        }
    }
}
