using System;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    public readonly struct RunMarksBreakdown
    {
        public int QualifyingOrdinaryMinutes { get; }
        public int SpecialPoiCredits { get; }
        public int ScheduledEliteCredits { get; }
        public int BossVictoryCredits { get; }
        public int OvertimeMinuteCredits { get; }
        public int TotalMarks { get; }

        public RunMarksBreakdown(
            int qualifyingOrdinaryMinutes,
            int specialPoiCredits,
            int scheduledEliteCredits,
            int bossVictoryCredits,
            int overtimeMinuteCredits,
            int totalMarks)
        {
            QualifyingOrdinaryMinutes = qualifyingOrdinaryMinutes;
            SpecialPoiCredits = specialPoiCredits;
            ScheduledEliteCredits = scheduledEliteCredits;
            BossVictoryCredits = bossVictoryCredits;
            OvertimeMinuteCredits = overtimeMinuteCredits;
            TotalMarks = totalMarks;
        }
    }

    public readonly struct RunMarksRecord
    {
        public string RunId { get; }
        public int QualifyingOrdinaryMinutes { get; }
        public int SpecialPoiCredits { get; }
        public int ScheduledEliteKillCredits { get; }
        public bool BossVictoryCredited { get; }
        public int QualifyingOvertimeMinutes { get; }

        public bool ScheduledEliteCredited => ScheduledEliteKillCredits > 0;

        public RunMarksRecord(
            string runId,
            int qualifyingOrdinaryMinutes,
            int specialPoiCredits,
            int scheduledEliteKillCredits,
            bool bossVictoryCredited,
            int qualifyingOvertimeMinutes)
        {
            RunId = runId ?? string.Empty;
            QualifyingOrdinaryMinutes = qualifyingOrdinaryMinutes;
            SpecialPoiCredits = specialPoiCredits;
            ScheduledEliteKillCredits = scheduledEliteKillCredits;
            BossVictoryCredited = bossVictoryCredited;
            QualifyingOvertimeMinutes = qualifyingOvertimeMinutes;
        }

        public RunMarksRecord(
            string runId,
            int qualifyingOrdinaryMinutes,
            int specialPoiCredits,
            bool scheduledEliteCredited,
            bool bossVictoryCredited,
            int qualifyingOvertimeMinutes)
            : this(
                runId,
                qualifyingOrdinaryMinutes,
                specialPoiCredits,
                scheduledEliteCredited ? 1 : 0,
                bossVictoryCredited,
                qualifyingOvertimeMinutes)
        {
        }

        public static RunMarksRecord Empty(string runId = null) => new(
            runId ?? string.Empty,
            0,
            0,
            false,
            false,
            0);
    }

    /// <summary>Marks settlement math for solo Saltwind (Step 09 / C09).</summary>
    public static class SaltwindStageSettlementLogic
    {
        public const int ApprovedFixtureTotalMarks = 57;

        public static RunMarksBreakdown ComputeBreakdown(
            StageBalanceProfileExport export,
            RunMarksRecord record)
        {
            if (export?.Economy == null)
                return new RunMarksBreakdown(0, 0, 0, 0, 0, 0);

            var ordinary = record.QualifyingOrdinaryMinutes * export.Economy.MarksPerQualifyingMinute;
            var special = record.SpecialPoiCredits * export.Economy.MarksPerSpecialPoi;
            var elite = record.ScheduledEliteKillCredits * export.Economy.MarksPerScheduledElite;
            var boss = record.BossVictoryCredited ? export.Economy.MarksBossVictory : 0;
            var otRaw = record.QualifyingOvertimeMinutes * export.Economy.MarksPerOvertimeMinute;
            var ot = Math.Min(otRaw, export.Economy.MarksOvertimeCap);

            var total = ordinary + special + elite + boss + ot;
            return new RunMarksBreakdown(ordinary, special, elite, boss, ot, total);
        }

        public static int ComputeTotalMarks(StageBalanceProfileExport export, RunMarksRecord record) =>
            ComputeBreakdown(export, record).TotalMarks;

        public static bool QualifiesOrdinaryMinute(int killsInMinute, int minKillsRequired) =>
            killsInMinute >= minKillsRequired;

        public static bool QualifiesOvertimeMinute(int killsInMinute, int minKillsRequired) =>
            killsInMinute >= minKillsRequired;

        public static RunMarksRecord BuildFixtureRecord(string runId = "fixture") => new(
            runId,
            5,
            2,
            2,
            true,
            2);

        public static int TryGetWeaponUnlockCost(StageBalanceProfileExport export, string contentId)
        {
            if (export?.WeaponUnlocks == null || string.IsNullOrEmpty(contentId)) return -1;
            for (var i = 0; i < export.WeaponUnlocks.Count; i++)
            {
                var entry = export.WeaponUnlocks[i];
                if (entry != null && entry.ContentId == contentId)
                    return entry.MarksCost;
            }

            return -1;
        }

        public static int TryGetContentUnlockCost(StageBalanceProfileExport export, string contentId)
        {
            var weaponCost = TryGetWeaponUnlockCost(export, contentId);
            if (weaponCost >= 0) return weaponCost;
            if (export?.CaptainUnlocks == null || string.IsNullOrEmpty(contentId)) return -1;
            for (var i = 0; i < export.CaptainUnlocks.Count; i++)
            {
                var entry = export.CaptainUnlocks[i];
                if (entry != null && entry.ContentId == contentId)
                    return entry.MarksCost;
            }

            return -1;
        }
    }
}
