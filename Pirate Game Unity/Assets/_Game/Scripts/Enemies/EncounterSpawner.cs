using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.Enemies
{
    public sealed class EncounterSpawner : MonoBehaviour
    {
        public static EncounterSpawner Instance { get; private set; }

        [SerializeField] EnemyWorld enemyWorld;
        [SerializeField] Transform player;
        [SerializeField] Camera spawnCamera;
        [SerializeField] EncounterSpawnSchedule schedule;

        Camera _cachedCamera;
        float _spawnRateMultiplier = 1f;
        bool _ambientScheduleEnabled = true;
        float _globalSpawnGap;
        int _nextProfileInstanceId = 1;
        readonly List<ActiveProfileRun> _activeProfiles = new(32);
        readonly Dictionary<int, int> _profileActiveCounts = new();
        readonly List<TriggeredRequest> _triggerQueue = new(16);

        struct TriggeredRequest
        {
            public SpawnProfile profile;
            public int burstCountOverride;
        }

        sealed class ActiveProfileRun
        {
            public SpawnProfile profile;
            public int instanceId;
            public float elapsed;
            public float nextSpawnAt;
            public int spawned;
            public int loopIndex;
            public bool complete;
            public float cooldownUntil;
            public int leaderAgentId = -1;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("EncounterSpawner: duplicate instance destroyed.");
                Destroy(this);
                return;
            }

            Instance = this;
            if (enemyWorld == null) enemyWorld = FindFirstObjectByType<EnemyWorld>();
            if (player == null)
            {
                var ship = FindFirstObjectByType<PlayerShipController>();
                if (ship != null) player = ship.transform;
            }

            if (schedule == null)
            {
                schedule = Resources.Load<EncounterSpawnSchedule>("EncounterSpawnSchedule");
                if (schedule == null)
                    Debug.LogError(
                        "EncounterSpawner: Assign EncounterSpawnSchedule or add Assets/_Game/Resources/EncounterSpawnSchedule.asset " +
                        "(HighSeas → Encounter Spawns → Ensure Empty Schedule Shell).");
            }

            _cachedCamera = spawnCamera != null ? spawnCamera : Camera.main;
            _globalSpawnGap = schedule != null ? schedule.globalMinSpawnGapSeconds : 0.3f;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (schedule == null) return;

            if (RunManager.Instance == null || RunManager.Instance.IsPaused) return;
            if (RunManager.Instance.IsRunEnded) return;
            if (RunManager.Instance.State != RunState.Playing && RunManager.Instance.State != RunState.BossFight) return;

            ProcessTriggerQueue();

            if (!_ambientScheduleEnabled)
                return;

            if (RunManager.Instance.IsOvertime)
                return;

            Tick(Time.deltaTime);
        }

        public bool AmbientScheduleEnabled => _ambientScheduleEnabled;

        public void SetAmbientScheduleEnabled(bool enabled)
        {
            _ambientScheduleEnabled = enabled;
            if (!enabled)
                _activeProfiles.Clear();
            else if (RunManager.Instance != null && RunManager.Instance.IsGameplayActive)
                BootstrapScheduleEntries();
        }

        public void ResetForNewRun()
        {
            _activeProfiles.Clear();
            _profileActiveCounts.Clear();
            _triggerQueue.Clear();
            _spawnRateMultiplier = 1f;
            _globalSpawnGap = schedule != null ? schedule.globalMinSpawnGapSeconds : 0.3f;
            _nextProfileInstanceId = 1;
            BootstrapScheduleEntries();
        }

        public void SetSpawnRateMultiplier(float multiplier) =>
            _spawnRateMultiplier = Mathf.Max(0.1f, multiplier);

        public float DebugSpawnRateMultiplier => _spawnRateMultiplier;

        public void TriggerProfile(SpawnProfile profile, int countOverride = 0)
        {
            if (profile == null) return;
            _triggerQueue.Add(new TriggeredRequest { profile = profile, burstCountOverride = countOverride });
        }

        public bool TrySpawnPressureBeat(SpawnProfile profile) =>
            profile != null && TryExecuteSpawnBurst(profile, 1, true, 1f, 1f, 0, ref _dummyLeader);

        public bool TrySpawnOvertime(SpawnProfile profile, float overtimeMinutes)
        {
            if (profile == null || !RunManager.Instance.IsOvertime)
                return false;
            var hpMult = Mathf.Pow(2f, overtimeMinutes * 0.5f);
            var dmgMult = 1f + overtimeMinutes * 0.12f;
            return TryExecuteSpawnBurst(profile, 1, false, hpMult, dmgMult, 0, ref _dummyLeader);
        }

        public void Tick(float dt)
        {
            if (enemyWorld == null || player == null) return;
            var grace = schedule != null ? schedule.gracePeriodSeconds : 2f;
            if (RunManager.Instance.ElapsedSeconds < grace)
                return;

            ProcessTriggerQueue();
            TickScheduledProfiles(dt);
        }

        void BootstrapScheduleEntries()
        {
            if (schedule?.entries == null) return;
            for (var i = 0; i < schedule.entries.Length; i++)
            {
                var entry = schedule.entries[i];
                if (entry?.profile == null) continue;
                _activeProfiles.Add(new ActiveProfileRun
                {
                    profile = entry.profile,
                    instanceId = _nextProfileInstanceId++,
                    nextSpawnAt = entry.startTimeSeconds + entry.profile.timing.startDelaySeconds,
                    elapsed = -entry.startTimeSeconds
                });
            }
        }

        void ProcessTriggerQueue()
        {
            for (var i = 0; i < _triggerQueue.Count; i++)
            {
                var req = _triggerQueue[i];
                var count = req.burstCountOverride > 0 ? req.burstCountOverride : req.profile.timing.spawnCount;
                TryExecuteSpawnBurst(req.profile, count, true);
            }

            _triggerQueue.Clear();
        }

        void TickScheduledProfiles(float dt)
        {
            var runTime = RunManager.Instance.ElapsedSeconds;
            for (var i = 0; i < _activeProfiles.Count; i++)
            {
                var run = _activeProfiles[i];
                if (run.complete)
                    continue;

                if (run.cooldownUntil > runTime)
                    continue;

                run.elapsed += dt * _spawnRateMultiplier;
                if (runTime < run.nextSpawnAt)
                    continue;

                if (!CanSpawnMoreFromProfile(run.profile))
                {
                    run.nextSpawnAt = runTime + 0.25f;
                    _activeProfiles[i] = run;
                    continue;
                }

                var spawnedThisTick = ExecuteProfileSpawn(run);
                if (spawnedThisTick > 0)
                {
                    run.spawned += spawnedThisTick;
                    run.nextSpawnAt = runTime + GetIntervalForProfile(run.profile);
                }
                else if (run.profile.placement.fallbackPolicy == SpawnFallbackPolicy.RetryLater)
                    run.nextSpawnAt = runTime + 0.35f;

                if (IsProfileRunComplete(run))
                {
                    run.complete = true;
                    run.cooldownUntil = runTime + run.profile.timing.cooldownAfterCompleteSeconds;
                }

                _activeProfiles[i] = run;
            }
        }

        float GetIntervalForProfile(SpawnProfile profile)
        {
            var mode = profile.spawnMode;
            if (mode == SpawnMode.Continuous)
                return Mathf.Max(_globalSpawnGap, profile.timing.spawnIntervalSeconds / _spawnRateMultiplier);
            if (mode == SpawnMode.Sequence || mode == SpawnMode.TimedWave)
                return Mathf.Max(_globalSpawnGap, profile.timing.spawnIntervalSeconds);
            return _globalSpawnGap;
        }

        bool IsProfileRunComplete(ActiveProfileRun run)
        {
            var timing = run.profile.timing;
            if (run.profile.spawnMode == SpawnMode.Continuous)
            {
                if (timing.durationSeconds <= 0f)
                    return false;
                return run.elapsed >= timing.durationSeconds;
            }

            return run.spawned >= timing.spawnCount;
        }

        bool CanSpawnMoreFromProfile(SpawnProfile profile)
        {
            if (enemyWorld.ActiveCount >= GetEnemyCap())
                return false;
            if (profile.timing.maxActiveFromProfile <= 0)
                return true;
            var id = profile.GetInstanceID();
            _profileActiveCounts.TryGetValue(id, out var count);
            return count < profile.timing.maxActiveFromProfile;
        }

        int ExecuteProfileSpawn(ActiveProfileRun run)
        {
            var profile = run.profile;
            return profile.spawnMode switch
            {
                SpawnMode.Single => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId) ? 1 : 0,
                SpawnMode.Sequence => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId) ? 1 : 0,
                SpawnMode.Burst => TryExecuteSpawnBurst(profile, profile.timing.spawnCount, false, 1f, 1f, run.instanceId, ref run.leaderAgentId)
                    ? profile.timing.spawnCount
                    : 0,
                SpawnMode.Group => TryExecuteSpawnBurst(profile, profile.timing.spawnCount, false, 1f, 1f, run.instanceId, ref run.leaderAgentId)
                    ? profile.timing.spawnCount
                    : 0,
                SpawnMode.Continuous => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId) ? 1 : 0,
                SpawnMode.TimedWave => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId) ? 1 : 0,
                _ => 0
            };
        }

        bool TryExecuteSpawnBurst(
            SpawnProfile profile,
            int count,
            bool ignoreCapSoft,
            float hpMult,
            float dmgMult,
            int profileInstanceId,
            ref int leaderAgentId)
        {
            if (profile?.enemy == null || enemyWorld == null || player == null)
                return false;

            if (!ignoreCapSoft && enemyWorld.ActiveCount >= GetEnemyCap())
                return false;

            var defIndex = FindDefinitionIndex(profile.enemy);
            if (defIndex < 0)
                return false;

            profileInstanceId = profileInstanceId > 0 ? profileInstanceId : _nextProfileInstanceId++;
            var spawnedAny = false;
            count = Mathf.Max(1, count);
            for (var i = 0; i < count; i++)
            {
                if (enemyWorld.ActiveCount >= GetEnemyCap())
                    break;

                var role = ResolveFormationRole(profile, i, count);
                if (role == FormationRole.Leader)
                    leaderAgentId = -1;

                if (!SpawnPlacementService.TryPickOffScreenPosition(
                        player,
                        GetCamera(),
                        profile.placement,
                        profile.enemy.radius + 0.35f,
                        RunManager.Instance.Rng,
                        out var pos))
                    continue;

                SpawnPlacementService.OffsetGroupPosition(
                    profile.groupLayout,
                    i,
                    count,
                    profile.placement.groupSpacing,
                    player,
                    ref pos);

                var slot = enemyWorld.SpawnFromProfile(
                    defIndex,
                    pos,
                    profile,
                    profileInstanceId,
                    role,
                    i,
                    leaderAgentId,
                    hpMult,
                    dmgMult);

                if (slot < 0)
                    continue;

                spawnedAny = true;
                if (role == FormationRole.Leader || (role == FormationRole.Auto && i == 0))
                    leaderAgentId = enemyWorld.Agents[slot].id;

                TrackProfileSpawn(profile);
            }

            return spawnedAny;
        }

        bool TryExecuteSpawnBurst(SpawnProfile profile, int count, bool ignoreCapSoft) =>
            TryExecuteSpawnBurst(profile, count, ignoreCapSoft, 1f, 1f, 0, ref _dummyLeader);

        int _dummyLeader = -1;

        static FormationRole ResolveFormationRole(SpawnProfile profile, int index, int count)
        {
            if (profile.specialBehavior != EnemySpecialBehavior.Formation)
                return FormationRole.Auto;
            if (profile.formation.role != FormationRole.Auto)
                return profile.formation.role;
            return index == 0 ? FormationRole.Leader : FormationRole.Follower;
        }

        void TrackProfileSpawn(SpawnProfile profile)
        {
            var id = profile.GetInstanceID();
            _profileActiveCounts.TryGetValue(id, out var count);
            _profileActiveCounts[id] = count + 1;
        }

        public void NotifyAgentDespawned(SpawnProfile profile)
        {
            if (profile == null) return;
            var id = profile.GetInstanceID();
            if (!_profileActiveCounts.TryGetValue(id, out var count)) return;
            _profileActiveCounts[id] = Mathf.Max(0, count - 1);
        }

        int FindDefinitionIndex(EnemyDefinition def)
        {
            var defs = enemyWorld.Definitions;
            if (defs == null || def == null) return -1;
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i] == def || defs[i]?.id == def.id)
                    return i;
            }

            return -1;
        }

        public int FindDefinitionIndexById(string enemyId)
        {
            var defs = enemyWorld?.Definitions;
            if (defs == null || string.IsNullOrEmpty(enemyId)) return -1;
            for (var i = 0; i < defs.Length; i++)
            {
                if (defs[i]?.id == enemyId)
                    return i;
            }

            return -1;
        }

        int GetEnemyCap()
        {
            var cap = schedule != null && schedule.maxActiveEnemies > 0
                ? schedule.maxActiveEnemies
                : Platform.QualityProfile.Current.enemyCap;
            return Mathf.Min(Mathf.Max(cap, 8), 180);
        }

        Camera GetCamera()
        {
            if (spawnCamera != null)
                return spawnCamera;
            if (_cachedCamera == null || !_cachedCamera.isActiveAndEnabled)
                _cachedCamera = Camera.main;
            return _cachedCamera;
        }

        public EncounterSpawnSchedule Schedule => schedule;

        public void SetSchedule(EncounterSpawnSchedule newSchedule)
        {
            schedule = newSchedule;
            _globalSpawnGap = schedule != null ? schedule.globalMinSpawnGapSeconds : 0.3f;
        }

        int _paidChestIndex = 1;

        public int GetChestPrice()
        {
            var cost = GameMath.ChestCost(_paidChestIndex);
            _paidChestIndex++;
            return Mathf.RoundToInt(cost);
        }

        public void ApplyCombatExperiment(CombatExperimentProfile profile)
        {
            profile ??= CombatExperimentContext.Active;
            SetAmbientScheduleEnabled(profile.encounterSpawnEnabled);
            SetSpawnRateMultiplier(Mathf.Max(0.1f, profile.spawnDensityMultiplier));
            enemyWorld?.SetExperimentMultipliers(profile.enemyHpMultiplier, profile.enemyDamageMultiplier);
        }

        public void ConfigureV3Pacing(bool enabled)
        {
            if (enabled)
                ResetForNewRun();
        }

        public bool TrySpawnPressureBeat(string enemyId)
        {
            if (schedule == null) return false;
            var profile = enemyId switch
            {
                "hammerhead" => schedule.hammerheadBeat,
                "kraken_spawn" => schedule.krakenBeat,
                _ => null
            };
            return profile != null && TrySpawnPressureBeat(profile);
        }

        public bool TrySpawnOvertimeEnemy(string enemyId, float overtimeMinutes)
        {
            if (schedule == null) return false;
            var profile = enemyId switch
            {
                "drowned_pursuer" => schedule.drownedPursuer,
                "drowned_harrier" => schedule.drownedHarrier,
                _ => null
            };
            return profile != null && TrySpawnOvertime(profile, overtimeMinutes);
        }

        public SpawnProfile GetScheduleProfile(string key) =>
            schedule == null ? null : key switch
            {
                "powder_cache" => schedule.powderCacheAmbush,
                "boss" => schedule.bossSummon,
                "route_raider" => schedule.routeTreasureRaider,
                "route_swordfish" => schedule.routeCursedRingSwordfish,
                "route_cannon" => schedule.routeCursedRingCannon,
                "swarm_pulse" => schedule.swarmPulse,
                _ => null
            };

        public int GetEnemyCapForDebug() => GetEnemyCap();
        public float GetEffectiveThreatPerSecond() => _spawnRateMultiplier * 4f;
    }
}
