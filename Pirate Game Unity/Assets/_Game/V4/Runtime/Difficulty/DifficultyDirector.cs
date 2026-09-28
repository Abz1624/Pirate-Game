using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Difficulty
{
    /// <summary>What the power index reads (implemented over the build and the ship; read-only).</summary>
    public interface IPowerInputs
    {
        IReadOnlyList<WeaponSlot> Weapons { get; }
        IWeaponModifiers Modifiers { get; }
        float Hull { get; }
        float MaxHull { get; }
        /// <summary>The build's incoming-damage multiplier (Brace!, Keel, armor): below 1 means tougher.</summary>
        float IncomingDamageMultiplier { get; }
    }

    /// <summary>Pressure for presentation, the console, traces and (M11) the levers.</summary>
    public interface IPressureView
    {
        DirectorMode Mode { get; }
        string Version { get; }
        /// <summary>Current Pressure (the forced value while one is set).</summary>
        float Pressure { get; }
        /// <summary>Test-only override from the console (M11): while set, Pressure is this value and the run is not ranked.</summary>
        float? ForcedPressure { get; set; }
        /// <summary>Share of the build's offense from close-range weapons (composition lever), 0..1.</summary>
        float CloseRangeShare { get; }
        /// <summary>Developer tools were used this run (the tuning console opened): the run record is never ranked.</summary>
        bool Unranked { get; }
        void MarkUnranked();
        float Offense { get; }
        float OffenseNorm { get; }
        float DefenseNorm { get; }
        float Performance { get; }
        /// <summary>The last samples (oldest first), for the console graph.</summary>
        IReadOnlyList<float> History { get; }
    }

    /// <summary>
    /// Player power index (M10): offense = Σ over weapons of damage × shots per second × projectiles × coverage;
    /// normalized against the run's starting offense grown by the enemy HP curve (the game expects the build to keep pace
    /// with enemy toughness). Defense = hull fraction ÷ incoming multiplier. Pure functions (tested).
    /// </summary>
    public static class PlayerPowerIndex
    {
        public static float Offense(IPowerInputs inputs, DirectorConfig d) => Offense(inputs, d, out _);

        /// <summary>Offense, and how much of it comes from close-range behaviours (<see cref="DirectorConfig.closeRangeBehaviors"/>).</summary>
        public static float Offense(IPowerInputs inputs, DirectorConfig d, out float closeRange)
        {
            closeRange = 0f;
            var m = inputs.Modifiers;
            if (inputs.Weapons == null || m == null) return 0f;
            var total = 0f;
            foreach (var w in inputs.Weapons)
            {
                if (w?.Config == null) continue;
                var damage = w.Config.baseDamage * WeaponStatResolver.LevelMult(w) * Mathf.Max(0f, m.GlobalDamage + m.CaptainAdditiveDamage)
                    * m.ItemGlobalDamageMultiplier * m.CaptainWeaponMultiplier(w.Id);
                var cooldown = Mathf.Max(0.05f, WeaponStatResolver.Cooldown(w, m, m.ItemAttackSpeedMultiplier));
                var quantity = WeaponStatResolver.ProjectileQuantity(w, m);
                var coverage = d.behaviorCoverage != null && (int)w.Config.behavior < d.behaviorCoverage.Length ? d.behaviorCoverage[(int)w.Config.behavior] : 1f;
                var part = damage / cooldown * quantity * coverage;
                total += part;
                if (d.closeRangeBehaviors != null && Array.IndexOf(d.closeRangeBehaviors, w.Config.behavior) >= 0) closeRange += part;
            }
            return total;
        }

        public static float Defense(IPowerInputs inputs) =>
            inputs.MaxHull > 0f ? Mathf.Clamp01(inputs.Hull / inputs.MaxHull) / Mathf.Max(0.1f, inputs.IncomingDamageMultiplier) : 1f;
    }

    /// <summary>One sample's inputs and the director's state (pure step, tested for replay determinism and shaping).</summary>
    public struct DirectorSample
    {
        public float OffenseNorm, DefenseNorm, Performance;
    }

    public static class DirectorMath
    {
        /// <summary>The raw target: 1 + gains × how far each part is from even, clamped to the curve's Pressure band.</summary>
        public static float Target(in DirectorSample s, DirectorConfig d, float min, float max)
        {
            var raw = 1f + d.offenseGain * (s.OffenseNorm - 1f) + d.defenseGain * (s.DefenseNorm - 1f) + d.performanceGain * (s.Performance - 1f);
            return float.IsNaN(raw) ? 1f : Mathf.Clamp(raw, min, max);
        }

        /// <summary>Exponential smoothing toward the target, then the slope limit, then the band.</summary>
        public static float Step(float pressure, float target, float dt, DirectorConfig d, float min, float max)
        {
            if (float.IsNaN(target) || float.IsInfinity(target)) target = pressure; // a broken sample never poisons Pressure
            var alpha = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, d.smoothingSeconds));
            var smoothed = pressure + (target - pressure) * alpha;
            var limit = d.maxChangePerSecond * dt;
            return Mathf.Clamp(Mathf.Clamp(smoothed, pressure - limit, pressure + limit), min, max);
        }

        /// <summary>Performance: kill/spawn ratio against even, minus the hull lost per second against its penalty; 1 = even.</summary>
        public static float Performance(int kills, int spawns, float hullLostFraction, float windowSeconds, DirectorConfig d)
        {
            var ratio = spawns > 0 ? kills / (float)spawns : d.evenKillRatio; // nothing arrived: even, not a lead
            var lead = Mathf.Clamp(ratio / Mathf.Max(0.01f, d.evenKillRatio), 0f, 2f);
            var loss = hullLostFraction / Mathf.Max(0.01f, windowSeconds) / Mathf.Max(0.0001f, d.hullLossPenaltyPerSecond);
            return Mathf.Clamp(lead - loss, 0f, 2f);
        }
    }

    /// <summary>
    /// The difficulty director (M10, observe): every sample interval it measures the player's power, updates Pressure and
    /// publishes a PowerChanged event. It reads only simulation state and the event stream (no random draws, no writes to
    /// other modules), so Observe mode cannot change a run. M11 lets levers read <see cref="IPressureView.Pressure"/>.
    /// </summary>
    public sealed class DifficultyDirector : IRunModule, IPressureView, IDifficultyLevers
    {
        const int EliteStreamSalt = 0x5EED11; // the elite lever's own stream: seed-derived, never a V3 RunRng stream
        const int HistoryLength = 180;

        readonly Queue<(float time, int kills, int spawns, float hullLost)> _window = new Queue<(float, int, int, float)>();
        readonly List<float> _history = new List<float>(HistoryLength);
        RunContext _context;
        IPowerInputs _inputs;
        RoguePirates.V4.Spawning.IRunStatus _run; // the run's clock (the console's jump-to-time moves it; enemies scale on it)
        DirectorConfig _config;
        DifficultyCurveConfig _curve;
        bool _failed;
        CombatEvents _events;
        float _sampleTimer, _offense0, _simTime;
        int _pendingKills, _pendingSpawns;
        float _pendingHullLost;
        float _pressure = 1f; // the smoothed value; Pressure shows the forced one while set
        float? _forced;
        bool _everForced;
        float _pressureSum, _pressurePeak = 1f;
        int _pressureSamples;
        System.Random _eliteRng = new System.Random(EliteStreamSalt);

        public string Id => "difficulty";
        public int TickOrder => 50; // after the clock, before spawning (plan §4.4)

        public DirectorMode Mode => Config != null ? Config.mode : DirectorMode.Off;
        public string Version => Config != null ? Config.version : string.Empty;
        public float Pressure => _forced ?? _pressure;
        public float? ForcedPressure
        {
            get => _forced;
            set
            {
                _forced = value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value)
                    ? Mathf.Clamp(value.Value, PressureMin, PressureMax) : (float?)null;
                if (_forced.HasValue) _everForced = true;
            }
        }
        public float CloseRangeShare { get; private set; }
        float PressureMin => Curve != null ? Curve.pressureMin : 0.85f;
        float PressureMax => Curve != null ? Curve.pressureMax : 1.35f;
        public float Offense { get; private set; }
        public float OffenseNorm { get; private set; } = 1f;
        public float DefenseNorm { get; private set; } = 1f;
        public float Performance { get; private set; } = 1f;
        public IReadOnlyList<float> History => _history;

        // Cached once: live edits change the run's copies in place (Single<T> scans the whole set).
        DirectorConfig Config => _config ??= _context?.Config?.Single<DirectorConfig>();
        DifficultyCurveConfig Curve => _curve ??= _context?.Config?.Single<DifficultyCurveConfig>();

        public void Initialize(RunContext context)
        {
            _context = context;
            context.TryGet(out _inputs);
            context.TryGet(out _run);
            _events = context.Events;
            if (_events != null) _events.TickPublished += OnEvents;
        }

        public void BeginRun(in RunStart start)
        {
            _pressure = 1f;
            _forced = null;
            _everForced = false;
            _pressureSum = 0f;
            _pressurePeak = 1f;
            _pressureSamples = 0;
            CloseRangeShare = 0f;
            _eliteRng = new System.Random(start.Seed ^ EliteStreamSalt);
            Unranked = false;
            OffenseNorm = DefenseNorm = Performance = 1f;
            Offense = _offense0 = 0f;
            _sampleTimer = 0f;
            _simTime = 0f;
            _window.Clear();
            _history.Clear();
            _pendingKills = _pendingSpawns = 0;
            _pendingHullLost = 0f;
            _failed = false;
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            foreach (var e in batch)
            {
                switch (e.Kind)
                {
                    case CombatEventKind.Killed: _pendingKills++; break;
                    case CombatEventKind.Spawned: _pendingSpawns++; break;
                    case CombatEventKind.PlayerDamaged:
                        if (_inputs != null && _inputs.MaxHull > 0f) _pendingHullLost += e.Z / _inputs.MaxHull;
                        break;
                }
            }
        }

        public void Tick(in SimTick tick)
        {
            _simTime = _run != null && _run.Exists ? _run.ElapsedSeconds : tick.SimTime;
            var d = Config;
            if (d == null || _inputs == null || _failed) return;
            if (d.mode == DirectorMode.Off)
            {
                // Off: Pressure is 1, and nothing gathered while off lands in the window when it comes back on.
                _pressure = OffenseNorm = DefenseNorm = Performance = 1f;
                _pendingKills = _pendingSpawns = 0;
                _pendingHullLost = 0f;
                _window.Clear();
                _sampleTimer = 0f;
                return;
            }
            _sampleTimer += tick.Dt;
            while (_sampleTimer + 0.0001f >= d.sampleIntervalSeconds) // 60 × (1/60) falls just short of 1 in floats
            {
                _sampleTimer = Mathf.Max(0f, _sampleTimer - d.sampleIntervalSeconds);
                try { Sample(d, d.sampleIntervalSeconds); }
                catch (Exception e)
                {
                    // Observe must never change a run: a failing sample stops the director (Pressure stays), it never
                    // stops the modules ticking after it.
                    _failed = true;
                    Debug.LogException(e);
                    return;
                }
            }
        }

        void Sample(DirectorConfig d, float dt)
        {
            var curve = Curve;
            var min = curve != null ? curve.pressureMin : 0.85f;
            var max = curve != null ? curve.pressureMax : 1.35f;

            _window.Enqueue((_simTime, _pendingKills, _pendingSpawns, _pendingHullLost));
            _pendingKills = _pendingSpawns = 0;
            _pendingHullLost = 0f;
            while (_window.Count > 0 && _window.Peek().time <= _simTime - d.performanceWindowSeconds) _window.Dequeue();
            int kills = 0, spawns = 0;
            var hullLost = 0f;
            foreach (var w in _window) { kills += w.kills; spawns += w.spawns; hullLost += w.hullLost; }

            Offense = PlayerPowerIndex.Offense(_inputs, d, out var closeRange);
            CloseRangeShare = Offense > 0f ? Mathf.Clamp01(closeRange / Offense) : 0f;
            // The baseline is the first offense seen, brought back to time 0 (weapons may arrive late, e.g. the Module Lab).
            if (_offense0 <= 0f && Offense > 0f) _offense0 = Offense / Mathf.Max(0.0001f, CurveEvaluator.ExpectedHpScale(curve, _simTime));
            var expected = _offense0 * CurveEvaluator.ExpectedHpScale(curve, _simTime);
            var sample = new DirectorSample
            {
                OffenseNorm = expected > 0f ? Offense / expected : 1f,
                DefenseNorm = PlayerPowerIndex.Defense(_inputs),
                Performance = DirectorMath.Performance(kills, spawns, hullLost, Mathf.Min(d.performanceWindowSeconds, Mathf.Max(1f, _simTime)), d),
            };
            OffenseNorm = sample.OffenseNorm;
            DefenseNorm = sample.DefenseNorm;
            Performance = sample.Performance;
            _pressure = DirectorMath.Step(_pressure, DirectorMath.Target(sample, d, min, max), dt, d, min, max);
            _pressureSum += Pressure;
            _pressureSamples++;
            _pressurePeak = Mathf.Max(_pressurePeak, Pressure);

            if (_history.Count >= HistoryLength) _history.RemoveAt(0);
            _history.Add(Pressure);
            _events?.Publish(new CombatEvent(CombatEventKind.PowerChanged, _context.Clock != null ? _context.Clock.TickIndex : 0, _simTime,
                (int)d.mode, 0, Pressure, OffenseNorm, DefenseNorm, d.version));
        }

        public void EndRun(in RunEnd end)
        {
            if (_events != null) _events.TickPublished -= OnEvents;
        }

        // ---- M11: the levers (neutral unless Adaptive and the lever is on) ----------------------------------------

        public bool Adaptive => !_failed && Config != null && Config.mode == DirectorMode.Adaptive;
        DirectorConfig IDifficultyLevers.Config => Config;

        public float ThreatRateMultiplier => Adaptive && Config.threatRateLever ? LeverMath.ThreatRate(Pressure, Config) : 1f;
        public float TierLeadSeconds => Adaptive && Config.tierTimingLever ? LeverMath.TierLead(Pressure, PressureMax, Config) : 0f;
        public float CapHpMultiplier => Adaptive && Config.capHpLever ? LeverMath.CapHp(Pressure, PressureMax, Config) : 1f;
        // Never below 1: a weak build must not delay a "Pressure at least" phase that fired without the director (early, never later).
        public float ConditionPressure => Adaptive && Config.pressureConditionTriggers ? Mathf.Max(1f, Pressure) : 1f;
        public bool CompositionActive => Adaptive && Config.compositionLever && LeverMath.Above(Pressure, PressureMax) > 0f;

        public float SpawnerWeight(AnimationCurve pressureWeight)
        {
            if (!Adaptive || !Config.compositionLever || pressureWeight == null || pressureWeight.length == 0) return 1f;
            var w = pressureWeight.Evaluate(Pressure);
            return float.IsNaN(w) ? 1f : Mathf.Max(0f, w);
        }

        public float CompositionWeight(IReadOnlyList<string> roleTags) =>
            CompositionActive ? LeverMath.Composition(roleTags, CloseRangeShare, LeverMath.Above(Pressure, PressureMax), Config) : 1f;

        public bool RollElite()
        {
            if (!Adaptive || !Config.eliteLever) return false;
            var chance = LeverMath.EliteChance(Pressure, PressureMax, Config);
            return chance > 0f && _eliteRng.NextDouble() < chance; // no draw at or below Pressure 1
        }

        /// <summary>M11: the run used developer tools (the tuning console): never ranked.</summary>
        public bool Unranked { get; private set; }
        public void MarkUnranked() => Unranked = true;

        /// <summary>The run's leaderboard record (M11). Ranked: Adaptive, the ranked version, never forced, no tuning edits.</summary>
        public RunScoreRecord CreateRecord(int seed, int kills, float runSeconds, string configHash, int tuningEdits)
        {
            var d = Config;
            var mode = d != null ? d.mode : DirectorMode.Off;
            return new RunScoreRecord
            {
                seed = seed,
                kills = kills,
                runSeconds = runSeconds,
                averagePressure = _pressureSamples > 0 ? _pressureSum / _pressureSamples : 1f,
                peakPressure = _pressurePeak,
                directorVersion = d != null ? d.version : string.Empty,
                directorMode = mode.ToString(),
                configHash = configHash ?? string.Empty,
                pressureForced = _everForced,
                tuningEdits = tuningEdits,
                ranked = d != null && mode == DirectorMode.Adaptive && d.version == d.rankedVersion && !_everForced && tuningEdits == 0 && !_failed && !Unranked,
                finishedUtc = DateTime.UtcNow.ToString("o"),
            };
        }
    }
}
