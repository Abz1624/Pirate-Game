using UnityEngine;

namespace RoguePirates.V4.Config
{

    /// <summary>Run-level settings for the V4 composition root.</summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : V4ConfigAsset
    {
        [Tooltip("V4 module ticks per second (fixed step).")] [AppliesFrom(0, "read when the run starts")] public float tickRate = 60f;
        [Tooltip("At most this many ticks per rendered frame; the rest is dropped.")] [AppliesFrom(0, "read when the run starts")] public int maxStepsPerFrame = 5;
        [Tooltip("V5 (demo baseline, 30 Sep 2026): the ship's top speed and acceleration, times this, on top of its move-speed stat. V3/V4: 1.")]
        [AppliesFrom(0, "read when the run starts")] [Min(0.1f)] public float shipSpeedMultiplier = 1f;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(tickRate), tickRate);
            report.Positive(this, nameof(shipSpeedMultiplier), shipSpeedMultiplier);
            if (maxStepsPerFrame < 1) report.Error(this, nameof(maxStepsPerFrame), "must be at least 1");
        }
    }
}
