using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Presentation.World
{
    /// <summary>
    /// The four play-area edge walls (M14.9b; port of V3's V3PlayBoundsWallVisual): the wall panel prefab on each edge of
    /// the play bounds, fading in as the ship comes within 32 m of that edge (14 m ramp). Bounds from the world view, the
    /// ship from the ship view; the panels are created once at bind.
    /// </summary>
    public sealed class BoundsWallPresenter : MonoBehaviour
    {
        const string DefaultPrefabResource = "V3PlayBoundsWallSegment";
        const float ShowDistance = 32f;
        const float FadeWidth = 14f;
        const float WallHeight = 14f;
        const float WallThickness = 1.2f;

        sealed class Wall
        {
            public PlayBoundsWallSegment Segment;
            public Vector3 OutwardNormal;
        }

        IWorldView _world;
        IShipView _ship;
        Wall _west, _east, _south, _north;
        float _lastHalfX = -1f, _lastHalfZ = -1f;
        Vector3 _lastCenter = new Vector3(float.NaN, 0f, float.NaN);

        public void Bind(IWorldView world, IShipView ship)
        {
            _world = world;
            _ship = ship;
            if (_west == null)
            {
                var resource = Resources.Load<GameObject>(DefaultPrefabResource);
                var prefab = resource != null ? resource.GetComponent<PlayBoundsWallSegment>() : null;
                _west = CreateWall("WestWall", Vector3.left, prefab);
                _east = CreateWall("EastWall", Vector3.right, prefab);
                _south = CreateWall("SouthWall", Vector3.back, prefab);
                _north = CreateWall("NorthWall", Vector3.forward, prefab);
            }
            _lastHalfX = -1f;
        }

        /// <summary>Whether the wall on an edge (0 west, 1 east, 2 south, 3 north) is showing.</summary>
        public bool WallShown(int edge)
        {
            var wall = edge switch { 0 => _west, 1 => _east, 2 => _south, _ => _north };
            return wall != null && wall.Segment != null && wall.Segment.gameObject.activeSelf;
        }

        void Update()
        {
            if (_world == null || _west == null) return;
            var center = _world.BoundsCenter;
            var halfX = _world.BoundsHalfX;
            var halfZ = _world.BoundsHalfZ;
            if (Vector3.Distance(_lastCenter, center) > 0.05f || !Mathf.Approximately(_lastHalfX, halfX) || !Mathf.Approximately(_lastHalfZ, halfZ))
                Layout(center, halfX, halfZ);

            if (_ship == null || !_ship.Exists)
            {
                SetFade(_west, 0f);
                SetFade(_east, 0f);
                SetFade(_south, 0f);
                SetFade(_north, 0f);
                return;
            }

            var local = _ship.Position - center;
            SetFade(_west, EdgeFade(local.x + halfX));
            SetFade(_east, EdgeFade(halfX - local.x));
            SetFade(_south, EdgeFade(local.z + halfZ));
            SetFade(_north, EdgeFade(halfZ - local.z));
        }

        static float EdgeFade(float clearance) =>
            clearance >= ShowDistance ? 0f : Mathf.Clamp01((ShowDistance - clearance) / Mathf.Max(0.5f, FadeWidth));

        void Layout(Vector3 center, float halfX, float halfZ)
        {
            _lastHalfX = halfX;
            _lastHalfZ = halfZ;
            _lastCenter = center;
            var y = center.y + WallHeight * 0.5f;
            var outward = WallThickness * 0.5f;
            Place(_west, new Vector3(center.x - halfX - outward, y, center.z), new Vector3(halfZ * 2f, WallHeight, WallThickness));
            Place(_east, new Vector3(center.x + halfX + outward, y, center.z), new Vector3(halfZ * 2f, WallHeight, WallThickness));
            Place(_south, new Vector3(center.x, y, center.z - halfZ - outward), new Vector3(halfX * 2f, WallHeight, WallThickness));
            Place(_north, new Vector3(center.x, y, center.z + halfZ + outward), new Vector3(halfX * 2f, WallHeight, WallThickness));
        }

        static void Place(Wall wall, Vector3 position, Vector3 scale)
        {
            var t = wall.Segment.transform;
            t.position = position;
            t.rotation = Quaternion.LookRotation(wall.OutwardNormal, Vector3.up);
            t.localScale = scale;
        }

        static void SetFade(Wall wall, float fade) => wall.Segment.SetProximityFade(fade);

        Wall CreateWall(string name, Vector3 outwardNormal, PlayBoundsWallSegment prefab)
        {
            PlayBoundsWallSegment segment;
            if (prefab != null) segment = Instantiate(prefab, transform);
            else
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(transform, false);
                var collider = go.GetComponent<Collider>();
                if (collider != null) DestroyImmediate(collider);
                segment = go.AddComponent<PlayBoundsWallSegment>();
            }
            segment.name = name;
            segment.FaceOutward(outwardNormal);
            segment.SetProximityFade(0f);
            return new Wall { Segment = segment, OutwardNormal = outwardNormal.normalized };
        }
    }
}
