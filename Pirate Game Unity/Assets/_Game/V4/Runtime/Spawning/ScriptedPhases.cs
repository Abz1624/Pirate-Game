using System.Collections.Generic;
using HighSeas.Enemies;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using UnityEngine;

namespace RoguePirates.V4.Spawning
{
    /// <summary>
    /// Phases that spawn one focus enemy (Reef Crab miniboss, scheduled elites) and phases that raise the spawn rate
    /// for a while (swarm pulses). Port of V3PressureBeatDirector, plus the miniboss arena (V3MinibossArena): while a
    /// pauseAmbient phase's focus enemy lives, ambient spawns stop; when it dies every ambient run tries once at once.
    /// </summary>
    internal sealed class ScriptedPhases
    {
        sealed class State
        {
            public bool Warned;
            public bool Done;
            public bool Skipped;
            public float StartedAt = -1f;
            public float DeferUntil;
            public int FocusUnitId = -1;
            public float PulseTimer;
            public bool Manual;
        }

        readonly SpawnServices _s;
        readonly SpawnExecutor _executor;
        readonly AmbientSpawner _ambient;
        readonly SpawnModifierStack _modifiers;
        readonly Dictionary<string, State> _states = new Dictionary<string, State>();

        bool _arenaActive;
        int _arenaFocus = -1;
        float _arenaBlockedSince = -1f;

        public bool ArenaActive => _arenaActive;
        public int ArenaFocusUnitId => _arenaFocus;

        readonly TimelineTriggers _triggers;

        public ScriptedPhases(SpawnServices services, SpawnExecutor executor, AmbientSpawner ambient, SpawnModifierStack modifiers, TimelineTriggers triggers)
        {
            _s = services;
            _executor = executor;
            _ambient = ambient;
            _modifiers = modifiers;
            _triggers = triggers;
        }

        public static bool IsFocus(TimelinePhase p) => PhaseKinds.IsFocus(p);
        public static bool IsPulse(TimelinePhase p) => PhaseKinds.IsPulse(p);
        static string PulseKey(TimelinePhase p) => "pulse:" + p.phaseId;

        public void BeginRun()
        {
            _states.Clear();
            _arenaActive = false;
            _arenaFocus = -1;
            _arenaBlockedSince = -1f;
        }

        State StateOf(TimelinePhase p)
        {
            if (!_states.TryGetValue(p.phaseId, out var state)) _states[p.phaseId] = state = new State();
            return state;
        }

        public bool IsDone(TimelinePhase p) => _states.TryGetValue(p.phaseId, out var s) && (s.Done || s.Skipped);
        public bool IsSkipped(TimelinePhase p) => _states.TryGetValue(p.phaseId, out var s) && s.Skipped;
        public bool IsWarned(TimelinePhase p) => _states.TryGetValue(p.phaseId, out var s) && s.Warned;
        public float StartedAt(TimelinePhase p) => _states.TryGetValue(p.phaseId, out var s) ? s.StartedAt : -1f;

        /// <summary>Cleared: done and, for a focus phase, its focus enemy no longer alive.</summary>
        public bool IsCleared(TimelinePhase p)
        {
            if (!_states.TryGetValue(p.phaseId, out var s) || !(s.Done || s.Skipped)) return false;
            return s.FocusUnitId <= 0 || !_s.Sink.IsUnitActive(s.FocusUnitId);
        }

        public void TriggerNow(TimelinePhase p) => StateOf(p).Manual = true;

        public void Skip(TimelinePhase p)
        {
            var state = StateOf(p);
            state.Skipped = true;
            if (IsPulse(p)) { state.PulseTimer = 0f; _modifiers.Remove(PulseKey(p)); }
        }

        // ---- Arena ----------------------------------------------------------------------------------------

        /// <summary>Ends the arena once its focus enemy is gone (V3 ends it from the kill path; V4 checks each slot).</summary>
        public void PollArena()
        {
            if (_arenaActive && !_s.Sink.IsUnitActive(_arenaFocus)) EndArena();
        }

        bool BeginArena(int unitId, string label)
        {
            if (unitId <= 0 || !_s.Sink.IsUnitActive(unitId)) return false;
            if (_arenaActive)
            {
                if (_arenaFocus == unitId) return true;
                EndArena();
            }
            _arenaFocus = unitId;
            _arenaActive = true;
            _arenaBlockedSince = _s.Run.ElapsedSeconds;
            _s.Feedback.Toast($"MINI BOSS — {label}", 4f);
            _s.Feedback.Telemetry("miniboss_arena_begin", new Dictionary<string, object> { ["agentId"] = unitId, ["label"] = label, ["culled"] = 0 });
            return true;
        }

