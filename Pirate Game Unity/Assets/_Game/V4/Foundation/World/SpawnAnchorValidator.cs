using System.Collections.Generic;

using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Core;

using RoguePirates.V4.Foundation.Data;

using RoguePirates.V4.Foundation.World;

using UnityEngine;



namespace RoguePirates.V4.Foundation.World

{

    /// <summary>Validates player spawn, beacon route, and POI anchors for the Saltwind conquest layout.</summary>

    public static class SpawnAnchorValidator

    {

        public const float SpawnSafeRadius = 6f;

        public const float ChestProximityMax = 35f;

        public const float BeaconMinDistance = 60f;

        public const float BeaconMaxDistance = 180f;



        static readonly POIType[] SliceSpecialTypes =

        {

            POIType.GildedWreck,

            POIType.MerchantSkiff,

            POIType.TrialBuoy,

            POIType.CursedIdol

        };



        public static bool TryResolveAnchors(StageLayout layout, IslandLayoutDefinition islandLayout, RunRng rng)

        {

            if (layout == null) return false;



            var hullRadius = 1.15f;

            var resolved = ScanForSafeSpawn(layout, islandLayout, hullRadius, layout.playerSpawn);

            if (!IsSpawnSafe(resolved, islandLayout, hullRadius))

            {

                Debug.LogWarning("SpawnAnchorValidator: could not resolve safe spawn during POI placement.");

                return false;

            }



            layout.playerSpawn = resolved;

            if (!TryFindBeacon(resolved, layout, islandLayout, rng, out var beacon))
                beacon = FindFallbackBeacon(resolved, layout, islandLayout);

            layout.bossBeacon = beacon;
            UpdateBeaconSlot(layout);

            return true;

        }



        public static bool IsSpawnSafe(Vector3 position, IslandLayoutDefinition islandLayout = null, float hullRadius = 1.15f)

        {

            var clearance = SpawnSafeRadius + hullRadius;

            if (!IslandSpawnExclusionRegistry.IsClear(position, clearance))

                return false;



            if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(position, hullRadius))

                return false;



            if (!PlayBounds.IsInside(position, clearance))
                return false;



            if (!IsSpawnSafeGeometric(position, islandLayout, clearance))

                return false;



            return true;

        }



        public static Vector3 ResolveRuntimeSpawn(

            StageLayout layout,

            IslandLayoutDefinition islandLayout,

            int seed,

            float hullRadius = 1.15f)

        {

            if (layout == null)

                return Vector3.zero;



            Physics.SyncTransforms();



            var prefer = layout.playerSpawn;

            var resolved = ScanForSafeSpawn(layout, islandLayout, hullRadius, prefer);

            if (!IsSpawnSafe(resolved, islandLayout, hullRadius))

            {

                Debug.LogError($"SpawnAnchorValidator: failed to find safe spawn for seed {seed}.");

                resolved = ScanForSafeSpawn(layout, islandLayout, hullRadius, null);

            }



            layout.playerSpawn = resolved;

            return resolved;

        }



        static Vector3 ScanForSafeSpawn(

            StageLayout layout,

            IslandLayoutDefinition islandLayout,

            float hullRadius,

            Vector3? preferNear)

