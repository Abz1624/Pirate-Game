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
    }

    public struct POISlot
    {
        public POIType type;
        public Vector3 position;
        public bool used;
    }

    public sealed class StageGenerator
    {
        public StageLayout Generate(int seed, int stageIndex)
        {
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
                PlaceIslands(layout, rng);
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

        static void PlaceIslands(StageLayout layout, RunRng rng)
        {
            var targetCells = rng.MapInt(30, 45);
            var placed = 0;
            while (placed < targetCells)
            {
                var x = rng.MapInt(1, GameConstants.GridCells - 1);
                var z = rng.MapInt(1, GameConstants.GridCells - 1);
                if (layout.occupancy[x, z]) continue;
                layout.occupancy[x, z] = true;
                placed++;
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

        static Vector3 CellToWorld(int x, int z)
        {
            var origin = -Vector3.one * (GameConstants.MapSizeMeters * 0.5f);
            return origin + new Vector3(x * GameConstants.CellSizeMeters + GameConstants.CellSizeMeters * 0.5f, 0f,
                z * GameConstants.CellSizeMeters + GameConstants.CellSizeMeters * 0.5f);
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

    public sealed class StageBuilder : MonoBehaviour
    {
        [SerializeField] Transform islandRoot;
        [SerializeField] Material islandMaterial;

        public StageLayout Build(int seed, int stageIndex)
        {
            var gen = new StageGenerator();
            var layout = gen.Generate(seed, stageIndex);
            if (islandRoot != null)
            {
                for (var i = islandRoot.childCount - 1; i >= 0; i--)
                    Destroy(islandRoot.GetChild(i).gameObject);
            }
            for (var x = 0; x < GameConstants.GridCells; x++)
            {
                for (var z = 0; z < GameConstants.GridCells; z++)
                {
                    if (!layout.occupancy[x, z]) continue;
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = $"Island_{x}_{z}";
                    go.transform.SetParent(islandRoot != null ? islandRoot : transform, false);
                    var origin = -Vector3.one * (GameConstants.MapSizeMeters * 0.5f);
                    go.transform.position = origin + new Vector3(x * GameConstants.CellSizeMeters, 0f, z * GameConstants.CellSizeMeters);
                    go.transform.localScale = new Vector3(GameConstants.CellSizeMeters * 0.9f, 2f, GameConstants.CellSizeMeters * 0.9f);
                    var islandLayer = LayerMask.NameToLayer(IslandObstacles.LayerName);
                    go.layer = islandLayer >= 0 ? islandLayer : LayerMask.NameToLayer("Default");
                    if (islandMaterial != null) go.GetComponent<Renderer>().material = islandMaterial;
                }
            }
            return layout;
        }
    }
}
