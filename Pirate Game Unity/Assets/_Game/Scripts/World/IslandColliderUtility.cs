using UnityEngine;

namespace HighSeas.World
{
    public static class IslandColliderUtility
    {
        const float CollisionShrink = 0.64f;

        public static void ConfigureIslandCollision(GameObject islandRoot, float fallbackRadius = 6f, float fallbackHeight = 3f)
        {
            if (islandRoot == null) return;

            RemoveCollider<CapsuleCollider>(islandRoot);

            var ground = FindGround(islandRoot.transform);
            if (ground != null && TrySetupTightCollider(ground.gameObject, ground.gameObject))
            {
                DisableCollidersExcept(islandRoot, ground.gameObject);
                RegisterExclusion(islandRoot);
                return;
            }

            if (TrySetupTightCollider(islandRoot, islandRoot))
            {
                RegisterExclusion(islandRoot);
                return;
            }

            SetupCapsuleCollider(islandRoot, fallbackRadius * CollisionShrink, fallbackHeight);
            RegisterExclusion(islandRoot);
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

        static bool TrySetupTightCollider(GameObject colliderHost, GameObject boundsRoot)
        {
            if (!TryGetGroundBounds(boundsRoot, out var bounds))
                return false;

            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z) * CollisionShrink;
            if (radius < 0.5f) return false;

            RemoveCollider<MeshCollider>(colliderHost);
            var collider = colliderHost.GetComponent<CapsuleCollider>();
            if (collider == null)
                collider = colliderHost.AddComponent<CapsuleCollider>();

            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, bounds.size.y * CollisionShrink);
            collider.direction = 1;
            collider.center = colliderHost.transform.InverseTransformPoint(bounds.center);
            collider.enabled = true;
            ApplyIslandLayer(colliderHost);
            return true;
        }

        static CapsuleCollider SetupCapsuleCollider(GameObject islandRoot, float radius, float height)
        {
            var collider = islandRoot.GetComponent<CapsuleCollider>();
            if (collider == null)
                collider = islandRoot.AddComponent<CapsuleCollider>();

            collider.radius = radius;
            collider.height = height;
            collider.direction = 1;
            collider.center = new Vector3(0f, height * 0.5f, 0f);
            collider.enabled = true;

            ApplyIslandLayer(islandRoot);
            return collider;
        }

        static bool TryGetGroundBounds(GameObject boundsRoot, out Bounds bounds)
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

        static bool IsDecorationRenderer(Renderer renderer)
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

        static Transform FindGround(Transform islandRoot)
        {
            for (var i = 0; i < islandRoot.childCount; i++)
            {
                var child = islandRoot.GetChild(i);
                if (child.name == "Ground" || child.name == "FallbackMesh")
                    return child;
            }

            return null;
        }

        static void DisableCollidersExcept(GameObject islandRoot, GameObject keepColliderOn)
        {
            var colliders = islandRoot.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var owner = colliders[i].gameObject;
                colliders[i].enabled = owner == keepColliderOn;
            }
        }

        static void RemoveCollider<T>(GameObject target) where T : Collider
        {
            var collider = target.GetComponent<T>();
            if (collider == null) return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(collider);
            else
#endif
                Object.Destroy(collider);
        }

        static void ApplyIslandLayer(GameObject target)
        {
            var layer = IslandObstacles.Layer;
            if (layer >= 0)
                target.layer = layer;
        }
    }
}
