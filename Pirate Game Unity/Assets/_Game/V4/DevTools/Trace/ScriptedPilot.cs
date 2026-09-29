using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Deterministic steering for trace runs: full throttle and a slow wandering turn driven only by run time,
    /// so the same seed produces the same path in V3 and V4. Injected through PlayerShipController.ConfigureBotControl.
    /// </summary>
    public sealed class ScriptedPilot : IShipSteerInput
    {
        public const string PatternId = "wander-v1";

        public Vector2 ReadSteerThrottle()
        {
            var t = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            return Sample(t);
        }

        public static Vector2 Sample(float t)
        {
            var steer = 0.55f * Mathf.Sin(t * 0.4f) + 0.2f * Mathf.Sin(t * 0.13f + 1f);
            return new Vector2(Mathf.Clamp(steer, -1f, 1f), 1f);
        }
    }
}
