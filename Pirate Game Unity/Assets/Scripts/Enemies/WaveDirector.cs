using UnityEngine;

namespace PirateGame.Enemies
{
    /// <summary>
    /// Drives <see cref="EnemySpawner"/> through a survivor-style wave timeline.
    /// Disables the spawner's built-in auto loop and schedules bursts, ramps, and calm periods.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        [SerializeField] EnemySpawner spawner;
        [SerializeField] WaveTimeline timeline;
        [SerializeField] bool startOnPlay = true;
        [SerializeField] bool logWaveChanges = true;

        float runTime;
        float stepLocalTime;
        float spawnTimer;
        int rampCount;
        WaveStep activeStep;
        bool running;
        int defaultMaxAlive;

        public float RunTime => runTime;
        public float RunDuration => timeline != null ? timeline.runDurationSeconds : 0f;
        public string CurrentWaveLabel => activeStep?.label ?? "—";
        public bool IsRunning => running;

        void Awake()
        {
            if (spawner == null)
                spawner = GetComponent<EnemySpawner>();
            if (spawner == null)
                spawner = FindFirstObjectByType<EnemySpawner>();

            if (timeline == null)
                timeline = WaveTimeline.CreateDefaultShallows();

            if (spawner != null)
            {
                spawner.SetAutoSpawn(false);
                spawner.SetSpawnOnStart(false);
                defaultMaxAlive = spawner.MaxAlive;
            }
        }

        void Start()
        {
            if (startOnPlay)
                BeginRun();
        }

        public void BeginRun()
        {
            runTime = 0f;
            stepLocalTime = 0f;
            spawnTimer = 0f;
            rampCount = 0;
            running = true;
            spawner?.ClearAliveEnemies();
            ApplyStep(timeline.GetStepAtTime(0f));
        }

        public void EndRun()
        {
            running = false;
            spawner?.ClearAliveEnemies();
        }

        void Update()
        {
            if (!running || spawner == null || timeline == null) return;

            runTime += Time.deltaTime;
            if (runTime >= timeline.runDurationSeconds)
            {
                if (logWaveChanges)
                    Debug.Log("[Wave] Run complete.");
                EndRun();
                return;
            }

            UpdateUnlockedEnemyTypes();

            var nextStep = timeline.GetStepAtTime(runTime);
            if (nextStep != activeStep)
                ApplyStep(nextStep);

            if (activeStep == null || activeStep.pattern == WaveSpawnPattern.Calm)
                return;

            if (activeStep.pattern == WaveSpawnPattern.Boss)
                return;

            stepLocalTime += Time.deltaTime;
            if (activeStep.pattern == WaveSpawnPattern.Burst)
            {
                if (stepLocalTime <= Time.deltaTime * 2f)
                    spawner.SpawnWave(activeStep.spawnCount);
                return;
            }

            if (stepLocalTime > activeStep.duration)
                return;

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f) return;

            var count = activeStep.spawnCount;
            if (activeStep.pattern == WaveSpawnPattern.Ramp)
            {
                count = activeStep.spawnCount + rampCount;
                rampCount += activeStep.rampIncrement;
            }
            else if (activeStep.pattern == WaveSpawnPattern.Swarm)
            {
                count = activeStep.spawnCount;
            }

            spawner.SpawnWave(Mathf.Max(1, count));
            spawnTimer = Mathf.Max(0.25f, activeStep.spawnInterval);
        }

        void ApplyStep(WaveStep step)
        {
            activeStep = step;
            stepLocalTime = 0f;
            spawnTimer = 0f;
            rampCount = 0;

            if (step == null) return;

            if (step.maxAliveOverride > 0)
                spawner.MaxAlive = step.maxAliveOverride;
            else
                spawner.MaxAlive = defaultMaxAlive;

            spawner.HealthMultiplier = step.healthMultiplier > 0f ? step.healthMultiplier : 1f;

            if (step.pattern == WaveSpawnPattern.Boss && !string.IsNullOrEmpty(step.bossId))
                spawner.SpawnBoss(step.bossId);

            if (logWaveChanges)
                Debug.Log($"[Wave] {step.label} ({step.pattern}) @ {runTime:0}s");
        }

        void OnGUI()
        {
            if (!running || timeline == null) return;

            var pad = 10f;
            var y = Screen.height - 72f;
            GUI.Label(new Rect(pad, y, 400f, 22f), $"Wave: {CurrentWaveLabel}");
            GUI.Label(new Rect(pad, y + 22f, 400f, 22f),
                $"Time {FormatTime(runTime)} / {FormatTime(timeline.runDurationSeconds)}  |  Enemies {spawner?.AliveCount ?? 0}");
        }

        static string FormatTime(float seconds)
        {
            var m = Mathf.FloorToInt(seconds / 60f);
            var s = Mathf.FloorToInt(seconds % 60f);
            return $"{m}:{s:00}";
        }

        void UpdateUnlockedEnemyTypes()
        {
            if (spawner == null || spawner.EnemyTypeCount <= 0) return;

            var unlocked = Mathf.Clamp(2 + Mathf.FloorToInt(runTime / 75f), 2, spawner.EnemyTypeCount);
            spawner.SetUnlockedEnemyCount(unlocked);
        }
    }
}
