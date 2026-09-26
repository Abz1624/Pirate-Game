using System;
using UnityEngine;

namespace RoguePirates.V4.Config
{
    public enum CurveMode { Polynomial, Keyframes }

    /// <summary>value(x) = c0 + c1·x + c2·x² (Polynomial) or keyframes.Evaluate(x) (Keyframes).</summary>
    [Serializable]
    public sealed class CurveSpec
    {
        public CurveMode mode = CurveMode.Polynomial;
        public float c0 = 1f, c1, c2;
        public AnimationCurve keyframes = AnimationCurve.Linear(0f, 1f, 20f, 1f);

        public float Evaluate(float x) => mode == CurveMode.Polynomial ? c0 + c1 * x + c2 * x * x : keyframes.Evaluate(x);
    }

    [Serializable]
    public sealed class TierStageMultipliers
    {
        [Tooltip("Run tier 1, 2, 3+.")] public float[] tier = { 1f, 1f, 1f };
        [Tooltip("Stage index 0, 1, 2+.")] public float[] stage = { 1f, 1f, 1f };
    }

    /// <summary>
    /// Enemy HP and damage growth. Defaults equal V3: GameMath.EnemyHP/EnemyDamage per minute with tier and stage
    /// multipliers and the global HP factor 2/3, the Saltwind ordinary-time multipliers after 2:00, and the overtime
    /// multipliers. The overtime values reach the game in M1 through the legacy profile; the rest from M3/M8.
    /// </summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Difficulty Curve", fileName = "DifficultyCurve")]
    public sealed class DifficultyCurveConfig : V4ConfigAsset
    {
        [Header("Per-minute growth (GameMath)")]
        [AppliesFrom(8)] public CurveSpec enemyHpPerMinute = new CurveSpec { c0 = 1f, c1 = 0.09f, c2 = 0.018f };
        [AppliesFrom(8)] public TierStageMultipliers hpMultipliers = new TierStageMultipliers { tier = new[] { 1f, 1.45f, 2.05f }, stage = new[] { 1f, 1.22f, 1.5f } };
        [AppliesFrom(8)] public float globalHpMultiplier = 2f / 3f;
        [AppliesFrom(8)] public CurveSpec enemyDamagePerMinute = new CurveSpec { c0 = 1f, c1 = 0.055f };
        [AppliesFrom(8)] public TierStageMultipliers damageMultipliers = new TierStageMultipliers { tier = new[] { 1f, 1.25f, 1.6f }, stage = new[] { 1f, 1.12f, 1.28f } };

        [Header("Saltwind ordinary time (SaltwindFinaleLogic)")]
        [AppliesFrom(3)] public float ordinaryScalingDelaySeconds = 120f;
        [Tooltip("x = minutes after the delay.")] [AppliesFrom(3)] public CurveSpec ordinaryHp = new CurveSpec { c0 = 1f, c1 = 0.11f, c2 = 0.022f };
        [AppliesFrom(3)] public CurveSpec ordinaryDamage = new CurveSpec { c0 = 1f, c1 = 0.14f, c2 = 0.028f };

        [Header("Overtime (live through the stage profile)")]
        public float overtimeHpMultiplierBase = 2f;
        [Tooltip("HP multiplier = base × 2^(overtime minutes × this).")] public float overtimeHpMultiplierExponentPerMinute = 0.5f;
        public float overtimeDamageBonusPerMinute = 0.12f;

        [Header("Director (M10)")]
        [AppliesFrom(10)] public float pressureMin = 0.85f;
        [AppliesFrom(10)] public float pressureMax = 1.35f;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(globalHpMultiplier), globalHpMultiplier);
            report.NotNegative(this, nameof(ordinaryScalingDelaySeconds), ordinaryScalingDelaySeconds);
            report.Positive(this, nameof(overtimeHpMultiplierBase), overtimeHpMultiplierBase);
            if (pressureMax < pressureMin) report.Error(this, nameof(pressureMax), "below pressureMin");
            foreach (var (name, m) in new[] { ("hpMultipliers", hpMultipliers), ("damageMultipliers", damageMultipliers) })
                if (m.tier.Length < 3 || m.stage.Length < 3) report.Error(this, name, "needs three tier and three stage values");
        }
    }
}
