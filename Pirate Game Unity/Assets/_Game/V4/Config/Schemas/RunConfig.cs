using UnityEngine;

namespace RoguePirates.V4.Config
{

    /// <summary>Run-level settings for the V4 composition root.</summary>
    [CreateAssetMenu(menuName = "RoguePirates/V4/Run Config", fileName = "RunConfig")]
    public sealed class RunConfig : V4ConfigAsset
    {
        [Tooltip("V4 module ticks per second (fixed step).")] [AppliesFrom(0, "read when the run starts")] public float tickRate = 60f;
        [Tooltip("At most this many ticks per rendered frame; the rest is dropped.")] [AppliesFrom(0, "read when the run starts")] public int maxStepsPerFrame = 5;

        public override void Validate(ConfigValidationReport report)
        {
            report.Positive(this, nameof(tickRate), tickRate);
            if (maxStepsPerFrame < 1) report.Error(this, nameof(maxStepsPerFrame), "must be at least 1");
        }
    }
}
