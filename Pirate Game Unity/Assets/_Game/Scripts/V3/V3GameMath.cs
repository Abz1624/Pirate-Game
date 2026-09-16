using HighSeas.Data;
using UnityEngine;

namespace HighSeas.V3
{
    public static class V3GameMath
    {
        /// <summary>GDD v3.1 inspired curve; combat experiment applies early-level easing from DemoPacingSettings.</summary>
        public static float XPToNext(int level, DemoPacingSettings pacing = null)
        {
            var xp = 25f + 12f * Mathf.Max(0, level);
            if (pacing != null && level <= pacing.earlyLevelCap)
                xp = Mathf.Max(10f, xp * pacing.earlyLevelXpMultiplier);
            return xp;
        }
    }
}
