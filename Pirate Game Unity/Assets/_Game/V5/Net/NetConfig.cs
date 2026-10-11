using System;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9c: the match server's protocol and stepping knobs. It lives with the net code, outside the voyage's ConfigSet, so
    /// it never changes the voyage's config hash (the run checks and the clients' rules stay as they are). The server scene
    /// (V5NetSpike) holds the asset (Assets/_Game/V5/Net/V5NetConfig.asset); a server made without one (the tests) uses
    /// <see cref="Default"/>, the class defaults, which equal the asset's values.
    /// </summary>
    [CreateAssetMenu(menuName = "Rogue Pirates/V5 Net Config", fileName = "V5NetConfig")]
    public sealed class NetConfig : ScriptableObject
    {
        /// <summary>Test presets only (as RP_TEST_PRESET): overrides <see cref="worldStepsPerTick"/> on a measurement server.</summary>
        public const string WorldStepsVariable = "RP_WORLD_STEPS_PER_TICK";

        [Tooltip("V5 M9c S1: the run's frame steps per 30 Hz net tick on the server (2: two of 1/60 s, as the M9b server's 60 fps " +
                 "frames and a 60 fps voyage; 1: one of 1/30 s, which slows fast and burst weapons, at most one shot a frame: " +
                 "measured in plan/v5/phase-09c-multiship/s1). The enemy world, the frame steps and the run clock move once per " +
                 "step; the ship always moves once per tick (its input).")]
        [Range(1, 4)] public int worldStepsPerTick = 2;

        /// <summary>The steps per tick this server uses: the asset's, or the test-preset override when test presets are allowed.</summary>
        public int WorldStepsPerTick
        {
            get
            {
                if (NetTestPresets.Allowed && int.TryParse(Environment.GetEnvironmentVariable(WorldStepsVariable), out var n) && n >= 1 && n <= 4) return n;
                return Mathf.Clamp(worldStepsPerTick, 1, 4);
            }
        }

        /// <summary>A config with the class defaults (a server made without the scene's asset).</summary>
        public static NetConfig Default()
        {
            var c = CreateInstance<NetConfig>();
            c.name = "V5NetConfig (defaults)";
            c.hideFlags = HideFlags.DontSave;
            return c;
        }
    }
}
