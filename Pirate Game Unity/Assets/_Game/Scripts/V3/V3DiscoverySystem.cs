using System.Collections.Generic;
using HighSeas.Balance;
using HighSeas.Core;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    public enum BeaconRevealState
    {
        Hidden,
        DirectionSector,
        ExactMarker,
        Discovered
    }

    /// <summary>
    /// Minimap-only exploration and discovery state. Tracks explored cells and POI visibility for
    /// <see cref="V3MinimapHud"/> — it does not hide or fog the 3D game world.
    /// </summary>
    public sealed class V3DiscoverySystem : MonoBehaviour
    {
        public const float DiscoveryRadius = 24f;
        public const float ExploreCellSize = 6f;
        public const float SectorHintMinute = 7f;
        public const float ExactMarkerMinute = 9f;

        static V3DiscoverySystem _instance;
        public static V3DiscoverySystem Instance => _instance;

        readonly HashSet<long> _exploredCells = new();
        readonly HashSet<int> _discoveredPoiIndices = new();
        readonly HashSet<int> _revealedChestIndices = new();

        StageLayout _layout;
        BeaconRevealState _beaconState = BeaconRevealState.Hidden;
        PlayerShipController _player;

        public BeaconRevealState BeaconState => _beaconState;
        public IReadOnlyCollection<int> DiscoveredPoiIndices => _discoveredPoiIndices;
        public IReadOnlyCollection<int> RevealedChestIndices => _revealedChestIndices;
        /// <summary>Bumps when new exploration cells are revealed (minimap fog rebuild trigger).</summary>
        public int ExplorationRevision { get; private set; }

        public static V3DiscoverySystem Ensure(StageLayout layout)
        {
            if (_instance != null)
            {
                _instance.Initialize(layout);
                return _instance;
            }

            var go = new GameObject("V3DiscoverySystem");
            _instance = go.AddComponent<V3DiscoverySystem>();
            _instance.Initialize(layout);
            return _instance;
        }

        public void Initialize(StageLayout layout)
        {
            _layout = layout;
            _exploredCells.Clear();
            _discoveredPoiIndices.Clear();
            _revealedChestIndices.Clear();
            _beaconState = BeaconRevealState.Hidden;
            ExplorationRevision = 0;
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        void Update()
        {
            if (_layout == null || RunManager.Instance == null) return;
            if (RunManager.Instance.IsPaused) return;

            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();
            if (_player == null) return;

            var pos = _player.Position;
            ExploreAround(pos, 10f);
            DiscoverNearbyPois(pos);
            UpdateBeaconTimeline(pos, RunManager.Instance.ElapsedSeconds / 60f);
        }

        public void ExploreAround(Vector3 worldPos, float radius)
        {
            var cellRadius = Mathf.CeilToInt(radius / ExploreCellSize);
            var cx = Mathf.FloorToInt(worldPos.x / ExploreCellSize);
            var cz = Mathf.FloorToInt(worldPos.z / ExploreCellSize);
            for (var x = -cellRadius; x <= cellRadius; x++)
            {
                for (var z = -cellRadius; z <= cellRadius; z++)
                {
                    var wx = (cx + x) * ExploreCellSize + ExploreCellSize * 0.5f;
                    var wz = (cz + z) * ExploreCellSize + ExploreCellSize * 0.5f;
                    var dx = wx - worldPos.x;
                    var dz = wz - worldPos.z;
                    if (dx * dx + dz * dz > radius * radius)
                        continue;
                    if (_exploredCells.Add(PackCell(cx + x, cz + z)))
                        ExplorationRevision++;
                }
            }
        }

        public bool IsCellExplored(Vector3 worldPos)
        {
            var cx = Mathf.FloorToInt(worldPos.x / ExploreCellSize);
            var cz = Mathf.FloorToInt(worldPos.z / ExploreCellSize);
            return _exploredCells.Contains(PackCell(cx, cz));
        }

        public bool IsPoiVisible(int index)
        {
            if (_layout == null || index < 0 || index >= _layout.pois.Count)
                return false;

            var poi = _layout.pois[index];
            if (poi.used)
                return false;

            if (poi.type == POIType.BossBeacon)
                return _beaconState >= BeaconRevealState.Discovered;

            if (poi.type == POIType.SalvageChest)
                return _revealedChestIndices.Contains(index) || _discoveredPoiIndices.Contains(index);

            return _discoveredPoiIndices.Contains(index);
        }

        public void RevealNearestChests(int count)
        {
            if (_layout == null || count <= 0) return;

            var player = _player != null ? _player.Position : Vector3.zero;
            var candidates = new List<(int index, float dist)>();
            for (var i = 0; i < _layout.pois.Count; i++)
            {
                var poi = _layout.pois[i];
                if (poi.type != POIType.SalvageChest || poi.used) continue;
                candidates.Add((i, Vector3.Distance(player, poi.position)));
            }

            candidates.Sort((a, b) => a.dist.CompareTo(b.dist));
            var revealCount = Mathf.Min(count, candidates.Count);
            for (var i = 0; i < revealCount; i++)
                _revealedChestIndices.Add(candidates[i].index);
        }

        public Vector3? GetBeaconPosition()
        {
            if (_layout == null) return null;
            return _layout.bossBeacon;
        }

        public float GetBeaconBearingDegrees(Vector3 from)
        {
            var beacon = GetBeaconPosition();
            if (!beacon.HasValue) return 0f;
            var delta = beacon.Value - from;
            delta.y = 0f;
            return Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
        }

        void DiscoverNearbyPois(Vector3 playerPos)
        {
            if (_layout?.pois == null) return;

            for (var i = 0; i < _layout.pois.Count; i++)
            {
                if (_discoveredPoiIndices.Contains(i)) continue;
                var poi = _layout.pois[i];
                if (poi.used) continue;

                if (poi.type == POIType.BossBeacon)
                {
                    if (_beaconState >= BeaconRevealState.Discovered)
                        _discoveredPoiIndices.Add(i);
                    continue;
                }

                if (Vector3.Distance(playerPos, poi.position) <= DiscoveryRadius)
                    _discoveredPoiIndices.Add(i);
            }
        }

        void UpdateBeaconTimeline(Vector3 playerPos, float minute)
        {
            if (_layout == null) return;

            var sectorMinute = SectorHintMinute;
            var exactMinute = ExactMarkerMinute;
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export?.Timing != null)
            {
                sectorMinute = StageBalanceProfileContext.Export.Timing.BeaconDirectionHintSeconds / 60f;
                exactMinute = StageBalanceProfileContext.Export.Timing.BeaconExactMarkerSeconds / 60f;
            }

            var previous = _beaconState;
            var beaconDist = Vector3.Distance(playerPos, _layout.bossBeacon);
            if (_beaconState == BeaconRevealState.Hidden && beaconDist <= DiscoveryRadius)
                _beaconState = BeaconRevealState.Discovered;
            else if (_beaconState == BeaconRevealState.Hidden && minute >= exactMinute)
                _beaconState = BeaconRevealState.ExactMarker;
            else if (_beaconState < BeaconRevealState.DirectionSector && minute >= sectorMinute)
                _beaconState = BeaconRevealState.DirectionSector;

            if (_beaconState != previous)
            {
                if (_beaconState == BeaconRevealState.Discovered &&
                    previous == BeaconRevealState.Hidden &&
                    beaconDist <= DiscoveryRadius)
                {
                    V3ToastHud.Instance?.Show("Treasury Beacon spotted — follow the guide.", 3.5f);
                }

                ExplorationRevision++;
            }
        }

        static long PackCell(int x, int z) => ((long)x << 32) ^ (uint)z;
    }
}
