using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.World
{
    /// <summary>Places Island_Tropical_* prefabs on an even grid across the playable map.</summary>
    public static class TropicalIslandSpawner
    {
        const float RotationJitter = 22f;

        public static int SpawnEvenlyAcrossMap(
            Transform islandRoot,
            int seed,
            int targetCount,
            GameObject[] prefabs,
            ProceduralIslandSettings settings)
        {
            if (islandRoot == null || targetCount <= 0 || prefabs == null || prefabs.Length == 0)
                return 0;

            var resolved = settings.Clamped();
            var rng = new RunRng(seed);
            var radius = resolved.islandDiameterMeters * 0.5f;
            var margin = radius + 4f;
            var halfExtent = GameConstants.MapSizeMeters * 0.5f - margin;
            var minDistSq = resolved.minSeparationMeters * resolved.minSeparationMeters;
            var footprint = resolved.islandDiameterMeters > 0f ? resolved.islandDiameterMeters : 18f;
            var clearance = radius + 3f;

            var candidates = BuildGridCandidates(targetCount, halfExtent, rng);
            var placed = new List<Vector3>(targetCount);
            var spawned = 0;

            for (var i = 0; i < candidates.Count && spawned < targetCount; i++)
            {
                if (!TryAccept(candidates[i], placed, minDistSq, clearance, out var position))
                    continue;

                var prefab = prefabs[rng.MapInt(0, prefabs.Length)];
                SpawnInstance(islandRoot, prefab, position, seed, spawned, footprint, rng);
                placed.Add(position);
                spawned++;
            }

            if (spawned < targetCount)
                spawned += FillRemainingRandom(
                    islandRoot,
                    seed,
                    targetCount - spawned,
                    spawned,
                    prefabs,
                    placed,
                    resolved,
                    halfExtent,
                    clearance,
                    footprint,
                    rng);

            Physics.SyncTransforms();
            return spawned;
        }

        static List<Vector3> BuildGridCandidates(int targetCount, float halfExtent, RunRng rng)
        {
            var cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(targetCount * 1.12f)));
            var rows = Mathf.Max(1, Mathf.CeilToInt(targetCount / (float)cols));
            var cellW = (2f * halfExtent) / cols;
            var cellH = (2f * halfExtent) / rows;

            var candidates = new List<Vector3>(targetCount);
            for (var r = 0; r < rows && candidates.Count < targetCount; r++)
            {
                for (var c = 0; c < cols && candidates.Count < targetCount; c++)
                {
                    var cx = -halfExtent + (c + 0.5f) * cellW;
                    var cz = -halfExtent + (r + 0.5f) * cellH;
                    var jitterX = (rng.MapFloat() - 0.5f) * cellW * 0.5f;
                    var jitterZ = (rng.MapFloat() - 0.5f) * cellH * 0.5f;
                    candidates.Add(new Vector3(cx + jitterX, 0f, cz + jitterZ));
                }
            }

            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = rng.MapInt(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            return candidates;
        }

        static int FillRemainingRandom(
            Transform islandRoot,
            int seed,
            int remaining,
            int indexOffset,
            GameObject[] prefabs,
            List<Vector3> placed,
            ProceduralIslandSettings settings,
            float halfExtent,
            float clearance,
            float footprint,
            RunRng rng)
        {
            var minDistSq = settings.minSeparationMeters * settings.minSeparationMeters;
            var attemptsPerIsland = Mathf.Max(160, settings.maxPlacementAttempts / Mathf.Max(1, remaining));
            var spawned = 0;

            for (var n = 0; n < remaining; n++)
            {
                for (var attempt = 0; attempt < attemptsPerIsland; attempt++)
                {
                    var pos = new Vector3(
                        (rng.MapFloat() * 2f - 1f) * halfExtent,
                        0f,
                        (rng.MapFloat() * 2f - 1f) * halfExtent);

                    if (!TryAccept(pos, placed, minDistSq, clearance, out pos))
                        continue;

                    var prefab = prefabs[rng.MapInt(0, prefabs.Length)];
                    SpawnInstance(islandRoot, prefab, pos, seed, indexOffset + spawned, footprint, rng);
                    placed.Add(pos);
                    spawned++;
                    break;
                }
            }

            return spawned;
        }

        static bool TryAccept(
            Vector3 candidate,
            List<Vector3> placed,
            float minDistSq,
            float clearance,
            out Vector3 position)
        {
            position = candidate;
            if (!IsFarEnough(candidate, placed, minDistSq))
                return false;

            if (IslandSpawnExclusionRegistry.Count > 0 &&
                !IslandSpawnExclusionRegistry.IsClear(candidate, clearance))
                return false;

            return true;
        }

        public static void SpawnAt(
            Transform islandRoot,
            GameObject prefab,
            Vector3 position,
            int seed,
            int index,
            float targetFootprintMeters,
            RunRng rng)
        {
            SpawnInstance(islandRoot, prefab, position, seed, index, targetFootprintMeters, rng);
        }

        static void SpawnInstance(
            Transform islandRoot,
            GameObject prefab,
            Vector3 position,
            int seed,
            int index,
            float targetFootprintMeters,
            RunRng rng)
        {
            var yaw = rng.MapFloat() * 360f;
            var rotation = Quaternion.Euler(0f, yaw + (rng.MapFloat() * 2f - 1f) * RotationJitter, 0f);
            var instance = Object.Instantiate(prefab, position, rotation, islandRoot);
            instance.name = prefab.name;

            var uniform = IslandVisualUtility.ComputeGroundUniformScale(prefab, targetFootprintMeters, 1f);
            instance.transform.localScale = Vector3.one * uniform;
            IslandColliderUtility.ConfigureIslandCollision(instance);
        }

        static bool IsFarEnough(Vector3 candidate, List<Vector3> placed, float minDistSq)
        {
            for (var i = 0; i < placed.Count; i++)
            {
                var delta = candidate - placed[i];
                delta.y = 0f;
                if (delta.sqrMagnitude < minDistSq)
                    return false;
            }

            return true;
        }
    }
}
