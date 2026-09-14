using UnityEngine;

namespace HighSeas.V3
{
    public static class V3GameMath
    {
        /// <summary>GDD v3.1 inspired curve, slowed for Saltwind prototype pacing.</summary>
        public static float XPToNext(int level) => 25f + 12f * Mathf.Max(0, level);
    }
}
