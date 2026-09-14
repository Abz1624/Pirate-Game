using UnityEngine;

namespace HighSeas.World
{
    /// <summary>
    /// Island prefabs use two mesh colliders: convex ground + non-convex foliage.
    /// Assign explicitly or leave empty to auto-detect by the Convex flag.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IslandCollisionAuthoring : MonoBehaviour
    {
        [Tooltip("Convex mesh collider for island shoreline/ground blocking.")]
        public MeshCollider groundCollider;

        [Tooltip("Non-convex mesh collider for trees/foliage blocking.")]
        public MeshCollider foliageCollider;
    }
}
