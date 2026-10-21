using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Shared priority between the feel drivers: hits on the ship (damage, island bumps) mark themselves here, and the
    /// cannon-fire feel stays quiet for a short window afterwards, so constant auto-fire never hides a hit.
    /// </summary>
    public static class FeelPriority
    {
        static float _lastHit = -100f;

        /// <summary>Call when the ship is hit (damage or an obstacle bump).</summary>
        public static void MarkHit() => _lastHit = Time.unscaledTime;

        /// <summary>True when the ship was hit less than <paramref name="window"/> seconds ago.</summary>
        public static bool HitWithin(float window) => Time.unscaledTime - _lastHit < window;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => _lastHit = -100f; // fast enter-play-mode safe
    }
}
