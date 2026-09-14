using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Places 12 salvage chests, 4 special POIs and a boss beacon for v3 conquest layout.</summary>
    public static class V3StagePoiPlacer
    {
        const int SalvageChestCount = 12;
        const float MinSpacing = 16f;
        const float MinRadius = 20f;
        const float MaxRadius = 78f;
        const float Clearance = 2.5f;

        static float PoiEdgeMargin => PlayBounds.StormEdgeMargin + Clearance;

        static readonly POIType[] SpecialTypes =
        {
            POIType.GildedWreck,
            POIType.MerchantSkiff,
            POIType.TrialBuoy,
            POIType.CursedIdol
        };

        public static void Place(StageLayout layout, RunRng rng, IslandLayoutDefinition islandLayout)
        {
            layout.pois.Clear();
            V3SpawnAnchorValidator.TryResolveAnchors(layout, islandLayout, rng);

            var spawn = layout.playerSpawn;
            var placed = new List<Vector3>();

            for (var i = 0; i < SpecialTypes.Length; i++)
                TryPlace(layout, rng, spawn, placed, SpecialTypes[i], islandLayout);

            for (var i = 0; i < SalvageChestCount; i++)
                TryPlace(layout, rng, spawn, placed, POIType.SalvageChest, islandLayout);
        }

        static void TryPlace(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            POIType type,
            IslandLayoutDefinition islandLayout)
        {
            for (var attempt = 0; attempt < 120; attempt++)
            {
                var angle = rng.MapFloat() * Mathf.PI * 2f;
                var maxRadius = GetMaxRadiusFromSpawn(spawn);
                var radius = Mathf.Lerp(MinRadius, maxRadius, rng.MapFloat());
                var pos = spawn + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Vector3.Distance(pos, spawn) < MinRadius)
                    continue;

                if (!TryAcceptPosition(pos, placed, islandLayout, out pos))
                    continue;

                placed.Add(pos);
                layout.pois.Add(new POISlot { type = type, position = pos });
                return;
            }

            if (TryPlaceFromGrid(spawn, placed, islandLayout, out var gridPos))
            {
                placed.Add(gridPos);
                layout.pois.Add(new POISlot { type = type, position = gridPos });
                return;
            }

            Debug.LogWarning($"V3StagePoiPlacer: could not place {type}; using fallback near spawn.");
            var fallback = PlayBounds.Clamp(
                spawn + new Vector3(placed.Count * 4f, 0f, MinRadius + placed.Count * 2f),
                PoiEdgeMargin);
            placed.Add(fallback);
            layout.pois.Add(new POISlot { type = type, position = fallback });
        }

        static bool TryPlaceFromGrid(
            Vector3 spawn,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            out Vector3 position)
        {
            position = default;
            var bestScore = float.MaxValue;
            var found = false;

            var center = PlayBounds.Center;
            var limitX = PlayBounds.HalfExtentX - PoiEdgeMargin;
            var limitZ = PlayBounds.HalfExtentZ - PoiEdgeMargin;
            for (var x = -limitX; x <= limitX; x += 6f)
            {
                for (var z = -limitZ; z <= limitZ; z += 6f)
                {
                    var candidate = center + new Vector3(x, 0f, z);
                    if (!TryAcceptPosition(candidate, placed, islandLayout, out candidate))
                        continue;

                    var score = Vector3.Distance(candidate, spawn);
                    if (score < bestScore)
                    {
                        bestScore = score;
                        position = candidate;
                        found = true;
                    }
                }
            }

            return found;
        }

        static bool TryAcceptPosition(
            Vector3 pos,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            out Vector3 resolved)
        {
            resolved = PlayBounds.Clamp(pos, PoiEdgeMargin);

            for (var i = 0; i < placed.Count; i++)
            {
                if (Vector3.Distance(resolved, placed[i]) < MinSpacing)
                    return false;
            }

            if (!IsOpenWater(resolved, islandLayout, Clearance))
                return false;

            return true;
        }

        static float GetMaxRadiusFromSpawn(Vector3 spawn)
        {
            var local = spawn - PlayBounds.Center;
            var reachX = PlayBounds.HalfExtentX - PoiEdgeMargin - Mathf.Abs(local.x);
            var reachZ = PlayBounds.HalfExtentZ - PoiEdgeMargin - Mathf.Abs(local.z);
            var maxReach = Mathf.Min(reachX, reachZ);
            return Mathf.Clamp(maxReach, MinRadius, MaxRadius);
        }

        static bool IsOpenWater(Vector3 position, IslandLayoutDefinition islandLayout, float clearance)
        {
            if (!PlayBounds.IsInside(position, PoiEdgeMargin))
                return false;

            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, clearance))
                return false;

            if (IslandSpawnExclusionRegistry.Count > 0 &&
                !IslandSpawnExclusionRegistry.IsClear(position, clearance))
                return false;

            if (!IslandObstacles.IsAvailable && islandLayout != null &&
                !V3SpawnAnchorValidator.IsSpawnSafe(position, islandLayout, clearance))
                return false;

            return true;
        }
    }
}
