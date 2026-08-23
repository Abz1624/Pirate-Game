using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Enemies
{
    public sealed class SpawnDirector : MonoBehaviour
    {
        enum ScriptedPhase
        {
            WaitingToStart,
            DelayBeforeWave,
            Spawning,
            PauseAfterWave,
            Finished
        }

        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] Transform player;
        [SerializeField] SpawnDirectorSettings settings;
        [SerializeField] Camera spawnCamera;

        Camera _cachedSpawnCamera;

        float _budget;
        float _eliteChance;
        float _spawnCooldown;
        float _intervalTimer;
        float _waveTimer;
        int _waveSpawnRemaining;
        int _paidChestIndex = 1;
        bool _miniboss30;
        bool _miniboss72;

        ScriptedPhase _scriptedPhase = ScriptedPhase.WaitingToStart;
        int _scriptedWaveIndex;
        float _scriptedTimer;
        float _scriptedWaveElapsed;
        readonly List<string> _scriptedQueue = new(64);

        void Awake()
        {
            if (enemyWorld == null) enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (player == null)
            {
                var ship = FindFirstObjectByType<Player.PlayerShipController>();
                if (ship != null) player = ship.transform;
            }

            if (settings == null)
                settings = Resources.Load<SpawnDirectorSettings>("SpawnDirectorSettings");

            _cachedSpawnCamera = spawnCamera != null ? spawnCamera : Camera.main;
            ResetTimingState();
        }

        void ResetTimingState()
        {
            _spawnCooldown = 0f;
            _intervalTimer = 0f;
            _waveTimer = settings != null ? settings.spawnIntervalSeconds : 2.5f;
            _waveSpawnRemaining = 0;
            ResetScriptedState();
        }

        void ResetScriptedState()
        {
            _scriptedPhase = ScriptedPhase.WaitingToStart;
            _scriptedWaveIndex = 0;
            _scriptedTimer = 0f;
            _scriptedWaveElapsed = 0f;
            _scriptedQueue.Clear();
        }

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.State != RunState.Playing && RunManager.Instance.State != RunState.BossFight) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (enemyWorld == null || player == null) return;
            if (RunManager.Instance.ElapsedSeconds < GetGracePeriod())
                return;

            var minute = RunManager.Instance.ElapsedSeconds / 60f;
            var eliteMult = settings != null ? settings.eliteChanceMultiplier : 1f;
            _eliteChance = Mathf.Min(0.18f, (0.02f + 0.008f * minute) * eliteMult);

            TrySpawnMinibosses();

            if (_spawnCooldown > 0f)
                _spawnCooldown -= dt;

            var mode = settings != null ? settings.timingMode : SpawnTimingMode.ContinuousThreat;
            switch (mode)
            {
                case SpawnTimingMode.SteadyInterval:
                    TickSteadyInterval(dt);
                    break;
                case SpawnTimingMode.BurstWaves:
                    TickBurstWaves(dt);
                    break;
                case SpawnTimingMode.ScriptedWaves:
                    TickScriptedWaves(dt);
                    break;
                default:
                    TickContinuousThreat(dt);
                    break;
            }
        }

        void TickScriptedWaves(float dt)
        {
            if (_scriptedPhase == ScriptedPhase.Finished)
            {
                HandleScriptedWavesFinished(dt);
                return;
            }

            if (_scriptedPhase == ScriptedPhase.WaitingToStart)
            {
                BeginScriptedWave(0);
                return;
            }

            if (_scriptedPhase == ScriptedPhase.DelayBeforeWave)
            {
                _scriptedTimer -= dt;
                if (_scriptedTimer <= 0f)
                    BeginScriptedWave(_scriptedWaveIndex);
                return;
            }

            if (_scriptedPhase == ScriptedPhase.PauseAfterWave)
            {
                _scriptedTimer -= dt;
                if (_scriptedTimer <= 0f)
                    BeginScriptedWave(_scriptedWaveIndex + 1);
                return;
            }

            if (_scriptedPhase == ScriptedPhase.Spawning)
            {
                var wave = GetScriptedWave(_scriptedWaveIndex);
                if (wave == null)
                {
                    _scriptedPhase = ScriptedPhase.Finished;
                    return;
                }

                _scriptedWaveElapsed += dt;
                if (_scriptedWaveElapsed >= wave.waveDurationSeconds)
                {
                    FinishCurrentScriptedWave();
                    return;
                }

                if (_spawnCooldown > 0f)
                    return;

                if (_scriptedQueue.Count == 0)
                {
                    if (wave.loopComposition)
                        BuildScriptedQueue(wave, _scriptedQueue);
                    else
                        return;
                }

                if (_scriptedQueue.Count == 0)
                    return;

                var enemyId = _scriptedQueue[0];
                if (!TrySpawnEnemyId(enemyId))
                    return;

                _scriptedQueue.RemoveAt(0);
                _spawnCooldown = wave.spawnGapSeconds;
            }
        }

        void HandleScriptedWavesFinished(float dt)
        {
            var endMode = settings != null ? settings.afterScriptedWaves : ScriptedWaveEndMode.ContinueThreat;
            switch (endMode)
            {
                case ScriptedWaveEndMode.LoopWaves:
                    ResetScriptedState();
                    break;
                case ScriptedWaveEndMode.ContinueThreat:
                    TickContinuousThreat(dt);
                    break;
            }
        }

        void BeginScriptedWave(int waveIndex)
        {
            var wave = GetScriptedWave(waveIndex);
            if (wave == null)
            {
                _scriptedPhase = ScriptedPhase.Finished;
                return;
            }

            _scriptedWaveIndex = waveIndex;
            _scriptedWaveElapsed = 0f;
            _scriptedQueue.Clear();
            BuildScriptedQueue(wave, _scriptedQueue);

            if (_scriptedQueue.Count == 0)
            {
                _scriptedPhase = ScriptedPhase.PauseAfterWave;
                _scriptedTimer = wave.delayBeforeWaveSeconds;
                return;
            }

            _scriptedPhase = ScriptedPhase.Spawning;
        }

        void FinishCurrentScriptedWave()
        {
            var wave = GetScriptedWave(_scriptedWaveIndex);
            var nextWave = GetScriptedWave(_scriptedWaveIndex + 1);
            if (nextWave == null)
            {
                _scriptedPhase = ScriptedPhase.Finished;
                return;
            }

            _scriptedPhase = ScriptedPhase.PauseAfterWave;
            _scriptedTimer = nextWave.delayBeforeWaveSeconds;
        }

        SpawnWaveDefinition GetScriptedWave(int index)
        {
            if (settings?.waves == null || index < 0 || index >= settings.waves.Length)
                return null;
            return settings.waves[index];
        }

        static void BuildScriptedQueue(SpawnWaveDefinition wave, List<string> queue)
        {
            if (wave?.spawns == null)
                return;

            for (var i = 0; i < wave.spawns.Length; i++)
            {
                var entry = wave.spawns[i];
                if (entry == null || string.IsNullOrEmpty(entry.enemyId) || entry.count <= 0)
                    continue;

                for (var c = 0; c < entry.count; c++)
                    queue.Add(entry.enemyId);
            }
        }

        void TickContinuousThreat(float dt)
        {
            var threatMult = settings != null ? settings.threatMultiplier : 1f;
            var budgetCapMult = settings != null ? settings.budgetCapMultiplier : 3.5f;
            var threatPerSec = RunManager.Instance.ThreatPerSecond * threatMult;
            _budget += threatPerSec * dt;
            var cap = threatPerSec * budgetCapMult;
            if (_budget > cap) _budget = cap;

            var enemyCap = GetEnemyCap();
            while (_budget > 0f && enemyWorld.ActiveCount < enemyCap && _spawnCooldown <= 0f)
            {
                if (!TrySpawnOne())
                    break;

                _spawnCooldown = GetMinSpawnGap();
            }
        }

        void TickSteadyInterval(float dt)
        {
            _intervalTimer -= dt;
            if (_intervalTimer > 0f || _spawnCooldown > 0f)
                return;

            if (TrySpawnOne())
            {
                _intervalTimer = settings != null ? settings.spawnIntervalSeconds : 2.5f;
                _spawnCooldown = GetMinSpawnGap();
            }
            else
            {
                _intervalTimer = 0.25f;
            }
        }

        void TickBurstWaves(float dt)
        {
            if (_waveSpawnRemaining > 0)
            {
                if (_spawnCooldown > 0f)
                    return;

                if (TrySpawnOne())
                {
                    _waveSpawnRemaining--;
                    _spawnCooldown = GetMinSpawnGap();
                }
                else
                {
                    _waveSpawnRemaining = 0;
                }

                return;
            }

            _waveTimer -= dt;
            if (_waveTimer > 0f)
                return;

            _waveSpawnRemaining = settings != null ? settings.waveSize : 4;
            _waveTimer = settings != null ? settings.spawnIntervalSeconds : 2.5f;
        }

        bool TrySpawnOne()
        {
            if (enemyWorld.ActiveCount >= GetEnemyCap())
                return false;

            var defIndex = PickEnemyDefinition();
            if (defIndex < 0)
                return false;

            return TrySpawnDefinition(defIndex);
        }

        bool TrySpawnEnemyId(string enemyId)
        {
            if (string.IsNullOrEmpty(enemyId) || enemyWorld.ActiveCount >= GetEnemyCap())
                return false;

            var defIndex = FindDefinitionIndex(enemyId);
            if (defIndex < 0)
                return false;

            return TrySpawnDefinition(defIndex, scripted: true);
        }

        int FindDefinitionIndex(string enemyId)
        {
            var defs = enemyWorld.Definitions;
            if (defs == null)
                return -1;

            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i] != null && defs[i].id == enemyId)
                    return i;
            }

            return -1;
        }

        bool TrySpawnDefinition(int defIndex, bool scripted = false)
        {
            var def = enemyWorld.Definitions[defIndex];
            if (!CanSpawnDefinition(defIndex, def, scripted))
                return false;

            var mode = settings != null ? settings.timingMode : SpawnTimingMode.ContinuousThreat;
            if (mode == SpawnTimingMode.ContinuousThreat && _budget < def.threatCost)
                return false;

            if (mode == SpawnTimingMode.ContinuousThreat)
                _budget -= def.threatCost;

            var affix = EliteAffix.None;
            if (RunManager.Instance.Rng.DirectorFloat() < _eliteChance)
                affix = (EliteAffix)RunManager.Instance.Rng.DirectorInt(1, 5);

            var speedMult = settings != null ? settings.GetSpeedMultiplier(def.id) : 1f;
            if (!TrySpawnAtValidPosition(defIndex, affix, speedMult))
                return false;

            return true;
        }

        bool TrySpawnAtValidPosition(int defIndex, EliteAffix affix, float speedMult)
        {
            var def = enemyWorld.Definitions[defIndex];
            var attempts = settings != null ? settings.spawnPositionAttempts : 16;
            var spawnRadius = def.radius + 0.35f;

            for (var i = 0; i < attempts; i++)
            {
                var position = PickSpawnPosition();
                if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, spawnRadius))
                    continue;

                enemyWorld.Spawn(defIndex, position, affix, speedMult);
                return true;
            }

            enemyWorld.Spawn(defIndex, PickSpawnPositionInArc(GetSpawnDistanceMin(), GetSpawnDistanceMax(), RunManager.Instance.Rng), affix, speedMult);
            return true;
        }

        float GetSpawnDistanceMin() => settings != null ? settings.spawnDistanceMin : GameConstants.SpawnAnnulusMin;
        float GetSpawnDistanceMax() => settings != null ? settings.spawnDistanceMax : GameConstants.SpawnAnnulusMax;

        float GetGracePeriod() => settings != null ? settings.gracePeriodSeconds : 0f;
        float GetMinSpawnGap() => settings != null ? settings.minSecondsBetweenSpawns : 0.35f;

        int GetEnemyCap()
        {
            if (settings != null && settings.maxActiveEnemies > 0)
                return settings.maxActiveEnemies;
            return Platform.QualityProfile.Current.enemyCap;
        }

        bool CanSpawnDefinition(int definitionIndex, Data.EnemyDefinition def, bool scriptedSpawn = false)
        {
            if (!scriptedSpawn)
            {
                var tuning = settings != null ? settings.GetOrDefault(def.id) : null;
                if (tuning != null && !tuning.spawnEnabled)
                    return false;
                if (settings != null && !settings.IsEnemyAvailable(def.id, RunManager.Instance.ElapsedSeconds / 60f))
                    return false;
                if (tuning == null || tuning.maxConcurrent <= 0)
                    return true;
                return enemyWorld.CountActiveByDefinition(definitionIndex) < tuning.maxConcurrent;
            }

            return !def.isBoss && !def.isMiniboss;
        }

        int PickEnemyDefinition()
        {
            var defs = enemyWorld.Definitions;
            if (defs == null || defs.Length == 0) return -1;

            var minute = RunManager.Instance.ElapsedSeconds / 60f;
            var creatureStart = settings != null ? settings.creatureChanceAtStart : 0.2f;
            var creatureLate = settings != null ? settings.creatureChanceAtLateGame : 0.55f;
            var creatureRamp = settings != null ? settings.creatureChanceRampMinutes : 6f;
            var rampT = creatureRamp > 0.01f ? Mathf.Clamp01(minute / creatureRamp) : 1f;
            var creatureChance = Mathf.Lerp(creatureStart, creatureLate, rampT);
            var preferCreature = RunManager.Instance.Rng.DirectorFloat() < creatureChance;

            var weights = new float[defs.Length];
            var any = false;
            BuildWeights(defs, weights, preferCreature, ref any);

            if (!any)
                BuildWeights(defs, weights, !preferCreature, ref any);

            if (!any)
            {
                for (var i = 0; i < defs.Length; i++)
                {
                    if (defs[i].isBoss || defs[i].isMiniboss) { weights[i] = 0f; continue; }
                    weights[i] = GetDefinitionWeight(i, defs[i]);
                    if (weights[i] > 0f) any = true;
                }
            }

            return any ? RunManager.Instance.Rng.WeightedPick(weights) : -1;
        }

        void BuildWeights(Data.EnemyDefinition[] defs, float[] weights, bool preferCreature, ref bool any)
        {
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i].isBoss || defs[i].isMiniboss)
                {
                    weights[i] = 0f;
                    continue;
                }

                var isCreature = defs[i].category == EnemyCategory.Creature;
                if (preferCreature != isCreature)
                {
                    weights[i] = 0f;
                    continue;
                }

                weights[i] = GetDefinitionWeight(i, defs[i]);
                if (weights[i] > 0f) any = true;
            }
        }

        float GetDefinitionWeight(int definitionIndex, Data.EnemyDefinition def)
        {
            if (settings != null && !settings.IsEnemyAvailable(def.id, RunManager.Instance.ElapsedSeconds / 60f))
                return 0f;

            var tuning = settings != null ? settings.GetOrDefault(def.id) : null;
            if (tuning != null && !tuning.spawnEnabled)
                return 0f;
            if (tuning != null && tuning.maxConcurrent > 0 &&
                enemyWorld.CountActiveByDefinition(definitionIndex) >= tuning.maxConcurrent)
                return 0f;

            var weightMult = tuning != null ? tuning.weight : 1f;
            return (1f / Mathf.Max(1, def.threatCost)) * Mathf.Max(0f, weightMult);
        }

        void TrySpawnMinibosses()
        {
            var elapsed = RunManager.Instance.ElapsedSeconds;
            var stageDuration = GameConstants.StageDurationSeconds;
            if (!_miniboss30 && elapsed >= stageDuration * 0.30f)
            {
                _miniboss30 = true;
                SpawnMiniboss();
            }
            if (!_miniboss72 && elapsed >= stageDuration * 0.72f)
            {
                _miniboss72 = true;
                SpawnMiniboss();
            }
        }

        void SpawnMiniboss()
        {
            var defs = enemyWorld.Definitions;
            for (var i = 0; i < defs.Length; i++)
            {
                if (!defs[i].isMiniboss) continue;
                var speedMult = settings != null ? settings.GetSpeedMultiplier(defs[i].id) : 1f;
                TrySpawnAtValidPosition(i, EliteAffix.Rich, speedMult);
                break;
            }
        }

        Vector3 PickSpawnPosition()
        {
            var rng = RunManager.Instance.Rng;
            var minDist = GetSpawnDistanceMin();
            var maxDist = GetSpawnDistanceMax();
            var mode = GetSpawnPlacementMode();

            return mode switch
            {
                SpawnPlacementMode.ScreenEdge => PickSpawnAtScreenEdge(minDist, maxDist, rng),
                SpawnPlacementMode.OffCamera => PickSpawnPositionOutsideCameraView(minDist, maxDist, rng),
                _ => PickSpawnPositionInArc(minDist, maxDist, rng)
            };
        }

        SpawnPlacementMode GetSpawnPlacementMode()
        {
            if (settings == null)
                return SpawnPlacementMode.ScreenEdge;

            if (settings.spawnPlacement != SpawnPlacementMode.Annulus)
                return settings.spawnPlacement;

            return settings.spawnOutsideCameraView
                ? SpawnPlacementMode.OffCamera
                : SpawnPlacementMode.Annulus;
        }

        Vector3 PickSpawnAtScreenEdge(float minDist, float maxDist, Core.RunRng rng)
        {
            var cam = GetSpawnCamera();
            var attempts = settings != null ? settings.spawnPositionAttempts : 16;
            var margin = settings != null ? settings.cameraViewportMargin : 0.05f;
            var planeY = player.position.y;

            if (cam != null)
            {
                for (var i = 0; i < attempts; i++)
                {
                    var edge = rng.DirectorFloat();
                    var u = rng.DirectorFloat();
                    float vx;
                    float vy;
                    if (edge < 0.25f)
                    {
                        vx = u;
                        vy = -margin;
                    }
                    else if (edge < 0.5f)
                    {
                        vx = 1f + margin;
                        vy = u;
                    }
                    else if (edge < 0.75f)
                    {
                        vx = u;
                        vy = 1f + margin;
                    }
                    else
                    {
                        vx = -margin;
                        vy = u;
                    }

                    var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
                    if (!TryIntersectWaterPlane(ray, planeY, out var hitPoint))
                        continue;

                    hitPoint = ClampSpawnDistance(hitPoint, minDist, maxDist, planeY);
                    if (IsSpawnCandidateValid(hitPoint, 1.1f))
                        return hitPoint;
                }
            }

            return PickSpawnPositionInArc(minDist, maxDist, rng);
        }

        static bool TryIntersectWaterPlane(Ray ray, float planeY, out Vector3 hitPoint)
        {
            hitPoint = default;
            if (Mathf.Abs(ray.direction.y) < 0.0001f)
                return false;

            var t = (planeY - ray.origin.y) / ray.direction.y;
            if (t < 0f)
                return false;

            hitPoint = ray.origin + ray.direction * t;
            hitPoint.y = planeY;
            return true;
        }

        Vector3 ClampSpawnDistance(Vector3 point, float minDist, float maxDist, float planeY)
        {
            var offset = point - player.position;
            offset.y = 0f;
            var dist = offset.magnitude;
            if (dist < 0.01f)
                return player.position + Vector3.forward * minDist;

            if (dist >= minDist && dist <= maxDist)
                return point;

            var clamped = Mathf.Clamp(dist, minDist, maxDist);
            var result = player.position + offset.normalized * clamped;
            result.y = planeY;
            return result;
        }

        bool IsSpawnCandidateValid(Vector3 point, float radius)
        {
            if (!IslandObstacles.IsAvailable)
                return true;

            return !IslandObstacles.IsPositionBlocked(point, radius);
        }

        Vector3 PickSpawnPositionOutsideCameraView(float minDist, float maxDist, Core.RunRng rng)
        {
            var cam = GetSpawnCamera();
            var margin = settings != null ? settings.cameraViewportMargin : 0.08f;
            var attempts = settings != null ? settings.spawnPositionAttempts : 12;

            for (var i = 0; i < attempts; i++)
            {
                var pos = PickSpawnPositionInArc(minDist, maxDist, rng);
                if (!IsWorldPositionVisible(pos, cam, margin))
                    return pos;
            }

            return PickSpawnNearCameraSide(minDist, maxDist, rng);
        }

        Vector3 PickSpawnPositionInArc(float minDist, float maxDist, Core.RunRng rng)
        {
            var arc = settings != null ? settings.spawnArcDegrees : 360f;
            var yawOffset = settings != null ? settings.spawnArcYawOffset : 0f;
            var dist = Mathf.Lerp(minDist, maxDist, rng.DirectorFloat());
            var angle = rng.DirectorFloat() * Mathf.PI * 2f;

            if (arc < 359f)
            {
                var playerYaw = player != null ? player.eulerAngles.y : 0f;
                var halfArc = arc * 0.5f * Mathf.Deg2Rad;
                var center = (playerYaw + yawOffset) * Mathf.Deg2Rad;
                angle = center + (rng.DirectorFloat() * 2f - 1f) * halfArc;
            }

            var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            return player.position + offset;
        }

        Vector3 PickSpawnNearCameraSide(float minDist, float maxDist, Core.RunRng rng)
        {
            var cam = GetSpawnCamera();
            var dist = Mathf.Lerp(minDist, maxDist, rng.DirectorFloat());
            var direction = Vector3.back;

            if (cam != null)
            {
                direction = cam.transform.position - player.position;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.01f)
                    direction = -cam.transform.forward;
                direction.y = 0f;
            }

            if (direction.sqrMagnitude < 0.01f)
                direction = Vector3.back;

            direction.Normalize();
            var baseAngle = Mathf.Atan2(direction.z, direction.x);
            var spread = 85f * Mathf.Deg2Rad;
            var angle = baseAngle + (rng.DirectorFloat() * 2f - 1f) * spread;
            var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            return player.position + offset;
        }

        Camera GetSpawnCamera()
        {
            if (spawnCamera != null)
                return spawnCamera;

            if (_cachedSpawnCamera == null)
                _cachedSpawnCamera = Camera.main;

            return _cachedSpawnCamera;
        }

        static bool IsWorldPositionVisible(Vector3 worldPosition, Camera camera, float viewportMargin)
        {
            if (camera == null)
                return false;

            var viewport = camera.WorldToViewportPoint(worldPosition);
            if (viewport.z <= 0f)
                return false;

            return viewport.x >= -viewportMargin
                && viewport.x <= 1f + viewportMargin
                && viewport.y >= -viewportMargin
                && viewport.y <= 1f + viewportMargin;
        }

        public int GetChestPrice()
        {
            var cost = GameMath.ChestCost(_paidChestIndex);
            _paidChestIndex++;
            return Mathf.RoundToInt(cost);
        }
    }
}
