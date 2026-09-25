using System;
using RoguePirates.Simulation;

namespace RoguePirates.V4
{
    /// <summary>
    /// Fixed-step clock for V4 modules: accumulates frame time and yields whole ticks (capped per frame).
    /// Pause reasons are a set, so clearing one reason never clears another.
    /// </summary>
    public sealed class RunClock
    {
        public const float DefaultTickRate = 60f;
        public const int DefaultMaxStepsPerFrame = 5;

        readonly PauseReasonSet _pause = new PauseReasonSet();
        float _accumulator;

        public float TickDuration { get; }
        public int MaxStepsPerFrame { get; }
        public long TickIndex { get; private set; }
        public float SimTime => TickIndex * TickDuration;
        public bool IsPaused => _pause.IsPaused;
        public PauseReasonSet Pause => _pause;

        public RunClock(float tickRate = DefaultTickRate, int maxStepsPerFrame = DefaultMaxStepsPerFrame)
        {
            if (tickRate <= 0f) throw new ArgumentOutOfRangeException(nameof(tickRate));
            if (maxStepsPerFrame < 1) throw new ArgumentOutOfRangeException(nameof(maxStepsPerFrame));
            TickDuration = 1f / tickRate;
            MaxStepsPerFrame = maxStepsPerFrame;
        }

        /// <summary>Adds frame time and returns how many ticks to run now. Excess time beyond the cap is dropped.</summary>
        public int Advance(float frameDeltaSeconds)
        {
            if (IsPaused || frameDeltaSeconds <= 0f) return 0;
            _accumulator += frameDeltaSeconds;
            var steps = 0;
            while (_accumulator >= TickDuration && steps < MaxStepsPerFrame)
            {
                _accumulator -= TickDuration;
                steps++;
            }

            if (steps == MaxStepsPerFrame && _accumulator >= TickDuration)
                _accumulator = 0f;
            return steps;
        }

        /// <summary>Consumes one tick and returns its description.</summary>
        public SimTick NextTick()
        {
            var tick = new SimTick(TickIndex, TickDuration, SimTime);
            TickIndex++;
            return tick;
        }

        public void Reset()
        {
            TickIndex = 0;
            _accumulator = 0f;
        }
    }
}
