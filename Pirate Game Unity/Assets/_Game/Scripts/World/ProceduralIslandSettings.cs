using System;
using UnityEngine;

namespace HighSeas.World
{
    [Serializable]
    public struct ProceduralIslandSettings
    {
        [Tooltip("Minimum islands placed per run.")]
        [Min(1)]
        public int countMin;

        [Tooltip("Maximum islands placed per run (inclusive).")]
        [Min(1)]
        public int countMax;

        [Tooltip("Diameter of each island sand footprint in meters.")]
        [Min(4f)]
        public float islandDiameterMeters;

        [Tooltip("Minimum center-to-center distance between islands in meters.")]
        [Min(6f)]
        public float minSeparationMeters;

        [Tooltip("How many random placement attempts before giving up.")]
        [Min(100)]
        public int maxPlacementAttempts;

        public static ProceduralIslandSettings Default => new()
        {
            countMin = 40,
            countMax = 40,
            islandDiameterMeters = 12f,
            minSeparationMeters = 22f,
            maxPlacementAttempts = 8000
        };

        public ProceduralIslandSettings Clamped()
        {
            if (countMin <= 0 && countMax <= 0)
                return Default;

            var s = this;
            s.countMin = Mathf.Max(1, s.countMin);
            s.countMax = Mathf.Max(s.countMin, s.countMax);
            s.islandDiameterMeters = Mathf.Clamp(s.islandDiameterMeters > 0f ? s.islandDiameterMeters : Default.islandDiameterMeters, 4f, 24f);
            s.minSeparationMeters = Mathf.Max(
                s.minSeparationMeters > 0f ? s.minSeparationMeters : Default.minSeparationMeters,
                s.islandDiameterMeters * 1.2f);
            s.maxPlacementAttempts = Mathf.Max(100, s.maxPlacementAttempts);
            return s;
        }
    }
}
