using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.Balance
{
    public readonly struct SaltwindPoiLayoutReport
    {
        public bool IsValid { get; }
        public string Error { get; }

        SaltwindPoiLayoutReport(bool isValid, string error)
        {
            IsValid = isValid;
            Error = error;
        }

        public static SaltwindPoiLayoutReport Ok() => new(true, null);
        public static SaltwindPoiLayoutReport Fail(string error) => new(false, error);
    }

    /// <summary>Finite POI layout validation for Saltwind solo (Step 06 / C06). Boss beacons use route checks only; barrels are point-checked separately.</summary>
    public static class SaltwindPoiLayoutLogic
    {
        public const float DefaultSaltwindCruiseSpeed = 5.5f;
        public const float DefaultNavClearance = 3.15f;
        public const int SpecialPoiCount = 4;

        public static float EstimateTravelSeconds(Vector3 from, Vector3 to, float cruiseSpeed)
        {
            cruiseSpeed = Mathf.Max(0.5f, cruiseSpeed);
            var flat = to - from;
            flat.y = 0f;
            return flat.magnitude / cruiseSpeed;
        }

        public static bool HasClearWaterRoute(
            Vector3 from,
            Vector3 to,
            float clearance,
            IslandLayoutDefinition islandLayout)
        {
            var dist = Vector3.Distance(from, to);
            var steps = Mathf.Max(2, Mathf.CeilToInt(dist / 4f));
            for (var i = 0; i <= steps; i++)
            {
                var t = i / (float)steps;
                var sample = Vector3.Lerp(from, to, t);
                if (!IsNavigable(sample, clearance, islandLayout))
                    return false;
            }

            return true;
        }

        public static bool IsNavigable(Vector3 position, float clearance, IslandLayoutDefinition islandLayout)
        {
            if (!PlayBounds.IsInside(position, PlayBounds.StormEdgeMargin + 2f))
                return false;

            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, clearance))
                return false;

            if (IslandSpawnExclusionRegistry.Count > 0 &&
                !IslandSpawnExclusionRegistry.IsClear(position, clearance))
                return false;

            if (!IslandObstacles.IsAvailable && islandLayout != null &&
                !V3SpawnAnchorValidator.IsSpawnSafe(position, islandLayout, clearance))
                return false;

            return PlayBounds.IsOpenWater(position, clearance);
        }

        public static SaltwindPoiLayoutReport Validate(
            StageLayout layout,
            V3StagePoiPlacementOptions options,
            IslandLayoutDefinition islandLayout = null)
        {
            if (layout?.pois == null)
                return SaltwindPoiLayoutReport.Fail("layout.pois is null.");

            var chests = 0;
            var specials = 0;
            var beacons = 0;
            Vector3? firstChest = null;
            var minChestDist = float.MaxValue;
            var clearance = options.NavClearance;

            for (var i = 0; i < layout.pois.Count; i++)
            {
                var poi = layout.pois[i];
                if (islandLayout != null &&
                    poi.type != POIType.BossBeacon &&
                    !IsNavigable(poi.position, clearance, islandLayout))
                {
                    return SaltwindPoiLayoutReport.Fail(
                        $"POI {poi.type} at {poi.position} is not navigable.");
                }

                switch (poi.type)
                {
                    case POIType.SalvageChest:
                        chests++;
                        var dist = Vector3.Distance(layout.playerSpawn, poi.position);
                        if (dist < minChestDist)
                        {
                            minChestDist = dist;
                            firstChest = poi.position;
                        }
                        break;
                    case POIType.BossBeacon:
                        beacons++;
                        break;
                    case POIType.GildedWreck:
                    case POIType.MerchantSkiff:
                    case POIType.TrialBuoy:
                    case POIType.CursedIdol:
                        specials++;
                        break;
                }

                if (!PlayBounds.IsInsideStormSafeZone(poi.position))
                    return SaltwindPoiLayoutReport.Fail($"POI {poi.type} outside storm-safe bounds.");
            }

            if (chests != options.SalvageChestCount)
                return SaltwindPoiLayoutReport.Fail($"Expected {options.SalvageChestCount} chests, got {chests}.");
            if (specials != SpecialPoiCount)
                return SaltwindPoiLayoutReport.Fail($"Expected {SpecialPoiCount} special POIs, got {specials}.");
            if (beacons < 1 && layout.bossBeacon == Vector3.zero)
                return SaltwindPoiLayoutReport.Fail("Missing boss beacon anchor.");

            if (firstChest.HasValue)
            {
                var travel = EstimateTravelSeconds(
                    layout.playerSpawn,
                    firstChest.Value,
                    options.RepresentativeCruiseSpeed);
                if (travel > options.FirstChestMaxTravelSeconds + 0.5f)
                    return SaltwindPoiLayoutReport.Fail(
                        $"Nearest chest travel {travel:F1}s exceeds {options.FirstChestMaxTravelSeconds}s.");

                if (islandLayout != null &&
                    !HasClearWaterRoute(
                        layout.playerSpawn,
                        firstChest.Value,
                        clearance,
                        islandLayout))
                {
                    return SaltwindPoiLayoutReport.Fail("Nearest chest has no clear water route from spawn.");
                }
            }

            var beaconPos = layout.bossBeacon;
            if (beaconPos == Vector3.zero)
            {
                for (var i = 0; i < layout.pois.Count; i++)
                {
                    if (layout.pois[i].type == POIType.BossBeacon)
                    {
                        beaconPos = layout.pois[i].position;
                        break;
                    }
                }
            }

            if (islandLayout != null && beaconPos != Vector3.zero &&
                !HasClearWaterRoute(layout.playerSpawn, beaconPos, clearance, islandLayout))
            {
                return SaltwindPoiLayoutReport.Fail("Boss beacon has no clear water route from spawn.");
            }

            if (layout.explosiveBarrelPositions != null &&
                layout.explosiveBarrelPositions.Count != options.ExplosiveBarrelCount)
                return SaltwindPoiLayoutReport.Fail("Explosive barrel count mismatch.");

            return SaltwindPoiLayoutReport.Ok();
        }

        public static int CountUnopenedChests(IReadOnlyList<POISlot> pois)
        {
            var count = 0;
            for (var i = 0; i < pois.Count; i++)
            {
                if (pois[i].type == POIType.SalvageChest && !pois[i].used)
                    count++;
            }

            return count;
        }
    }
}
