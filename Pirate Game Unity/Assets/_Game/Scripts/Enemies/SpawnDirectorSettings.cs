using System;
using UnityEngine;

namespace HighSeas.Enemies
{
    public enum SpawnPlacementMode
    {
        Annulus = 0,
        OffCamera = 1,
        ScreenEdge = 2
    }

    [Serializable]
    public sealed class EnemySpawnTuning
    {
        public string enemyId;
        public bool spawnEnabled = true;
        [Min(0f)] public float weight = 1f;
        [Min(0)] public int maxConcurrent;
        [Min(0f)] public float availableAfterMinutes;
        [Min(0f)] public float availableUntilMinutes;
        [Tooltip("Multiplies this enemy's move speed. 1 = use definition speed.")]
        [Range(0.1f, 3f)] public float speedMultiplier = 1f;
    }

    [CreateAssetMenu(fileName = "SpawnDirectorSettings", menuName = "HighSeas/Spawn Director Settings")]
    public sealed class SpawnDirectorSettings : ScriptableObject
    {
        [Header("Timing")]
        [Min(0f)] public float gracePeriodSeconds = 2f;
        [Min(0f)] public float minSecondsBetweenSpawns = 0.55f;

        [Header("Enemy Speed")]
        [Tooltip("Global multiplier applied to every enemy's move speed.")]
        [Range(0.1f, 3f)] public float globalSpeedMultiplier = 1f;

        [Header("Global Pressure")]
        [Range(0.1f, 2f)] public float threatMultiplier = 0.7f;
        [Range(0.5f, 6f)] public float budgetCapMultiplier = 3.5f;
        [Min(0)] public int maxActiveEnemies;
        [Range(0f, 2f)] public float eliteChanceMultiplier = 1f;

        [Header("Ship vs Creature Mix")]
        [Range(0f, 1f)] public float creatureChanceAtStart = 0.2f;
        [Range(0f, 1f)] public float creatureChanceAtLateGame = 0.55f;
        [Min(0.5f)] public float creatureChanceRampMinutes = 6f;

        [Header("Spawn Position")]
        public SpawnPlacementMode spawnPlacement = SpawnPlacementMode.OffCamera;
        [Min(0f)] public float spawnDistanceMin = 10.8f;
        [Min(0f)] public float spawnDistanceMax = 18f;
        [Range(30f, 360f)] public float spawnArcDegrees = 360f;
        [Range(-180f, 180f)] public float spawnArcYawOffset;
        [Range(0f, 0.35f)] public float cameraViewportMargin = 0.08f;
        [Min(1)] public int spawnPositionAttempts = 16;

        [Header("Per-enemy")]
        public EnemySpawnTuning[] enemies;

        public EnemySpawnTuning GetOrDefault(string enemyId)
        {
            if (enemies == null || string.IsNullOrEmpty(enemyId))
                return DefaultTuning(enemyId);

            for (var i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null && enemies[i].enemyId == enemyId)
                    return enemies[i];
            }

            return DefaultTuning(enemyId);
        }

        public bool IsEnemyAvailable(string enemyId, float elapsedMinutes)
        {
            var tuning = GetOrDefault(enemyId);
            if (elapsedMinutes < tuning.availableAfterMinutes)
                return false;
            if (tuning.availableUntilMinutes > 0f && elapsedMinutes > tuning.availableUntilMinutes)
                return false;
            return true;
        }

        public float GetSpeedMultiplier(string enemyId)
        {
            var tuning = GetOrDefault(enemyId);
            return globalSpeedMultiplier * Mathf.Max(0.1f, tuning.speedMultiplier);
        }

        public static EnemySpawnTuning[] CreateDefaultEnemyTuning() =>
            new[]
            {
                Tuning("cannonboat", 1f, 0, 1.05f, 0f),
                Tuning("shark", 1f, 0, 1.1f, 0f),
                Tuning("swordfish", 0.9f, 14, 1.08f, 0f),
                Tuning("hammerhead", 1f, 0, 1.05f, 1f),
                Tuning("kraken_spawn", 0.85f, 6, 0.95f, 2.5f),
                Tuning("reef_crab_miniboss", 1f, 0, 1f, 0f, false),
                Tuning("barnacle_king", 1f, 0, 1f, 0f, false)
            };

        static EnemySpawnTuning Tuning(
            string id,
            float weight,
            int maxConcurrent,
            float speed,
            float availableAfterMinutes,
            bool enabled = true) =>
            new()
            {
                enemyId = id,
                spawnEnabled = enabled,
                weight = weight,
                maxConcurrent = maxConcurrent,
                speedMultiplier = speed,
                availableAfterMinutes = availableAfterMinutes
            };

        static EnemySpawnTuning DefaultTuning(string enemyId) =>
            new() { enemyId = enemyId, spawnEnabled = true, weight = 1f, maxConcurrent = 0, speedMultiplier = 1f };
    }
}
