using System.Collections.Generic;
using UnityEngine;

namespace HighSeas.World
{
    /// <summary>Runtime island exclusion zones used to guarantee open-water player spawns.</summary>
    public static class IslandSpawnExclusionRegistry
    {
        struct Zone
        {
            public Vector3 Center;
            public float Radius;
        }

        static readonly List<Zone> Zones = new();

        public static int Count => Zones.Count;

        public static void Clear() => Zones.Clear();

        public static void Register(GameObject islandRoot, float extraMargin = 4f)
        {
            if (islandRoot == null) return;

            var hasBounds = false;
            var bounds = new Bounds(islandRoot.transform.position, Vector3.zero);

            var renderers = islandRoot.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                if (!hasBounds)
                {
                    bounds = renderers[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
            }

            var colliders = islandRoot.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].enabled) continue;
                if (!hasBounds)
                {
                    bounds = colliders[i].bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(colliders[i].bounds);
                }
            }

            if (!hasBounds) return;

            var radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + extraMargin;
            Zones.Add(new Zone { Center = bounds.center, Radius = radius });
        }

        public static void RegisterPredicted(Vector3 center, float radius)
        {
            Zones.Add(new Zone { Center = center, Radius = radius });
        }

        public static bool IsClear(Vector3 position, float clearanceRadius)
        {
            for (var i = 0; i < Zones.Count; i++)
            {
                var zone = Zones[i];
                var dx = position.x - zone.Center.x;
                var dz = position.z - zone.Center.z;
                var dist = Mathf.Sqrt(dx * dx + dz * dz);
                if (dist < zone.Radius + clearanceRadius)
                    return false;
            }

            return true;
        }

        public static bool TryGetExtents(out float minX, out float maxX, out float minZ, out float maxZ)
        {
            minX = minZ = float.MaxValue;
            maxX = maxZ = float.MinValue;
            if (Zones.Count == 0)
                return false;

            for (var i = 0; i < Zones.Count; i++)
            {
                var zone = Zones[i];
                minX = Mathf.Min(minX, zone.Center.x - zone.Radius);
                maxX = Mathf.Max(maxX, zone.Center.x + zone.Radius);
                minZ = Mathf.Min(minZ, zone.Center.z - zone.Radius);
                maxZ = Mathf.Max(maxZ, zone.Center.z + zone.Radius);
            }

            return minX < maxX && minZ < maxZ;
        }
    }
}
