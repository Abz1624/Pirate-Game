using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RoguePirates.Simulation.Balance
{
    public enum BalanceRegressionPreset { FirstThreeMinutes, MinuteFourPulse, MinuteSevenPulse, BossArrival, Overtime }

    /// <summary>Named repeatable checks that make a tuning regression visible without rebuilding a scenario by hand.</summary>
    public static class BalanceLabPresets
    {
        public static BalanceLabScenario Apply(BalanceLabScenario source, BalanceRegressionPreset preset)
        {
            var scenario = source?.Clone() ?? new BalanceLabScenario();
            scenario.Name = "Regression — " + preset;
            scenario.Runs = Math.Max(100, scenario.Runs);
            switch (preset)
            {
                case BalanceRegressionPreset.FirstThreeMinutes: scenario.DurationSeconds = 180f; scenario.BossSpawnSeconds = 900f; break;
                case BalanceRegressionPreset.MinuteFourPulse: scenario.DurationSeconds = 270f; scenario.BossSpawnSeconds = 900f; break;
                case BalanceRegressionPreset.MinuteSevenPulse: scenario.DurationSeconds = 450f; scenario.BossSpawnSeconds = 900f; break;
                case BalanceRegressionPreset.BossArrival: scenario.DurationSeconds = Math.Max(620f, scenario.BossSpawnSeconds + 20f); break;
                case BalanceRegressionPreset.Overtime: scenario.DurationSeconds = 900f; scenario.BossSpawnSeconds = 600f; break;
            }
            return scenario;
        }
    }

    /// <summary>Small, dependency-free bridge between RunCombatTelemetry JSON and deterministic lab summaries.</summary>
    public sealed class BalanceLiveTelemetry
    {
        public float DamageTaken;
        public float PeakEnemies;
        public float TimeToWeapon2;
        public int GoldEarned;
        public int GoldSpent;
        public int TimelineEventCount;
        public float FirstEventSeconds;
        public float LastEventSeconds;

        public static BalanceLiveTelemetry Parse(string json)
        {
            var result = new BalanceLiveTelemetry();
            if (string.IsNullOrEmpty(json)) return result;
            result.DamageTaken = Number(json, "damageTaken");
            result.PeakEnemies = Number(json, "peakEnemyCount");
            result.TimeToWeapon2 = Number(json, "timeToWeapon2");
            result.GoldEarned = (int)Number(json, "goldEarned");
            result.GoldSpent = (int)Number(json, "goldSpent");
            var times = Regex.Matches(json, "\\\"t\\\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)");
            result.TimelineEventCount = times.Count;
            if (times.Count > 0)
            {
                result.FirstEventSeconds = ParseNumber(times[0].Groups[1].Value);
                result.LastEventSeconds = ParseNumber(times[times.Count - 1].Groups[1].Value);
            }
            return result;
        }

        static float Number(string json, string key)
        {
            var match = Regex.Match(json, "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)");
            return match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        static float ParseNumber(string value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
    }

    public sealed class BalanceCalibrationReport
    {
        public float PredictedGold;
        public float ActualGold;
        public float PredictedPeakEnemies;
        public float ActualPeakEnemies;
        public float GoldError;
        public float PeakEnemyError;
        public int LiveTimelineEvents;

        public string ToHumanSummary() =>
            $"Calibration: gold sim {PredictedGold:F1} / live {ActualGold:F1} ({GoldError:+0.0;-0.0;0.0}); " +
            $"peak enemies sim {PredictedPeakEnemies:F1} / live {ActualPeakEnemies:F1} ({PeakEnemyError:+0.0;-0.0;0.0}); live timeline events {LiveTimelineEvents}.";
    }

    public static class BalanceLabCalibration
    {
        public static BalanceCalibrationReport Compare(BalanceLabSummary simulation, BalanceLiveTelemetry live) => new()
        {
            PredictedGold = simulation?.AverageGold ?? 0f,
            ActualGold = (live?.GoldEarned ?? 0) - (live?.GoldSpent ?? 0),
            PredictedPeakEnemies = simulation?.AveragePeakEnemies ?? 0f,
            ActualPeakEnemies = live?.PeakEnemies ?? 0f,
            GoldError = (simulation?.AverageGold ?? 0f) - ((live?.GoldEarned ?? 0) - (live?.GoldSpent ?? 0)),
            PeakEnemyError = (simulation?.AveragePeakEnemies ?? 0f) - (live?.PeakEnemies ?? 0f),
            LiveTimelineEvents = live?.TimelineEventCount ?? 0
        };
    }
}
