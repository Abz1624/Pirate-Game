using System;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>Compares runtime Saltwind formulas with lab/sim helpers (Step 12 / C12).</summary>
    public static class SaltwindLabRuntimeParityLogic
    {
        public static int QuoteChestPrice(StageBalanceProfileExport export, int purchaseIndex) =>
            StageBalanceProfileExportCodec.ChestPriceForPurchaseIndex(export, purchaseIndex);

        public static int QuoteXpToNext(StageBalanceProfileExport export, int level) =>
            StageBalanceProfileExportCodec.XpToNextLevel(export, level);

        public static float QuoteOvertimeSpawnInterval(StageBalanceProfileExport export, float overtimeSeconds) =>
            StageBalanceProfileExportCodec.OvertimeSpawnInterval(export, overtimeSeconds);

        public static float QuoteStageDeadline(StageBalanceProfileExport export) =>
            export?.Timing?.StageDeadlineSeconds ?? 600f;

        public static bool SchedulesMatchSaltwindPressureBeats(float elapsedSeconds, float pulseDuration)
        {
            var first = elapsedSeconds >= 240f && elapsedSeconds < 240f + pulseDuration;
            var second = elapsedSeconds >= 420f && elapsedSeconds < 420f + pulseDuration;
            return first || second;
        }
    }
}
