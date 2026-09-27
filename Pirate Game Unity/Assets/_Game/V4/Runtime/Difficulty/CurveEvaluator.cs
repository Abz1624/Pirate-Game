using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Difficulty
{
    /// <summary>
    /// Enemy HP and damage growth from the difficulty curve (M10). The defaults reproduce V3's GameMath.EnemyHP and
    /// EnemyDamage to the bit (same expression order), at any time, not just whole minutes; the curves can be switched to
    /// keyframes in the config.
    /// </summary>
    public static class CurveEvaluator
    {
        static int TierIndex(int tier) => tier == 1 ? 0 : tier == 2 ? 1 : 2;  // V3: 1, 2, anything else
        static int StageIndex(int stage) => stage == 0 ? 0 : stage == 1 ? 1 : 2;

        static float Pick(float[] values, int index) => values != null && values.Length > 0 ? values[Mathf.Min(index, values.Length - 1)] : 1f;

        // The polynomial is written inline, in V3's shape: the runtime may keep float intermediates at higher precision,
        // so a curve value rounded to float before the multiplications would differ from V3 in the last bit.

        /// <summary>V3 GameMath.EnemyHP: base × curve(minute) × tier × stage × global.</summary>
        public static float EnemyHp(DifficultyCurveConfig c, float baseHp, float minute, int tier, int stageIndex)
        {
            var s = c.enemyHpPerMinute;
            var tierMult = Pick(c.hpMultipliers.tier, TierIndex(tier));
            var stageMult = Pick(c.hpMultipliers.stage, StageIndex(stageIndex));
            var global = c.globalHpMultiplier;
            if (s.mode != CurveMode.Polynomial) return baseHp * s.keyframes.Evaluate(minute) * tierMult * stageMult * global;
            return baseHp * (s.c0 + s.c1 * minute + s.c2 * minute * minute) * tierMult * stageMult * global;
        }

        /// <summary>V3 GameMath.EnemyDamage: base × curve(minute) × tier × stage.</summary>
        public static float EnemyDamage(DifficultyCurveConfig c, float baseDamage, float minute, int tier, int stageIndex)
        {
            var s = c.enemyDamagePerMinute;
            var tierMult = Pick(c.damageMultipliers.tier, TierIndex(tier));
            var stageMult = Pick(c.damageMultipliers.stage, StageIndex(stageIndex));
            if (s.mode != CurveMode.Polynomial) return baseDamage * s.keyframes.Evaluate(minute) * tierMult * stageMult;
            if (s.c2 == 0f) return baseDamage * (s.c0 + s.c1 * minute) * tierMult * stageMult; // V3 has no square term
            return baseDamage * (s.c0 + s.c1 * minute + s.c2 * minute * minute) * tierMult * stageMult;
        }

        /// <summary>How much tougher an ambient enemy is at this time than at the start (the per-minute curve × the ordinary-time curve).</summary>
        public static float ExpectedHpScale(DifficultyCurveConfig c, float elapsedSeconds)
        {
            if (c == null) return 1f;
            var scale = c.enemyHpPerMinute.Evaluate(elapsedSeconds / 60f) / Mathf.Max(0.0001f, c.enemyHpPerMinute.Evaluate(0f));
            if (elapsedSeconds >= c.ordinaryScalingDelaySeconds) scale *= c.ordinaryHp.Evaluate((elapsedSeconds - c.ordinaryScalingDelaySeconds) / 60f);
            return scale;
        }
    }
}
