using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Stage
{
    /// <summary>
    /// Minimap exploration and discovery (M7): port of V3DiscoverySystem. Explored cells around the ship, POIs within the
    /// discovery radius, chests revealed by the Treasure Chart, and the Treasury Beacon's reveal timeline (direction hint,
    /// exact marker, discovered by sailing near it). It only changes what the minimap and quest show, never the world.
    /// V3 quirks kept, each with a fix switch in <see cref="StageContentConfig"/> (off = V3):
    /// R-93 sailing to the Beacon only counts as finding it while nothing has been hinted yet;
    /// R-94 the exact marker is only reached straight from hidden, so after the 5:00 direction hint it never shows.
    /// </summary>
    public sealed class BeaconDiscovery
    {
        readonly HashSet<long> _exploredCells = new HashSet<long>();
        readonly HashSet<int> _discoveredPoiIndices = new HashSet<int>();
        readonly HashSet<int> _revealedChestIndices = new HashSet<int>();
        readonly Func<StageContentConfig> _config;
        readonly Func<Vector3?> _playerPosition;
        StageLayout _layout;
        BeaconReveal _beacon = BeaconReveal.Hidden;

        /// <summary>Raised once when the Beacon is spotted by sailing near it (V3's toast).</summary>
        public event Action BeaconSpotted;
        /// <summary>Raised whenever the Beacon's reveal state changes.</summary>
        public event Action<BeaconReveal> BeaconChanged;

        public BeaconDiscovery(Func<StageContentConfig> config, Func<Vector3?> playerPosition)
        {
            _config = config;
            _playerPosition = playerPosition;
        }

        public BeaconReveal Beacon => _beacon;
        public int ExplorationRevision { get; private set; }
        public bool HasLayout => _layout != null;
        StageContentConfig C => _config?.Invoke();
        public float DiscoveryRadius => C != null ? C.discoveryRadius : 24f;
        public float ExploreCellSize => C != null ? C.exploreCellSize : 6f;

        public void Initialize(StageLayout layout)
        {
            _layout = layout;
            _exploredCells.Clear();
            _discoveredPoiIndices.Clear();
            _revealedChestIndices.Clear();
            _beacon = BeaconReveal.Hidden;
            ExplorationRevision = 0;
            if (layout != null) ExploreAround(layout.playerSpawn, DiscoveryRadius);
        }

        /// <summary>V3DiscoverySystem.Update (its own runtime object; the V4 slot takes its place in the frame).</summary>
        public void Tick(bool paused, float elapsedSeconds)
        {
            if (_layout == null || paused) return;
            var player = _playerPosition?.Invoke();
            if (!player.HasValue) return;
            var pos = player.Value;
            ExploreAround(pos, C != null ? C.exploreRadius : 10f);
            DiscoverNearbyPois(pos);
            UpdateBeaconTimeline(pos, elapsedSeconds / 60f);
        }

        public void ExploreAround(Vector3 worldPos, float radius)
        {
            var cs = ExploreCellSize;
            var cellRadius = Mathf.CeilToInt(radius / cs);
            var cx = Mathf.FloorToInt(worldPos.x / cs);
            var cz = Mathf.FloorToInt(worldPos.z / cs);
            for (var x = -cellRadius; x <= cellRadius; x++)
            for (var z = -cellRadius; z <= cellRadius; z++)
            {
                var wx = (cx + x) * cs + cs * 0.5f;
                var wz = (cz + z) * cs + cs * 0.5f;
                var dx = wx - worldPos.x;
                var dz = wz - worldPos.z;
                if (dx * dx + dz * dz > radius * radius) continue;
                if (_exploredCells.Add(PackCell(cx + x, cz + z))) ExplorationRevision++;
            }
        }

        public bool IsCellExplored(Vector3 worldPos)
        {
            var cs = ExploreCellSize;
            return _exploredCells.Contains(PackCell(Mathf.FloorToInt(worldPos.x / cs), Mathf.FloorToInt(worldPos.z / cs)));
        }

        public bool IsPoiVisible(int index)
        {
            if (_layout == null || index < 0 || index >= _layout.pois.Count) return false;
            var poi = _layout.pois[index];
            if (poi.used) return false;
            if (poi.type == POIType.BossBeacon) return _beacon >= BeaconReveal.Discovered;
            if (poi.type == POIType.SalvageChest) return _revealedChestIndices.Contains(index) || _discoveredPoiIndices.Contains(index);
            return _discoveredPoiIndices.Contains(index);
        }

        /// <summary>Treasure Chart: the nearest unopened chests show on the minimap.</summary>
        public void RevealNearestChests(int count, Vector3 player)
        {
            if (_layout == null || count <= 0) return;
            var candidates = new List<(int index, float dist)>();
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                var poi = _layout.pois[i];
                if (poi.type != POIType.SalvageChest || poi.used) continue;
                candidates.Add((i, Vector3.Distance(player, poi.position)));
            }
            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            var revealCount = Mathf.Min(count, candidates.Count);
            for (var i = 0; i < revealCount; i++) _revealedChestIndices.Add(candidates[i].index);
        }

        public Vector3? BeaconPosition => _layout != null ? _layout.bossBeacon : (Vector3?)null;

        /// <summary>V3 GetBeaconBearingDegrees (compass bearing, 0 = +Z).</summary>
        public float BeaconBearingDegrees(Vector3 from)
        {
            var beacon = BeaconPosition;
            if (!beacon.HasValue) return 0f;
            var delta = beacon.Value - from;
            delta.y = 0f;
            return Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
        }

        void DiscoverNearbyPois(Vector3 playerPos)
        {
            if (_layout?.pois == null) return;
            var radius = DiscoveryRadius;
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                if (_discoveredPoiIndices.Contains(i)) continue;
                var poi = _layout.pois[i];
                if (poi.used) continue;
                if (poi.type == POIType.BossBeacon)
                {
                    if (_beacon >= BeaconReveal.Discovered) _discoveredPoiIndices.Add(i);
                    continue;
                }
                if (Vector3.Distance(playerPos, poi.position) <= radius) _discoveredPoiIndices.Add(i);
            }
        }

        void UpdateBeaconTimeline(Vector3 playerPos, float minute)
        {
            if (_layout == null) return;
            var c = C;
            var sectorMinute = (c != null ? c.beaconDirectionHintSeconds : 420f) / 60f;
            var exactMinute = (c != null ? c.beaconExactMarkerSeconds : 540f) / 60f;
            var proximityAfterHints = c != null && c.beaconFoundByProximityAfterHints;
            var exactAfterHint = c != null && c.beaconExactMarkerAfterHint;

            var previous = _beacon;
            var beaconDist = Vector3.Distance(playerPos, _layout.bossBeacon);
            var near = beaconDist <= DiscoveryRadius;
            if (near && (_beacon == BeaconReveal.Hidden || (proximityAfterHints && _beacon < BeaconReveal.Discovered)))
                _beacon = BeaconReveal.Discovered;
            else if (minute >= exactMinute && (_beacon == BeaconReveal.Hidden || (exactAfterHint && _beacon == BeaconReveal.DirectionSector)))
                _beacon = BeaconReveal.ExactMarker;
            else if (_beacon < BeaconReveal.DirectionSector && minute >= sectorMinute)
                _beacon = BeaconReveal.DirectionSector;

            if (_beacon == previous) return;
            if (_beacon == BeaconReveal.Discovered && near && (previous == BeaconReveal.Hidden || proximityAfterHints))
                BeaconSpotted?.Invoke();
            ExplorationRevision++;
            BeaconChanged?.Invoke(_beacon);
        }

        /// <summary>Console action: the Beacon counts as discovered now (no toast).</summary>
        public void ForceDiscovered()
        {
            if (_beacon == BeaconReveal.Discovered) return;
            _beacon = BeaconReveal.Discovered;
            ExplorationRevision++;
            BeaconChanged?.Invoke(_beacon);
        }

        static long PackCell(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
