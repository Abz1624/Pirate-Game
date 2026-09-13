using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.World
{
    public static class IslandLayoutSpawner
    {
        public static int Spawn(IslandLayoutDefinition layout, Transform islandRoot, int seed)
        {
            if (layout == null || !layout.HasIslands || islandRoot == null)
                return 0;

            var rng = new RunRng(seed);
            var count = 0;

            for (var c = 0; c < layout.clusters.Length; c++)
            {
                var cluster = layout.clusters[c];
                if (cluster.islands == null || cluster.islands.Length == 0) continue;

                var clusterJitter = RandomInCircle(rng, cluster.clusterJitterRadius);
                var clusterOrigin = cluster.anchorPosition + new Vector3(clusterJitter.x, 0f, clusterJitter.y);

                for (var i = 0; i < cluster.islands.Length; i++)
                {
                    var entry = cluster.islands[i];
                    if (entry.prefab == null) continue;

                    var localJitter = RandomInCircle(rng, entry.positionJitter);
                    var position = clusterOrigin + entry.offsetFromAnchor + new Vector3(localJitter.x, 0f, localJitter.y);
                    var yaw = entry.yRotation + (rng.MapFloat() * 2f - 1f) * entry.rotationJitter;
                    var rotation = Quaternion.Euler(0f, yaw, 0f);

                    var instance = Object.Instantiate(entry.prefab, position, rotation, islandRoot);
                    instance.name = entry.prefab.name;
                    instance.transform.localScale = ResolveScale(entry);
                    IslandColliderUtility.ConfigureIslandCollision(instance);
                    count++;
                }
            }

            return count;
        }

        static Vector2 RandomInCircle(RunRng rng, float radius)
        {
            if (radius <= 0f) return Vector2.zero;
            var angle = rng.MapFloat() * Mathf.PI * 2f;
            var dist = Mathf.Sqrt(rng.MapFloat()) * radius;
            return new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
        }

        static Vector3 ResolveScale(IslandPrefabEntry entry)
        {
            if (entry == null) return Vector3.one;
            if (!entry.autoScaleToFootprint || entry.prefab == null)
                return entry.scale;

            var uniform = IslandVisualUtility.ComputeGroundUniformScale(
                entry.prefab,
                entry.targetFootprintMeters > 0f ? entry.targetFootprintMeters : 20f,
                1f);
            return entry.scale * uniform;
        }
    }
}
