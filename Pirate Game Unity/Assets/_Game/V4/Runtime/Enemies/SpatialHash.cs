using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>Uniform grid of enemy ids for neighbour queries (M15: moved out of V3's EnemyWorld, unchanged).</summary>
    public sealed class SpatialHash
    {
        readonly Dictionary<long, List<int>> _cells = new();
        readonly float _cellSize;
        readonly List<int> _queryBuffer = new(64);

        public SpatialHash(float cellSize = 4f)
        {
            _cellSize = cellSize;
        }

        static long Key(int x, int z) => ((long)x << 32) | (uint)z;

        public void Clear() => _cells.Clear();

        public void Insert(int enemyId, Vector3 pos)
        {
            var key = Key(Mathf.FloorToInt(pos.x / _cellSize), Mathf.FloorToInt(pos.z / _cellSize));
            if (!_cells.TryGetValue(key, out var list))
            {
                list = new List<int>(8);
                _cells[key] = list;
            }
            list.Add(enemyId);
        }

        public List<int> Query(Vector3 center, float radius, int maxResults = 32)
        {
            _queryBuffer.Clear();
            var rCells = Mathf.CeilToInt(radius / _cellSize);
            var cx = Mathf.FloorToInt(center.x / _cellSize);
            var cz = Mathf.FloorToInt(center.z / _cellSize);
            for (var dx = -rCells; dx <= rCells; dx++)
            {
                for (var dz = -rCells; dz <= rCells; dz++)
                {
                    if (!_cells.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
                    for (var i = 0; i < list.Count; i++)
                    {
                        if (_queryBuffer.Count >= maxResults) return _queryBuffer;
                        _queryBuffer.Add(list[i]);
                    }
                }
            }
            return _queryBuffer;
        }
    }
}
