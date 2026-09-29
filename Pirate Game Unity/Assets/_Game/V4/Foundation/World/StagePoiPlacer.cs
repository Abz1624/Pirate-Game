using System.Collections.Generic;
using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Foundation.World
{
    /// <summary>Places salvage chests and finite encounter POIs across playable water; boss beacon uses spawn anchors.</summary>
    public static class StagePoiPlacer
    {
        const float MinSpacing = 16f;
        const float MinRadius = 20f;
        const float FirstChestMaxPlacementRadius = 78f;
        const float Clearance = 2.5f;

        static float PoiEdgeMargin => PlayBounds.StormEdgeMargin + Clearance;

        static readonly POIType[] SpecialTypes =
        {
            POIType.GildedWreck,
            POIType.MerchantSkiff,
            POIType.TrialBuoy,
            POIType.CursedIdol
        };

        public static void Place(
            StageLayout layout,
            RunRng rng,
            IslandLayoutDefinition islandLayout,
            StagePoiPlacementOptions? optionsOverride = null)
        {
            var options = optionsOverride ?? StagePoiPlacementOptions.ResolveActive();
            layout.pois.Clear();
            layout.explosiveBarrelPositions.Clear();
            SpawnAnchorValidator.TryResolveAnchors(layout, islandLayout, rng);

            var spawn = layout.playerSpawn;
            var placed = new List<Vector3>();
            var clearance = Mathf.Max(Clearance, options.NavClearance);

            PlaceSpecialPoisAcrossPlayable(layout, rng, placed, islandLayout, clearance);

            TryPlaceFirstChest(layout, rng, spawn, placed, islandLayout, options, clearance);
            PlaceSalvageChestsAcrossPlayable(layout, rng, placed, islandLayout, options, clearance);

            PlaceExplosiveBarrels(layout, placed, options, clearance, islandLayout);
            SpawnAnchorValidator.SyncBossBeaconPoi(layout);
        }

        static void TryPlaceFirstChest(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            StagePoiPlacementOptions options,
            float clearance)
        {
            var maxDist = options.RepresentativeCruiseSpeed * options.FirstChestMaxTravelSeconds;
            maxDist = Mathf.Clamp(maxDist, MinRadius, FirstChestMaxPlacementRadius);
            var bestPos = Vector3.zero;
            var found = false;
            var bestScore = float.MaxValue;

            for (var attempt = 0; attempt < 160; attempt++)
            {
                var angle = rng.MapFloat() * Mathf.PI * 2f;
                var radius = Mathf.Lerp(MinRadius, maxDist, rng.MapFloat());
                var pos = spawn + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!TryAcceptPosition(pos, placed, islandLayout, clearance, out pos))
                    continue;
                if (!SaltwindPoiLayoutLogic.HasClearWaterRoute(spawn, pos, clearance, islandLayout))
                    continue;

                var travel = SaltwindPoiLayoutLogic.EstimateTravelSeconds(
                    spawn, pos, options.RepresentativeCruiseSpeed);
                if (travel > options.FirstChestMaxTravelSeconds + 0.25f)
                    continue;

                if (travel < bestScore)
                {
                    bestScore = travel;
                    bestPos = pos;
                    found = true;
                }
            }

            if (!found && TryPlaceFromGrid(spawn, placed, islandLayout, clearance, maxDist, out bestPos))
                found = true;

            if (found &&
                !SaltwindPoiLayoutLogic.HasClearWaterRoute(spawn, bestPos, clearance, islandLayout))
                found = false;

            if (!found)
            {
                if (TryPlaceNavigableFallback(spawn, placed, islandLayout, clearance, POIType.SalvageChest, layout, requireRouteFromSpawn: true))
                    return;
                Debug.LogWarning("StagePoiPlacer: could not place opening chest with navigable fallback.");
                return;
            }

            placed.Add(bestPos);
            layout.pois.Add(new POISlot { type = POIType.SalvageChest, position = bestPos });
        }

        static void PlaceSpecialPoisAcrossPlayable(
            StageLayout layout,
            RunRng rng,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            float clearance)
        {
            if (!TryGetPlayableLimits(out var center, out var limitX, out var limitZ))
            {
                for (var i = 0; i < SpecialTypes.Length; i++)
                    TryPlaceLegacyNearSpawn(layout, rng, layout.playerSpawn, placed, SpecialTypes[i], islandLayout, clearance);
                return;
            }

            PreparePlayableCellOrder(
                rng,
                SpecialTypes.Length,
                limitX,
                limitZ,
                out var cells,
                out var gridCols,
                out var gridRows,
                out var cellW,
                out var cellH);

            for (var i = 0; i < SpecialTypes.Length; i++)
            {
                if (TryPlaceSinglePoiAcrossPlayable(
                        layout,
                        rng,
                        layout.playerSpawn,
                        placed,
                        islandLayout,
                        clearance,
                        SpecialTypes[i],
                        cells,
                        i,
                        gridCols,
                        gridRows,
                        cellW,
                        cellH,
                        center,
                        limitX,
                        limitZ))
                    continue;

                Debug.LogWarning(
                    $"StagePoiPlacer: could not place {SpecialTypes[i]} across playable area.");
            }
        }

        static void PlaceSalvageChestsAcrossPlayable(
            StageLayout layout,
            RunRng rng,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            StagePoiPlacementOptions options,
            float clearance)
        {
            var remaining = options.SalvageChestCount - 1;
            if (remaining <= 0) return;

            if (!TryGetPlayableLimits(out var center, out var limitX, out var limitZ))
            {
                for (var i = 0; i < remaining; i++)
                    TryPlaceLegacyNearSpawn(layout, rng, layout.playerSpawn, placed, POIType.SalvageChest, islandLayout, clearance);
                return;
            }

            PreparePlayableCellOrder(rng, remaining, limitX, limitZ, out var cells, out var gridCols, out var gridRows, out var cellW, out var cellH);

            for (var c = 0; c < remaining; c++)
            {
                if (TryPlaceSinglePoiAcrossPlayable(
                        layout,
                        rng,
                        layout.playerSpawn,
                        placed,
                        islandLayout,
                        clearance,
                        POIType.SalvageChest,
                        cells,
                        c,
                        gridCols,
                        gridRows,
                        cellW,
                        cellH,
                        center,
                        limitX,
                        limitZ))
                    continue;

                Debug.LogWarning("StagePoiPlacer: could not place salvage chest across playable area.");
            }
        }

        static bool TryGetPlayableLimits(out Vector3 center, out float limitX, out float limitZ)
        {
            center = PlayBounds.Center;
            limitX = PlayBounds.HalfExtentX - PoiEdgeMargin;
            limitZ = PlayBounds.HalfExtentZ - PoiEdgeMargin;
            return limitX >= MinRadius && limitZ >= MinRadius;
        }

        static void PreparePlayableCellOrder(
            RunRng rng,
            int poiCount,
            float limitX,
            float limitZ,
            out List<int> cells,
            out int gridCols,
            out int gridRows,
            out float cellW,
            out float cellH)
        {
            gridCols = Mathf.Max(3, Mathf.CeilToInt(Mathf.Sqrt(poiCount * (limitX / Mathf.Max(limitZ, 1f)))));
            gridRows = Mathf.Max(1, Mathf.CeilToInt((float)poiCount / gridCols));
            cellW = (limitX * 2f) / gridCols;
            cellH = (limitZ * 2f) / gridRows;
            cells = new List<int>(gridCols * gridRows);
            for (var i = 0; i < gridCols * gridRows; i++)
                cells.Add(i);
            ShuffleCellOrder(cells, rng);
        }

        static bool TryPlaceSinglePoiAcrossPlayable(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            float clearance,
            POIType type,
            List<int> cells,
            int poiIndex,
            int gridCols,
            int gridRows,
            float cellW,
            float cellH,
            Vector3 center,
            float limitX,
            float limitZ)
        {
            var cellIndex = cells[poiIndex % cells.Count];
            var col = cellIndex % gridCols;
            var row = cellIndex / gridCols;

            for (var attempt = 0; attempt < 72; attempt++)
            {
                var localX = -limitX + (col + rng.MapFloat()) * cellW;
                var localZ = -limitZ + (row + rng.MapFloat()) * cellH;
                var pos = center + new Vector3(localX, 0f, localZ);
                if (!TryAcceptPosition(pos, placed, islandLayout, clearance, out pos))
                    continue;

                placed.Add(pos);
                layout.pois.Add(new POISlot { type = type, position = pos });
                return true;
            }

            return TryPlacePoiRandomAcrossPlayable(layout, rng, spawn, placed, islandLayout, clearance, type);
        }

        static void ShuffleCellOrder(List<int> cells, RunRng rng)
        {
            for (var i = cells.Count - 1; i > 0; i--)
            {
                var j = rng.MapInt(0, i + 1);
                (cells[i], cells[j]) = (cells[j], cells[i]);
            }
        }

        static bool TryPlacePoiRandomAcrossPlayable(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            float clearance,
            POIType type)
        {
            var limitX = PlayBounds.HalfExtentX - PoiEdgeMargin;
            var limitZ = PlayBounds.HalfExtentZ - PoiEdgeMargin;
            var center = PlayBounds.Center;

            for (var attempt = 0; attempt < 280; attempt++)
            {
                var localX = (rng.MapFloat() * 2f - 1f) * limitX;
                var localZ = (rng.MapFloat() * 2f - 1f) * limitZ;
                var pos = center + new Vector3(localX, 0f, localZ);
                if (!TryAcceptPosition(pos, placed, islandLayout, clearance, out pos))
                    continue;

                placed.Add(pos);
                layout.pois.Add(new POISlot { type = type, position = pos });
                return true;
            }

            if (TryPlaceFromGridAcrossPlayable(placed, islandLayout, clearance, out var gridPos))
            {
                placed.Add(gridPos);
                layout.pois.Add(new POISlot { type = type, position = gridPos });
                return true;
            }

            return TryPlaceNavigableFallback(
                spawn,
                placed,
                islandLayout,
                clearance,
                type,
                layout,
                requireRouteFromSpawn: false);
        }

        static void PlaceExplosiveBarrels(
            StageLayout layout,
            List<Vector3> placed,
            StagePoiPlacementOptions options,
            float clearance,
            IslandLayoutDefinition islandLayout)
        {
            if (options.ExplosiveBarrelCount <= 0) return;

            var chests = new List<Vector3>();
            for (var i = 0; i < layout.pois.Count; i++)
            {
                if (layout.pois[i].type == POIType.SalvageChest)
                    chests.Add(layout.pois[i].position);
            }

            for (var b = 0; b < options.ExplosiveBarrelCount; b++)
            {
                if (chests.Count <= b + 1) break;
                var anchor = chests[b + 1];
                var offsetDir = (anchor - layout.playerSpawn).normalized;
                if (offsetDir.sqrMagnitude < 0.01f)
                    offsetDir = Vector3.right;
                var side = Quaternion.Euler(0f, b == 0 ? 35f : -35f, 0f) * offsetDir;
                var candidate = anchor + side * 7f;
                if (!TryAcceptPosition(candidate, placed, islandLayout, clearance, out candidate))
                    candidate = PlayBounds.Clamp(anchor + side * 5f, PoiEdgeMargin);

                placed.Add(candidate);
                layout.explosiveBarrelPositions.Add(candidate);
            }
        }

        static void TryPlaceLegacyNearSpawn(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            POIType type,
            IslandLayoutDefinition islandLayout,
            float clearance)
        {
            for (var attempt = 0; attempt < 280; attempt++)
            {
                var angle = rng.MapFloat() * Mathf.PI * 2f;
                var maxRadius = GetMaxRadiusFromSpawn(spawn);
                var radius = Mathf.Lerp(MinRadius, maxRadius, rng.MapFloat());
                var pos = spawn + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Vector3.Distance(pos, spawn) < MinRadius)
                    continue;

                if (!TryAcceptPosition(pos, placed, islandLayout, clearance, out pos))
                    continue;

                placed.Add(pos);
                layout.pois.Add(new POISlot { type = type, position = pos });
                return;
            }

            if (TryPlaceFromGrid(spawn, placed, islandLayout, clearance, GetMaxRadiusFromSpawn(spawn), out var gridPos))
            {
                placed.Add(gridPos);
                layout.pois.Add(new POISlot { type = type, position = gridPos });
                return;
            }

            if (TryPlaceNavigableFallback(
                    spawn,
                    placed,
                    islandLayout,
                    clearance,
                    type,
                    layout,
                    requireRouteFromSpawn: false))
                return;

            Debug.LogWarning($"StagePoiPlacer: could not place {type}; skipping fallback without navigable clearance.");
        }

        // Spiral search for any still-valid navigable POI slot.
        static bool TryPlaceNavigableFallback(
            Vector3 spawn,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            float clearance,
            POIType type,
            StageLayout layout,
            bool requireRouteFromSpawn)
        {
            for (var i = 0; i < 192; i++)
            {
                var angle = i * 19.7f * Mathf.Deg2Rad;
                var radius = MinRadius + (i % 24) * 2.75f;
                var candidate = spawn + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (!TryAcceptPosition(candidate, placed, islandLayout, clearance, out candidate) &&
                    !TryAcceptPositionRelaxed(candidate, placed, islandLayout, clearance, out candidate))
                    continue;
                if (requireRouteFromSpawn &&
                    !SaltwindPoiLayoutLogic.HasClearWaterRoute(spawn, candidate, clearance, islandLayout))
                    continue;

                placed.Add(candidate);
                layout.pois.Add(new POISlot { type = type, position = candidate });
                return true;
            }

            return false;
        }

        static bool TryAcceptPositionRelaxed(
            Vector3 pos,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            float clearance,
            out Vector3 resolved)
        {
            resolved = PlayBounds.Clamp(pos, PoiEdgeMargin);
            for (var i = 0; i < placed.Count; i++)
            {
                if (Vector3.Distance(resolved, placed[i]) < MinSpacing * 0.65f)
                    return false;
            }

            return SaltwindPoiLayoutLogic.IsNavigable(resolved, clearance, islandLayout);
        }

        static bool TryPlaceFromGridAcrossPlayable(
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            float clearance,
            out Vector3 position)
        {
            position = default;
            var bestScore = -1f;
            var found = false;

            var center = PlayBounds.Center;
            var limitX = PlayBounds.HalfExtentX - PoiEdgeMargin;
            var limitZ = PlayBounds.HalfExtentZ - PoiEdgeMargin;
            for (var x = -limitX; x <= limitX; x += 3f)
            {
                for (var z = -limitZ; z <= limitZ; z += 3f)
                {
                    var candidate = center + new Vector3(x, 0f, z);
                    if (!TryAcceptPosition(candidate, placed, islandLayout, clearance, out candidate))
                        continue;

                    var minDist = float.MaxValue;
                    for (var i = 0; i < placed.Count; i++)
                        minDist = Mathf.Min(minDist, Vector3.Distance(candidate, placed[i]));

                    if (minDist > bestScore)
                    {
                        bestScore = minDist;
                        position = candidate;
                        found = true;
                    }
                }
            }

            return found;
        }

        static bool TryPlaceFromGrid(
            Vector3 spawn,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            float clearance,
            float maxDistanceFromSpawn,
            out Vector3 position)
        {
            position = default;
            var bestScore = float.MaxValue;
            var found = false;

            var center = PlayBounds.Center;
            var limitX = PlayBounds.HalfExtentX - PoiEdgeMargin;
            var limitZ = PlayBounds.HalfExtentZ - PoiEdgeMargin;
            for (var x = -limitX; x <= limitX; x += 3f)
            {
                for (var z = -limitZ; z <= limitZ; z += 3f)
                {
                    var candidate = center + new Vector3(x, 0f, z);
                    if (Vector3.Distance(candidate, spawn) > maxDistanceFromSpawn)
                        continue;
                    if (!TryAcceptPosition(candidate, placed, islandLayout, clearance, out candidate))
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
            float clearance,
            out Vector3 resolved)
        {
            resolved = PlayBounds.Clamp(pos, PoiEdgeMargin);

            for (var i = 0; i < placed.Count; i++)
            {
                if (Vector3.Distance(resolved, placed[i]) < MinSpacing)
                    return false;
            }

            return SaltwindPoiLayoutLogic.IsNavigable(resolved, clearance, islandLayout);
        }

        static float GetMaxRadiusFromSpawn(Vector3 spawn)
        {
            var local = spawn - PlayBounds.Center;
            var reachX = PlayBounds.HalfExtentX - PoiEdgeMargin - Mathf.Abs(local.x);
            var reachZ = PlayBounds.HalfExtentZ - PoiEdgeMargin - Mathf.Abs(local.z);
            var maxReach = Mathf.Min(reachX, reachZ);
            return Mathf.Max(MinRadius, maxReach);
        }
    }
}
