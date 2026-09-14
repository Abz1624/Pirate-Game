using UnityEngine;

namespace HighSeas.World
{
    public static class IslandColliderUtility
    {
        public static void ConfigureIslandCollision(GameObject islandRoot, float fallbackRadius = 6f, float fallbackHeight = 3f)
        {
            if (islandRoot == null) return;

            DisableCapsuleColliders(islandRoot);
            ConfigureMeshColliders(islandRoot);
            RegisterExclusion(islandRoot);
        }

        static void ConfigureMeshColliders(GameObject islandRoot)
        {
            var authoring = islandRoot.GetComponent<IslandCollisionAuthoring>();
            var ground = authoring != null ? authoring.groundCollider : null;
            var foliage = authoring != null ? authoring.foliageCollider : null;
            if (ground == null || foliage == null)
                AutoDetectMeshColliders(islandRoot, ref ground, ref foliage);

            var hasCollision = false;
            if (EnableMeshCollider(ground, requireConvex: true))
                hasCollision = true;
            if (EnableMeshCollider(foliage, requireConvex: false))
                hasCollision = true;

            DisableExtraMeshColliders(islandRoot, ground, foliage);

            if (!hasCollision)
            {
                Debug.LogWarning(
                    $"Island '{islandRoot.name}' has no enabled mesh colliders. " +
                    "Expected a convex ground mesh and a non-convex foliage mesh on the prefab.",
                    islandRoot);
            }
        }

        static void AutoDetectMeshColliders(GameObject islandRoot, ref MeshCollider ground, ref MeshCollider foliage)
        {
            var meshColliders = islandRoot.GetComponentsInChildren<MeshCollider>(true);
            for (var i = 0; i < meshColliders.Length; i++)
            {
                var meshCollider = meshColliders[i];
                if (meshCollider == null)
                    continue;

                if (meshCollider.convex && ground == null)
                    ground = meshCollider;
                else if (!meshCollider.convex && foliage == null)
                    foliage = meshCollider;
            }
        }

        static bool EnableMeshCollider(MeshCollider meshCollider, bool requireConvex)
        {
            if (meshCollider == null)
                return false;

            meshCollider.enabled = true;
            if (requireConvex)
                meshCollider.convex = true;

            ApplyIslandLayer(meshCollider.gameObject);
            return true;
        }

        static void DisableExtraMeshColliders(GameObject islandRoot, MeshCollider ground, MeshCollider foliage)
        {
            var meshColliders = islandRoot.GetComponentsInChildren<MeshCollider>(true);
            for (var i = 0; i < meshColliders.Length; i++)
            {
                var meshCollider = meshColliders[i];
                if (meshCollider == null || meshCollider == ground || meshCollider == foliage)
                    continue;
                meshCollider.enabled = false;
            }
        }

        static void DisableCapsuleColliders(GameObject islandRoot)
        {
            if (islandRoot == null) return;

            var colliders = islandRoot.GetComponentsInChildren<CapsuleCollider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        public static void DisableNonCapsuleColliders(GameObject islandRoot)
        {
            DisableCapsuleColliders(islandRoot);
        }

        static void RegisterExclusion(GameObject islandRoot)
        {
            Physics.SyncTransforms();
            IslandSpawnExclusionRegistry.Register(islandRoot);
        }

        public static void DisableChildColliders(GameObject root)
        {
            if (root == null) return;

            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i].gameObject == root) continue;
                colliders[i].enabled = false;
            }
        }

        public static bool TryGetGroundBounds(GameObject boundsRoot, out Bounds bounds)
        {
            bounds = default;
            var hasBounds = false;

            var renderers = boundsRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;
                if (IsDecorationRenderer(renderer)) continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        public static bool IsDecorationRenderer(Renderer renderer)
        {
            var name = renderer.gameObject.name;
            if (name == "Ground" || name == "FallbackMesh") return false;

            var lower = name.ToLowerInvariant();
            return lower.Contains("tree") ||
                   lower.Contains("palm") ||
                   lower.Contains("rock") ||
                   lower.Contains("bush") ||
                   lower.Contains("deco") ||
                   lower.Contains("prop");
        }

        static void ApplyIslandLayer(GameObject target)
        {
            var layer = IslandObstacles.Layer;
            if (layer >= 0)
                target.layer = layer;
        }
    }
}
