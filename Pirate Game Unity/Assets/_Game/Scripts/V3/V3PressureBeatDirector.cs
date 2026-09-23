using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Enemies;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Profile-scheduled elites and swarm pulses (Saltwind C04 / R5).</summary>
    public sealed class V3PressureBeatDirector : MonoBehaviour
    {
        const float SpawnRetryWindowSeconds = SaltwindPressureBeatLogic.SpawnRetryWindowSeconds;

        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] EncounterSpawner encounterSpawner;
        [SerializeField] Transform player;

        bool _hammerheadDone;
        bool _krakenDone;
        bool _firstPulseDone;
        bool _secondPulseDone;
        bool _hammerheadWarned;
        bool _krakenWarned;
        bool _firstPulseWarned;
        bool _secondPulseWarned;
        float _hammerheadDeferUntil;
        float _krakenDeferUntil;
        float _pulseTimer;

        public void Initialize(EnemyWorld world, EncounterSpawner director, Transform playerTransform)
        {
            enemyWorld = world;
            encounterSpawner = director;
            player = playerTransform;
            encounterSpawner?.ConfigureV3Pacing(true);
        }

        public void ResetForRun()
        {
            _hammerheadDone = false;
            _krakenDone = false;
            _firstPulseDone = false;
            _secondPulseDone = false;
            _hammerheadWarned = false;
            _krakenWarned = false;
            _firstPulseWarned = false;
            _secondPulseWarned = false;
            _hammerheadDeferUntil = 0f;
            _krakenDeferUntil = 0f;
            _pulseTimer = 0f;
            encounterSpawner?.ClearSpawnModifier("v3_swarm_pulse");
        }

        StageBalanceProfile Profile => StageBalanceProfileContext.Authoring;

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.State != RunState.Playing && RunManager.Instance.State != RunState.BossFight)
                return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            var profile = Profile;
            var warning = SaltwindPressureBeatLogic.GetWarningSeconds(profile);

            TickBeat(ref _hammerheadDone, ref _hammerheadDeferUntil, ref _hammerheadWarned,
                SaltwindPressureBeatLogic.GetEliteBeatSeconds(profile, 0),
                warning, 0, "hammerhead", "Hammerhead elite");
            TickBeat(ref _krakenDone, ref _krakenDeferUntil, ref _krakenWarned,
                SaltwindPressureBeatLogic.GetEliteBeatSeconds(profile, 1),
                warning, 1, "kraken_spawn", "Kraken Spawn elite");
            TickPulses(elapsed, profile);
        }

        void TickBeat(
            ref bool done,
            ref float deferUntil,
            ref bool warned,
            float beatTime,
            float warningSeconds,
            int scheduledEliteIndex,
            string enemyId,
            string label)
        {
            if (done) return;

            var elapsed = RunManager.Instance.ElapsedSeconds;
            if (!warned && SaltwindPressureBeatLogic.ShouldShowEventWarning(elapsed, beatTime, warningSeconds))
            {
                warned = true;
                V3ToastHud.Instance?.Show($"WARNING — {label} incoming!", warningSeconds);
            }

            if (elapsed < beatTime)
                return;

            if (SaltwindPressureBeatLogic.ShouldSkipScheduledEventForBoss(
                    elapsed, beatTime, RunManager.Instance.State == RunState.BossFight))
            {
                done = true;
                RunCombatTelemetry.Instance?.Record("pressure_elite_skipped", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["label"] = label,
                    ["reason"] = "boss_conflict"
                });
                V3ToastHud.Instance?.Show($"{label} skipped — boss active.", 3f);
                return;
            }

            if (encounterSpawner != null &&
                encounterSpawner.TrySpawnPressureBeat(enemyId, scheduledEliteIndex, out _))
            {
                done = true;
                RunCombatTelemetry.Instance?.Record("pressure_elite_spawned", new System.Collections.Generic.Dictionary<string, object> { ["label"] = label });
                V3ToastHud.Instance?.Show($"{label} has arrived!", 4f);
                return;
            }

            if (deferUntil <= 0f)
                deferUntil = elapsed + SpawnRetryWindowSeconds;
            if (elapsed >= deferUntil)
            {
                done = true;
                RunCombatTelemetry.Instance?.Record("pressure_elite_skipped", new System.Collections.Generic.Dictionary<string, object> { ["label"] = label });
                V3ToastHud.Instance?.Show($"{label} skipped — no spawn slot.", 3f);
            }
        }

        void TickPulses(float elapsed, StageBalanceProfile profile)
        {
            var bossFight = RunManager.Instance.State == RunState.BossFight;
            var warning = SaltwindPressureBeatLogic.GetWarningSeconds(profile);
            var pulseA = SaltwindPressureBeatLogic.GetSwarmStartSeconds(profile, 0);
            var pulseB = SaltwindPressureBeatLogic.GetSwarmStartSeconds(profile, 1);
            var duration = SaltwindPressureBeatLogic.GetSwarmDurationSeconds(profile);
            var mult = SaltwindPressureBeatLogic.GetSwarmMultiplier(profile);

            if (!_firstPulseWarned && SaltwindPressureBeatLogic.ShouldShowEventWarning(elapsed, pulseA, warning))
            {
                _firstPulseWarned = true;
                V3ToastHud.Instance?.Show("WARNING — swarm pulse at 4:00!", warning);
            }

            if (!_secondPulseWarned && SaltwindPressureBeatLogic.ShouldShowEventWarning(elapsed, pulseB, warning))
            {
                _secondPulseWarned = true;
                V3ToastHud.Instance?.Show("WARNING — swarm pulse at 7:00!", warning);
            }

            if (!_firstPulseDone &&
                SaltwindPressureBeatLogic.ShouldSkipScheduledEventForBoss(elapsed, pulseA, bossFight))
            {
                _firstPulseDone = true;
                RunCombatTelemetry.Instance?.Record("pressure_pulse_skipped", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["beat"] = "swarm_a",
                    ["reason"] = "boss_conflict"
                });
            }
            else if (SaltwindPressureBeatLogic.ShouldStartSwarmPulse(elapsed, profile, 0, bossFight, _firstPulseDone))
            {
                _firstPulseDone = true;
                _pulseTimer = duration;
                encounterSpawner?.SetTimedSpawnModifier("v3_swarm_pulse", mult, duration);
                RunCombatTelemetry.Instance?.Record("pressure_pulse_started", new System.Collections.Generic.Dictionary<string, object> { ["beat"] = "swarm_a" });
                V3ToastHud.Instance?.Show($"SWARM PULSE — +{(mult - 1f) * 100f:F0}% spawn rate", 4f);
            }
            else if (!_secondPulseDone &&
                     SaltwindPressureBeatLogic.ShouldSkipScheduledEventForBoss(elapsed, pulseB, bossFight))
            {
                _secondPulseDone = true;
                RunCombatTelemetry.Instance?.Record("pressure_pulse_skipped", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["beat"] = "swarm_b",
                    ["reason"] = "boss_conflict"
                });
            }
            else if (SaltwindPressureBeatLogic.ShouldStartSwarmPulse(elapsed, profile, 1, bossFight, _secondPulseDone))
            {
                _secondPulseDone = true;
                _pulseTimer = duration;
                encounterSpawner?.SetTimedSpawnModifier("v3_swarm_pulse", mult, duration);
                RunCombatTelemetry.Instance?.Record("pressure_pulse_started", new System.Collections.Generic.Dictionary<string, object> { ["beat"] = "swarm_b" });
                V3ToastHud.Instance?.Show($"SWARM PULSE — +{(mult - 1f) * 100f:F0}% spawn rate", 4f);
            }

            if (_pulseTimer > 0f)
            {
                _pulseTimer -= Time.deltaTime;
                if (_pulseTimer <= 0f)
                    encounterSpawner?.ClearSpawnModifier("v3_swarm_pulse");
            }
        }

        void OnDestroy()
        {
            encounterSpawner?.ClearSpawnModifier("v3_swarm_pulse");
            encounterSpawner?.SetSpawnRateMultiplier(1f);
        }
    }
}
