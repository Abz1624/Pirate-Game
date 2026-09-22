using HighSeas.Combat;
using HighSeas.Core;

namespace HighSeas.Balance
{
    /// <summary>Applies Marks settlement once per run at death (Step 09 / C09).</summary>
    public static class SaltwindRunSettlement
    {
        public static RunMarksBreakdown LastBreakdown { get; private set; }
        public static int LastMarksAwarded { get; private set; }
        public static bool LastSettlementApplied { get; private set; }

        public static void ClearLastResults()
        {
            LastBreakdown = default;
            LastMarksAwarded = 0;
            LastSettlementApplied = false;
        }

        public static bool TrySettleCurrentRun(SaveService save)
        {
            if (!StageBalanceProfileContext.IsActive || save == null)
                return false;

            var export = StageBalanceProfileContext.Export;
            if (export == null) return false;

            var record = RunMarksTracker.BuildRecord();
            var breakdown = SaltwindStageSettlementLogic.ComputeBreakdown(export, record);
            LastBreakdown = breakdown;
            LastMarksAwarded = breakdown.TotalMarks;

            if (string.IsNullOrEmpty(record.RunId))
                return false;

            if (!save.TrySettleRun(record.RunId, breakdown.TotalMarks))
            {
                LastSettlementApplied = false;
                return false;
            }

            LastSettlementApplied = true;
            RunCombatTelemetry.Instance?.RecordMarksSettlement(
                breakdown.TotalMarks,
                true,
                breakdown.TotalMarks,
                breakdown.QualifyingOrdinaryMinutes,
                breakdown.SpecialPoiCredits,
                breakdown.ScheduledEliteCredits,
                breakdown.BossVictoryCredits,
                breakdown.OvertimeMinuteCredits);
            return true;
        }
    }
}