        {

            Vector3? best = null;

            var bestScore = float.MaxValue;



            var center = PlayBounds.Center;
            var limitX = PlayBounds.HalfExtentX - (SpawnSafeRadius + hullRadius);
            var limitZ = PlayBounds.HalfExtentZ - (SpawnSafeRadius + hullRadius);

            for (var x = -limitX; x <= limitX; x += 4f)

            {

                for (var z = -limitZ; z <= limitZ; z += 4f)

                {

                    var candidate = center + new Vector3(x, 0f, z);

                    if (!IsSpawnSafe(candidate, islandLayout, hullRadius))

                        continue;



                    var score = preferNear.HasValue

                        ? Vector3.Distance(candidate, preferNear.Value)

                        : new Vector2(x, z).magnitude;

                    if (score < bestScore)

                    {

                        bestScore = score;

                        best = candidate;

                    }

                }

            }



            if (best.HasValue)

                return FinalizeSpawnPosition(best.Value, islandLayout, hullRadius);



            for (var ring = 1; ring <= 48; ring++)

            {

                var radius = ring * 3f;

                for (var i = 0; i < 36; i++)

                {

                    var angle = (i / 36f) * Mathf.PI * 2f;

                    var candidate = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

                    if (!IsSpawnSafe(candidate, islandLayout, hullRadius))

                        continue;



                    return FinalizeSpawnPosition(candidate, islandLayout, hullRadius);

                }

            }



            var fallback = FindLeastBlockedSpawn(islandLayout, hullRadius);
            if (fallback.HasValue)
                return FinalizeSpawnPosition(fallback.Value, islandLayout, hullRadius);

            Debug.LogError("SpawnAnchorValidator: exhaustive spawn search failed; using map center fallback.");

            return FinalizeSpawnPosition(center, islandLayout, hullRadius);

        }



        static Vector3 FinalizeSpawnPosition(Vector3 position, IslandLayoutDefinition islandLayout, float hullRadius)

        {

            var resolved = position;

            if (IslandObstacles.IsAvailable)

            {

                var height = IslandObstacles.GetCollisionHeight(hullRadius);

                resolved = IslandObstacles.Depenetrate(resolved, hullRadius, height);

            }



            if (IslandObstacles.IsAvailable)
            {
                var height = IslandObstacles.GetCollisionHeight(hullRadius);
                resolved = IslandObstacles.Depenetrate(resolved, hullRadius, height);
            }

            if (IsSpawnSafe(resolved, islandLayout, hullRadius))
                return resolved;

            for (var ring = 1; ring <= 24; ring++)

            {

                for (var i = 0; i < 16; i++)

                {

                    var angle = (i / 16f) * Mathf.PI * 2f;

                    var offset = new Vector3(Mathf.Cos(angle) * ring * 3f, 0f, Mathf.Sin(angle) * ring * 3f);

                    var candidate = position + offset;

                    if (!IsSpawnSafe(candidate, islandLayout, hullRadius))

                        continue;



                    if (IslandObstacles.IsAvailable)

                    {

                        var height = IslandObstacles.GetCollisionHeight(hullRadius);

                        candidate = IslandObstacles.Depenetrate(candidate, hullRadius, height);

                    }



                    if (IsSpawnSafe(candidate, islandLayout, hullRadius))

                        return candidate;

                }

            }



            return ScanForSafeSpawn(null, islandLayout, hullRadius, null);

        }



        static bool IsSpawnSafeGeometric(Vector3 position, IslandLayoutDefinition islandLayout, float clearance)

        {

            if (islandLayout?.clusters == null)

                return true;



            for (var c = 0; c < islandLayout.clusters.Length; c++)

            {

                var cluster = islandLayout.clusters[c];

                if (cluster.islands == null) continue;



                var clusterPos = cluster.anchorPosition;

                var clusterRadius = Mathf.Max(8f, cluster.clusterJitterRadius) + clearance + 6f;

                if (FlatDistance(position, clusterPos) < clusterRadius)

                    return false;



                for (var i = 0; i < cluster.islands.Length; i++)

                {

                    var entry = cluster.islands[i];

                    var islandPos = clusterPos + entry.offsetFromAnchor;

                    var footprint = entry.targetFootprintMeters > 0f ? entry.targetFootprintMeters : 20f;

                    var scale = entry.scale.x > 0f ? Mathf.Max(entry.scale.x, entry.scale.z) : 1f;

                    var islandRadius = footprint * 0.85f * scale + clearance + entry.positionJitter + cluster.clusterJitterRadius + 8f;

                    if (FlatDistance(position, islandPos) < islandRadius)

                        return false;

                }

            }



            return true;

        }



        static float FlatDistance(Vector3 a, Vector3 b)

        {

            var dx = a.x - b.x;

            var dz = a.z - b.z;

            return Mathf.Sqrt(dx * dx + dz * dz);

        }



        static bool TryFindBeacon(

            Vector3 spawn,

            StageLayout layout,

            IslandLayoutDefinition islandLayout,

            RunRng rng,

            out Vector3 beacon)

