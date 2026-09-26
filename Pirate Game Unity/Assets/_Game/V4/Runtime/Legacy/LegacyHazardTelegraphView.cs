using System.Reflection;
using HighSeas.Combat;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Draws V3's pending ground hazards (Kraken trap circles, mines, nets from <see cref="SaltwindHazardTelegraph"/>),
    /// which V3 never shows: a trap was invisible until it exploded on the ship. Each hazard gets an outline ring and
    /// an inner ring that grows to the outline as the fuse runs down. Visual only: it reads the hazard slots in
    /// LateUpdate, owns no colliders and draws no random numbers, so gameplay and traces are unchanged.
    /// </summary>
    [DeletedInPhase(8, "V4 HazardTelegraphs and their presenter")]
    public sealed class LegacyHazardTelegraphView : MonoBehaviour
    {
        const int Segments = 40;
        const float Height = 0.15f;
        static readonly Color TrapColor = new Color(1f, 0.25f, 0.15f, 0.95f);
        static readonly Color MineColor = new Color(1f, 0.7f, 0.1f, 0.95f);
        static readonly Color NetColor = new Color(0.35f, 0.8f, 1f, 0.9f);

        static readonly FieldInfo SlotsField = typeof(SaltwindHazardTelegraph).GetField("Slots", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo _active, _kind, _center, _radius, _timer, _armed;

        LineRenderer[] _outline, _fill;
        float[] _fuseTotal;
        Vector3[] _lastCenter;
        bool[] _wasActive;
        Material _material;

        public static void Ensure()
        {
            if (SlotsField == null || FindFirstObjectByType<LegacyHazardTelegraphView>() != null) return;
            new GameObject("V4HazardTelegraphs").AddComponent<LegacyHazardTelegraphView>();
        }

        void Awake()
        {
            _material = new Material(Shader.Find("Sprites/Default"));
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
        }

        void LateUpdate()
        {
            if (!(SlotsField?.GetValue(null) is System.Array slots)) return;
            if (_outline == null || _outline.Length != slots.Length) Allocate(slots.Length);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots.GetValue(i);
                if (slot != null && _active == null) BindFields(slot.GetType());
                var active = slot != null && _active != null && (bool)_active.GetValue(slot);
                if (!active)
                {
                    _wasActive[i] = false;
                    Show(i, false);
                    continue;
                }

                var kind = _kind.GetValue(slot).ToString();
                var center = (Vector3)_center.GetValue(slot);
                var radius = (float)_radius.GetValue(slot);
                var timer = (float)_timer.GetValue(slot);
                if (!_wasActive[i] || center != _lastCenter[i]) _fuseTotal[i] = Mathf.Max(0.01f, timer); // a new hazard in this slot
                _wasActive[i] = true;
                _lastCenter[i] = center;

                var color = kind == "Mine" ? MineColor : kind == "NetSlow" ? NetColor : TrapColor;
                // Mines: fill while arming, then a full ring while armed. Traps: fill as the fuse runs down. Nets: full.
                var armed = kind == "Mine" && _armed != null && (bool)_armed.GetValue(slot);
                var progress = kind == "NetSlow" || armed ? 1f : 1f - Mathf.Clamp01(timer / _fuseTotal[i]);
                Show(i, true);
                SetCircle(_outline[i], center, radius, color, 0.3f);
                SetCircle(_fill[i], center, Mathf.Max(0.05f, radius * progress), new Color(color.r, color.g, color.b, 0.55f + 0.4f * progress), 0.45f);
            }
        }

        void BindFields(System.Type t)
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            _active = t.GetField("active", f);
            _kind = t.GetField("kind", f);
            _center = t.GetField("center", f);
            _radius = t.GetField("radius", f);
            _timer = t.GetField("timer", f);
            _armed = t.GetField("armed", f);
        }

        void Allocate(int count)
        {
            _outline = new LineRenderer[count];
            _fill = new LineRenderer[count];
            _fuseTotal = new float[count];
            _lastCenter = new Vector3[count];
            _wasActive = new bool[count];
            for (var i = 0; i < count; i++)
            {
                _outline[i] = CreateRing("HazardOutline" + i);
                _fill[i] = CreateRing("HazardFill" + i);
            }
        }

        LineRenderer CreateRing(string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = Segments;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = _material;
            child.SetActive(false);
            return line;
        }

        void Show(int i, bool visible)
        {
            if (_outline[i].gameObject.activeSelf != visible) _outline[i].gameObject.SetActive(visible);
            if (_fill[i].gameObject.activeSelf != visible) _fill[i].gameObject.SetActive(visible);
        }

        static void SetCircle(LineRenderer line, Vector3 center, float radius, Color color, float width)
        {
            line.startColor = color;
            line.endColor = color;
            line.widthMultiplier = width;
            for (var s = 0; s < Segments; s++)
            {
                var a = s * Mathf.PI * 2f / Segments;
                line.SetPosition(s, new Vector3(center.x + Mathf.Cos(a) * radius, Height, center.z + Mathf.Sin(a) * radius));
            }
        }
    }
}
