using System;
using System.Text;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>Documented lab win-rate bands and per-skill survivability tuning (Codex R9 calibration).</summary>
    public static class SaltwindLabMatrixCalibration
    {
        public const float ExpertMarksHullFactorDefault = 0.36f;
        public const float ExpertMarksHullFactorAreaControl = 0.47f;
        public const float ExpertMarksBossHullScale = 3.10f;
        public const float AverageMarksBossHullScale = 2.05f;
        /// <summary>Post boss-arena snap: conservative finale is reachable; was 0.76 when kite drift blocked all boss damage.</summary>
        public const float ConservativeBossHullScale = 1.28f;
        public const float ConservativeMarksBossHullScale = 1.12f;
        public const float AverageBossHullScale = 0.92f;
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

        public static float BossHullMultiplierForSkill(
            BalanceSkillPreset skill,
            SaltwindLabUnlockPool pool,
            SaltwindLabBuildPolicy policy)
        {
            switch (skill)
            {
                case BalanceSkillPreset.Conservative:
                {
                    var mult = ConservativeBossHullScale;
                    if (pool == SaltwindLabUnlockPool.MarksUnlocked)
                        mult *= ConservativeMarksBossHullScale;
                    return mult;
                }
                case BalanceSkillPreset.Average:
                {
                    var mult = AverageBossHullScale;
                    if (pool == SaltwindLabUnlockPool.MarksUnlocked)
                        mult *= AverageMarksBossHullScale;
                    return mult;
                }
                case BalanceSkillPreset.Expert:
                    return pool == SaltwindLabUnlockPool.MarksUnlocked ? ExpertMarksBossHullScale : 1f;
                default:
                    return 1f;
            }
        }

        public static SimInputPolicy InputPolicyForSkill(BalanceSkillPreset skill) => skill switch
        {
            BalanceSkillPreset.Conservative => SimInputPolicy.Kite,
            BalanceSkillPreset.Average => SimInputPolicy.Orbit,
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
            "Average between Expert and Conservative. Movement: Conservative kite pre-boss; boss finale orbit for all skills; Average/Expert orbit in ambient.";

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
