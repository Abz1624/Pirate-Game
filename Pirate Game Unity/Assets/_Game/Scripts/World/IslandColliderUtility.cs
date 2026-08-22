using UnityEngine;

namespace HighSeas.World
{
    public static class IslandColliderUtility
    {
        public static void ConfigureIslandCollision(GameObject islandRoot, float fallbackRadius = 6f, float fallbackHeight = 3f)
        {
            if (islandRoot == null) return;

            RemoveCollider<CapsuleCollider>(islandRoot);

            var ground = FindGround(islandRoot.transform);
            if (ground != null && SetupGroundMeshCollider(ground.gameObject))
            {
                DisableCollidersExcept(islandRoot, ground.gameObject);
                return;
            }

            SetupCapsuleCollider(islandRoot, fallbackRadius, fallbackHeight);
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

        static bool SetupGroundMeshCollider(GameObject ground)
        {
            var meshFilter = ground.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return false;

            var meshCollider = ground.GetComponent<MeshCollider>();
            if (meshCollider == null)
                meshCollider = ground.AddComponent<MeshCollider>();

            if (meshCollider.sharedMesh != meshFilter.sharedMesh)
                meshCollider.sharedMesh = meshFilter.sharedMesh;

            meshCollider.convex = false;
            meshCollider.enabled = true;
            ApplyIslandLayer(ground);
            return true;
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
