using System.Collections.Generic;
using UnityEngine;

namespace HighSeas.World
{
    /// <summary>
    /// Fits multiple capsule colliders over irregular island ground meshes.
    /// </summary>
    public static class IslandCompoundColliderBuilder
    {
        public const string CollisionRootName = "Collision";
        public const float DefaultShrink = 0.58f;
        const float MinCellSize = 3.5f;
        const float MaxCellSize = 8f;
        const int MaxCapsules = 24;

        public static int Build(GameObject islandRoot, float shrink = DefaultShrink)
        {
            if (islandRoot == null)
                return 0;

            IslandColliderUtility.DisableNonCapsuleColliders(islandRoot);
            var collisionRoot = EnsureCollisionRoot(islandRoot.transform);
            ClearGeneratedCapsules(collisionRoot);

            if (!IslandColliderUtility.TryGetGroundBounds(islandRoot, out var bounds))
                return 0;

            var groundRenderers = GetGroundRenderers(islandRoot);
            if (groundRenderers.Length == 0)
                return 0;

            var footprint = Mathf.Max(bounds.size.x, bounds.size.z);
            var cellSize = ResolveCellSize(footprint);
            var cols = Mathf.Clamp(Mathf.CeilToInt(bounds.size.x / cellSize), 1, 6);
            var rows = Mathf.Clamp(Mathf.CeilToInt(bounds.size.z / cellSize), 1, 6);
            var stepX = bounds.size.x / cols;
            var stepZ = bounds.size.z / rows;
            var radius = Mathf.Clamp(Mathf.Min(stepX, stepZ) * 0.5f * shrink, 1.1f, 9f);
            var height = Mathf.Max(radius * 2f, bounds.size.y * shrink);
            var minX = bounds.min.x;
            var minZ = bounds.min.z;

            var count = 0;
            for (var iz = 0; iz < rows && count < MaxCapsules; iz++)
            {
                for (var ix = 0; ix < cols && count < MaxCapsules; ix++)
                {
                    var centerX = minX + (ix + 0.5f) * stepX;
                    var centerZ = minZ + (iz + 0.5f) * stepZ;
                    if (!CellOverlapsGround(centerX, centerZ, stepX, stepZ, groundRenderers))
                        continue;

                    var center = new Vector3(centerX, bounds.min.y + height * 0.5f, centerZ);
                    CreateCapsule(collisionRoot, islandRoot.transform, center, radius, height, count);
                    count++;
                }
            }

            if (count == 0)
            {
                var fallbackRadius = Mathf.Max(bounds.extents.x, bounds.extents.z) * shrink;
                var fallbackCenter = new Vector3(bounds.center.x, bounds.min.y + height * 0.5f, bounds.center.z);
                CreateCapsule(collisionRoot, islandRoot.transform, fallbackCenter, fallbackRadius, height, 0);
                count = 1;
            }

            return count;
        }

        static float ResolveCellSize(float footprint)
        {
            if (footprint <= 10f)
                return Mathf.Clamp(footprint * 0.55f, MinCellSize, MaxCellSize);
            return Mathf.Clamp(footprint / 4f, MinCellSize, MaxCellSize);
        }

        static bool CellOverlapsGround(float centerX, float centerZ, float stepX, float stepZ, Renderer[] groundRenderers)
        {
            var hits = 0;
            var offsets = new[]
            {
                Vector2.zero,
                new Vector2(-0.35f, -0.35f),
                new Vector2(0.35f, -0.35f),
                new Vector2(-0.35f, 0.35f),
                new Vector2(0.35f, 0.35f)
            };

            for (var i = 0; i < offsets.Length; i++)
            {
                var x = centerX + offsets[i].x * stepX;
                var z = centerZ + offsets[i].y * stepZ;
                if (IsPointOverGround(x, z, groundRenderers))
                    hits++;
            }

            return hits >= 2;
        }

        static bool IsPointOverGround(float x, float z, Renderer[] groundRenderers)
        {
            for (var i = 0; i < groundRenderers.Length; i++)
            {
                var bounds = groundRenderers[i].bounds;
                if (x >= bounds.min.x && x <= bounds.max.x &&
                    z >= bounds.min.z && z <= bounds.max.z)
                {
                    return true;
                }
            }

            return false;
        }

        static Renderer[] GetGroundRenderers(GameObject islandRoot)
        {
            var list = new List<Renderer>(8);
            var renderers = islandRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || IslandColliderUtility.IsDecorationRenderer(renderer))
                    continue;
                list.Add(renderer);
            }

            return list.ToArray();
        }

        static Transform EnsureCollisionRoot(Transform islandRoot)
        {
            var existing = islandRoot.Find(CollisionRootName);
            if (existing != null)
                return existing;

            var go = new GameObject(CollisionRootName);
            go.transform.SetParent(islandRoot, false);
            return go.transform;
        }

        static void ClearGeneratedCapsules(Transform collisionRoot)
        {
            for (var i = collisionRoot.childCount - 1; i >= 0; i--)
            {
                var child = collisionRoot.GetChild(i);
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(child.gameObject);
                else
#endif
                    Object.Destroy(child.gameObject);
            }
        }

        static void CreateCapsule(
            Transform collisionRoot,
            Transform islandRoot,
            Vector3 worldCenter,
            float radius,
            float height,
            int index)
        {
            var capsuleGo = new GameObject($"Cap_{index:00}");
            capsuleGo.transform.SetParent(collisionRoot, false);
            capsuleGo.transform.position = worldCenter;
            capsuleGo.transform.rotation = Quaternion.identity;

            var collider = capsuleGo.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = radius;
            collider.height = height;
            collider.center = Vector3.zero;
            collider.enabled = true;

            var layer = IslandObstacles.Layer;
            if (layer >= 0)
                capsuleGo.layer = layer;
        }
    }
}
