namespace RoguePirates.Simulation.Balance
{
    /// <summary>Headless-friendly pass/fail entry point for a versioned balance scenario.</summary>
    public static class BalanceLabCiGate
    {
        public static bool MeetsTargets(BalanceLabScenario scenario, out string report)
        {
            var result = BalanceLabRunner.Run(scenario);
            var merchantRate = scenario.PlannedMerchantPurchases == 0 ? 1f : result.AverageMerchantPurchases / scenario.PlannedMerchantPurchases;
            var survival = result.SurvivalRate >= scenario.TargetAverageSurvivalMin && result.SurvivalRate <= scenario.TargetAverageSurvivalMax;
            var merchant = merchantRate >= scenario.TargetMerchantAffordabilityMin;
            var expertScenario = scenario.Clone();
            expertScenario.SkillPreset = BalanceSkillPreset.Expert;
            var expert = BalanceLabRunner.Run(expertScenario);
            var boss = expert.WinRate >= scenario.TargetExpertBossWinMin;
            report = BalanceLabAnalysis.TargetBandReport(result, scenario) + " Expert boss win " + expert.WinRate.ToString("P0") +
                " (target " + scenario.TargetExpertBossWinMin.ToString("P0") + "). " + BalanceLabAnalysis.PercentileReport(result);
            return survival && merchant && boss;
        }
    }
}
