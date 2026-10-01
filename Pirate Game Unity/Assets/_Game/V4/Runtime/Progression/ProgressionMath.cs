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
            // V5 M3c: the fitted table (growing level-up gaps), extended past its end by its average step over its last
            // four levels (one noisy step would set overtime's levelling).
            var table = c.xpToNextTable;
            if (table != null && table.Length > 0)
            {
                if (level < table.Length) return Math.Max(1, table[level]);
                var last = table[table.Length - 1];
                var n = Math.Min(4, table.Length - 1);
                var step = n > 0 ? Math.Max(1, (int)Math.Round((last - table[table.Length - 1 - n]) / (double)n, MidpointRounding.AwayFromZero)) : 1;
                return Math.Max(1, last + step * (level - table.Length + 1));
            }
            if (level == 0) return c.levelZeroToOneXp;
            var value = c.xpCurveMultiplier * Math.Pow(level + 1, c.xpCurveExponent);
            return (int)Math.Round(c.xpCurveOffset + value, MidpointRounding.AwayFromZero);
        }
    }
}
