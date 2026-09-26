using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Overtime (port of V3OvertimeDirector): after the stage deadline, ambient spawns stop and one enemy spawns per
    /// interval, pursuer or harrier spawner by weight, with overtime HP/damage multipliers. The interval shrinks
    /// every pressure step.
    /// </summary>
    internal sealed class OvertimePhase
    {
        readonly SpawnServices _s;
        readonly SpawnExecutor _executor;
        float _spawnTimer;
        bool _announced;

        public OvertimePhase(SpawnServices services, SpawnExecutor executor)
        {
            _s = services;
            _executor = executor;
        }

        public bool Announced => _announced;

        public void BeginRun()
        {
            _spawnTimer = 0f;
            _announced = false;
        }

        public static bool IsOvertime(WaveTimelineConfig t, float elapsed) => t != null && elapsed > t.stageDeadlineSeconds;

        public static float OvertimeMinutes(WaveTimelineConfig t, float elapsed) => (elapsed - t.stageDeadlineSeconds) / 60f;

        public static float SpawnInterval(WaveTimelineConfig t, float elapsed)
        {
            var o = t.overtime;
            var overtimeSeconds = Mathf.Max(0f, elapsed - t.stageDeadlineSeconds);
            var steps = (int)Math.Floor(overtimeSeconds / o.pressureStepSeconds);
            var interval = o.initialSpawnIntervalSeconds * Math.Pow(o.intervalDecayPerStep, steps);
            return (float)Math.Max(o.intervalFloorSeconds, interval);
        }

        public static float PursuerWeight(WaveTimelineConfig t, float elapsed) =>
            OvertimeMinutes(t, elapsed) < 2f ? t.overtime.pursuerWeightFirstTwoMinutes : t.overtime.pursuerWeightAfterTwoMinutes;

        public static float HpMultiplier(WaveTimelineConfig t, DifficultyCurveConfig d, float elapsed) =>
            d.overtimeHpMultiplierBase * Mathf.Pow(2f, Mathf.Max(0f, OvertimeMinutes(t, elapsed)) * d.overtimeHpMultiplierExponentPerMinute);

        public static float DamageMultiplier(WaveTimelineConfig t, DifficultyCurveConfig d, float elapsed) =>
            1f + Mathf.Max(0f, OvertimeMinutes(t, elapsed)) * d.overtimeDamageBonusPerMinute;

        public void Tick(float dt, TimelinePhase phase)
        {
            var t = _s.Timeline;
            if (phase == null || t == null || !IsOvertime(t, _s.Run.ElapsedSeconds)) return;

            if (!_announced)
            {
                _announced = true;
                _s.Feedback.Telemetry("overtime_started", new Dictionary<string, object> { ["mode"] = "drowned_pursuit" });
                _s.Feedback.Toast(t.overtime.startToast, 4f);
                _s.Publish(CombatEventKind.PhaseChanged, 0, 1, default, phase.phaseId);
            }

            _spawnTimer -= dt;
            if (_spawnTimer > 0f) return;

            var elapsed = _s.Run.ElapsedSeconds;
            var spawnPursuer = _s.Random.DirectorFloat() < PursuerWeight(t, elapsed);
            var first = spawnPursuer ? phase.spawner : phase.alternateSpawner;
            var second = spawnPursuer ? phase.alternateSpawner : phase.spawner;
            if (TrySpawn(first, elapsed)) _s.Feedback.Telemetry("overtime_enemy_spawned", new Dictionary<string, object> { ["enemyId"] = first.legacyProfileId });
            else if (TrySpawn(second, elapsed)) _s.Feedback.Telemetry("overtime_enemy_spawned", new Dictionary<string, object> { ["enemyId"] = second.legacyProfileId });

            _spawnTimer = SpawnInterval(t, elapsed);
        }

        bool TrySpawn(EnemySpawnerConfig spawner, float elapsed)
        {
            var t = _s.Timeline;
            var d = _s.Difficulty;
            if (spawner == null || d == null || !IsOvertime(t, _s.Run.ElapsedSeconds)) return false;
            var profile = _s.Profiles.For(spawner);
            if (profile == null) return false;
            var leader = -1;
            return _executor.TryBurst(profile, 1, false, HpMultiplier(t, d, elapsed), DamageMultiplier(t, d, elapsed), 0, ref leader) > 0;
        }
    }

    /// <summary>
    /// The storm (ports of V3SaltwindStormDirector and V3StormEdgeDamage, one clock): warning, then the safe region
    /// contracts over the contraction window; outside it the ship takes damage growing with depth, after an 8 s run
    /// grace. Steady by default (R-57 decision); PerFrameHit keeps V3's hit-per-frame through the ship's hit immunity.
    /// </summary>
    internal sealed class StormPhase
    {
        const float RunStartGraceSeconds = 8f;
        readonly SpawnServices _s;
        bool _warned;
        bool _announced;
        float _grace;
        float _toastCooldown;

        public StormPhase(SpawnServices services) { _s = services; }

        public bool Warned => _warned;
        public bool Announced => _announced;

        public void BeginRun()
        {
            _warned = false;
            _announced = false;
            _grace = RunStartGraceSeconds;
            _toastCooldown = 0f;
            _s.Storm.ResetForRun();
        }

        public static float SafeScale(StormSettings storm, float elapsed)
        {
            if (elapsed < storm.contractionStartSeconds) return storm.safeRegionStartFraction;
            if (elapsed >= storm.contractionEndSeconds) return storm.safeRegionEndFraction;
            var t = (elapsed - storm.contractionStartSeconds) / Mathf.Max(0.001f, storm.contractionEndSeconds - storm.contractionStartSeconds);
            return Mathf.Lerp(storm.safeRegionStartFraction, storm.safeRegionEndFraction, t);
        }

        /// <summary>The storm director's slot: warning, start, contraction.</summary>
        public void Tick(TimelinePhase phase)
        {
            var t = _s.Timeline;
            if (phase == null || t == null) return;
            var elapsed = _s.Run.ElapsedSeconds;
            var start = phase.trigger.timeSeconds;
            if (!_warned && elapsed >= start - phase.warning.leadSeconds)
            {
                _warned = true;
                _s.Feedback.Toast(SpawnServices.Format(phase.warning.template, phase), 4f);
                _s.Publish(CombatEventKind.Warning, 0, 0, default, phase.phaseId);
            }
            if (elapsed < start) return;

            if (!_announced)
            {
                _announced = true;
                _s.Feedback.Telemetry("storm_started");
                _s.Feedback.Toast(t.storm.startToast, 4f);
                _s.Storm.ShowRing();
                _s.Publish(CombatEventKind.PhaseChanged, 0, 1, default, phase.phaseId);
            }
            _s.Storm.SetSafeScale(SafeScale(t.storm, elapsed));
            _s.Storm.RefreshRing();
        }

        /// <summary>The storm edge damage slot on the ship.</summary>
        public void TickEdge(float dt, TimelinePhase phase)
        {
            var t = _s.Timeline;
            if (t == null || !_s.Player.Exists || !_s.Player.IsAlive) return;
            if (_grace > 0f)
            {
                _grace -= dt;
                return;
            }
            var position = _s.Player.Position;
            if (_s.Storm.IsInsideSafeZone(position)) return;
            if (phase == null || _s.Run.ElapsedSeconds < phase.trigger.timeSeconds) return;

            var depth = _s.Storm.Depth(position);
            var damage = t.storm.outsideDamagePerSecond * (1f + depth * 0.08f) * dt;
            if (t.storm.damageMode == StormDamageMode.Steady) _s.Player.DamageOverTime(damage, position);
            else _s.Player.Damage(damage, position);

            _toastCooldown -= dt;
            if (_toastCooldown <= 0f)
            {
                _toastCooldown = 2.5f;
                _s.Feedback.Toast(t.storm.edgeToast, 2f);
            }
        }
    }
}
