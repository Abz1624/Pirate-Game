using System.Collections.Generic;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Ambient phases (ratePerSecond &gt; 0): each keeps a spawner running from its start until the next ambient phase
    /// starts (or its own duration ends). Port of EncounterSpawner.TickScheduledProfiles: interval scheduling with no
    /// catch-up, retry after a cap or placement failure with no stored debt, per-spawner caps.
    /// </summary>
    internal sealed class AmbientSpawner
    {
        sealed class Run
        {
            public string PhaseId;
            public SpawnProfile Profile;
            public int InstanceId;
            public float ScheduledStart;
            public float Elapsed;
            public float NextSpawnAt;
            public float NextAttemptAt;
            public int Spawned;
            public bool Complete;
            public float CooldownUntil;
            public int LeaderUnitId = -1;
            public float LastThreatCost = 1f;
            public bool Announced;
            public bool Manual;
        }

        /// <summary>Raised the first time a run is inside its window (phase started; console, telemetry).</summary>
        public System.Action<string> PhaseStarted;

        readonly SpawnServices _s;
        readonly SpawnExecutor _executor;
        readonly List<Run> _runs = new List<Run>();

        public AmbientSpawner(SpawnServices services, SpawnExecutor executor)
        {
            _s = services;
            _executor = executor;
        }

        public static bool IsAmbient(TimelinePhase p) => PhaseKinds.IsAmbient(p);

        /// <summary>Like V3's schedule bootstrap: every time-triggered ambient phase gets its run at run start.</summary>
        public void BeginRun()
        {
            _runs.Clear();
            var timeline = _s.Timeline;
            if (timeline == null) return;
            foreach (var phase in timeline.phases)
                if (IsAmbient(phase) && phase.trigger.kind == PhaseTriggerKind.Time)
                    AddRun(phase, phase.trigger.timeSeconds);
        }

        /// <summary>
        /// Starts a run now: an ambient phase whose trigger is not time, or any ambient phase triggered by hand (a run
        /// that already finished starts over).
        /// </summary>
        public void Start(TimelinePhase phase, float now, bool manual)
        {
            var run = Find(phase.phaseId);
            if (run == null)
            {
                AddRun(phase, now);
                run = Find(phase.phaseId);
                if (run == null) return;
            }
            run.ScheduledStart = now;
            run.NextSpawnAt = run.NextAttemptAt = now + (run.Profile != null ? run.Profile.timing.startDelaySeconds : 0f);
            run.Complete = false;
            run.Elapsed = 0f;
            run.Spawned = 0;
            run.CooldownUntil = 0f;
            run.Announced = false;
            run.Manual = manual;
        }

        /// <summary>
        /// Live timeline edits: a time-triggered ambient phase added mid-run gets its run, and a phase that has not
        /// started yet follows its (edited) trigger time. With the default timeline this changes nothing.
        /// </summary>
        public void Sync(WaveTimelineConfig timeline)
        {
            foreach (var phase in timeline.phases)
            {
                if (!IsAmbient(phase) || phase.trigger.kind != PhaseTriggerKind.Time) continue;
                var run = Find(phase.phaseId);
                if (run == null) { AddRun(phase, phase.trigger.timeSeconds); continue; }
                if (run.Announced || run.Manual || run.Complete || Mathf.Approximately(run.ScheduledStart, phase.trigger.timeSeconds)) continue;
                run.ScheduledStart = phase.trigger.timeSeconds;
                run.NextSpawnAt = run.NextAttemptAt = run.ScheduledStart + run.Profile.timing.startDelaySeconds;
            }
        }

        /// <summary>Marks runs whose window has passed (or whose phase was removed) as done, even while spawning is paused.</summary>
        public void MarkEnded(WaveTimelineConfig timeline, float now)
        {
            foreach (var run in _runs)
            {
                if (run.Complete) continue;
                var phase = timeline.Phase(run.PhaseId);
                if (phase == null || now >= WindowEnd(timeline, phase, run)) run.Complete = true;
            }
        }

        public void Stop(string phaseId)
        {
            var run = Find(phaseId);
            if (run != null) run.Complete = true;
        }

        public bool IsRunning(string phaseId, float now)
        {
            var run = Find(phaseId);
            return run != null && !run.Complete && now >= run.ScheduledStart;
        }

        public bool HasRun(string phaseId) => Find(phaseId) != null;

        void AddRun(TimelinePhase phase, float start)
        {
            var profile = _s.Profiles.For(phase.spawner);
            if (profile == null) return;
            _runs.Add(new Run
            {
                PhaseId = phase.phaseId,
                Profile = profile,
                InstanceId = _executor.NextInstanceId(),
                ScheduledStart = start,
                NextSpawnAt = start + profile.timing.startDelaySeconds,
                NextAttemptAt = start + profile.timing.startDelaySeconds,
            });
        }

        Run Find(string phaseId)
        {
            foreach (var r in _runs) if (r.PhaseId == phaseId) return r;
            return null;
        }

        /// <summary>A miniboss arena ended: every run tries once immediately (V3 ReleaseMinibossArenaSpawnCatchUp).</summary>
        public void ReleaseAfterArena(float now)
        {
            foreach (var run in _runs)
            {
                run.NextSpawnAt = now;
                run.NextAttemptAt = now;
            }
        }

        public void Tick(float dt, float rateMultiplier)
        {
            var timeline = _s.Timeline;
            if (timeline == null) return;
            var runTime = _s.Run.ElapsedSeconds;
            foreach (var run in _runs)
            {
                if (run.Complete) continue;
                var phase = timeline.Phase(run.PhaseId);
                if (phase == null) { run.Complete = true; continue; } // removed from the timeline

                var windowEnd = WindowEnd(timeline, phase, run);
                if (runTime >= windowEnd) { run.Complete = true; continue; }
                if (run.CooldownUntil > runTime) continue;
                if (runTime < run.ScheduledStart) continue;
                if (!run.Announced) { run.Announced = true; PhaseStarted?.Invoke(run.PhaseId); }

                run.Elapsed += dt;
                if (runTime < run.NextAttemptAt) continue;

                if (!CanSpawnMore(run.Profile))
                {
                    if (_s.Sink.ActiveCount >= _executor.AmbientCap) _executor.NoteAmbientCapBlocked(runTime); // M11 cap-HP lever
                    run.NextAttemptAt = runTime + timeline.spawnRetryIntervalSeconds;
                    continue;
                }

                var spawned = Execute(run, phase);
                if (spawned > 0)
                {
                    run.Spawned += spawned;
                    run.NextSpawnAt = runTime + Interval(timeline, phase, run, rateMultiplier);
                    run.NextAttemptAt = run.NextSpawnAt;
                }
                else if (run.Profile.placement.fallbackPolicy == SpawnFallbackPolicy.RetryLater)
                    run.NextAttemptAt = runTime + timeline.spawnRetryIntervalSeconds;

                if (IsComplete(runTime, windowEnd, run))
                {
                    run.Complete = true;
                    run.CooldownUntil = runTime + run.Profile.timing.cooldownAfterCompleteSeconds;
                }
            }
        }

        /// <summary>
        /// Until the next ambient phase by start time begins (V3: the next schedule entry, which is also the next by
        /// time), or its own duration, or the stage deadline. Phases are ordered by time, not by list position, so
        /// reordering or appending phases in the console keeps the bands sequential.
        /// </summary>
        float WindowEnd(WaveTimelineConfig timeline, TimelinePhase phase, Run run)
        {
            if (phase.duration.kind == PhaseDurationKind.Seconds) return run.ScheduledStart + phase.duration.seconds;
            var end = timeline.stageDeadlineSeconds;
            foreach (var next in timeline.phases)
            {
                if (next == phase || !IsAmbient(next)) continue;
                var nextRun = Find(next.phaseId);
                if (nextRun != null && nextRun.Complete && nextRun.Spawned == 0 && !nextRun.Announced) continue; // stopped before it began
                var start = nextRun != null ? nextRun.ScheduledStart : next.trigger.kind == PhaseTriggerKind.Time ? next.trigger.timeSeconds : float.PositiveInfinity;
                if (start > run.ScheduledStart && start < end) end = start;
            }
            return end;
        }

        bool CanSpawnMore(SpawnProfile profile)
        {
            if (_s.Sink.ActiveCount >= _executor.AmbientCap) return false;
            if (profile.timing.maxActiveFromProfile <= 0) return true;
            return _s.Sink.CountActiveFromProfile(profile) < profile.timing.maxActiveFromProfile;
        }

        int Execute(Run run, TimelinePhase phase)
        {
            var profile = run.Profile;
            if (profile.spawnMode == SpawnMode.Triggered) return 0; // V3: triggered profiles never spawn on a schedule
            var count = profile.spawnMode == SpawnMode.Burst || profile.spawnMode == SpawnMode.Group ? profile.timing.spawnCount : 1;
            if (phase.burstCount > 0) count = phase.burstCount;
            var before = _s.Sink.MaxActiveUnitId();
            var spawned = _executor.TryBurst(profile, count, false, 1f, 1f, run.InstanceId, ref run.LeaderUnitId, phase, ambient: true);
            if (spawned > 0 && phase.budgetUnit == SpawnBudgetUnit.Threat) run.LastThreatCost = Mathf.Max(0.1f, _s.Sink.ThreatOfNewestAfter(before));
            return spawned;
        }

        float Interval(WaveTimelineConfig timeline, TimelinePhase phase, Run run, float rateMultiplier)
        {
            var gap = timeline.globalMinSpawnGapSeconds;
            var profile = run.Profile;
            switch (profile.spawnMode)
            {
                case SpawnMode.Continuous:
                    // Threat budget: the phase rate is threat per second, so an enemy costing N waits N times longer.
                    var baseInterval = phase.ratePerSecond > 0f
                        ? (phase.budgetUnit == SpawnBudgetUnit.Threat ? run.LastThreatCost : 1f) / phase.ratePerSecond
                        : profile.timing.spawnIntervalSeconds;
                    var weighted = rateMultiplier;
                    // M11: the spawner's pressureWeight curve (composition lever; 1 unless Adaptive).
                    if (_s.Levers != null && _s.Levers.Adaptive && phase.spawner != null) weighted *= _s.Levers.SpawnerWeight(phase.spawner.pressureWeight);
                    return Mathf.Max(gap, baseInterval / Mathf.Max(0.1f, weighted));
                case SpawnMode.Sequence:
                case SpawnMode.TimedWave:
                    return Mathf.Max(gap, profile.timing.spawnIntervalSeconds);
                default:
                    return gap;
            }
        }

        static bool IsComplete(float runTime, float windowEnd, Run run)
        {
            if (runTime >= windowEnd) return true;
            var profile = run.Profile;
            if (profile.spawnMode != SpawnMode.Continuous) return run.Spawned >= profile.timing.spawnCount;
            return profile.timing.durationSeconds > 0f && run.Elapsed >= profile.timing.durationSeconds;
        }
    }
}
