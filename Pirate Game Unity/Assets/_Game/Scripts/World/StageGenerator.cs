using System.Collections.Generic;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class StageLayout
    {
        public int seed;
        public bool[,] occupancy;
        public Vector3 playerSpawn;
        public Vector3 bossBeacon;
        public List<POISlot> pois = new();
        public List<Vector3> islandCenters = new();
        public List<Vector3> explosiveBarrelPositions = new();
    }

    public struct POISlot
    {
        public POIType type;
        public Vector3 position;
        public bool used;
    }

    public sealed class StageGenerator
    {
        public StageLayout Generate(int seed, int stageIndex, ProceduralIslandSettings islandSettings)
        {
            var settings = islandSettings.Clamped();
            var rng = new RunRng(seed + stageIndex * 991);
            var layout = new StageLayout
            {
                seed = seed,
                occupancy = new bool[GameConstants.GridCells, GameConstants.GridCells]
            };

            var salt = 0;
            while (salt < 20)
            {
                layout.occupancy = new bool[GameConstants.GridCells, GameConstants.GridCells];
                layout.islandCenters.Clear();
                PlaceIslands(layout, rng, settings);
                RasterizeIslandsToGrid(layout, settings.islandDiameterMeters * 0.5f);
                CarveBackbone(layout);
                if (ValidateReachability(layout)) break;
                salt++;
                rng = new RunRng(seed + stageIndex * 991 + salt);
            }

            layout.playerSpawn = CellToWorld(GameConstants.GridCells / 2, 2);
            layout.bossBeacon = FindFarthestOpen(layout, layout.playerSpawn);
            PlacePOIs(layout, rng);
            return layout;
        }

        static void PlaceIslands(StageLayout layout, RunRng rng, ProceduralIslandSettings settings)
        {
            var target = rng.MapInt(settings.countMin, settings.countMax + 1);
            var radius = settings.islandDiameterMeters * 0.5f;
            var margin = radius + 4f;
            var halfMap = GameConstants.MapSizeMeters * 0.5f - margin;
            var spacing = settings.minSeparationMeters;
            var minDistSq = spacing * spacing;

            var candidates = new List<Vector3>(512);
            for (var x = -halfMap; x <= halfMap + 0.01f; x += spacing)
            {
                for (var z = -halfMap; z <= halfMap + 0.01f; z += spacing)
                    candidates.Add(new Vector3(x, 0f, z));
            }

            for (var i = candidates.Count - 1; i > 0; i--)
            {
                var j = rng.MapInt(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }

            var attempts = 0;
            for (var i = 0; i < candidates.Count && layout.islandCenters.Count < target; i++)
            {
                attempts++;
                var candidate = candidates[i];
                if (!IsFarEnough(layout.islandCenters, candidate, minDistSq))
                    continue;

                layout.islandCenters.Add(candidate);
            }

            while (layout.islandCenters.Count < target && attempts < settings.maxPlacementAttempts)
            {
                attempts++;
                var candidate = new Vector3(
                    rng.MapFloat() * 2f * halfMap - halfMap,
                    0f,
                    rng.MapFloat() * 2f * halfMap - halfMap);

                if (!IsFarEnough(layout.islandCenters, candidate, minDistSq))
                    continue;

                layout.islandCenters.Add(candidate);
            }

            if (layout.islandCenters.Count < target)
            {
                Debug.LogWarning(
                    $"StageGenerator: placed {layout.islandCenters.Count}/{target} islands " +
                    $"(sep {spacing:F0}m, dia {settings.islandDiameterMeters:F0}m, attempts {attempts}).");
            }
        }

        static bool IsFarEnough(List<Vector3> placed, Vector3 candidate, float minDistSq)
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

        static void RasterizeIslandsToGrid(StageLayout layout, float islandRadius)
        {
            var radiusSq = islandRadius * islandRadius;
            for (var x = 0; x < GameConstants.GridCells; x++)
            {
                for (var z = 0; z < GameConstants.GridCells; z++)
                {
                    var cellCenter = CellToWorld(x, z);
                    for (var i = 0; i < layout.islandCenters.Count; i++)
                    {
                        var delta = layout.islandCenters[i] - cellCenter;
                        delta.y = 0f;
                        if (delta.sqrMagnitude <= radiusSq)
                        {
                            layout.occupancy[x, z] = true;
                            break;
                        }
                    }
                }
            }
        }

        static void CarveBackbone(StageLayout layout)
        {
            for (var z = 1; z < GameConstants.GridCells - 1; z++)
                layout.occupancy[GameConstants.GridCells / 2, z] = false;
            for (var x = 1; x < GameConstants.GridCells - 1; x++)
                layout.occupancy[x, GameConstants.GridCells / 2] = false;
        }

        static bool ValidateReachability(StageLayout layout)
        {
            var start = new Vector2Int(GameConstants.GridCells / 2, 2);
            var goal = new Vector2Int(GameConstants.GridCells / 2, GameConstants.GridCells - 3);
            if (layout.occupancy[start.x, start.y] || layout.occupancy[goal.x, goal.y]) return false;
            var visited = new bool[GameConstants.GridCells, GameConstants.GridCells];
            var q = new Queue<Vector2Int>();
            q.Enqueue(start);
            visited[start.x, start.y] = true;
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c == goal) return true;
                TryVisit(layout, visited, q, c + Vector2Int.up);
                TryVisit(layout, visited, q, c + Vector2Int.down);
                TryVisit(layout, visited, q, c + Vector2Int.left);
                TryVisit(layout, visited, q, c + Vector2Int.right);
            }
            return false;
        }

        static void TryVisit(StageLayout layout, bool[,] visited, Queue<Vector2Int> q, Vector2Int c)
        {
            if (c.x < 0 || c.y < 0 || c.x >= GameConstants.GridCells || c.y >= GameConstants.GridCells) return;
            if (visited[c.x, c.y] || layout.occupancy[c.x, c.y]) return;
            visited[c.x, c.y] = true;
            q.Enqueue(c);
        }

        public static Vector3 CellToWorld(int x, int z)
        {
            var halfMap = GameConstants.MapSizeMeters * 0.5f;
            var cell = GameConstants.CellSizeMeters;
            return new Vector3(
                -halfMap + x * cell + cell * 0.5f,
                0f,
                -halfMap + z * cell + cell * 0.5f);
        }

        static Vector3 FindFarthestOpen(StageLayout layout, Vector3 from)
        {
            var best = from;
            var bestDist = 0f;
            for (var x = 1; x < GameConstants.GridCells - 1; x++)
            {
                for (var z = 1; z < GameConstants.GridCells - 1; z++)
                {
                    if (layout.occupancy[x, z]) continue;
                    var w = CellToWorld(x, z);
                    var d = Vector3.Distance(from, w);
                    if (d > bestDist) { bestDist = d; best = w; }
                }
            }
            return best;
        }

        static void PlacePOIs(StageLayout layout, RunRng rng)
        {
            var types = new[] { POIType.SalvageChest, POIType.LighthouseOfFortune, POIType.TrialBuoy, POIType.BossBeacon };
            var placed = 0;
            for (var attempt = 0; attempt < 200 && placed < types.Length; attempt++)
            {
                var x = rng.MapInt(1, GameConstants.GridCells - 1);
                var z = rng.MapInt(1, GameConstants.GridCells - 1);
                if (layout.occupancy[x, z]) continue;
                var pos = CellToWorld(x, z);
                if (Vector3.Distance(pos, layout.playerSpawn) < 18f) continue;
                var tooClose = false;
                for (var i = 0; i < layout.pois.Count; i++)
                    if (Vector3.Distance(pos, layout.pois[i].position) < 18f) tooClose = true;
                if (tooClose) continue;
                layout.pois.Add(new POISlot { type = types[placed], position = pos });
                placed++;
            }
            layout.pois.Add(new POISlot { type = POIType.BossBeacon, position = layout.bossBeacon });
        }
    }
}
