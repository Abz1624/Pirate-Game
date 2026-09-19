using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>
    /// Spawns four <see cref="V3PlayBoundsWallSegment"/> prefabs on playable bounds; fades them in near edges.
    /// </summary>
    public sealed class V3PlayBoundsWallVisual : MonoBehaviour
    {
        const string DefaultPrefabResource = "V3PlayBoundsWallSegment";

        [Header("Wall prefab")]
        [Tooltip("Boundary panel prefab (color/texture/scroll live on V3PlayBoundsWallSegment).")]
        public V3PlayBoundsWallSegment wallSegmentPrefab;

        [Header("Proximity fade")]
        [Tooltip("Walls begin appearing when the ship is within this many meters of an edge.")]
        public float showDistance = 32f;

        [Tooltip("World-units fade width from showDistance down to the edge.")]
        public float fadeWidth = 14f;

        [Header("Geometry")]
        public float wallHeight = 14f;
        public float wallThickness = 1.2f;
        public float wallYOffset = 0f;

        WallSlot _west;
        WallSlot _east;
        WallSlot _south;
        WallSlot _north;
        PlayerShipController _player;
        float _lastHalfX = -1f;
        float _lastHalfZ = -1f;
        Vector3 _lastCenter = new(float.NaN, 0f, float.NaN);

        sealed class WallSlot
        {
            public V3PlayBoundsWallSegment Segment;
            public Transform Root;
            public Vector3 OutwardNormal;
        }

        public static V3PlayBoundsWallVisual Ensure()
        {
            var existing = FindFirstObjectByType<V3PlayBoundsWallVisual>();
            if (existing != null)
                return existing;

            var go = new GameObject("V3PlayBoundsWalls");
            return go.AddComponent<V3PlayBoundsWallVisual>();
        }

        void Awake()
        {
            ResolvePrefab();
            _west = CreateWall("WestWall", Vector3.left);
            _east = CreateWall("EastWall", Vector3.right);
            _south = CreateWall("SouthWall", Vector3.back);
            _north = CreateWall("NorthWall", Vector3.forward);
            RefreshLayout();
        }

        void Start() => RefreshLayoutIfBoundsChanged(force: true);

        void ResolvePrefab()
        {
            if (wallSegmentPrefab != null)
                return;

            var resource = Resources.Load<GameObject>(DefaultPrefabResource);
            if (resource != null)
                wallSegmentPrefab = resource.GetComponent<V3PlayBoundsWallSegment>();
        }

        void Update()
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();

            if (Vector3.Distance(_lastCenter, PlayBounds.Center) > 0.05f ||
                !Mathf.Approximately(_lastHalfX, PlayBounds.HalfExtentX) ||
                !Mathf.Approximately(_lastHalfZ, PlayBounds.HalfExtentZ))
                RefreshLayoutIfBoundsChanged(force: true);

            if (_player == null)
            {
                SetWallFade(_west, 0f);
                SetWallFade(_east, 0f);
                SetWallFade(_south, 0f);
                SetWallFade(_north, 0f);
                return;
            }

            var local = _player.Position - PlayBounds.Center;
            local.y = 0f;
            var halfX = PlayBounds.HalfExtentX;
            var halfZ = PlayBounds.HalfExtentZ;

            SetWallFade(_west, ComputeEdgeFade(local.x + halfX));
            SetWallFade(_east, ComputeEdgeFade(halfX - local.x));
            SetWallFade(_south, ComputeEdgeFade(local.z + halfZ));
            SetWallFade(_north, ComputeEdgeFade(halfZ - local.z));
        }

        float ComputeEdgeFade(float clearanceToEdge)
        {
            if (clearanceToEdge >= showDistance)
                return 0f;

            var ramp = (showDistance - clearanceToEdge) / Mathf.Max(0.5f, fadeWidth);
            return Mathf.Clamp01(ramp);
        }

        void RefreshLayoutIfBoundsChanged(bool force = false)
        {
            var center = PlayBounds.Center;
            if (!force &&
                Mathf.Approximately(_lastHalfX, PlayBounds.HalfExtentX) &&
                Mathf.Approximately(_lastHalfZ, PlayBounds.HalfExtentZ) &&
                Vector3.Distance(_lastCenter, center) < 0.05f)
                return;

            RefreshLayout();
        }

        void RefreshLayout()
        {
            _lastHalfX = PlayBounds.HalfExtentX;
            _lastHalfZ = PlayBounds.HalfExtentZ;
            _lastCenter = PlayBounds.Center;

            var center = PlayBounds.Center;
            var halfX = PlayBounds.HalfExtentX;
            var halfZ = PlayBounds.HalfExtentZ;
            var y = center.y + wallYOffset + wallHeight * 0.5f;
            var outward = wallThickness * 0.5f;

            PlaceWall(_west, new Vector3(center.x - halfX - outward, y, center.z), new Vector3(halfZ * 2f, wallHeight, wallThickness));
            PlaceWall(_east, new Vector3(center.x + halfX + outward, y, center.z), new Vector3(halfZ * 2f, wallHeight, wallThickness));
            PlaceWall(_south, new Vector3(center.x, y, center.z - halfZ - outward), new Vector3(halfX * 2f, wallHeight, wallThickness));
            PlaceWall(_north, new Vector3(center.x, y, center.z + halfZ + outward), new Vector3(halfX * 2f, wallHeight, wallThickness));
        }

        static void PlaceWall(WallSlot wall, Vector3 position, Vector3 scale)
        {
            if (wall?.Root == null) return;
            wall.Root.position = position;
            wall.Root.rotation = Quaternion.LookRotation(wall.OutwardNormal.normalized, Vector3.up);
            wall.Root.localScale = scale;
        }

        static void SetWallFade(WallSlot wall, float fade01)
        {
            wall?.Segment?.SetProximityFade(fade01);
        }

        WallSlot CreateWall(string name, Vector3 outwardNormal)
        {
            V3PlayBoundsWallSegment segment;
            if (wallSegmentPrefab != null)
            {
                segment = Instantiate(wallSegmentPrefab, transform);
            }
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
                var col = go.GetComponent<Collider>();
                if (col != null)
                    Destroy(col);
                segment = go.AddComponent<V3PlayBoundsWallSegment>();
            }

            segment.name = name;
            outwardNormal.Normalize();
            segment.FaceOutward(outwardNormal);
            segment.SetProximityFade(0f);
            return new WallSlot
            {
                Segment = segment,
                Root = segment.transform,
                OutwardNormal = outwardNormal
            };
        }
    }
}
