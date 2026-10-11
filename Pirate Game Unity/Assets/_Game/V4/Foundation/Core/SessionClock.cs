using System;

namespace RoguePirates.V4.Foundation.Core
{
    /// <summary>
    /// V5 M9c S1: the clock of one run session, injected into everything of the run that reads time (never a static).
    /// <list type="bullet">
    /// <item><see cref="ClockMode.UnityFrame"/> (single player, the default): every value is Unity's (<c>Time.deltaTime</c>,
    /// <c>Time.timeAsDouble</c>, <c>Time.fixedDeltaTime</c>, <c>Time.frameCount</c>), so a voyage plays exactly as before.</item>
    /// <item><see cref="ClockMode.Driven"/> (a match server's session): the clock moves only inside a step its owner runs
    /// (<see cref="BeginStep"/>/<see cref="EndStep"/>, through <c>V4Game.StepFrame</c>); between steps the delta time is 0 and
    /// nothing advances. A hold (before the player's first inputs, while the player is away, while the cards are up) is
    /// simply "not stepped": <see cref="Held"/> makes the session's step a no-op. Unity's time scale is never involved, so
    /// several sessions in one process can hold and run independently.</item>
    /// </list>
    /// This is the one file of the run's gameplay code that reads <c>UnityEngine.Time</c> (the structure test
    /// <c>V5SessionClockStructureTests</c> holds the rest to it).
    /// </summary>
    public sealed class SessionClock
    {
        public enum ClockMode { UnityFrame, Driven }

        double _time;
        float _deltaTime;
        int _frameCount;
        bool _stepping;
        bool _held;

        /// <summary>A single-player clock (Unity's frame).</summary>
        public SessionClock() : this(ClockMode.UnityFrame, 0f) { }

        /// <param name="fixedDeltaTime">Driven only: the movement step of a ship stepped by the frame (a server steps its
        /// ships itself with the net tick, so this is only the default).</param>
        public SessionClock(ClockMode mode, float fixedDeltaTime)
        {
            Mode = mode;
            if (mode == ClockMode.Driven && !(fixedDeltaTime > 0f)) throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            _fixedDeltaTime = fixedDeltaTime;
        }

        readonly float _fixedDeltaTime;

        public ClockMode Mode { get; }
        public bool IsDriven => Mode == ClockMode.Driven;

        /// <summary>Seconds of this frame (UnityFrame) or of the step running now (Driven; 0 between steps).</summary>
        public float DeltaTime => IsDriven ? _deltaTime : UnityEngine.Time.deltaTime;

        /// <summary>The session's time in seconds: Unity's scaled time (UnityFrame), or the sum of the steps run (Driven, from 0).</summary>
        public double Time => IsDriven ? _time : UnityEngine.Time.timeAsDouble;

        /// <summary><see cref="Time"/> as Unity's float <c>Time.time</c> reads it (UnityFrame), for the readers that took that float.</summary>
        public float TimeSingle => IsDriven ? (float)_time : UnityEngine.Time.time;

        /// <summary>The movement step of a frame-stepped ship.</summary>
        public float FixedDeltaTime => IsDriven ? _fixedDeltaTime : UnityEngine.Time.fixedDeltaTime;

        /// <summary>Unity's frame count (UnityFrame), or the steps run (Driven).</summary>
        public int FrameCount => IsDriven ? _frameCount : UnityEngine.Time.frameCount;

        /// <summary>Steps run so far (Driven); 0 in UnityFrame mode.</summary>
        public long Steps => IsDriven ? _frameCount : 0;

        /// <summary>True inside a Driven step.</summary>
        public bool Stepping => _stepping;

        /// <summary>
        /// The session holds: Driven, its owner set it (the session's steps do nothing until it is released); UnityFrame,
        /// the frame is frozen (<c>Time.timeScale</c> 0, the dev console's slider).
        /// </summary>
        public bool Held => IsDriven ? _held : UnityEngine.Time.timeScale <= 0f;

        /// <summary>Driven: hold or release the session (a held session's step is a no-op).</summary>
        public void SetHeld(bool held)
        {
            RequireDriven();
            _held = held;
        }

        /// <summary>Driven: one step of <paramref name="dt"/> seconds starts (time and frame count move on).</summary>
        public void BeginStep(float dt)
        {
            RequireDriven();
            if (_stepping) throw new InvalidOperationException("SessionClock: a step is already running");
            if (!(dt > 0f)) throw new ArgumentOutOfRangeException(nameof(dt));
            _deltaTime = dt;
            _time += dt;
            _frameCount++;
            _stepping = true;
        }

        /// <summary>Driven: the step ends (the delta time is 0 again until the next one).</summary>
        public void EndStep()
        {
            RequireDriven();
            _stepping = false;
            _deltaTime = 0f;
        }

        void RequireDriven()
        {
            if (!IsDriven) throw new InvalidOperationException("SessionClock: only a Driven clock is stepped or held by its owner");
        }
    }
}
