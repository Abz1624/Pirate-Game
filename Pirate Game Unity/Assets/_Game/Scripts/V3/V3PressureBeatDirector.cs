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
        float _hammerheadDeferUntil;
        float _krakenDeferUntil;
        float _pulseTimer;
        float _hammerheadWarnTimer = -1f;
        float _krakenWarnTimer = -1f;

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
            TickBeat(ref _hammerheadDone, ref _hammerheadDeferUntil, ref _hammerheadWarnTimer,
                HammerheadBeat, "hammerhead", "Hammerhead pressure");
            TickBeat(ref _krakenDone, ref _krakenDeferUntil, ref _krakenWarnTimer,
                KrakenBeat, "kraken_spawn", "Kraken Spawn pressure");
            TickPulses(elapsed);
            TickWarnings();
        }

        void TickBeat(
            ref bool done,
            ref float deferUntil,
            ref float warnTimer,
            float beatTime,
            string enemyId,
            string label)
        {
            if (done) return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            if (elapsed < beatTime)
                return;

            if (warnTimer < 0f)
                warnTimer = WarningSeconds;

            if (warnTimer > 0f)
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
                V3ToastHud.Instance?.Show($"{label} incoming!", 4f);
                return;
            }

            if (deferUntil <= 0f)
                deferUntil = elapsed + DeferWindow;
            if (elapsed >= deferUntil)
            {
                done = true;
                V3ToastHud.Instance?.Show($"{label} skipped — no spawn slot.", 3f);
            }
        }

        void TickWarnings()
        {
            if (_hammerheadWarnTimer > 0f)
            {
                _hammerheadWarnTimer -= Time.deltaTime;
                if (_hammerheadWarnTimer <= 0f)
                    V3ToastHud.Instance?.Show("WARNING — Hammerhead elite at 3:00", 5f);
            }

            if (_krakenWarnTimer > 0f)
            {
                _krakenWarnTimer -= Time.deltaTime;
                if (_krakenWarnTimer <= 0f)
                    V3ToastHud.Instance?.Show("WARNING — Kraken Spawn elite at 8:00", 5f);
            }
        }

        void TickPulses(float elapsed)
        {
            if (!_firstPulseDone && elapsed >= FirstPulseBeat)
            {
                _firstPulseDone = true;
                _pulseTimer = PulseDuration;
                V3ToastHud.Instance?.Show("SWARM PULSE — +25% spawn rate (4:00)", 4f);
            }
            else if (!_secondPulseDone && elapsed >= SecondPulseBeat)
            {
                _secondPulseDone = true;
                _pulseTimer = PulseDuration;
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
            {
                spawnDirector.ConfigureV3Pacing(false);
                spawnDirector.SetSpawnRateMultiplier(1f);
            }
        }
    }
}
