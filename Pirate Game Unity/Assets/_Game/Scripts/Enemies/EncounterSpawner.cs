using System.Collections.Generic;
using HighSeas.Balance;
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
        readonly SpawnModifierStack _spawnModifiers = new();
        readonly AmbientSpawnBudget _ambientBudget = new();
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
            public int runEncounterId;
        }

        sealed class ActiveProfileRun
        {
            public ScheduledSpawnEntry scheduleEntry;
            public SpawnProfile profile;
            public int instanceId;
            public float scheduledStartSeconds;
            public float elapsed;
            public float nextSpawnAt;
            public float nextAttemptAt;
            public int spawned;
            public int loopCycleIndex;
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
            _spawnModifiers.Reset(1f);
            _ambientBudget.Reset();
            _globalSpawnGap = schedule != null ? schedule.globalMinSpawnGapSeconds : 0.3f;
            _nextProfileInstanceId = 1;
            SaltwindSpawnTelemetryLogic.ResetForNewRun();
            BootstrapScheduleEntries();
        }

        public void SetSpawnRateMultiplier(float multiplier) =>
            _spawnModifiers.SetBaseRateMultiplier(Mathf.Max(0.1f, multiplier));

        public void SetTimedSpawnModifier(string key, float rateMultiplier, float durationSeconds)
        {
            if (RunManager.Instance == null) return;
            _spawnModifiers.SetTimedModifier(key, rateMultiplier, RunManager.Instance.ElapsedSeconds, durationSeconds);
        }

        public void ClearSpawnModifier(string key) => _spawnModifiers.Remove(key);

        public float DebugSpawnRateMultiplier => GetEffectiveRateMultiplier();

        float GetEffectiveRateMultiplier()
        {
            var elapsed = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            var mult = _spawnModifiers.GetRateMultiplier(elapsed);
            if (RunManager.Instance != null &&
                RunManager.Instance.State == RunState.BossFight &&
                StageBalanceProfileContext.Export?.Spawn != null)
                mult *= StageBalanceProfileContext.Export.Spawn.BossAmbientRateMultiplier;
            return mult;
        }

        public void TriggerProfile(SpawnProfile profile, int countOverride = 0, int runEncounterId = 0)
        {
            if (profile == null) return;
            _triggerQueue.Add(new TriggeredRequest
            {
                profile = profile,
                burstCountOverride = countOverride,
                runEncounterId = runEncounterId
            });
        }

        /// <summary>Queue then process triggers immediately (boss/cache encounters).</summary>
        public int TriggerProfileImmediate(SpawnProfile profile, int countOverride, int runEncounterId)
        {
            TriggerProfile(profile, countOverride, runEncounterId);
            return ProcessTriggerQueue();
        }

        public bool TrySpawnPressureBeat(SpawnProfile profile) =>
            profile != null && TryExecuteSpawnBurst(profile, 1, true, 1f, 1f, 0, ref _dummyLeader) > 0;

        public bool TrySpawnOvertime(SpawnProfile profile, float overtimeMinutes)
        {
            if (profile == null || !RunManager.Instance.IsOvertime)
                return false;

            float hpMult;
            float dmgMult;
            if (StageBalanceProfileContext.IsActive && RunManager.Instance != null)
            {
                var export = StageBalanceProfileContext.Export;
                var elapsed = RunManager.Instance.ElapsedSeconds;
                hpMult = SaltwindFinaleLogic.GetOvertimeEnemyHpMultiplier(elapsed, export);
                dmgMult = SaltwindFinaleLogic.GetOvertimeEnemyDamageMultiplier(elapsed, export);
            }
            else
            {
                hpMult = Mathf.Pow(2f, overtimeMinutes * 0.5f);
                dmgMult = 1f + overtimeMinutes * 0.12f;
            }

            return TryExecuteSpawnBurst(profile, 1, false, hpMult, dmgMult, 0, ref _dummyLeader) > 0;
        }

        public void Tick(float dt)
        {
            if (enemyWorld == null || player == null) return;
            var grace = schedule != null ? schedule.gracePeriodSeconds : 2f;
            if (RunManager.Instance.ElapsedSeconds < grace)
                return;

            ProcessTriggerQueue();
            TickScheduledProfiles(dt);

            SaltwindSpawnTelemetryLogic.MaybeRecordCheckpoint(
                RunManager.Instance.ElapsedSeconds,
                enemyWorld.ActiveCount,
                _ambientBudget.Accumulated,
                GetEffectiveRateMultiplier());
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
                    scheduleEntry = entry,
                    profile = entry.profile,
                    instanceId = _nextProfileInstanceId++,
                    scheduledStartSeconds = entry.startTimeSeconds,
                    nextSpawnAt = entry.startTimeSeconds + entry.profile.timing.startDelaySeconds,
                    nextAttemptAt = entry.startTimeSeconds + entry.profile.timing.startDelaySeconds,
                    elapsed = 0f,
                    loopCycleIndex = 0
                });
            }
        }

        int ProcessTriggerQueue()
        {
            var totalSpawned = 0;
            for (var i = 0; i < _triggerQueue.Count; i++)
            {
                var req = _triggerQueue[i];
                var count = req.burstCountOverride > 0 ? req.burstCountOverride : req.profile.timing.spawnCount;
                var spawned = TryExecuteSpawnBurst(
                    req.profile, count, true, 1f, 1f, 0, ref _dummyLeader, req.runEncounterId);
                totalSpawned += spawned;
                if (req.runEncounterId > 0 && spawned <= 0)
                    Encounters.RunEncounterTracker.MarkFailed(req.runEncounterId);
            }

            _triggerQueue.Clear();
            return totalSpawned;
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

                if (runTime < run.scheduledStartSeconds)
                    continue;

                run.elapsed += dt;
                if (runTime < run.nextAttemptAt)
                    continue;

                if (!CanSpawnMoreFromProfile(run.profile))
                {
                    var interval = Mathf.Max(0.1f, GetIntervalForProfile(run.profile));
                    _ambientBudget.AccumulateWhileBlocked(dt, 1f / interval, blocked: true);
                    run.nextAttemptAt = runTime + GetSpawnRetryIntervalSeconds();
                    _activeProfiles[i] = run;
                    continue;
                }

                _ambientBudget.AccumulateWhileBlocked(dt, 0f, blocked: false);

                var spawnedThisTick = ExecuteProfileSpawn(run);
                if (spawnedThisTick > 0)
                {
                    run.spawned += spawnedThisTick;
                    run.nextSpawnAt = runTime + GetIntervalForProfile(run.profile);
                    run.nextAttemptAt = run.nextSpawnAt;
                }
                else if (run.profile.placement.fallbackPolicy == SpawnFallbackPolicy.RetryLater)
                    run.nextAttemptAt = runTime + GetSpawnRetryIntervalSeconds();

                if (IsProfileRunComplete(run))
                {
                    if (TryRestartLoopCycle(run, runTime))
                    {
                        _activeProfiles[i] = run;
                        continue;
                    }

                    run.complete = true;
                    run.cooldownUntil = runTime + run.profile.timing.cooldownAfterCompleteSeconds;
                }

                _activeProfiles[i] = run;
            }
        }

        float GetSpawnRetryIntervalSeconds()
        {
            if (HighSeas.Balance.StageBalanceProfileContext.Export?.Spawn != null)
                return HighSeas.Balance.StageBalanceProfileContext.Export.Spawn.SpawnRetryIntervalSeconds;
            return 0.35f;
        }

        bool TryRestartLoopCycle(ActiveProfileRun run, float runTime)
        {
            var state = new EncounterScheduleRunLogic.LoopState
            {
                ScheduledStartSeconds = run.scheduledStartSeconds,
                Elapsed = run.elapsed,
                Spawned = run.spawned,
                LoopCycleIndex = run.loopCycleIndex,
                NextSpawnAt = run.nextSpawnAt,
                NextAttemptAt = run.nextAttemptAt,
                Complete = run.complete
            };

            if (!EncounterScheduleRunLogic.TryRestartLoopCycle(
                    run.scheduleEntry, run.profile, runTime, ref state))
                return false;

            run.loopCycleIndex = state.LoopCycleIndex;
            run.spawned = state.Spawned;
            run.elapsed = state.Elapsed;
            run.scheduledStartSeconds = state.ScheduledStartSeconds;
            run.nextSpawnAt = state.NextSpawnAt;
            run.nextAttemptAt = state.NextAttemptAt;
            run.complete = state.Complete;
            run.leaderAgentId = -1;
            return true;
        }

        float GetIntervalForProfile(SpawnProfile profile)
        {
            var mode = profile.spawnMode;
            if (mode == SpawnMode.Continuous)
                return EncounterScheduleRunLogic.GetContinuousSpawnInterval(
                    profile, _globalSpawnGap, GetEffectiveRateMultiplier());
            if (mode == SpawnMode.Sequence || mode == SpawnMode.TimedWave)
                return Mathf.Max(_globalSpawnGap, profile.timing.spawnIntervalSeconds);
            return _globalSpawnGap;
        }

        bool IsProfileRunComplete(ActiveProfileRun run) =>
            EncounterScheduleRunLogic.IsProfileRunComplete(run.profile, run.elapsed, run.spawned);

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
                SpawnMode.Single => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId),
                SpawnMode.Sequence => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId),
                SpawnMode.Burst => TryExecuteSpawnBurst(profile, profile.timing.spawnCount, false, 1f, 1f, run.instanceId, ref run.leaderAgentId),
                SpawnMode.Group => TryExecuteSpawnBurst(profile, profile.timing.spawnCount, false, 1f, 1f, run.instanceId, ref run.leaderAgentId),
                SpawnMode.Continuous => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId),
                SpawnMode.TimedWave => TryExecuteSpawnBurst(profile, 1, false, 1f, 1f, run.instanceId, ref run.leaderAgentId),
                _ => 0
            };
        }

        int TryExecuteSpawnBurst(
            SpawnProfile profile,
            int count,
            bool ignoreCapSoft,
            float hpMult,
            float dmgMult,
            int profileInstanceId,
            ref int leaderAgentId,
            int runEncounterId = 0)
        {
            if (profile?.enemy == null || enemyWorld == null || player == null)
                return 0;

            if (StageBalanceProfileContext.IsActive && profile.enemy.isBoss && RunManager.Instance != null)
                hpMult *= SaltwindFinaleLogic.GetOrdinaryTimeHpMultiplier(RunManager.Instance.ElapsedSeconds);

            if (!ignoreCapSoft && enemyWorld.ActiveCount >= GetEnemyCap())
                return 0;

            var spawnDef = profile.enemy;
            var ambientBandIndex = -1;
            var pickedAmbientId = string.Empty;
            if (StageBalanceProfileContext.IsActive &&
                RunManager.Instance?.Rng != null &&
                SaltwindAmbientCompositionLogic.TryGetAmbientBandIndex(profile.ResolvedId, out ambientBandIndex) &&
                SaltwindAmbientCompositionLogic.TryPickEnemyId(
                    ambientBandIndex,
                    RunManager.Instance.Rng,
                    CountActiveEnemiesById,
                    StageBalanceProfileContext.Export,
                    out pickedAmbientId))
            {
                var pickedIndex = FindDefinitionIndexById(pickedAmbientId);
                if (pickedIndex >= 0)
                    spawnDef = enemyWorld.Definitions[pickedIndex];
            }

            var defIndex = FindDefinitionIndex(spawnDef);
            if (defIndex < 0)
                return 0;

            profileInstanceId = profileInstanceId > 0 ? profileInstanceId : _nextProfileInstanceId++;
            var spawnedCount = 0;
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
                        spawnDef.radius + 0.35f,
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

                if (!SpawnPlacementService.IsFormationPositionValid(
                        pos, spawnDef.radius + 0.35f, GetCamera(), profile.placement))
                    continue;

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

                spawnedCount++;
                if (ambientBandIndex >= 0)
                {
                    SaltwindSpawnTelemetryLogic.RecordAmbientSpawn(
                        ambientBandIndex,
                        spawnDef.id,
                        _ambientBudget.Accumulated,
                        enemyWorld.ActiveCount);
                }

                if (runEncounterId > 0)
                    Encounters.RunEncounterTracker.RegisterSpawnedAgent(
                        runEncounterId, enemyWorld.Agents[slot].id);
                if (role == FormationRole.Leader || (role == FormationRole.Auto && i == 0))
                    leaderAgentId = enemyWorld.Agents[slot].id;

                TrackProfileSpawn(profile);
            }

            return spawnedCount;
        }

        int CountActiveEnemiesById(string enemyId)
        {
            if (enemyWorld == null || string.IsNullOrEmpty(enemyId))
                return 0;
            var targetIndex = FindDefinitionIndexById(enemyId);
            if (targetIndex < 0)
                return 0;
            var count = 0;
            for (var i = 0; i < enemyWorld.Agents.Length; i++)
            {
                var agent = enemyWorld.Agents[i];
                if (agent.active && agent.definitionIndex == targetIndex)
                    count++;
            }

            return count;
        }

        bool TryExecuteSpawnBurst(SpawnProfile profile, int count, bool ignoreCapSoft) =>
            TryExecuteSpawnBurst(profile, count, ignoreCapSoft, 1f, 1f, 0, ref _dummyLeader) > 0;

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
        public float GetEffectiveThreatPerSecond() => GetEffectiveRateMultiplier() * 4f;
        public float DebugAmbientBudgetCarry => _ambientBudget.Accumulated;
    }
}
