using System;
using System.Collections.Generic;
using RoguePirates.Simulation.Combat;

namespace RoguePirates.Simulation.Balance
{
    public sealed class BalanceLabRunResult
    {
        public int Seed;
        public int Kills;
        public int Level;
        public bool ObjectiveSecured;
        public bool PlayerSurvived;
        public float SimSeconds;
        public int PeakEnemyCount;
        public int Gold;
        public int MerchantPurchases;
        public float TimeToDeathSeconds = -1f;
        public float BossKillSeconds = -1f;
        public readonly List<BalanceLabTimePoint> TimeSeries = new();
    }

    public sealed class BalanceLabTimePoint
    {
        public float Seconds;
        public float HullFraction;
        public int Kills;
        public int Level;
        public int Gold;
        public int EnemyCount;
    }

    public sealed class BalanceLabSummary
    {
        public string ScenarioName = string.Empty;
        public int Runs;
        public float WinRate;
        public float SurvivalRate;
        public float AverageKills;
        public float AverageLevel;
        public float AverageSeconds;
        public float AveragePeakEnemies;
        public float AverageGold;
        public float AverageMerchantPurchases;
        public float TimeToDeathP10;
        public float TimeToDeathP50;
        public float TimeToDeathP90;
        public float BossKillP50;
        public readonly List<BalanceLabRunResult> Samples = new();

        public string ToHumanSummary() =>
            $"{ScenarioName}: {Runs} runs | objective {WinRate:P0} | survival {SurvivalRate:P0} | " +
            $"kills {AverageKills:F1} | level {AverageLevel:F1} | gold {AverageGold:F1} | peak enemies {AveragePeakEnemies:F1}";

        public string Diagnostics()
        {
            if (SurvivalRate < .35f) return "Difficulty warning: fewer than 35% of simulated runs survive. Reduce early pressure or improve baseline damage/hull.";
            if (SurvivalRate > .95f && AveragePeakEnemies < 8f) return "Pacing warning: very safe runs with low screen pressure. Consider denser waves or stronger late-game roster weights.";
            if (AverageMerchantPurchases < .5f) return "Economy warning: the planned merchant purchase is rarely affordable.";
            if (WinRate < .2f && AverageSeconds >= 0.9f * 600f) return "Boss warning: boss conversion is low despite runs reaching the endgame.";
            return "Curve check: no automatic red flags. Compare this result with live telemetry and other skill presets.";
        }
    }

    public static class BalanceLabRunner
    {
        public static BalanceLabSummary Run(BalanceLabScenario scenario)
        {
            if (scenario == null) throw new ArgumentNullException(nameof(scenario));
            scenario = scenario.Clone();
            scenario.Normalize();

            var summary = new BalanceLabSummary { ScenarioName = scenario.Name, Runs = scenario.Runs };
            var ticks = Math.Max(1, (int)Math.Ceiling(scenario.DurationSeconds / SimConstants.TickDurationSeconds));
            for (var i = 0; i < scenario.Runs; i++)
            {
                var seed = scenario.Seed + i;
                var sim = new CombatSimulator(seed);
                sim.ApplyLabScenario(scenario);
                var timeSeries = new List<BalanceLabTimePoint>();
                for (var tick = 0; tick < ticks && sim.State != SimRunState.PlayerDead && sim.State != SimRunState.RunComplete; tick++)
                {
                    ApplyInput(sim, seed, tick, ResolveInputPolicy(scenario));
                    sim.Step(SimConstants.TickDurationSeconds);
                    if (tick % 900 == 0)
                        timeSeries.Add(new BalanceLabTimePoint
                        {
                            Seconds = sim.SimSeconds, HullFraction = sim.PlayerHullFraction, Kills = sim.Kills,
                            Level = sim.Level, Gold = sim.Gold, EnemyCount = sim.LivingEnemyCount
                        });
                }

                var result = new BalanceLabRunResult
                {
                    Seed = seed, Kills = sim.Kills, Level = sim.Level,
                    ObjectiveSecured = sim.ObjectiveSecured,
                    PlayerSurvived = sim.State != SimRunState.PlayerDead,
                    SimSeconds = sim.SimSeconds, PeakEnemyCount = sim.PeakEnemyCount,
                    Gold = sim.Gold, MerchantPurchases = sim.MerchantPurchasesCompleted,
                    TimeToDeathSeconds = sim.State == SimRunState.PlayerDead ? sim.SimSeconds : -1f,
                    BossKillSeconds = sim.ObjectiveSecuredSeconds
                };
                if (timeSeries.Count == 0 || timeSeries[timeSeries.Count - 1].Seconds < sim.SimSeconds)
                    timeSeries.Add(new BalanceLabTimePoint { Seconds = sim.SimSeconds, HullFraction = sim.PlayerHullFraction, Kills = sim.Kills, Level = sim.Level, Gold = sim.Gold, EnemyCount = sim.LivingEnemyCount });
                result.TimeSeries.AddRange(timeSeries);
                summary.Samples.Add(result);
                summary.WinRate += result.ObjectiveSecured ? 1f : 0f;
                summary.SurvivalRate += result.PlayerSurvived ? 1f : 0f;
                summary.AverageKills += result.Kills;
                summary.AverageLevel += result.Level;
                summary.AverageSeconds += result.SimSeconds;
                summary.AveragePeakEnemies += result.PeakEnemyCount;
                summary.AverageGold += result.Gold;
                summary.AverageMerchantPurchases += result.MerchantPurchases;
            }

            summary.WinRate /= scenario.Runs;
            summary.SurvivalRate /= scenario.Runs;
            summary.AverageKills /= scenario.Runs;
            summary.AverageLevel /= scenario.Runs;
            summary.AverageSeconds /= scenario.Runs;
            summary.AveragePeakEnemies /= scenario.Runs;
            summary.AverageGold /= scenario.Runs;
            summary.AverageMerchantPurchases /= scenario.Runs;
            var deaths = new List<float>();
            var bossKills = new List<float>();
            for (var i = 0; i < summary.Samples.Count; i++)
            {
                deaths.Add(summary.Samples[i].TimeToDeathSeconds < 0f ? scenario.DurationSeconds : summary.Samples[i].TimeToDeathSeconds);
                if (summary.Samples[i].BossKillSeconds >= 0f) bossKills.Add(summary.Samples[i].BossKillSeconds);
            }
            deaths.Sort(); bossKills.Sort();
            summary.TimeToDeathP10 = Percentile(deaths, .10f);
            summary.TimeToDeathP50 = Percentile(deaths, .50f);
            summary.TimeToDeathP90 = Percentile(deaths, .90f);
            summary.BossKillP50 = Percentile(bossKills, .50f);
            return summary;
        }

        static float Percentile(List<float> values, float percentile)
        {
            if (values.Count == 0) return 0f;
            return values[Math.Min(values.Count - 1, (int)MathF.Round((values.Count - 1) * percentile))];
        }

        static void ApplyInput(CombatSimulator sim, int seed, int tick, SimInputPolicy policy)
        {
            if (policy == SimInputPolicy.Kite)
            {
                sim.SetKiteInput();
                return;
            }
            if (policy == SimInputPolicy.Orbit)
            {
                var phase = (seed + tick) * 0.05f;
                sim.SetMoveInput(MathF.Cos(phase), MathF.Sin(phase));
            }
            else
                sim.SetMoveInput(1f, 0f);
        }

        static SimInputPolicy ResolveInputPolicy(BalanceLabScenario scenario) =>
            SaltwindLabMatrixCalibration.InputPolicyForSkill(scenario.SkillPreset);
    }
}
