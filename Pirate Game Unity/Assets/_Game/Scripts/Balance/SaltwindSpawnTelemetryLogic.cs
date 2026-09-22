using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;

namespace HighSeas.Balance
{
    /// <summary>C02 spawn scheduling checkpoints + ambient spawn event helpers.</summary>
    public static class SaltwindSpawnTelemetryLogic
    {
        static readonly float[] CheckpointSeconds = { 120f, 240f, 360f };
        static readonly HashSet<int> _recordedCheckpointIndices = new();

        public static void ResetForNewRun() => _recordedCheckpointIndices.Clear();

        public static void MaybeRecordCheckpoint(
            float elapsedSeconds,
            int activeEnemyCount,
            float ambientBudgetCarry,
            float spawnRateMultiplier)
        {
            if (!StageBalanceProfileContext.IsActive)
                return;

            for (var i = 0; i < CheckpointSeconds.Length; i++)
            {
                if (_recordedCheckpointIndices.Contains(i))
                    continue;
                if (elapsedSeconds + 0.001f < CheckpointSeconds[i])
                    continue;

                _recordedCheckpointIndices.Add(i);
                RunCombatTelemetry.Instance?.Record("spawn_checkpoint", new Dictionary<string, object>
                {
                    ["checkpointSeconds"] = CheckpointSeconds[i],
                    ["activeEnemies"] = activeEnemyCount,
                    ["ambientBudgetCarry"] = ambientBudgetCarry,
                    ["spawnRateMultiplier"] = spawnRateMultiplier
                });
            }
        }

        public static void RecordAmbientSpawn(
            int bandIndex,
            string enemyId,
            float ambientBudgetCarry,
            int activeEnemyCount)
        {
            if (!StageBalanceProfileContext.IsActive)
                return;

            RunCombatTelemetry.Instance?.Record("ambient_spawn", new Dictionary<string, object>
            {
                ["band"] = bandIndex,
                ["enemyId"] = enemyId ?? string.Empty,
                ["ambientBudgetCarry"] = ambientBudgetCarry,
                ["activeEnemies"] = activeEnemyCount
            });
        }
    }
}
