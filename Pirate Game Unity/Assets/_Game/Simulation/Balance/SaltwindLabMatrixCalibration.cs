using System;
using System.Text;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>Documented lab win-rate bands and per-skill survivability tuning (Codex R9 calibration).</summary>
    public static class SaltwindLabMatrixCalibration
    {
        public const float ExpertMarksHullFactorDefault = 0.36f;
        public const float ExpertMarksHullFactorAreaControl = 0.50f;
        public const float ExpertMarksBossHullScale = 2.0f;
        public const float ExpertWinRateMin = 0.40f;
        public const float ExpertWinRateMax = 0.70f;
        public const float ConservativeWinRateMin = 0.55f;
        public const float ConservativeWinRateMax = 0.95f;

        public static float PlayerHullMultiplierForSkill(
            BalanceSkillPreset skill,
            SaltwindLabUnlockPool pool = SaltwindLabUnlockPool.FreshSave,
            SaltwindLabBuildPolicy buildPolicy = SaltwindLabBuildPolicy.None)
        {
            var hull = skill switch
            {
                BalanceSkillPreset.Conservative => 1.26f,
                BalanceSkillPreset.Average => 1.10f,
                BalanceSkillPreset.Expert => 1.08f,
                BalanceSkillPreset.Aggressive => 0.70f,
                _ => 1.08f
            };

            if (skill == BalanceSkillPreset.Expert && pool == SaltwindLabUnlockPool.MarksUnlocked)
                hull *= ExpertMarksHullFactor(buildPolicy);

            return hull;
        }

        public static float ExpertMarksHullFactor(SaltwindLabBuildPolicy policy) =>
            policy == SaltwindLabBuildPolicy.AreaControl
                ? ExpertMarksHullFactorAreaControl
                : ExpertMarksHullFactorDefault;

        public static SimInputPolicy InputPolicyForSkill(BalanceSkillPreset skill) => skill switch
        {
            BalanceSkillPreset.Conservative => SimInputPolicy.Kite,
            BalanceSkillPreset.Average => SimInputPolicy.Kite,
            BalanceSkillPreset.Aggressive => SimInputPolicy.Forward,
            BalanceSkillPreset.Expert => SimInputPolicy.Orbit,
            _ => SimInputPolicy.Orbit
        };

        public static bool IsWithinDocumentedBand(BalanceSkillPreset skill, float winRate) => skill switch
        {
            BalanceSkillPreset.Expert => winRate >= ExpertWinRateMin && winRate <= ExpertWinRateMax,
            BalanceSkillPreset.Conservative => winRate >= ConservativeWinRateMin && winRate <= ConservativeWinRateMax,
            _ => winRate >= ExpertWinRateMin && winRate <= ConservativeWinRateMax
        };

        public static string FormatTargetBandLegend() =>
            $"Lab target bands: Expert {ExpertWinRateMin:P0}–{ExpertWinRateMax:P0}; " +
            $"Conservative {ConservativeWinRateMin:P0}–{ConservativeWinRateMax:P0}; " +
            "Average between Expert and Conservative. Movement: Conservative/Average kite, Expert orbit.";

        public static void AppendTargetBandWarnings(SaltwindBalanceLabMatrixReport report, StringBuilder sb)
        {
            if (report?.Cells == null || sb == null) return;
            for (var i = 0; i < report.Cells.Count; i++)
            {
                var cell = report.Cells[i];
                if (IsWithinDocumentedBand(cell.Skill, cell.Simulation.WinRate)) continue;
                sb.Append("Band warning: ")
                    .Append(cell.BuildPolicy).Append('/').Append(cell.Skill).Append('/').Append(cell.UnlockPool)
                    .Append(" win ").Append(cell.Simulation.WinRate.ToString("P0"))
                    .Append(" outside documented band.").AppendLine();
            }
        }
    }
}
