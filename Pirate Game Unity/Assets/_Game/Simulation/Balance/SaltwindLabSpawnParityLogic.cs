using System;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>Export-driven ambient spawn quotes for Balance Lab (mirrors SaltwindStageTimelineLogic bands).</summary>
    public static class SaltwindLabSpawnParityLogic
    {
        public static float GetAmbientRateAtTime(StageBalanceProfileExport export, float elapsedSeconds)
        {
            if (export?.Spawn == null) return 0f;
            var rates = export.Spawn.AmbientRatePerSecond;
            if (rates == null || rates.Length == 0) return 0f;
            var bandDuration = Math.Max(0.01f, export.Spawn.AmbientBandDurationSeconds);
            var band = (int)Math.Floor(elapsedSeconds / bandDuration);
            band = Math.Clamp(band, 0, rates.Length - 1);
            return rates[band];
        }

        public static float EffectiveAmbientSpawnsPerSecond(
            StageBalanceProfileExport export,
            float elapsedSeconds,
            bool bossFightActive)
        {
            if (export?.Timing == null || export.Spawn == null) return 0f;
            if (bossFightActive)
                return 0f;

            if (elapsedSeconds >= export.Timing.StageDeadlineSeconds)
                return 0f;

            var rate = GetAmbientRateAtTime(export, elapsedSeconds);
            if (IsSwarmPulseActive(export, elapsedSeconds))
                rate *= export.Spawn.SwarmIntensityMultiplier;
            return Math.Max(0f, rate);
        }

        public static bool IsSwarmPulseActive(StageBalanceProfileExport export, float elapsedSeconds)
        {
            if (export?.Spawn == null) return false;
            var duration = export.Spawn.SwarmDurationSeconds;
            if (IsWithinPulse(elapsedSeconds, export.Spawn.SwarmStartSecondsA, duration)) return true;
            return IsWithinPulse(elapsedSeconds, export.Spawn.SwarmStartSecondsB, duration);
        }

        static bool IsWithinPulse(float elapsedSeconds, float startSeconds, float durationSeconds)
        {
            if (durationSeconds <= 0f) return false;
            return elapsedSeconds >= startSeconds && elapsedSeconds < startSeconds + durationSeconds;
        }
    }
}
