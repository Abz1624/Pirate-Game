using System;
using HighSeas.Balance;
using RoguePirates.Simulation.Balance;

namespace HighSeas.Balance
{
    /// <summary>Fixed-seed checkpoints shared by runtime helpers and Balance Lab quotes (R9).</summary>
    public readonly struct SaltwindLabTraceCheckpoint
    {
        public float ElapsedSeconds { get; }
        public int ChestPrice { get; }
        public int XpToNextLevel { get; }
        public float AmbientRate { get; }
        public float OvertimeSpawnInterval { get; }
        public float StormSafeScale { get; }

        public SaltwindLabTraceCheckpoint(
            float elapsedSeconds,
            int chestPrice,
            int xpToNextLevel,
            float ambientRate,
            float overtimeSpawnInterval,
            float stormSafeScale)
        {
            ElapsedSeconds = elapsedSeconds;
            ChestPrice = chestPrice;
            XpToNextLevel = xpToNextLevel;
            AmbientRate = ambientRate;
            OvertimeSpawnInterval = overtimeSpawnInterval;
            StormSafeScale = stormSafeScale;
        }
    }

    public static class SaltwindLabTraceParityLogic
    {
        public static SaltwindLabTraceCheckpoint BuildRuntimeCheckpoint(
            StageBalanceProfileExport export,
            float elapsedSeconds,
            int chestPurchases,
            int playerLevel)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            var overtime = Math.Max(0f, elapsedSeconds - export.Timing.StageDeadlineSeconds);
            return new SaltwindLabTraceCheckpoint(
                elapsedSeconds,
                SaltwindStageEconomyLogic.QuoteChestPrice(export, chestPurchases),
                (int)SaltwindStageProgressionLogic.XpToNext(export, playerLevel),
                SaltwindStageTimelineLogic.GetAmbientRateAtTime(export, elapsedSeconds),
                SaltwindFinaleLogic.GetOvertimeSpawnInterval(elapsedSeconds, export),
                SaltwindFinaleLogic.GetStormSafeRegionScale(elapsedSeconds, export));
        }

        public static SaltwindLabTraceCheckpoint BuildLabCheckpoint(
            StageBalanceProfileExport export,
            float elapsedSeconds,
            int chestPurchases,
            int playerLevel)
        {
            if (export == null) throw new ArgumentNullException(nameof(export));
            var overtime = Math.Max(0f, elapsedSeconds - export.Timing.StageDeadlineSeconds);
            return new SaltwindLabTraceCheckpoint(
                elapsedSeconds,
                SaltwindLabRuntimeParityLogic.QuoteChestPrice(export, chestPurchases),
                SaltwindLabRuntimeParityLogic.QuoteXpToNext(export, playerLevel),
                SaltwindStageTimelineLogic.GetAmbientRateAtTime(export, elapsedSeconds),
                SaltwindLabRuntimeParityLogic.QuoteOvertimeSpawnInterval(export, overtime),
                SaltwindFinaleLogic.GetStormSafeRegionScale(elapsedSeconds, export));
        }

        public static bool CheckpointsMatch(SaltwindLabTraceCheckpoint runtime, SaltwindLabTraceCheckpoint lab) =>
            runtime.ChestPrice == lab.ChestPrice &&
            runtime.XpToNextLevel == lab.XpToNextLevel &&
            Math.Abs(runtime.AmbientRate - lab.AmbientRate) < 0.001f &&
            Math.Abs(runtime.OvertimeSpawnInterval - lab.OvertimeSpawnInterval) < 0.001f &&
            Math.Abs(runtime.StormSafeScale - lab.StormSafeScale) < 0.001f;
    }
}