        {

            beacon = layout.bossBeacon;

            var candidates = new List<Vector3>();



            if (islandLayout?.clusters != null)

            {

                for (var i = 0; i < islandLayout.clusters.Length; i++)

                {

                    var anchor = islandLayout.clusters[i].anchorPosition;

                    candidates.Add(anchor + new Vector3(8f, 0f, 8f));

                }

            }



            candidates.Add(layout.bossBeacon);

            for (var i = 0; i < 24; i++)

            {

                var angle = rng.MapFloat() * Mathf.PI * 2f;

                var dist = Mathf.Lerp(BeaconMinDistance, BeaconMaxDistance, rng.MapFloat());

                candidates.Add(spawn + new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist));

            }



            for (var i = 0; i < candidates.Count; i++)

            {

                var pos = candidates[i];

                var dist = Vector3.Distance(spawn, pos);

                if (dist < BeaconMinDistance || dist > BeaconMaxDistance)

                    continue;

                if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(pos, 3f))

                    continue;

                if (!SaltwindPoiLayoutLogic.HasClearWaterRoute(
                        spawn,
                        pos,
                        SaltwindPoiLayoutLogic.DefaultNavClearance,
                        islandLayout))
                    continue;

                beacon = pos;

                return true;

            }



            return false;

        }



        static Vector3 FindFallbackBeacon(
            Vector3 spawn,
            StageLayout layout,
            IslandLayoutDefinition islandLayout)
        {
            var best = layout.bossBeacon;
            var bestScore = float.MinValue;

            for (var angle = 0; angle < 16; angle++)
            {
                var radians = (angle / 16f) * Mathf.PI * 2f;
                for (var dist = BeaconMinDistance; dist <= BeaconMaxDistance; dist += 12f)
                {
                    var candidate = spawn + new Vector3(Mathf.Cos(radians) * dist, 0f, Mathf.Sin(radians) * dist);
                    if (!PlayBounds.IsOpenWater(candidate, 3f))
                        continue;

                    if (!SaltwindPoiLayoutLogic.HasClearWaterRoute(
                            spawn,
                            candidate,
                            SaltwindPoiLayoutLogic.DefaultNavClearance,
                            islandLayout))
                        continue;

                    var score = Vector3.Distance(spawn, candidate);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            if (bestScore > float.MinValue)
                return best;

            if (islandLayout?.clusters != null && islandLayout.clusters.Length > 0)
                return islandLayout.clusters[0].anchorPosition + new Vector3(12f, 0f, 12f);

            return PlayBounds.Clamp(spawn + new Vector3(0f, 0f, BeaconMinDistance), 3f);
        }



        static Vector3? FindLeastBlockedSpawn(IslandLayoutDefinition islandLayout, float hullRadius)
        {
            Vector3? best = null;
            var bestScore = float.MaxValue;
            var center = PlayBounds.Center;

            for (var x = -90f; x <= 90f; x += 6f)
            {
                for (var z = -90f; z <= 90f; z += 6f)
                {
                    var candidate = center + new Vector3(x, 0f, z);
                    if (!PlayBounds.IsInside(candidate, hullRadius))
                        continue;

                    var blocked = IslandObstacles.IsAvailable &&
                                  IslandObstacles.IsPositionBlocked(candidate, hullRadius);
                    var local = new Vector2(x, z);
                    var score = blocked ? 1000f + local.magnitude : local.magnitude;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            return best;
        }



        public static void SyncBossBeaconPoi(StageLayout layout) => UpdateBeaconSlot(layout);

        static void UpdateBeaconSlot(StageLayout layout)
        {
            for (var i = layout.pois.Count - 1; i >= 0; i--)
            {
                if (layout.pois[i].type != POIType.BossBeacon)
                    continue;

                var slot = layout.pois[i];
                slot.position = layout.bossBeacon;
                layout.pois[i] = slot;
                return;
            }

            layout.pois.Add(new POISlot { type = POIType.BossBeacon, position = layout.bossBeacon });
        }

    }

}


