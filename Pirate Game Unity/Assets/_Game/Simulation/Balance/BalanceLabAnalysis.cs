using System;
using System.Collections.Generic;

namespace RoguePirates.Simulation.Balance
{
    public static class BalanceLabAnalysis
    {
        public static string Compare(BalanceLabSummary baseline, BalanceLabSummary candidate)
        {
            if (baseline == null || candidate == null) return "Comparison requires a baseline and a candidate batch.";
            var survivalDelta = candidate.SurvivalRate - baseline.SurvivalRate;
            var winDelta = candidate.WinRate - baseline.WinRate;
            var pressureDelta = candidate.AveragePeakEnemies - baseline.AveragePeakEnemies;
            var verdict = survivalDelta < -.05f || winDelta < -.05f ? "REGRESSION" : survivalDelta > .05f || winDelta > .05f ? "IMPROVEMENT" : "NEUTRAL";
            return $"Baseline vs candidate: survival {survivalDelta:+0.0%;-0.0%;0.0%}, boss win {winDelta:+0.0%;-0.0%;0.0%}, peak pressure {pressureDelta:+0.0;-0.0;0.0}; {verdict}.";
        }

        public static string PercentileReport(BalanceLabSummary summary) =>
            $"Seed reliability: time-to-death P10/P50/P90 = {summary.TimeToDeathP10:F0}/{summary.TimeToDeathP50:F0}/{summary.TimeToDeathP90:F0}s; boss-kill median {summary.BossKillP50:F0}s.";
        public static string TargetBandReport(BalanceLabSummary summary, BalanceLabScenario scenario)
        {
            var merchantRate = scenario.PlannedMerchantPurchases == 0 ? 1f : summary.AverageMerchantPurchases / scenario.PlannedMerchantPurchases;
            var survival = summary.SurvivalRate >= scenario.TargetAverageSurvivalMin && summary.SurvivalRate <= scenario.TargetAverageSurvivalMax;
            return $"Target bands: average survival {summary.SurvivalRate:P0} ({(survival ? "PASS" : "OUTSIDE")} {scenario.TargetAverageSurvivalMin:P0}–{scenario.TargetAverageSurvivalMax:P0}); " +
                   $"merchant affordability {merchantRate:P0} ({(merchantRate >= scenario.TargetMerchantAffordabilityMin ? "PASS" : "LOW")} ≥ {scenario.TargetMerchantAffordabilityMin:P0}).";
        }

        public static string SensitivityReport(BalanceLabScenario source)
        {
            var baseline = BalanceLabRunner.Run(source);
            var checks = new List<(string name, Func<BalanceLabScenario, BalanceLabScenario> change)>
            {
                ("player hull +10%", s => { s.PlayerHullMultiplier *= 1.1f; return s; }),
                ("weapon damage +10%", s => { s.WeaponDamageMultiplier *= 1.1f; return s; }),
                ("enemy hull +10%", s => { s.EnemyHullMultiplier *= 1.1f; return s; }),
                ("enemy damage +10%", s => { s.EnemyDamageMultiplier *= 1.1f; return s; }),
                ("spawn density +10%", s => { s.SpawnDensityMultiplier *= 1.1f; return s; })
            };
            var results = new List<(string name, float delta)>();
            for (var i = 0; i < checks.Count; i++)
            {
                var next = checks[i].change(source.Clone());
                var outcome = BalanceLabRunner.Run(next);
                results.Add((checks[i].name, outcome.SurvivalRate - baseline.SurvivalRate));
            }
            results.Sort((a, b) => Math.Abs(b.delta).CompareTo(Math.Abs(a.delta)));
            var text = "Sensitivity (survival-rate change): ";
            for (var i = 0; i < results.Count; i++) text += (i == 0 ? "" : " | ") + results[i].name + " " + results[i].delta.ToString("+0.0%;-0.0%;0.0%");
            return text;
        }

        public static string BuildDiversityReport(BalanceLabScenario scenario)
        {
            if (scenario.WeaponLoadout.Count == 0) return "Build diversity: using the expected-loadout proxy. Snapshot authored weapons to inspect the actual loadout.";
            var ids = new HashSet<string>();
            var duplicates = 0;
            for (var i = 0; i < scenario.WeaponLoadout.Count; i++) if (!ids.Add(scenario.WeaponLoadout[i].Id)) duplicates++;
            return $"Build diversity: {ids.Count} distinct weapons across {scenario.WeaponLoadout.Count} slots; duplicate slots {duplicates}. Use Analyze Offers to find never-picked/trap offer paths.";
        }
    }
}
