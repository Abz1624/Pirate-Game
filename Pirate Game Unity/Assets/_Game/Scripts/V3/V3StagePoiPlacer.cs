using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Places salvage chests, 4 finite encounter POIs, and the treasury beacon for the v3 conquest layout.</summary>
    public static class V3StagePoiPlacer
    {
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

        public static void Place(
            StageLayout layout,
            RunRng rng,
            IslandLayoutDefinition islandLayout,
            V3StagePoiPlacementOptions? optionsOverride = null)
        {
            var options = optionsOverride ?? V3StagePoiPlacementOptions.ResolveActive();
            layout.pois.Clear();
            layout.explosiveBarrelPositions.Clear();
            V3SpawnAnchorValidator.TryResolveAnchors(layout, islandLayout, rng);

            var spawn = layout.playerSpawn;
            var placed = new List<Vector3>();
            var clearance = Mathf.Max(Clearance, options.NavClearance);

            for (var i = 0; i < SpecialTypes.Length; i++)
                TryPlace(layout, rng, spawn, placed, SpecialTypes[i], islandLayout, clearance);

            TryPlaceFirstChest(layout, rng, spawn, placed, islandLayout, options, clearance);
            for (var i = 1; i < options.SalvageChestCount; i++)
                TryPlace(layout, rng, spawn, placed, POIType.SalvageChest, islandLayout, clearance);

            PlaceExplosiveBarrels(layout, placed, options, clearance, islandLayout);
            V3SpawnAnchorValidator.SyncBossBeaconPoi(layout);
        }

        static void TryPlaceFirstChest(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            IslandLayoutDefinition islandLayout,
            V3StagePoiPlacementOptions options,
            float clearance)
        {
            var maxDist = options.RepresentativeCruiseSpeed * options.FirstChestMaxTravelSeconds;
            maxDist = Mathf.Clamp(maxDist, MinRadius, MaxRadius);
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

            if (!found)
            {
                bestPos = PlayBounds.Clamp(spawn + Vector3.forward * MinRadius, PoiEdgeMargin);
                found = true;
            }

            placed.Add(bestPos);
            layout.pois.Add(new POISlot { type = POIType.SalvageChest, position = bestPos });
        }

        static void PlaceExplosiveBarrels(
            StageLayout layout,
            List<Vector3> placed,
            V3StagePoiPlacementOptions options,
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

        static void TryPlace(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            POIType type,
            IslandLayoutDefinition islandLayout,
            float clearance)
        {
            for (var attempt = 0; attempt < 120; attempt++)
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

            if (TryPlaceFromGrid(spawn, placed, islandLayout, clearance, MaxRadius, out var gridPos))
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
            for (var x = -limitX; x <= limitX; x += 6f)
            {
                for (var z = -limitZ; z <= limitZ; z += 6f)
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
            return Mathf.Clamp(maxReach, MinRadius, MaxRadius);
        }
    }
}
