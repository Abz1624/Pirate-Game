using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    /// <summary>
    /// Marks a hand-authored demo island for baking into IslandLayoutDefinition.
    /// </summary>
    public sealed class DemoIslandMarker : MonoBehaviour
    {
        [Tooltip("Islands with the same cluster id move together at runtime.")]
        public int clusterId;

        [Tooltip("Per-island position jitter radius (meters) applied at runtime.")]
        public float positionJitter = 2.5f;

        [Tooltip("Per-island yaw jitter (degrees) applied at runtime.")]
        public float rotationJitter = 12f;

        [Tooltip("Capsule radius used when this island was authored (editor reference).")]
        public float capsuleRadius = 7f;
    }
}
