using System;
using Unity.Profiling;
using UnityEngine;

namespace RoguePirates.V4
{
    /// <summary>The per-frame run ticks, in the order they run (M14.14).</summary>
    public enum FrameStep
    {
        /// <summary>stage content: per-POI ticks, then chest spawns and Powder Cache (POIManager / POIInteractable)</summary>
        Stage = 0,
        /// <summary>Weapon Manager (WeaponController.Update)</summary>
        Weapons = 1,
        /// <summary>ambient spawning (EncounterSpawner.Update)</summary>
        Ambient = 2,
        /// <summary>Captain passives and active (V3CaptainController / V3CaptainActiveController)</summary>
        Captain = 3,
        /// <summary>Hellfire Nozzle patches (HellfirePatchWorld)</summary>
        Hellfire = 4,
        /// <summary>item triggers (V3RunItemEffects)</summary>
        Items = 5,
        /// <summary>Beacon discovery (V3DiscoverySystem)</summary>
        Discovery = 6,
        /// <summary>hold-to-interact and the chest prompt (V3InteractionController)</summary>
        Interaction = 7,
        /// <summary>storm damage outside the safe zone (V3StormEdgeDamage, on the ship)</summary>
        StormEdge = 8,
        /// <summary>movement and performance samples (CombatTelemetryDriver)</summary>
        Telemetry = 9,
        /// <summary>overtime spawning (V3OvertimeDirector)</summary>
        Overtime = 10,
        /// <summary>the storm's shrinking safe zone (V3SaltwindStormDirector)</summary>
        Storm = 11,
        /// <summary>scheduled beats: Reef Crab, elites, swarm pulses (V3PressureBeatDirector)</summary>
        Beats = 12,
        /// <summary>explosive barrels, only when the stage config places them (V3ExplosiveBarrelWorld)</summary>
        Barrels = 13,
        /// <summary>orbit blade positions from the run time (V3's juice controller at execution order 50)</summary>
        OrbitPositions = 14,
    }

    /// <summary>
    /// The run's frame (M14.14; replaces the legacy update slots, which ran each V4 tick from a component placed where
    /// the V3 component it replaced sat, so the scene and creation order decided the frame). One driver runs every
    /// registered tick in <see cref="FrameStep"/> order, the order captured from a live V4 run before the switch-over;
    /// all run after the run clock has advanced this frame (the driver's execution order is after the run manager's).
    /// The orbit step receives the scaled time (it places blades from it); the others the frame's delta time.
    /// </summary>
    public sealed class FramePipeline
    {
        static readonly int StepCount = Enum.GetValues(typeof(FrameStep)).Length;
        /// <summary>V5 M3e: one profiler marker per step ("V4.Frame.Weapons"...), so time and garbage show per module.</summary>
        static readonly ProfilerMarker[] Markers = BuildMarkers();

        static ProfilerMarker[] BuildMarkers()
        {
            var markers = new ProfilerMarker[StepCount];
            for (var i = 0; i < StepCount; i++) markers[i] = new ProfilerMarker("V4.Frame." + (FrameStep)i);
            return markers;
        }

        readonly Action<float>[] _ticks = new Action<float>[StepCount];
        readonly int[] _frameAt = new int[StepCount];
        readonly float[] _clockAt = new float[StepCount];

        /// <summary>The run clock the steps see (recorded per step for the frame-order tests).</summary>
        public Func<float> Clock;

        public FramePipeline()
        {
            for (var i = 0; i < StepCount; i++) { _frameAt[i] = -1; _clockAt[i] = -1f; }
        }

        /// <summary>Sets the tick of a step (a later registration replaces an earlier one).</summary>
        public void Register(FrameStep step, Action<float> tick) => _ticks[(int)step] = tick;

        public bool Has(FrameStep step) => _ticks[(int)step] != null;
        public int FrameAtTick(FrameStep step) => _frameAt[(int)step];
        public float ClockAtTick(FrameStep step) => _clockAt[(int)step];

        public void Clear()
        {
            for (var i = 0; i < StepCount; i++) _ticks[i] = null;
        }

        /// <summary>Runs one frame: every registered step in order.</summary>
        public void Run(float dt, float time, int frame)
        {
            var clock = Clock != null ? Clock() : -1f;
            for (var i = 0; i < StepCount; i++)
            {
                var tick = _ticks[i];
                if (tick == null) continue;
                _frameAt[i] = frame;
                _clockAt[i] = clock;
                using (Markers[i].Auto()) tick(i == (int)FrameStep.OrbitPositions ? time : dt);
            }
        }
    }

    /// <summary>Runs the run's <see cref="FramePipeline"/> each frame, after the run clock (the run manager, order 0).</summary>
    [DefaultExecutionOrder(10)]
    [AddComponentMenu("")]
    public sealed class V4FrameDriver : MonoBehaviour
    {
        public FramePipeline Pipeline;

        void Update()
        {
            if (RunRequest.RemoteSimulation) return; // V5 M9b part 2b: a net client's stage; the server runs the frame steps
            Pipeline?.Run(Time.deltaTime, Time.time, Time.frameCount);
        }
    }
}
