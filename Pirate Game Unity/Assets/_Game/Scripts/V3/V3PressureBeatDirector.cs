using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Minute 3/8 scheduled elites and minute 4/7 spawn-rate pulses per combat spec §9.</summary>
    public sealed class V3PressureBeatDirector : MonoBehaviour
    {
        const float HammerheadBeat = 180f;
        const float FirstPulseBeat = 240f;
        const float SecondPulseBeat = 420f;
        const float KrakenBeat = 480f;
        const float PulseDuration = 20f;
        const float PulseRateMultiplier = 1.25f;
        const float DeferWindow = 20f;
        const float WarningSeconds = 5f;

        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] SpawnDirector spawnDirector;
        [SerializeField] Transform player;

        bool _hammerheadDone;
        bool _krakenDone;
        bool _firstPulseDone;
        bool _secondPulseDone;
        bool _hammerheadWarned;
        bool _krakenWarned;
        float _hammerheadDeferUntil;
        float _krakenDeferUntil;
        float _pulseTimer;

        public void Initialize(EnemyWorld world, SpawnDirector director, Transform playerTransform)
        {
            enemyWorld = world;
            spawnDirector = director;
            player = playerTransform;
            spawnDirector?.ConfigureV3Pacing(true);
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.State != RunState.Playing && RunManager.Instance.State != RunState.BossFight)
                return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            TickBeat(ref _hammerheadDone, ref _hammerheadDeferUntil, ref _hammerheadWarned,
                HammerheadBeat, "hammerhead", "Hammerhead elite");
            TickBeat(ref _krakenDone, ref _krakenDeferUntil, ref _krakenWarned,
                KrakenBeat, "kraken_spawn", "Kraken Spawn elite");
            TickPulses(elapsed);
        }

        void TickBeat(
            ref bool done,
            ref float deferUntil,
            ref bool warned,
            float beatTime,
            string enemyId,
            string label)
        {
            if (done) return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            if (!warned && elapsed >= beatTime - WarningSeconds)
            {
                warned = true;
                V3ToastHud.Instance?.Show($"WARNING — {label} incoming!", 5f);
            }

            if (elapsed < beatTime)
                return;

            if (RunManager.Instance.State == RunState.BossFight)
            {
                if (deferUntil <= 0f)
                    deferUntil = elapsed + DeferWindow;
                if (elapsed < deferUntil)
                    return;

                done = true;
                V3ToastHud.Instance?.Show($"{label} deferred — boss active.", 3f);
                return;
            }

            if (spawnDirector != null && spawnDirector.TrySpawnPressureBeat(enemyId))
            {
                done = true;
                RunCombatTelemetry.Instance?.Record("pressure_elite_spawned", new System.Collections.Generic.Dictionary<string, object> { ["label"] = label });
                V3ToastHud.Instance?.Show($"{label} has arrived!", 4f);
                return;
            }

            if (deferUntil <= 0f)
                deferUntil = elapsed + DeferWindow;
            if (elapsed >= deferUntil)
            {
                done = true;
                RunCombatTelemetry.Instance?.Record("pressure_elite_skipped", new System.Collections.Generic.Dictionary<string, object> { ["label"] = label });
                V3ToastHud.Instance?.Show($"{label} skipped — no spawn slot.", 3f);
            }
        }

        void TickPulses(float elapsed)
        {
            if (!_firstPulseDone && elapsed >= FirstPulseBeat)
            {
                _firstPulseDone = true;
                _pulseTimer = PulseDuration;
                RunCombatTelemetry.Instance?.Record("pressure_pulse_started", new System.Collections.Generic.Dictionary<string, object> { ["beat"] = "minute_4" });
                V3ToastHud.Instance?.Show("SWARM PULSE — +25% spawn rate (4:00)", 4f);
            }
            else if (!_secondPulseDone && elapsed >= SecondPulseBeat)
            {
                _secondPulseDone = true;
                _pulseTimer = PulseDuration;
                RunCombatTelemetry.Instance?.Record("pressure_pulse_started", new System.Collections.Generic.Dictionary<string, object> { ["beat"] = "minute_7" });
                V3ToastHud.Instance?.Show("SWARM PULSE — +25% spawn rate (7:00)", 4f);
            }

            if (_pulseTimer > 0f)
            {
                _pulseTimer -= Time.deltaTime;
                spawnDirector?.SetSpawnRateMultiplier(PulseRateMultiplier);
                if (_pulseTimer <= 0f)
                    spawnDirector?.SetSpawnRateMultiplier(1f);
            }
        }

        void OnDestroy()
        {
            if (spawnDirector != null)
                spawnDirector.SetSpawnRateMultiplier(1f);
        }
    }
}
