using System;

namespace RoguePirates.Simulation.Balance
{
    /// <summary>Route timing quotes aligned with V3 POI placement options (not live POIManager navigation).</summary>
    public static class SaltwindLabRouteParityLogic
    {
        public const float DefaultFirstChestTravelSeconds = 15f;
        public const float DefaultCruiseSpeed = 5.5f;

        public static float FirstChestTravelSeconds(StageBalanceProfileExport export) =>
            export?.Exploration?.FirstChestMaxTravelSeconds ?? DefaultFirstChestTravelSeconds;

        public static float RepresentativeCruiseSpeed(StageBalanceProfileExport export) =>
            export?.Exploration?.RepresentativeCruiseSpeed ?? DefaultCruiseSpeed;

        public static float SalvageChestCheckpointSeconds(
            StageBalanceProfileExport export,
            int chestIndex,
            float stageDurationSeconds)
        {
            if (chestIndex < 0) throw new ArgumentOutOfRangeException(nameof(chestIndex));
            var first = FirstChestTravelSeconds(export);
            if (chestIndex == 0) return first;

            var count = Math.Max(1, export?.Exploration?.SalvageChestCount ?? 12);
            var remaining = Math.Max(30f, stageDurationSeconds - first);
            var spacing = remaining / count;
            return first + chestIndex * spacing;
        }

        public static float RepairWreckCheckpointSeconds(
            int wreckIndex,
            int wreckCount,
            float stageDurationSeconds)
        {
            if (wreckCount <= 0) return stageDurationSeconds;
            if (wreckIndex < 0) throw new ArgumentOutOfRangeException(nameof(wreckIndex));
            var spacing = stageDurationSeconds / (wreckCount + 1f);
            return spacing * (wreckIndex + 1);
        }
    }
}
