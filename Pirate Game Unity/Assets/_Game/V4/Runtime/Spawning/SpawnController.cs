using System.Collections.Generic;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// V4 decides what spawns and when (M3). The Saltwind schedule is one timeline asset: ambient bands, the Reef Crab
    /// miniboss and scheduled elites (focus phases with an arena), swarm pulses, overtime and the storm. The Beacon
    /// boss and Powder Cache still spawn through the legacy encounter executor until stage content moves (M7).
    ///
    /// Until the enemy world is V4 (M8), each part runs in the frame slot its V3 predecessor had (the legacy update
    /// slots call the Tick* methods), because the legacy enemy world, weapons and run clock tick around them.
    /// </summary>
    public sealed class SpawnController : IRunModule, ISpawnController, IEncounterExecutor
    {
        readonly SpawnServices _s = new SpawnServices();
        readonly SpawnModifierStack _modifiers = new SpawnModifierStack();
        readonly List<PhaseStatus> _statusBuffer = new List<PhaseStatus>();
        SpawnComposition _composition;
        SpawnExecutor _executor;
        AmbientSpawner _ambient;
        ScriptedPhases _scripted;
        OvertimePhase _overtime;
        StormPhase _storm;
        EncounterExecutor _encounters;
        TimelineTriggers _triggers;
        readonly HashSet<string> _skipped = new HashSet<string>();
        readonly HashSet<string> _manual = new HashSet<string>();
        bool _running;
        string _currentLabel = string.Empty;

        public string Id => "spawn";
        public int TickOrder => 100;

        /// <summary>Module Lab: nothing spawns by itself (the lab spawns from the console).</summary>
        public bool LabMode { get; set; }

        public void Initialize(RunContext context)
        {
            _s.Resolve(context);
            _composition = new SpawnComposition(_s);
            _executor = new SpawnExecutor(_s, _composition);
            _ambient = new AmbientSpawner(_s, _executor);
            _ambient.PhaseStarted = id => _s.Publish(CombatEventKind.PhaseChanged, 0, 1, default, id);
            _triggers = new TimelineTriggers(_s, IsCleared);
            _scripted = new ScriptedPhases(_s, _executor, _ambient, _modifiers, _triggers);
            _overtime = new OvertimePhase(_s, _executor);
            _storm = new StormPhase(_s);
            _encounters = new EncounterExecutor(_s, _executor, () => _scripted != null && _scripted.ArenaActive);
            context.Events.TickPublished += OnEvents;
            if (context.Config != null) context.Config.ConfigChanged += OnConfigChanged;
        }

        public void BeginRun(in RunStart start)
        {
            _modifiers.Reset(1f);
            _skipped.Clear();
            _manual.Clear();
            _executor.ResetForRun();
            _encounters.ResetForRun();
            _triggers.BeginRun();
            _ambient.BeginRun();
            _scripted.BeginRun();
            _overtime.BeginRun();
            _storm.BeginRun();
            ApplyExperiment();
            _running = true;
        }

        /// <summary>Ticks in the V4 fixed step are not used yet: see the class summary.</summary>
        public void Tick(in SimTick tick) { }

        public void EndRun(in RunEnd end) => _running = false;

        void OnEvents(IReadOnlyList<CombatEvent> events)
        {
            _triggers?.OnEvents(events);
            for (var i = 0; i < events.Count; i++)
                if (events[i].Kind == CombatEventKind.PhaseChanged && events[i].B == 1)
                {
                    var phase = _s.Timeline?.Phase(events[i].Tag);
                    if (phase != null) _currentLabel = phase.label;
                }
        }

        void OnConfigChanged(ConfigChange change)
        {
            _composition?.Invalidate();
            if (_running && _s.Config.Get<V4ConfigAsset>(change.configId) is ExperimentConfig) ApplyExperiment();
        }

        void ApplyExperiment()
        {
            var e = _s.Experiment;
            if (e != null) _s.Sink.SetExperimentMultipliers(e.enemyHpMultiplier, e.enemyDamageMultiplier);
        }

        /// <summary>Spawn-rate multiplier now: legacy base (Dead Calm) × experiment density × pulses × boss fight.</summary>
        public float EffectiveRateMultiplier
        {
            get
            {
                if (!_running) return 1f;
                var elapsed = _s.Run.ElapsedSeconds;
                var multiplier = _modifiers.GetRateMultiplier(elapsed);
                if (_s.LegacyRate != null) multiplier *= _s.LegacyRate.Multiplier;
                var experiment = _s.Experiment;
                if (experiment != null) multiplier *= UnityEngine.Mathf.Max(0.1f, experiment.spawnDensityMultiplier);
                var timeline = _s.Timeline;
                if (_s.Run.IsBossFight && timeline != null) multiplier *= timeline.bossAmbientRateMultiplier;
                return multiplier;
            }
        }

        // ---- Legacy update slots ----------------------------------------------------------------------------

        /// <summary>Ambient spawns, in EncounterSpawner.Update's slot (right after the run clock advances).</summary>
        public void TickAmbient(float dt)
        {
            if (!_running) return;
            // M7: the encounter queue (EncounterSpawner.Update ran it here, before its ambient tick). Also in the lab (M8
            // playtest), where only the stage content's requests (Powder Cache, Beacon boss) fill it.
            _encounters.Tick();
            var run = _s.Run;
            if (!run.Exists || run.IsPaused || run.IsRunEnded || !run.IsGameplayActive) return;
            _scripted.PollArena();
            var live = _s.Timeline;
            if (live != null)
            {
                _ambient.Sync(live);
                _ambient.MarkEnded(live, run.ElapsedSeconds);
            }
            if (_scripted.ArenaActive) return;
            if (LabMode) return;
            var experiment = _s.Experiment;
            if (experiment != null && !experiment.encounterSpawnEnabled) return;
            var timeline = _s.Timeline;
            if (timeline == null || OvertimePhase.IsOvertime(timeline, run.ElapsedSeconds)) return;
            if (run.ElapsedSeconds >= timeline.stageDeadlineSeconds) return;
            if (!_s.Placement.Ready) return;
            if (run.ElapsedSeconds < timeline.graceSeconds) return;
            StartTriggeredAmbientPhases(timeline, run.ElapsedSeconds);
            _ambient.Tick(dt, EffectiveRateMultiplier);
        }

        /// <summary>Ambient phases with event/condition triggers, or triggered by hand, start their run here.</summary>
        void StartTriggeredAmbientPhases(WaveTimelineConfig timeline, float now)
        {
            foreach (var phase in timeline.phases)
            {
                if (!AmbientSpawner.IsAmbient(phase) || _skipped.Contains(phase.phaseId)) continue;
                var manual = _manual.Remove(phase.phaseId);
                if (manual || (phase.trigger.kind != PhaseTriggerKind.Time && !_ambient.HasRun(phase.phaseId) && _triggers.IsTriggered(phase, now)))
                    _ambient.Start(phase, now, manual); // announced (PhaseChanged) by the ambient spawner when it begins
            }
        }

        /// <summary>Overtime spawns, in V3OvertimeDirector's slot (before the run clock advances).</summary>
        public void TickOvertime(float dt)
        {
            if (!_running || LabMode) return;
            var run = _s.Run;
            if (!run.Exists || run.IsPaused) return;
            _scripted.PollArena();
            var phase = FirstActive(p => p.isOvertime);
            _overtime.Tick(dt, phase);
        }

        /// <summary>The storm's warning, start and contraction, in V3SaltwindStormDirector's slot.</summary>
        public void TickStorm(float dt)
        {
            if (!_running) return;
            var run = _s.Run;
            if (!run.Exists || run.IsPaused || run.IsRunEnded) return;
            _storm.Tick(FirstActive(p => p.isStorm));
        }

        /// <summary>Focus spawns and pulses, in V3PressureBeatDirector's slot.</summary>
        public void TickBeats(float dt)
        {
            if (!_running) return;
            var run = _s.Run;
            if (!run.Exists || run.IsPaused || !run.IsGameplayActive) return;
            _scripted.Tick(dt, LabMode);
        }

        /// <summary>Storm edge damage, in V3StormEdgeDamage's slot on the ship.</summary>
        public void TickStormEdge(float dt)
        {
            if (!_running) return;
            var run = _s.Run;
            if (!run.Exists || run.IsRunEnded || run.IsPaused) return;
            _storm.TickEdge(dt, FirstActive(p => p.isStorm));
        }

        TimelinePhase FirstActive(System.Predicate<TimelinePhase> match)
        {
            var timeline = _s.Timeline;
            if (timeline == null) return null;
            foreach (var p in timeline.phases)
                if (match(p) && !_skipped.Contains(p.phaseId)) return p;
            return null;
        }

        // ---- ISpawnController ----------------------------------------------------------------------------

        // ---- Required encounters (M7): the stage content module queues the Beacon boss and Powder Cache here ----

        public void Trigger(EnemySpawnerConfig spawner, int count, int encounterId) => _encounters?.Trigger(spawner, count, encounterId);
        public int TriggerImmediate(EnemySpawnerConfig spawner, int count, int encounterId) => _encounters != null ? _encounters.TriggerImmediate(spawner, count, encounterId) : 0;
        public int ProcessNow() => _encounters != null ? _encounters.ProcessNow() : 0;
        public void Cancel(int encounterId) => _encounters?.Cancel(encounterId);
        public bool HasPending(int encounterId) => _encounters != null && _encounters.HasPending(encounterId);

        public bool ArenaActive => _scripted != null && _scripted.ArenaActive;
        public int ArenaFocusUnitId => _scripted != null ? _scripted.ArenaFocusUnitId : -1;
        public string CurrentPhaseLabel => _currentLabel;

        public IReadOnlyList<PhaseStatus> Phases
        {
            get
            {
                _statusBuffer.Clear();
                var timeline = _s.Timeline;
                if (timeline == null || !_running) return _statusBuffer;
                var now = _s.Run.ElapsedSeconds;
                foreach (var p in timeline.phases) _statusBuffer.Add(new PhaseStatus(p.phaseId, p.label, StateOf(p, now), StartOf(p)));
                return _statusBuffer;
            }
        }

        PhaseState StateOf(TimelinePhase p, float now)
        {
            if (_skipped.Contains(p.phaseId)) return PhaseState.Skipped;
            if (AmbientSpawner.IsAmbient(p))
            {
                if (_ambient.IsRunning(p.phaseId, now)) return PhaseState.Active;
                return _ambient.HasRun(p.phaseId) && now >= StartOf(p) ? PhaseState.Done : PhaseState.Waiting;
            }
            if (ScriptedPhases.IsFocus(p) || ScriptedPhases.IsPulse(p))
            {
                if (_scripted.IsSkipped(p)) return PhaseState.Skipped;
                if (_scripted.IsDone(p)) return ScriptedPhases.IsFocus(p) && !_scripted.IsCleared(p) ? PhaseState.Active : PhaseState.Done;
                return _scripted.IsWarned(p) ? PhaseState.Warned : PhaseState.Waiting;
            }
            if (p.isOvertime) return _overtime.Announced ? PhaseState.Active : PhaseState.Waiting;
            if (p.isStorm) return _storm.Announced ? PhaseState.Active : _storm.Warned ? PhaseState.Warned : PhaseState.Waiting;
            if (p.isBoss) return _s.Run.IsBossFight ? PhaseState.Active : PhaseState.External;
            return PhaseState.External; // run by the legacy encounter executor until M7
        }

        float StartOf(TimelinePhase p) =>
            ScriptedPhases.IsFocus(p) || ScriptedPhases.IsPulse(p) ? _scripted.StartedAt(p)
                : p.trigger.kind == PhaseTriggerKind.Time ? p.trigger.timeSeconds : -1f;

        bool IsCleared(string phaseId)
        {
            var p = _s.Timeline?.Phase(phaseId);
            if (p == null) return false;
            if (_skipped.Contains(phaseId)) return true;
            if (ScriptedPhases.IsFocus(p) || ScriptedPhases.IsPulse(p)) return _scripted.IsCleared(p);
            if (AmbientSpawner.IsAmbient(p)) return StateOf(p, _s.Run.ElapsedSeconds) == PhaseState.Done;
            return false;
        }

        public bool TriggerNow(string phaseId)
        {
            var p = _s.Timeline?.Phase(phaseId);
            if (p == null || !_running || _skipped.Contains(phaseId)) return false;
            if (ScriptedPhases.IsFocus(p) || ScriptedPhases.IsPulse(p)) { _scripted.TriggerNow(p); return true; }
            if (AmbientSpawner.IsAmbient(p)) { _manual.Add(phaseId); return true; }
            return false; // overtime, storm, boss and encounters follow the run clock or the legacy executor
        }

        public bool Skip(string phaseId)
        {
            var p = _s.Timeline?.Phase(phaseId);
            if (p == null || !_running) return false;
            _skipped.Add(phaseId);
            if (AmbientSpawner.IsAmbient(p)) _ambient.Stop(phaseId);
            if (ScriptedPhases.IsFocus(p) || ScriptedPhases.IsPulse(p)) _scripted.Skip(p);
            _s.Publish(CombatEventKind.PhaseChanged, 0, 0, default, phaseId);
            return true;
        }
    }
}
