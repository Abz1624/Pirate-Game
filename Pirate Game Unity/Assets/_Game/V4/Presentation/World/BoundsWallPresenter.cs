using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.World
{
    /// <summary>
    /// The four play-area edge walls (M14.9b; port of V3's V3PlayBoundsWallVisual): the wall panel prefab on each edge of
    /// the play bounds, fading in as the ship comes within 32 m of that edge (14 m ramp). Bounds from the world view, the
    /// ship from the ship view; the panels are created once at bind.
    /// Hackathon (owner, 13 Oct): the red walls stay hidden; instead a pulsing "Storm edge!" warning shows at the top
    /// of the screen while the ship is within <see cref="WarnDistance"/> of an edge (or beyond it, where the edge
    /// damage applies).
    /// </summary>
    public sealed class BoundsWallPresenter : MonoBehaviour
    {
        const string DefaultPrefabResource = "V3PlayBoundsWallSegment";
        const float ShowDistance = 32f;
        const float FadeWidth = 14f;
        const float WallHeight = 42f; // demo: 3x taller (was 14)
        const float WallThickness = 1.2f;
        static readonly bool ShowWalls = false;
        /// <summary>Metres inside the edge at which the warning appears.</summary>
        public const float WarnDistance = 10f;
        const string WarningText = "STORM EDGE! TURN BACK";

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
        GameObject _warningCanvas;
        TextMeshProUGUI _warning;

        /// <summary>Whether the storm edge warning is on screen, for checks.</summary>
        public bool WarningShown => _warningCanvas != null && _warningCanvas.activeSelf;

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
            if (_warningCanvas == null) CreateWarning();
            _warningCanvas.SetActive(false);
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
                SetWarning(false);
                return;
            }

            var local = _ship.Position - center;
            var west = local.x + halfX;
            var east = halfX - local.x;
            var south = local.z + halfZ;
            var north = halfZ - local.z;
            SetFade(_west, ShowWalls ? EdgeFade(west) : 0f);
            SetFade(_east, ShowWalls ? EdgeFade(east) : 0f);
            SetFade(_south, ShowWalls ? EdgeFade(south) : 0f);
            SetFade(_north, ShowWalls ? EdgeFade(north) : 0f);

            var clearance = Mathf.Min(Mathf.Min(west, east), Mathf.Min(south, north));
            SetWarning(halfX > 0f && halfZ > 0f && clearance < WarnDistance);
            if (_warning != null && _warningCanvas.activeSelf)
            {
                var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
                _warning.alpha = Mathf.Lerp(0.55f, 1f, pulse);
                _warning.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.08f, pulse);
            }
        }

        void SetWarning(bool on)
        {
            if (_warningCanvas != null && _warningCanvas.activeSelf != on) _warningCanvas.SetActive(on);
        }

        void CreateWarning()
        {
            _warningCanvas = new GameObject("StormEdgeWarning", typeof(RectTransform));
            _warningCanvas.transform.SetParent(transform, false);
            var canvas = _warningCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = _warningCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(_warningCanvas.transform, false);
            var rt = (RectTransform)textGo.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(1200f, 90f);
            rt.anchoredPosition = new Vector2(0f, -110f);
            _warning = textGo.AddComponent<TextMeshProUGUI>();
            _warning.text = WarningText;
            _warning.fontSize = 56f;
            _warning.fontStyle = FontStyles.Bold;
            _warning.color = new Color(1f, 0.32f, 0.25f, 1f);
            _warning.alignment = TextAlignmentOptions.Center;
            _warning.enableWordWrapping = false;
            _warning.raycastTarget = false;
            _warning.outlineWidth = 0.25f;
            _warning.outlineColor = new Color32(0, 0, 0, 255);
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
