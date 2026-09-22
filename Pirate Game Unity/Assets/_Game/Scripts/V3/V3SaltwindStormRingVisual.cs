using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>World-space safe-region ring aligned with storm damage boundary.</summary>
    public sealed class V3SaltwindStormRingVisual : MonoBehaviour
    {
        static readonly Color RingColor = new(0.35f, 0.08f, 0.55f, 0.72f);

        public static V3SaltwindStormRingVisual Instance { get; private set; }

        LineRenderer _ring;

        public static V3SaltwindStormRingVisual Ensure()
        {
            if (Instance != null)
            {
                Instance.gameObject.SetActive(true);
                Instance.Refresh();
                return Instance;
            }

            var go = new GameObject("V3SaltwindStormRing");
            return go.AddComponent<V3SaltwindStormRingVisual>();
        }

        public static void Disable()
        {
            if (Instance == null) return;
            Instance.gameObject.SetActive(false);
        }

        void Awake()
        {
            Instance = this;
            _ring = CreateRing();
            Refresh();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Refresh()
        {
            if (_ring == null) return;
            PlayBounds.GetStormRectCorners(PlayBounds.StormEdgeMargin, out var a, out var b, out var c, out var d);
            _ring.SetPosition(0, a);
            _ring.SetPosition(1, b);
            _ring.SetPosition(2, c);
            _ring.SetPosition(3, d);
        }

        LineRenderer CreateRing()
        {
            var child = new GameObject("StormSafeRing");
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = 0.5f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = RingColor;
            line.endColor = RingColor;
            return line;
        }
    }
}
