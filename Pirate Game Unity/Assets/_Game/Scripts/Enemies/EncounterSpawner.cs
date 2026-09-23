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
        bool _minibossArenaActive;
        float _minibossArenaBlockedSince = -1f;
        float _globalSpawnGap;
        int _nextProfileInstanceId = 1;
        readonly List<ActiveProfileRun> _activeProfiles = new(32);
        readonly Dictionary<int, int> _profileActiveCounts = new();
        readonly List<PendingTriggeredEncounter> _pendingEncounters = new(8);
        bool _processingTriggerQueue;

        const float EncounterRequestTimeoutSeconds = 10f;

        struct PendingTriggeredEncounter
        {
            public SpawnProfile profile;
            public int remainingCount;
            public int runEncounterId;
            public float nextAttemptAt;
            public float deadlineAt;
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
            public int scheduleEntryIndex;
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

            if (_minibossArenaActive || V3.V3MinibossArena.BlocksAmbientSpawning)
            {
                AccumulateAmbientBudgetWhileBlocked(Time.deltaTime);
                return;
            }

            ProcessTriggerQueue();

            if (!_ambientScheduleEnabled)
                return;

            if (RunManager.Instance.IsOvertime)
                return;

            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export != null &&
                !SaltwindStageTimelineLogic.ShouldTickAmbientSchedule(
                    RunManager.Instance.ElapsedSeconds,
                    false,
                    StageBalanceProfileContext.Export.Timing.StageDeadlineSeconds))
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
            _pendingEncounters.Clear();
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
            var runTime = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            var count = countOverride > 0 ? countOverride : profile.timing.spawnCount;
            _pendingEncounters.Add(new PendingTriggeredEncounter
            {
                profile = profile,
                remainingCount = count,
                runEncounterId = runEncounterId,
                nextAttemptAt = runTime,
                deadlineAt = runTime + EncounterRequestTimeoutSeconds
            });
        }

        /// <summary>Queue then process triggers immediately (boss/cache encounters).</summary>
        public int TriggerProfileImmediate(SpawnProfile profile, int countOverride, int runEncounterId)
        {
            TriggerProfile(profile, countOverride, runEncounterId);
            ProcessTriggerQueue();
            if (runEncounterId <= 0)
                return 0;
            var record = Encounters.RunEncounterTracker.Get(runEncounterId);
            return record?.SpawnedCount ?? 0;
        }

        public bool HasPendingEncounter(int runEncounterId)
        {
            if (runEncounterId <= 0) return false;
            for (var i = 0; i < _pendingEncounters.Count; i++)
            {
                if (_pendingEncounters[i].runEncounterId == runEncounterId)
                    return true;
            }

            return false;
        }

        public void CancelPendingEncounter(int runEncounterId)
        {
            if (runEncounterId <= 0) return;
            for (var i = _pendingEncounters.Count - 1; i >= 0; i--)
            {
                if (_pendingEncounters[i].runEncounterId != runEncounterId) continue;
                _pendingEncounters.RemoveAt(i);
            }
        }

        public bool TrySpawnPressureBeat(SpawnProfile profile) =>
            profile != null && TryExecuteSpawnBurst(profile, 1, true, 1f, 1f, 0, ref _dummyLeader) > 0;

        public bool TrySpawnPressureBeat(string enemyId, int scheduledEliteIndex, out int spawnedAgentId)
        {
            spawnedAgentId = -1;
            if (schedule == null) return false;
            var profile = enemyId switch
            {
                "hammerhead" => schedule.hammerheadBeat,
                "kraken_spawn" => schedule.krakenBeat,
                _ => null
            };
            if (profile == null || enemyWorld == null) return false;
            var beforeMaxId = GetMaxActiveAgentId();
            if (!TrySpawnPressureBeat(profile)) return false;
            spawnedAgentId = FindNewestAgentIdAfter(beforeMaxId, profile.enemy);
            if (spawnedAgentId > 0 && scheduledEliteIndex >= 0)
                Balance.RunMarksTracker.RegisterScheduledEliteAgent(spawnedAgentId, scheduledEliteIndex);
            return spawnedAgentId > 0;
        }

        int FindNewestAgentIdAfter(int beforeMaxId, EnemyDefinition expectedDef)
        {
            if (enemyWorld == null || expectedDef == null) return -1;
            var defIndex = FindDefinitionIndex(expectedDef);
            if (defIndex < 0) return -1;
            var bestId = -1;
            for (var i = 0; i < enemyWorld.Agents.Length; i++)
            {
                var agent = enemyWorld.Agents[i];
                if (!agent.active || agent.definitionIndex != defIndex) continue;
                if (agent.id <= beforeMaxId) continue;
                if (agent.id > bestId) bestId = agent.id;
            }

            return bestId;
        }

        int GetMaxActiveAgentId()
        {
            if (enemyWorld == null) return 0;
            var max = 0;
            for (var i = 0; i < enemyWorld.Agents.Length; i++)
            {
                var agent = enemyWorld.Agents[i];
                if (!agent.active) continue;
                if (agent.id > max) max = agent.id;
            }

            return max;
        }

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
                    scheduleEntryIndex = i,
                    nextSpawnAt = entry.startTimeSeconds + entry.profile.timing.startDelaySeconds,
                    nextAttemptAt = entry.startTimeSeconds + entry.profile.timing.startDelaySeconds,
                    elapsed = 0f,
                    loopCycleIndex = 0
                });
            }
        }

        int ProcessTriggerQueue()
        {
            if (RunManager.Instance == null) return 0;
            if (_processingTriggerQueue) return 0;

            _processingTriggerQueue = true;
            try
            {
                return ProcessTriggerQueueCore();
            }
            finally
            {
                _processingTriggerQueue = false;
            }
        }

        int ProcessTriggerQueueCore()
        {
            if (RunManager.Instance == null) return 0;
            var runTime = RunManager.Instance.ElapsedSeconds;
            var totalSpawned = 0;

            for (var i = _pendingEncounters.Count - 1; i >= 0; i--)
            {
                if (i >= _pendingEncounters.Count)
                    continue;

                var pending = _pendingEncounters[i];
                if (pending.profile == null)
                {
                    if (pending.runEncounterId > 0)
                        Encounters.RunEncounterTracker.MarkFailed(pending.runEncounterId);
                    _pendingEncounters.RemoveAt(i);
                    continue;
                }

                if (runTime < pending.nextAttemptAt)
                    continue;

                if (pending.runEncounterId > 0)
                {
                    var encounter = Encounters.RunEncounterTracker.Get(pending.runEncounterId);
                    if (encounter == null || encounter.IsTerminal)
                    {
                        _pendingEncounters.RemoveAt(i);
                        continue;
                    }
                }

                if (runTime > pending.deadlineAt)
                {
                    if (pending.runEncounterId > 0)
                        Encounters.RunEncounterTracker.MarkFailed(pending.runEncounterId);
                    _pendingEncounters.RemoveAt(i);
                    continue;
                }

                var spawned = TryExecuteSpawnBurst(
                    pending.profile,
                    pending.remainingCount,
                    true,
                    1f,
                    1f,
                    0,
                    ref _dummyLeader,
                    pending.runEncounterId);
                totalSpawned += spawned;

                if (spawned > 0)
                    pending.remainingCount -= spawned;

                if (pending.remainingCount <= 0)
                {
                    _pendingEncounters.RemoveAt(i);
                    continue;
                }

                pending.nextAttemptAt = runTime + GetSpawnRetryIntervalSeconds();
                if (i < _pendingEncounters.Count)
                    _pendingEncounters[i] = pending;
            }

            return totalSpawned;
        }

        void TickScheduledProfiles(float dt)
        {
            var runTime = RunManager.Instance.ElapsedSeconds;
            var deadline = GetStageDeadlineSeconds();
            for (var i = 0; i < _activeProfiles.Count; i++)
            {
                var run = _activeProfiles[i];
                if (run.complete)
                    continue;

                var windowEnd = EncounterScheduleRunLogic.ResolveScheduleEntryEndSeconds(
                    schedule?.entries,
                    run.scheduleEntryIndex,
                    deadline);

                if (runTime >= windowEnd)
                {
                    run.complete = true;
                    _activeProfiles[i] = run;
                    continue;
                }

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

        bool IsProfileRunComplete(ActiveProfileRun run)
        {
            var runTime = RunManager.Instance.ElapsedSeconds;
            var deadline = GetStageDeadlineSeconds();
            var windowEnd = EncounterScheduleRunLogic.ResolveScheduleEntryEndSeconds(
                schedule?.entries,
                run.scheduleEntryIndex,
                deadline);
            return EncounterScheduleRunLogic.IsScheduledContinuousWindowComplete(
                runTime,
                run.scheduledStartSeconds,
                windowEnd,
                run.profile,
                run.elapsed,
                run.spawned);
        }

        bool CanSpawnMoreFromProfile(SpawnProfile profile)
        {
            if (enemyWorld.ActiveCount >= GetAmbientPopulationCap())
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

            if (StageBalanceProfileContext.IsActive && RunManager.Instance != null)
            {
                var elapsed = RunManager.Instance.ElapsedSeconds;
                hpMult *= SaltwindFinaleLogic.GetOrdinaryTimeHpMultiplier(elapsed);
                dmgMult *= SaltwindFinaleLogic.GetOrdinaryTimeDamageMultiplier(elapsed);
            }

            if (!ignoreCapSoft && enemyWorld.ActiveCount >= GetAmbientPopulationCap())
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
                if (enemyWorld.ActiveCount >= (ignoreCapSoft ? GetEnemyCap() : GetAmbientPopulationCap()))
                    break;

                var role = ResolveFormationRole(profile, i, count);
                if (role == FormationRole.Leader)
                    leaderAgentId = -1;

                if (!SpawnPlacementService.TryPickSpawnPosition(
                        player,
                        GetCamera(),
                        profile.placement,
                        spawnDef.radius + 0.35f,
                        RunManager.Instance.Rng,
                        allowNearPlayerFallback: runEncounterId > 0 || spawnDef.isBoss,
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

        int GetReservedEncounterSlots()
        {
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export?.Spawn != null)
                return StageBalanceProfileContext.Export.Spawn.ReservedEncounterSlots;
            return 0;
        }

        int GetAmbientPopulationCap() =>
            Mathf.Max(1, GetEnemyCap() - GetReservedEncounterSlots());

        float GetStageDeadlineSeconds()
        {
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export?.Timing != null)
                return StageBalanceProfileContext.Export.Timing.StageDeadlineSeconds;
            return GameConstants.StageDurationSeconds;
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

        public void SetMinibossArenaActive(bool active)
        {
            if (active && !_minibossArenaActive)
                _minibossArenaBlockedSince = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            else if (!active && _minibossArenaActive)
                ReleaseMinibossArenaSpawnCatchUp();
            _minibossArenaActive = active;
            if (!active)
                _minibossArenaBlockedSince = -1f;
        }

        void AccumulateAmbientBudgetWhileBlocked(float dt)
        {
            if (dt <= 0f || schedule?.entries == null) return;
            var rate = 0f;
            for (var i = 0; i < schedule.entries.Length; i++)
            {
                var entry = schedule.entries[i];
                if (entry?.profile == null) continue;
                var interval = Mathf.Max(0.1f, entry.profile.timing.spawnIntervalSeconds);
                rate = Mathf.Max(rate, 1f / interval);
            }

            if (rate <= 0f)
                rate = 0.6f;
            _ambientBudget.AccumulateWhileBlocked(dt, rate, blocked: true);
        }

        void ReleaseMinibossArenaSpawnCatchUp()
        {
            if (_minibossArenaBlockedSince < 0f || RunManager.Instance == null) return;
            var blockedSeconds = Mathf.Max(0f, RunManager.Instance.ElapsedSeconds - _minibossArenaBlockedSince);
            if (blockedSeconds <= 0f) return;

            _ambientBudget.Accumulated = _ambientBudget.MaxCarry;
            var now = RunManager.Instance.ElapsedSeconds;
            for (var i = 0; i < _activeProfiles.Count; i++)
            {
                var run = _activeProfiles[i];
                run.nextSpawnAt = now;
                run.nextAttemptAt = now;
                _activeProfiles[i] = run;
            }
        }

        public bool TrySpawnMiniboss(string enemyId, out int spawnedAgentId)
        {
            spawnedAgentId = -1;
            if (enemyWorld == null || player == null || string.IsNullOrEmpty(enemyId)) return false;
            var defIndex = FindDefinitionIndexById(enemyId);
            if (defIndex < 0) return false;
            var def = enemyWorld.Definitions[defIndex];
            var rng = RunManager.Instance?.Rng;
            if (rng == null) return false;

            var angle = rng.LootFloat() * Mathf.PI * 2f;
            var pos = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 22f;
            pos.y = def.visualHeightOffset;
            var slot = enemyWorld.Spawn(defIndex, pos, EliteAffix.Rich, 1f, 4f, 1.15f);
            if (slot < 0) return false;
            spawnedAgentId = enemyWorld.Agents[slot].id;
            return spawnedAgentId > 0;
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

        public int GetAmbientPopulationCapForDebug() => GetAmbientPopulationCap();

        public int PendingTriggeredEncounterCount => _pendingEncounters.Count;

        public int ProcessPendingEncountersNow() => ProcessTriggerQueue();

        /// <summary>PlayMode / harness wiring when components are created at runtime.</summary>
        public void ConfigureRuntimeReferences(EnemyWorld world, Transform playerTransform, Camera cam = null)
        {
            enemyWorld = world;
            player = playerTransform;
            if (cam != null)
            {
                spawnCamera = cam;
                _cachedCamera = cam;
            }
        }
        public float GetEffectiveThreatPerSecond() => GetEffectiveRateMultiplier() * 4f;
        public float DebugAmbientBudgetCarry => _ambientBudget.Accumulated;
    }
}
