using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Enemies
{
    public sealed class SpawnDirector : MonoBehaviour
    {
        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] Transform player;
        [SerializeField] SpawnDirectorSettings settings;
        [SerializeField] Camera spawnCamera;

        Camera _cachedSpawnCamera;
        FollowCamera _cachedFollowCamera;

        float _budget;
        float _eliteChance;
        float _spawnCooldown;
        int _paidChestIndex = 1;
        bool _v3PressureBeatsEnabled = true;
        float _spawnDensityMultiplier = 1f;
        float _enemyHpMultiplier = 1f;
        float _enemyDamageMultiplier = 1f;
        float _spawnRateMultiplier = 1f;
        bool _settingsInstanced;
        float _nextPackTime;
        readonly List<string> _packSpawnQueue = new(32);

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

        void ResetTimingState() => _spawnCooldown = 0f;

        void Update()
        {
            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;
            if (RunManager.Instance.State != RunState.Playing && RunManager.Instance.State != RunState.BossFight) return;
            Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (enemyWorld == null || player == null) return;
            if (RunManager.Instance.ElapsedSeconds < GetGracePeriod())
                return;

            var minute = RunManager.Instance.ElapsedSeconds / 60f;
            _eliteChance = minute < 3f ? 0f : 0.02f;

            TickDirectorPackSpawns();

            if (_spawnCooldown > 0f)
                _spawnCooldown -= dt;

            TickContinuousThreat(dt);
        }

        static bool IsCannonboatWarmup() =>
            RunManager.Instance != null
            && RunManager.Instance.ElapsedSeconds < GameConstants.CannonboatWarmupSeconds;

        void TickContinuousThreat(float dt)
        {
            if (RunManager.Instance.IsOvertime)
                return;

            var budgetCapMult = settings != null ? settings.budgetCapMultiplier : 3.5f;
            var threatPerSec = ComputeThreatPerSecond() * _spawnRateMultiplier;
            var earlyPace = RunManager.Instance.ElapsedSeconds < GameConstants.EarlySpawnThreatWindowSeconds
                ? GameConstants.EarlySpawnThreatMultiplier
                : 1f;
            _budget += threatPerSec * dt * earlyPace;
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

            var budgetCost = 1f;
            if (def.id == "cannonboat")
                budgetCost = 1f / GameConstants.CannonboatSpawnRateMultiplier;
            if (_budget < budgetCost)
                return false;

            _budget -= budgetCost;

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
                if (!IsOffScreenSpawnAllowed(def, position))
                    continue;
                if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, spawnRadius))
                    continue;

                enemyWorld.Spawn(defIndex, position, affix, speedMult);
                return true;
            }

            return false;
        }

        public bool TrySpawnOvertimeEnemy(string enemyId, float overtimeMinutes)
        {
            if (enemyWorld == null || !RunManager.Instance.IsOvertime)
                return false;

            var defIndex = FindDefinitionIndex(enemyId);
            if (defIndex < 0)
                return false;

            var hpMult = Mathf.Pow(2f, overtimeMinutes * 0.5f);
            var dmgMult = 1f + overtimeMinutes * 0.12f;
            var attempts = settings != null ? settings.spawnPositionAttempts : 16;
            var radius = enemyWorld.Definitions[defIndex].radius + 0.35f;

            var def = enemyWorld.Definitions[defIndex];
            for (var i = 0; i < attempts; i++)
            {
                var pos = PickSpawnPosition();
                if (!IsOffScreenSpawnAllowed(def, pos))
                    continue;
                if (!IsSpawnCandidateValid(pos, radius))
                    continue;

                enemyWorld.Spawn(defIndex, pos, EliteAffix.None, 1f, hpMult, dmgMult);
                return true;
            }

            return false;
        }

        float GetSpawnDistanceMin() => settings != null ? settings.spawnDistanceMin : GameConstants.SpawnAnnulusMin;
        float GetSpawnDistanceMax() => settings != null ? settings.spawnDistanceMax : GameConstants.SpawnAnnulusMax;

        float GetGracePeriod() => settings != null ? settings.gracePeriodSeconds : 0f;
        float GetMinSpawnGap()
        {
            var gap = settings != null ? settings.minSecondsBetweenSpawns : 0.35f;
            return Mathf.Max(gap, 0.4f);
        }

        int GetEnemyCap()
        {
            var cap = settings != null && settings.maxActiveEnemies > 0
                ? settings.maxActiveEnemies
                : Platform.QualityProfile.Current.enemyCap;
            return Mathf.Min(Mathf.Max(cap, 180), 180);
        }

        public int GetEnemyCapForDebug() => GetEnemyCap();

        bool CanSpawnDefinition(int definitionIndex, Data.EnemyDefinition def, bool scriptedSpawn = false)
        {
            if (!scriptedSpawn && IsCannonboatWarmup() && def.id != "cannonboat")
                return false;

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

            if (IsCannonboatWarmup())
                return FindDefinitionIndex("cannonboat");

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
            if (IsCannonboatWarmup())
                return def.id == "cannonboat" ? 1f : 0f;

            if (settings != null && !settings.IsEnemyAvailable(def.id, RunManager.Instance.ElapsedSeconds / 60f))
                return 0f;

            var tuning = settings != null ? settings.GetOrDefault(def.id) : null;
            if (tuning != null && !tuning.spawnEnabled)
                return 0f;
            if (tuning != null && tuning.maxConcurrent > 0 &&
                enemyWorld.CountActiveByDefinition(definitionIndex) >= tuning.maxConcurrent)
                return 0f;

            var weightMult = tuning != null ? tuning.weight : 1f;
            var baseWeight = (1f / Mathf.Max(1, def.threatCost)) * Mathf.Max(0f, weightMult);
            var phaseMult = CombatDirectorV2Composition.GetWeightMultiplier(def.id, RunManager.Instance.ElapsedSeconds / 60f);
            if (phaseMult <= 0f) return 0f;
            var weight = baseWeight * phaseMult;
            if (def.id == "cannonboat")
                weight *= GameConstants.CannonboatSpawnRateMultiplier;
            return weight;
        }

        public void SetV3PressureBeatsEnabled(bool enabled) => _v3PressureBeatsEnabled = enabled;

        public void ConfigureV3Pacing(bool enabled)
        {
            if (enabled)
            {
                _v3PressureBeatsEnabled = true;
                ResetTimingState();
            }
        }

        public void ResetForNewRun()
        {
            ResetTimingState();
            _budget = 0f;
            _eliteChance = 0f;
            _paidChestIndex = 1;
            _nextPackTime = 120f;
            _packSpawnQueue.Clear();
        }

        public void ApplyCombatExperiment(CombatExperimentProfile profile)
        {
            profile ??= CombatExperimentContext.Active;
            _spawnDensityMultiplier = profile.spawnDensityMultiplier;
            _enemyHpMultiplier = profile.enemyHpMultiplier;
            _enemyDamageMultiplier = profile.enemyDamageMultiplier;
            _nextPackTime = 120f;
            enemyWorld?.SetExperimentMultipliers(_enemyHpMultiplier, _enemyDamageMultiplier);
        }

        void TickDirectorPackSpawns()
        {
            if (_packSpawnQueue.Count > 0)
            {
                if (_spawnCooldown > 0f || enemyWorld.ActiveCount >= GetEnemyCap())
                    return;

                var enemyId = _packSpawnQueue[0];
                _packSpawnQueue.RemoveAt(0);
                if (TrySpawnEnemyId(enemyId))
                    _spawnCooldown = GetMinSpawnGap();
                return;
            }

            if (RunManager.Instance == null) return;
            var elapsed = RunManager.Instance.ElapsedSeconds;
            if (elapsed < GameConstants.CannonboatWarmupSeconds) return;
            if (elapsed < _nextPackTime) return;
            if (enemyWorld.ActiveCount >= GetEnemyCap() - 4) return;

            var minute = elapsed / 60f;
            _nextPackTime = elapsed + (minute >= 6f ? 18f : 24f);
            var pack = SpawnPackRegistry.GetPack(minute);
            for (var i = 0; i < pack.Count; i++)
            {
                for (var c = 0; c < pack[i].Count; c++)
                    _packSpawnQueue.Add(pack[i].EnemyId);
            }
        }

        public void SetSpawnRateMultiplier(float multiplier) =>
            _spawnRateMultiplier = Mathf.Max(0.1f, multiplier);

        public void SetSpawnDensityMultiplier(float multiplier) =>
            _spawnDensityMultiplier = Mathf.Max(0.1f, multiplier);

        public void SetEnemyCombatMultipliers(float hpMultiplier, float damageMultiplier)
        {
            _enemyHpMultiplier = Mathf.Max(0.1f, hpMultiplier);
            _enemyDamageMultiplier = Mathf.Max(0.1f, damageMultiplier);
            enemyWorld?.SetExperimentMultipliers(_enemyHpMultiplier, _enemyDamageMultiplier);
        }

        public SpawnDirectorSettings GetMutableSettings()
        {
            if (settings != null && !_settingsInstanced)
            {
                settings = Instantiate(settings);
                settings.hideFlags = HideFlags.DontSave;
                _settingsInstanced = true;
            }

            return settings;
        }

        public float DebugThreatBudget => _budget;
        public float DebugEliteChance => _eliteChance;
        public float DebugSpawnRateMultiplier => _spawnRateMultiplier;
        public float DebugSpawnDensityMultiplier => _spawnDensityMultiplier;
        public float DebugEnemyHpMultiplier => _enemyHpMultiplier;
        public float DebugEnemyDamageMultiplier => _enemyDamageMultiplier;
        public bool DebugV3PacingEnabled => true;
        public bool DebugDirectorV2Enabled => true;

        public float GetEffectiveThreatPerSecond()
        {
            if (RunManager.Instance == null) return 0f;
            return ComputeThreatPerSecond() * _spawnRateMultiplier;
        }

        public void ClearThreatBudget() => _budget = 0f;

        public bool TrySpawnPressureBeat(string enemyId)
        {
            if (enemyWorld == null || string.IsNullOrEmpty(enemyId) || player == null)
                return false;

            var defIndex = FindDefinitionIndex(enemyId);
            if (defIndex < 0)
                return false;

            var radius = enemyWorld.Definitions[defIndex].radius + 0.35f;
            var attempts = settings != null ? settings.spawnPositionAttempts : 16;
            for (var i = 0; i < attempts; i++)
            {
                var pos = PickPressureBeatPosition();
                var def = enemyWorld.Definitions[defIndex];
                if (!IsOffScreenSpawnAllowed(def, pos))
                    continue;
                if (!IsSpawnCandidateValid(pos, radius))
                    continue;

                if (enemyWorld.SpawnPressureBeat(defIndex, pos) >= 0)
                    return true;
            }

            return false;
        }

        Vector3 PickPressureBeatPosition() => PickSpawnPosition();

        Vector3 PickSpawnPosition()
        {
            var rng = RunManager.Instance.Rng;
            var minDist = GetSpawnDistanceMin();
            var maxDist = GetSpawnDistanceMax();
            return PickSpawnPositionUniformRing(minDist, maxDist, rng);
        }

        Vector3 PickSpawnPositionUniformRing(float minDist, float maxDist, Core.RunRng rng)
        {
            var attempts = settings != null ? settings.spawnPositionAttempts : 16;
            for (var i = 0; i < attempts; i++)
            {
                var dist = Mathf.Lerp(minDist, maxDist, rng.DirectorFloat());
                var angle = rng.DirectorFloat() * Mathf.PI * 2f;
                var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
                var pos = player.position + offset;
                if (IsSpawnCandidateValid(pos, 1.1f))
                    return pos;
            }

            var fallbackAngle = rng.DirectorFloat() * Mathf.PI * 2f;
            var fallbackDist = Mathf.Lerp(minDist, maxDist, rng.DirectorFloat());
            return player.position + new Vector3(Mathf.Cos(fallbackAngle) * fallbackDist, 0f, Mathf.Sin(fallbackAngle) * fallbackDist);
        }

        SpawnPlacementMode GetSpawnPlacementMode() => SpawnPlacementMode.Annulus;

        bool UsesSurroundPlayerSpawns() => true;

        bool IsOffScreenSpawnAllowed(Data.EnemyDefinition def, Vector3 position) => true;

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

                    hitPoint = EnforceSpawnDistance(hitPoint, minDist, maxDist, planeY);
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

        /// <summary>Push spawns outward if too close; never pull inward (keeps off-screen edge hits valid).</summary>
        Vector3 EnforceSpawnDistance(Vector3 point, float minDist, float maxDist, float planeY)
        {
            var offset = point - player.position;
            offset.y = 0f;
            var dist = offset.magnitude;
            if (dist < 0.01f)
                return player.position + Vector3.forward * minDist;

            if (dist < minDist)
            {
                var result = player.position + offset.normalized * minDist;
                result.y = planeY;
                return result;
            }

            if (dist > maxDist)
            {
                var result = player.position + offset.normalized * maxDist;
                result.y = planeY;
                return result;
            }

            point.y = planeY;
            return point;
        }

        bool IsSpawnCandidateValid(Vector3 point, float radius)
        {
            if (!World.PlayBounds.IsInside(point, radius))
                return false;

            if (!IslandObstacles.IsAvailable)
                return true;

            return !IslandObstacles.IsPositionBlocked(point, radius);
        }

        Vector3 PickSpawnPositionOutsideCameraView(float minDist, float maxDist, Core.RunRng rng)
        {
            var cam = GetSpawnCamera();
            var margin = settings != null ? settings.cameraViewportMargin : 0.08f;
            var attempts = settings != null ? settings.spawnPositionAttempts : 12;

            if (cam != null)
            {
                for (var i = 0; i < attempts; i++)
                {
                    var edgePos = PickSpawnAtScreenEdge(minDist, maxDist, rng);
                    if (!IsWorldPositionVisible(edgePos, cam, margin))
                        return edgePos;
                }
            }

            for (var i = 0; i < attempts; i++)
            {
                var pos = PickSpawnPositionInArc(minDist, maxDist, rng);
                if (!IsWorldPositionVisible(pos, cam, margin))
                    return pos;
            }

            return UsesSurroundPlayerSpawns()
                ? PickSpawnPositionTacticalRing(minDist, maxDist, rng)
                : PickSpawnPositionInArc(minDist, maxDist, rng);
        }

        Vector3 PickSpawnPositionTacticalRing(float minDist, float maxDist, Core.RunRng rng)
        {
            var attempts = settings != null ? settings.spawnPositionAttempts : 16;
            for (var i = 0; i < attempts; i++)
            {
                var pos = SampleTacticalSpawnPoint(minDist, maxDist, rng);
                if (IsSpawnCandidateValid(pos, 1.1f))
                    return pos;
            }

            return SampleTacticalSpawnPoint(minDist, maxDist, rng);
        }

        Vector3 SampleTacticalSpawnPoint(float minDist, float maxDist, Core.RunRng rng)
        {
            var dist = Mathf.Lerp(minDist, maxDist, rng.DirectorFloat());
            var relativeAngle = SampleTacticalRelativeAngle(rng);
            var forward = GetPlayerMovementForward();
            var baseAngle = Mathf.Atan2(forward.z, forward.x);
            var angle = baseAngle + relativeAngle;
            var offset = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);
            return player.position + offset;
        }

        static float SampleTacticalRelativeAngle(Core.RunRng rng)
        {
            var roll = rng.DirectorFloat();
            if (roll < 0.42f)
                return (rng.DirectorFloat() * 2f - 1f) * 70f * Mathf.Deg2Rad;
            if (roll < 0.86f)
            {
                var side = rng.DirectorFloat() < 0.5f ? -1f : 1f;
                return side * Mathf.Lerp(70f, 135f, rng.DirectorFloat()) * Mathf.Deg2Rad;
            }

            var rearSide = rng.DirectorFloat() < 0.5f ? -1f : 1f;
            return rearSide * Mathf.Lerp(135f, 168f, rng.DirectorFloat()) * Mathf.Deg2Rad;
        }

        Vector3 GetPlayerMovementForward()
        {
            if (player != null)
            {
                var ship = player.GetComponent<PlayerShipController>();
                if (ship != null)
                {
                    var movement = ship.MovementForward;
                    movement.y = 0f;
                    if (movement.sqrMagnitude > 0.01f)
                        return movement.normalized;
                }

                var forward = player.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude > 0.01f)
                    return forward.normalized;
            }

            var cam = GetSpawnCamera();
            if (cam != null)
            {
                var camForward = cam.transform.forward;
                camForward.y = 0f;
                if (camForward.sqrMagnitude > 0.01f)
                    return camForward.normalized;
            }

            return Vector3.forward;
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

            if (_cachedSpawnCamera == null || !_cachedSpawnCamera.isActiveAndEnabled)
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

        /// <summary>GDD combat spec §9 Director V2 density curve.</summary>
        float ComputeThreatPerSecond()
        {
            if (RunManager.Instance == null) return GameConstants.StageBaseThreat;

            var profile = CombatExperimentContext.Active;
            var minute = Mathf.Min(10f, RunManager.Instance.ElapsedSeconds / 60f);
            var depth = RunManager.Instance.StageIndex + 1;
            var itemDensity = V3.V3RunItemEffects.Instance?.GetSpawnDensityMultiplier() ?? 1f;
            return Mathf.Min(
                16f,
                (2.5f + 0.55f * minute + 0.025f * minute * minute)
                * (1f + 0.25f * (depth - 1))
                * profile.spawnDensityMultiplier
                * _spawnDensityMultiplier
                * itemDensity);
        }
    }
}
