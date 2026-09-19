using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Drowned Pursuit overtime spawns and Dread Tide boundary pressure after minute 10.</summary>
    public sealed class V3OvertimeDirector : MonoBehaviour
    {
        const float SpawnInterval = 1.4f;
        const float DreadTideStartMinute = 14f;
        const float DreadTideShrinkPerMinute = 0.05f;
        const float DreadTideMinSafeFraction = 0.4f;
        const float DreadTideDamagePerSecond = 8f;

        EnemyWorld _enemyWorld;
        SpawnDirector _spawnDirector;
        PlayerShipController _player;
        float _spawnTimer;
        float _dreadPulseTimer;
        bool _overtimeAnnounced;

        public static V3OvertimeDirector Ensure(EnemyWorld world, SpawnDirector director, PlayerShipController player)
        {
            var existing = FindFirstObjectByType<V3OvertimeDirector>();
            if (existing != null)
            {
                existing.Initialize(world, director, player);
                return existing;
            }

            var go = new GameObject("V3OvertimeDirector");
            var component = go.AddComponent<V3OvertimeDirector>();
            component.Initialize(world, director, player);
            return component;
        }

        public void Initialize(EnemyWorld world, SpawnDirector director, PlayerShipController player)
        {
            _enemyWorld = world;
            _spawnDirector = director;
            _player = player;
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (!RunManager.Instance.IsOvertime) return;

            if (!_overtimeAnnounced)
            {
                _overtimeAnnounced = true;
                RunCombatTelemetry.Instance?.Record("overtime_started", new System.Collections.Generic.Dictionary<string, object> { ["mode"] = "drowned_pursuit" });
                V3ToastHud.Instance?.Show("DROWNED PURSUIT — spectral fleet incoming!", 4f);
            }

            TickOvertimeSpawns(Time.deltaTime);
            TickDreadTide(Time.deltaTime);
        }

        void TickOvertimeSpawns(float dt)
        {
            if (_enemyWorld == null || _spawnDirector == null) return;

            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;

            var overtimeMinutes = (RunManager.Instance.ElapsedSeconds - GameConstants.StageDurationSeconds) / 60f;
            var pursuerWeight = overtimeMinutes < 2f ? 0.8f : 0.6f;
            var spawnPursuer = RunManager.Instance.Rng.DirectorFloat() < pursuerWeight;
            var enemyId = spawnPursuer ? "drowned_pursuer" : "drowned_harrier";

            if (_spawnDirector.TrySpawnOvertimeEnemy(enemyId, overtimeMinutes))
                RunCombatTelemetry.Instance?.Record("overtime_enemy_spawned", new System.Collections.Generic.Dictionary<string, object> { ["enemyId"] = enemyId });
            else if (_spawnDirector.TrySpawnOvertimeEnemy(spawnPursuer ? "drowned_harrier" : "drowned_pursuer", overtimeMinutes))
                RunCombatTelemetry.Instance?.Record("overtime_enemy_spawned", new System.Collections.Generic.Dictionary<string, object> { ["enemyId"] = spawnPursuer ? "drowned_harrier" : "drowned_pursuer" });

            _spawnTimer = SpawnInterval;
        }

        void TickDreadTide(float dt)
        {
            if (_player == null) return;

            var minute = RunManager.Instance.ElapsedSeconds / 60f;
            if (minute < DreadTideStartMinute) return;

            var shrinkMinutes = minute - DreadTideStartMinute;
            var safeFraction = Mathf.Max(DreadTideMinSafeFraction, 1f - shrinkMinutes * DreadTideShrinkPerMinute);
            var safeRadius = GameConstants.MapSizeMeters * 0.5f * safeFraction;
            var dist = new Vector2(_player.Position.x, _player.Position.z).magnitude;

            if (dist <= safeRadius) return;

            _dreadPulseTimer -= dt;
            if (_dreadPulseTimer > 0f) return;
            _dreadPulseTimer = 1f;

            var overflow = dist - safeRadius;
            var damage = DreadTideDamagePerSecond * (1f + overflow / 20f);
            _player.Damage?.TakeDamage(damage, _player.Position, false);
            RunCombatTelemetry.Instance?.Record("dread_tide_damage", new System.Collections.Generic.Dictionary<string, object> { ["source"] = "storm_edge", ["amount"] = damage });
            V3ToastHud.Instance?.Show("DREAD TIDE — leave the storm edge!", 1.2f);
        }
    }
}
