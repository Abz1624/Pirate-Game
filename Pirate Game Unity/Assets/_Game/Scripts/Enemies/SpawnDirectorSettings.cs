using System;
using UnityEngine;

namespace HighSeas.Enemies
{
    public enum SpawnTimingMode
    {
        ContinuousThreat = 0,
        SteadyInterval = 1,
        BurstWaves = 2,
        ScriptedWaves = 3
    }

    public enum ScriptedWaveEndMode
    {
        StopSpawning = 0,
        LoopWaves = 1,
        ContinueThreat = 2
    }

    [Serializable]
    public sealed class SpawnWaveEntry
    {
        public string enemyId;
        [Min(1)] public int count = 1;
    }

    [Serializable]
    public sealed class SpawnWaveDefinition
    {
        public string waveName = "Wave";
        [Tooltip("Seconds to wait after the previous wave finishes before this one starts.")]
        [Min(0f)] public float delayBeforeWaveSeconds = 8f;
        [Tooltip("Gap between each enemy spawn inside this wave.")]
        [Min(0.05f)] public float spawnGapSeconds = 0.45f;
        public SpawnWaveEntry[] spawns;
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
        [Header("Timing Pattern")]
        public SpawnTimingMode timingMode = SpawnTimingMode.ScriptedWaves;
        [Min(0f)] public float gracePeriodSeconds = 5f;
        [Min(0f)] public float minSecondsBetweenSpawns = 0.35f;
        [Min(0.1f)] public float spawnIntervalSeconds = 2.5f;
        [Min(1)] public int waveSize = 4;

        [Header("Scripted Waves")]
        public SpawnWaveDefinition[] waves;
        public ScriptedWaveEndMode afterScriptedWaves = ScriptedWaveEndMode.ContinueThreat;

        [Header("Enemy Speed")]
        [Tooltip("Global multiplier applied to every enemy's move speed.")]
        [Range(0.1f, 3f)] public float globalSpeedMultiplier = 1f;

        [Header("Global Pressure")]
        [Range(0.1f, 2f)] public float threatMultiplier = 0.85f;
        [Range(0.5f, 6f)] public float budgetCapMultiplier = 3.5f;
        [Min(0)] public int maxActiveEnemies;
        [Range(0f, 2f)] public float eliteChanceMultiplier = 1f;

        [Header("Ship vs Creature Mix")]
        [Range(0f, 1f)] public float creatureChanceAtStart = 0.2f;
        [Range(0f, 1f)] public float creatureChanceAtLateGame = 0.55f;
        [Min(0.5f)] public float creatureChanceRampMinutes = 6f;

        [Header("Spawn Position")]
        [Min(0f)] public float spawnDistanceMin = 13f;
        [Min(0f)] public float spawnDistanceMax = 22f;
        [Tooltip("When Spawn Outside Camera View is off: 360 = anywhere around the player.")]
        [Range(30f, 360f)] public float spawnArcDegrees = 360f;
        [Range(-180f, 180f)] public float spawnArcYawOffset;
        [Tooltip("Prefer spawn points that are not visible on the main camera.")]
        public bool spawnOutsideCameraView = true;
        [Tooltip("Viewport padding used when testing visibility. Higher = stricter off-screen spawning.")]
        [Range(0f, 0.35f)] public float cameraViewportMargin = 0.08f;
        [Min(1)] public int spawnPositionAttempts = 12;

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

        public static SpawnWaveDefinition[] CreateDefaultWaveScript() =>
            new[]
            {
                Wave("1 — Cannonboats", 0f, 0.5f,
                    Entry("cannonboat", 4)),
                Wave("2 — Swordfish", 10f, 0.35f,
                    Entry("swordfish", 6)),
                Wave("3 — Cannonboats + Sharks", 10f, 0.45f,
                    Entry("cannonboat", 3), Entry("shark", 3)),
                Wave("4 — Swordfish + Sharks", 10f, 0.4f,
                    Entry("swordfish", 4), Entry("shark", 3)),
                Wave("5 — Hammerhead + Sharks", 12f, 0.45f,
                    Entry("hammerhead", 2), Entry("shark", 4)),
                Wave("6 — Kraken", 14f, 0.8f,
                    Entry("kraken_spawn", 2))
            };

        static SpawnWaveDefinition Wave(string name, float delay, float gap, params SpawnWaveEntry[] spawns) =>
            new()
            {
                waveName = name,
                delayBeforeWaveSeconds = delay,
                spawnGapSeconds = gap,
                spawns = spawns
            };

        static SpawnWaveEntry Entry(string enemyId, int count) =>
            new() { enemyId = enemyId, count = count };

        public static EnemySpawnTuning[] CreateDefaultEnemyTuning() =>
            new[]
            {
                Tuning("cannonboat", 1f, 0, 1f),
                Tuning("shark", 1f, 0, 1f),
                Tuning("swordfish", 0.5f, 8, 1.1f),
                Tuning("hammerhead", 0.8f, 0, 0.95f),
                Tuning("kraken_spawn", 0.7f, 4, 0.85f),
                Tuning("reef_crab_miniboss", 1f, 0, 1f, false),
                Tuning("barnacle_king", 1f, 0, 1f, false)
            };

        static EnemySpawnTuning Tuning(
            string id,
            float weight,
            int maxConcurrent,
            float speed,
            bool enabled = true) =>
            new()
            {
                enemyId = id,
                spawnEnabled = enabled,
                weight = weight,
                maxConcurrent = maxConcurrent,
                speedMultiplier = speed
            };

        static EnemySpawnTuning DefaultTuning(string enemyId) =>
            new() { enemyId = enemyId, spawnEnabled = true, weight = 1f, maxConcurrent = 0, speedMultiplier = 1f };
    }
}