        void EndArena()
        {
            _arenaActive = false;
            _arenaFocus = -1;
            var now = _s.Run.ElapsedSeconds;
            if (_arenaBlockedSince >= 0f && now - _arenaBlockedSince > 0f) _ambient.ReleaseAfterArena(now);
            _arenaBlockedSince = -1f;
            _s.Feedback.Toast("Miniboss defeated!", 3f);
            _s.Feedback.Telemetry("miniboss_arena_end");
        }

        // ---- Tick (the V3 beat director's slot) ----------------------------------------------------------

        public void Tick(float dt, bool lab)
        {
            var timeline = _s.Timeline;
            if (timeline == null) return;
            PollArena();
            var elapsed = _s.Run.ElapsedSeconds;
            var bossFight = _s.Run.IsBossFight;
            if (lab) return; // the Module Lab spawns only on request (M2 switched the whole beat director off)
            foreach (var phase in timeline.phases)
                if (IsFocus(phase)) TickFocus(phase, elapsed, bossFight);
            foreach (var phase in timeline.phases)
                if (IsPulse(phase)) TickPulse(phase, elapsed, bossFight, dt);
        }

        bool Triggered(TimelinePhase phase, State state, float elapsed) => state.Manual || _triggers.IsTriggered(phase, elapsed);

        void TickFocus(TimelinePhase phase, float elapsed, bool bossFight)
        {
            var state = StateOf(phase);
            if (state.Done || state.Skipped) return;
            var focus = phase.focus;
            if (focus.bossConflict == BossConflict.WaitUntilBossEnds && bossFight) return;

            MaybeWarn(phase, state, elapsed);
            if (!Triggered(phase, state, elapsed)) return;

            if (focus.bossConflict == BossConflict.SkipPermanently && bossFight)
            {
                state.Skipped = true;
                _s.Feedback.Telemetry("pressure_elite_skipped", new Dictionary<string, object> { ["label"] = phase.label, ["reason"] = "boss_conflict" });
                _s.Feedback.Toast($"{phase.label} skipped — boss active.", 3f);
                _s.Publish(CombatEventKind.PhaseChanged, 0, 0, default, phase.phaseId);
                return;
            }

            if (focus.waitForOtherArena && _arenaActive) return;

            if (TrySpawnFocus(phase, out var unitId, out var enemyId))
            {
                state.Done = true;
                state.StartedAt = elapsed;
                state.FocusUnitId = unitId;
                if (phase.pauseAmbient) BeginArena(unitId, phase.label);
                if (focus.scheduledEliteIndex >= 0)
                    _s.Feedback.Telemetry("pressure_elite_spawned", new Dictionary<string, object> { ["label"] = phase.label });
                else
                    _s.Feedback.Telemetry("miniboss_spawned", new Dictionary<string, object> { ["id"] = enemyId });
                var arrival = SpawnServices.Format(focus.arrivalToast, phase);
                if (arrival.Length > 0) _s.Feedback.Toast(arrival, 4f);
                _s.Publish(CombatEventKind.PhaseChanged, unitId, 1, default, phase.phaseId);
                return;
            }

            if (focus.retryWindowSeconds <= 0f) return; // keep trying (V3 Reef Crab)
            if (state.DeferUntil <= 0f) state.DeferUntil = elapsed + focus.retryWindowSeconds;
            if (elapsed >= state.DeferUntil)
            {
                state.Skipped = true;
                _s.Feedback.Telemetry("pressure_elite_skipped", new Dictionary<string, object> { ["label"] = phase.label });
                _s.Feedback.Toast($"{phase.label} skipped — no spawn slot.", 3f);
                _s.Publish(CombatEventKind.PhaseChanged, 0, 0, default, phase.phaseId);
            }
        }

