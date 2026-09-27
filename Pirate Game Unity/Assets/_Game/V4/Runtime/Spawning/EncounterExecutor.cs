using System.Collections.Generic;
using HighSeas.Enemies;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Spawning
{
    public enum EncounterKind { Boss, PowderCache }
    public enum EncounterState { Requested, Spawned, Completed, Failed }

    /// <summary>
    /// Required encounters (Beacon boss, Powder Cache ambush): their records and the kill bookkeeping that completes
    /// them. M7: the legacy static RunEncounterTracker (the legacy enemy world's kill path completes encounters there);
    /// M8: the V4 encounter tracker.
    /// </summary>
    public interface IEncounterTracker
    {
        void ResetForRun();
        int Request(EncounterKind kind, int expectedSpawnCount);
        bool Exists(int encounterId);
        EncounterState State(int encounterId);
        int SpawnedCount(int encounterId);
        bool IsTerminal(int encounterId);
        void RegisterSpawned(int encounterId, int unitId);
        void MarkFailed(int encounterId);
        bool TryGetOpenBossEncounter(out int encounterId);
        bool BossObjectiveSecured { get; }
    }

    /// <summary>Queues and spawns required encounters (what POIManager asked the legacy spawner to do).</summary>
    public interface IEncounterExecutor
    {
        /// <summary>
        /// Queues an encounter; it spawns on the next pass of the queue. <paramref name="noDeadline"/>: it keeps retrying
        /// until all have spawned instead of failing at the required-encounter timeout (the Powder Cache elite challenge).
        /// </summary>
        void Trigger(EnemySpawnerConfig spawner, int count, int encounterId, bool noDeadline = false);
        /// <summary>Queues an encounter and runs the queue now (boss summon); returns the encounter's spawned count.</summary>
        int TriggerImmediate(EnemySpawnerConfig spawner, int count, int encounterId);
        int ProcessNow();
        void Cancel(int encounterId);
        bool HasPending(int encounterId);
    }

    /// <summary>
    /// Port of EncounterSpawner's trigger queue (TriggerProfile, ProcessTriggerQueueCore): every pending encounter
    /// retries each spawn-retry interval until it has spawned its count, ignoring the ambient soft cap and allowed to
    /// fall back to spots near the ship; past the deadline (requiredEncounterTimeoutSeconds, V3 10 s) it fails. The
    /// queue is walked from the end, as V3 did. V3's queue stopped during miniboss arenas; V4's does not (an intended
    /// difference since M3) unless <c>encountersWaitDuringArenas</c> is on (the v3-parity preset).
    /// </summary>
    internal sealed class EncounterExecutor : IEncounterExecutor
    {
        struct Pending
        {
            public EnemySpawnerConfig spawner;
            public SpawnProfile profile;
            public int remaining;
            public int encounterId;
            public float nextAttemptAt;
            public float deadlineAt;
        }

        readonly SpawnServices _s;
        readonly SpawnExecutor _executor;
        readonly List<Pending> _pending = new List<Pending>(8);
        bool _processing;
        int _dummyLeader = -1;

        readonly System.Func<bool> _arenaActive;

        public EncounterExecutor(SpawnServices services, SpawnExecutor executor, System.Func<bool> arenaActive = null)
        {
            _s = services;
            _executor = executor;
            _arenaActive = arenaActive;
        }

        public void ResetForRun() => _pending.Clear();

        float Timeout => _s.Timeline != null ? _s.Timeline.requiredEncounterTimeoutSeconds : 10f;
        float RetryInterval => _s.Timeline != null && _s.Timeline.spawnRetryIntervalSeconds > 0f ? _s.Timeline.spawnRetryIntervalSeconds : 0.35f;

        public void Trigger(EnemySpawnerConfig spawner, int count, int encounterId, bool noDeadline = false)
        {
            if (spawner == null) return;
            var profile = _s.Profiles != null ? _s.Profiles.For(spawner) : null;
            if (profile == null) return;
            var now = _s.Run.Exists ? _s.Run.ElapsedSeconds : 0f;
            _pending.Add(new Pending
            {
                spawner = spawner,
                profile = profile,
                remaining = count > 0 ? count : profile.timing.spawnCount,
                encounterId = encounterId,
                nextAttemptAt = now,
                deadlineAt = noDeadline ? float.MaxValue : now + Timeout
            });
        }

        public int TriggerImmediate(EnemySpawnerConfig spawner, int count, int encounterId)
        {
            Trigger(spawner, count, encounterId);
            ProcessNow();
            return encounterId > 0 && _s.Encounters != null ? _s.Encounters.SpawnedCount(encounterId) : 0;
        }

        public bool HasPending(int encounterId)
        {
            if (encounterId <= 0) return false;
            foreach (var p in _pending)
                if (p.encounterId == encounterId) return true;
            return false;
        }

        public void Cancel(int encounterId)
        {
            if (encounterId <= 0) return;
            for (var i = _pending.Count - 1; i >= 0; i--)
                if (_pending[i].encounterId == encounterId) _pending.RemoveAt(i);
        }

        /// <summary>EncounterSpawner.Update's gate and queue pass (its slot: right after the run clock advances).</summary>
        public void Tick()
        {
            var run = _s.Run;
            if (!run.Exists || run.IsPaused || run.IsRunEnded || !run.IsGameplayActive) return;
            if (_s.Timeline != null && _s.Timeline.encountersWaitDuringArenas && _arenaActive != null && _arenaActive()) return;
            ProcessNow();
        }

        public int ProcessNow()
        {
            if (!_s.Run.Exists || _processing) return 0;
            _processing = true;
            try { return ProcessCore(); }
            finally { _processing = false; }
        }

        int ProcessCore()
        {
            var now = _s.Run.ElapsedSeconds;
            var total = 0;
            var tracker = _s.Encounters;
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (i >= _pending.Count) continue;
                var p = _pending[i];
                if (p.profile == null)
                {
                    if (p.encounterId > 0) tracker?.MarkFailed(p.encounterId);
                    _pending.RemoveAt(i);
                    continue;
                }
                if (now < p.nextAttemptAt) continue;
                if (p.encounterId > 0 && (tracker == null || !tracker.Exists(p.encounterId) || tracker.IsTerminal(p.encounterId)))
                {
                    _pending.RemoveAt(i);
                    continue;
                }
                if (now > p.deadlineAt)
                {
                    if (p.encounterId > 0) tracker?.MarkFailed(p.encounterId);
                    _pending.RemoveAt(i);
                    continue;
                }

                var spawned = _executor.TryBurst(p.profile, p.remaining, true, 1f, 1f, 0, ref _dummyLeader, runEncounterId: p.encounterId);
                total += spawned;
                if (spawned > 0) p.remaining -= spawned;
                // A MarkFailed from a spawn side effect can shrink the queue; V3 guarded the same way.
                if (p.remaining <= 0)
                {
                    if (i < _pending.Count) _pending.RemoveAt(i);
                    continue;
                }
                p.nextAttemptAt = now + RetryInterval;
                if (i < _pending.Count) _pending[i] = p;
            }
            return total;
        }
    }
}
