using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.World
{
    public static class IslandLayoutSpawner
    {
        const float V3DefaultClusterJitter = 14f;
        const float V3DefaultPositionJitter = 8f;
        const float V3DefaultRotationJitter = 18f;

        public static int Spawn(IslandLayoutDefinition layout, Transform islandRoot, int seed, bool useV3SeedVariation = false)
        {
            if (layout == null || !layout.HasIslands || islandRoot == null)
                return 0;

            var rng = new RunRng(seed);
            var count = 0;

            for (var c = 0; c < layout.clusters.Length; c++)
            {
                var cluster = layout.clusters[c];
                if (cluster.islands == null || cluster.islands.Length == 0) continue;

                var clusterRadius = ResolveClusterJitter(cluster.clusterJitterRadius, useV3SeedVariation);
                var clusterJitter = RandomInCircle(rng, clusterRadius);
                var clusterOrigin = cluster.anchorPosition + new Vector3(clusterJitter.x, 0f, clusterJitter.y);

                for (var i = 0; i < cluster.islands.Length; i++)
                {
                    var entry = cluster.islands[i];
                    if (entry.prefab == null) continue;

                    var positionRadius = ResolvePositionJitter(entry.positionJitter, useV3SeedVariation);
                    var localJitter = RandomInCircle(rng, positionRadius);
                    var position = clusterOrigin + entry.offsetFromAnchor + new Vector3(localJitter.x, 0f, localJitter.y);
                    var rotationJitter = ResolveRotationJitter(entry.rotationJitter, useV3SeedVariation);
                    var yaw = entry.yRotation + (rng.MapFloat() * 2f - 1f) * rotationJitter;
                    var rotation = Quaternion.Euler(0f, yaw, 0f);

                    var instance = Object.Instantiate(entry.prefab, position, rotation, islandRoot);
                    instance.name = entry.prefab.name;
                    instance.transform.localScale = ResolveScale(entry);
                    IslandColliderUtility.ConfigureIslandCollision(instance);
                    count++;
                }
            }

            Physics.SyncTransforms();
            return count;
        }

        public static void RegisterPredictedZones(IslandLayoutDefinition layout, int seed, bool useV3SeedVariation = false)
        {
            if (layout == null || !layout.HasIslands) return;

            var rng = new RunRng(seed);
            for (var c = 0; c < layout.clusters.Length; c++)
            {
                var cluster = layout.clusters[c];
                if (cluster.islands == null) continue;

                var clusterRadius = ResolveClusterJitter(cluster.clusterJitterRadius, useV3SeedVariation);
                var clusterJitter = RandomInCircle(rng, clusterRadius);
                var clusterOrigin = cluster.anchorPosition + new Vector3(clusterJitter.x, 0f, clusterJitter.y);

                for (var i = 0; i < cluster.islands.Length; i++)
                {
                    var entry = cluster.islands[i];
                    var positionRadius = ResolvePositionJitter(entry.positionJitter, useV3SeedVariation);
                    var localJitter = RandomInCircle(rng, positionRadius);
                    var position = clusterOrigin + entry.offsetFromAnchor + new Vector3(localJitter.x, 0f, localJitter.y);
                    var footprint = entry.targetFootprintMeters > 0f ? entry.targetFootprintMeters : 20f;
                    var scale = entry.scale.x > 0f ? Mathf.Max(entry.scale.x, entry.scale.z) : 1f;
                    var radius = footprint * 1.5f * scale + positionRadius + clusterRadius + 12f;
                    IslandSpawnExclusionRegistry.RegisterPredicted(position, radius);
                }
            }
        }

        static float ResolveClusterJitter(float configured, bool useV3SeedVariation) =>
            configured > 0f ? configured : useV3SeedVariation ? V3DefaultClusterJitter : 0f;

        static float ResolvePositionJitter(float configured, bool useV3SeedVariation) =>
            configured > 0f ? configured : useV3SeedVariation ? V3DefaultPositionJitter : 0f;

        static float ResolveRotationJitter(float configured, bool useV3SeedVariation) =>
            configured > 0f ? configured : useV3SeedVariation ? V3DefaultRotationJitter : 0f;

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