        bool TrySpawnFocus(TimelinePhase phase, out int unitId, out string enemyId)
        {
            unitId = -1;
            enemyId = null;
            var focus = phase.focus;
            // Playtest round 1: minibosses and elite beats are tougher for strong builds (their times never move; 1 unless Adaptive).
            var levers = _s.Levers != null && _s.Levers.Adaptive ? _s.Levers : null;
            var hp = focus.hpMultiplier * (levers != null ? levers.FocusHpMultiplier : 1f);
            var damage = focus.damageMultiplier * (levers != null ? levers.FocusDamageMultiplier : 1f);
            if (focus.placement == FocusPlacement.RingAroundPlayer)
            {
                if (focus.archetype == null || !_s.Placement.Ready) return false;
                enemyId = focus.archetype.enemyId;
                var index = _s.Sink.FindDefinition(enemyId);
                if (index < 0) return false;
                var angle = _s.Random.LootFloat() * Mathf.PI * 2f;
                var position = _s.Placement.PlayerPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * focus.ringDistance;
                position.y = _s.Sink.DefinitionHeightOffset(index);
                var slot = _s.Sink.Spawn(index, position, focus.affix, focus.speedMultiplier, hp, damage);
                if (slot < 0) return false;
                unitId = _s.Sink.UnitIdAt(slot);
                if (unitId > 0) _s.Publish(CombatEventKind.Spawned, unitId, index, position, enemyId);
                return unitId > 0;
            }

            var profile = _s.Profiles.For(phase.spawner);
            if (profile == null || profile.enemy == null) return false;
            var variantId = focus.variant != null ? focus.variant.variantId : null;
            var before = _s.Sink.MaxActiveUnitId();
            var leader = -1;
            if (_executor.TryBurst(profile, 1, true, hp, damage, 0, ref leader, enemyId: variantId) <= 0) return false;
            var definitionIndex = !string.IsNullOrEmpty(variantId) && _s.Sink.FindDefinition(variantId) >= 0 ? _s.Sink.FindDefinition(variantId) : _s.Sink.IndexOf(profile.enemy);
            unitId = _s.Sink.NewestUnitAfter(before, definitionIndex);
            enemyId = _s.Sink.DefinitionId(definitionIndex);
            // M12: Rum registers the scheduled elite from the PhaseChanged event (unit id) published for this spawn.
            return unitId > 0;
        }

        void TickPulse(TimelinePhase phase, float elapsed, bool bossFight, float dt)
        {
            var state = StateOf(phase);
            MaybeWarn(phase, state, elapsed);
            if (!state.Done && !state.Skipped && Triggered(phase, state, elapsed))
            {
                if (bossFight)
                {
                    state.Skipped = true;
                    _s.Feedback.Telemetry("pressure_pulse_skipped", new Dictionary<string, object> { ["beat"] = phase.phaseId, ["reason"] = "boss_conflict" });
                    _s.Publish(CombatEventKind.PhaseChanged, 0, 0, default, phase.phaseId);
                }
                else
                {
                    state.Done = true;
                    state.StartedAt = elapsed;
                    var duration = phase.duration.kind == PhaseDurationKind.Seconds ? phase.duration.seconds : 0f;
                    state.PulseTimer = duration;
                    _modifiers.SetTimedModifier(PulseKey(phase), phase.rateMultiplier, elapsed, duration);
                    _s.Feedback.Telemetry("pressure_pulse_started", new Dictionary<string, object> { ["beat"] = phase.phaseId });
                    _s.Feedback.Toast($"SWARM PULSE — +{(phase.rateMultiplier - 1f) * 100f:F0}% spawn rate", 4f);
                    _s.Publish(CombatEventKind.PhaseChanged, 0, 1, default, phase.phaseId);
                }
            }

            if (state.PulseTimer > 0f)
            {
                state.PulseTimer -= dt;
                if (state.PulseTimer <= 0f) _modifiers.Remove(PulseKey(phase));
            }
        }

        /// <summary>Time-triggered phases warn once, lead seconds before they start.</summary>
        void MaybeWarn(TimelinePhase phase, State state, float elapsed)
        {
            if (state.Warned || phase.trigger.kind != PhaseTriggerKind.Time || phase.warning.leadSeconds <= 0f) return;
            var t = phase.trigger.timeSeconds;
            if (elapsed < t - phase.warning.leadSeconds || elapsed >= t) return;
            state.Warned = true;
            _s.Feedback.Toast(SpawnServices.Format(phase.warning.template, phase), phase.warning.leadSeconds);
            _s.Publish(CombatEventKind.Warning, 0, 0, default, phase.phaseId);
        }
    }
}
