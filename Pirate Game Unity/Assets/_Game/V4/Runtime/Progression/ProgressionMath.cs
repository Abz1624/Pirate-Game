using System;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Progression
{
    /// <summary>Level-up arithmetic from <see cref="ProgressionConfig"/> (V3 StageBalanceProfileExportCodec.XpToNextLevel).</summary>
    public static class ProgressionMath
    {
        /// <summary>XP from <paramref name="level"/> to the next level.</summary>
        public static int XpToNext(ProgressionConfig c, int level)
        {
            if (c == null) throw new ArgumentNullException(nameof(c));
            if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
            if (level == 0) return c.levelZeroToOneXp;
            var value = c.xpCurveMultiplier * Math.Pow(level + 1, c.xpCurveExponent);
            return (int)Math.Round(c.xpCurveOffset + value, MidpointRounding.AwayFromZero);
        }
    }
}
