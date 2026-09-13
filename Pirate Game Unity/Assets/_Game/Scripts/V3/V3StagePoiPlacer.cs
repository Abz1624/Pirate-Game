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
        const float MaxRadius = 88f;

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
            var spawn = layout.playerSpawn;
            var placed = new List<Vector3>();

            for (var i = 0; i < SpecialTypes.Length; i++)
                TryPlace(layout, rng, spawn, placed, SpecialTypes[i]);

            for (var i = 0; i < SalvageChestCount; i++)
                TryPlace(layout, rng, spawn, placed, POIType.SalvageChest);

            var bossPos = ResolveBossPosition(layout, islandLayout, spawn);
            layout.bossBeacon = bossPos;
            layout.pois.Add(new POISlot { type = POIType.BossBeacon, position = bossPos });
        }

        static Vector3 ResolveBossPosition(StageLayout layout, IslandLayoutDefinition islandLayout, Vector3 spawn)
        {
            if (islandLayout?.clusters == null || islandLayout.clusters.Length == 0)
                return layout.bossBeacon;

            var best = layout.bossBeacon;
            var bestDist = 0f;
            for (var i = 0; i < islandLayout.clusters.Length; i++)
            {
                var anchor = islandLayout.clusters[i].anchorPosition;
                var dist = Vector3.Distance(spawn, anchor);
                if (dist > bestDist)
                {
                    bestDist = dist;
                    best = anchor + new Vector3(8f, 0f, 8f);
                }
            }

            return best;
        }

        static void TryPlace(
            StageLayout layout,
            RunRng rng,
            Vector3 spawn,
            List<Vector3> placed,
            POIType type)
        {
            for (var attempt = 0; attempt < 120; attempt++)
            {
                var angle = rng.MapFloat() * Mathf.PI * 2f;
                var radius = Mathf.Lerp(MinRadius, MaxRadius, rng.MapFloat());
                var pos = spawn + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                if (Vector3.Distance(pos, spawn) < MinRadius)
                    continue;

                var tooClose = false;
                for (var i = 0; i < placed.Count; i++)
                {
                    if (Vector3.Distance(pos, placed[i]) < MinSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;

                if (IslandObstacles.IsAvailable && IslandObstacles.IsPositionBlocked(pos, 2f))
                    continue;

                placed.Add(pos);
                layout.pois.Add(new POISlot { type = type, position = pos });
                return;
            }

            Debug.LogWarning($"V3StagePoiPlacer: could not place {type}; using fallback near spawn.");
            var fallback = spawn + new Vector3(placed.Count * 4f, 0f, MinRadius + placed.Count * 2f);
            placed.Add(fallback);
            layout.pois.Add(new POISlot { type = type, position = fallback });
        }
    }
}
