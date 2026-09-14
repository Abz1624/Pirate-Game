using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>World-space boundary rings that show playable water and storm edge.</summary>
    public sealed class V3StormEdgeVisual : MonoBehaviour
    {
        LineRenderer _playableRing;
        LineRenderer _stormRing;
        float _pulse;

        public static V3StormEdgeVisual Ensure()
        {
            var existing = FindFirstObjectByType<V3StormEdgeVisual>();
            if (existing != null)
            {
                existing.Refresh();
                return existing;
            }

            var go = new GameObject("V3StormEdgeVisual");
            return go.AddComponent<V3StormEdgeVisual>();
        }

        void Awake()
        {
            _playableRing = CreateRing("PlayableWaterEdge", new Color(0.35f, 0.82f, 0.98f, 0.42f), 0.28f);
            _stormRing = CreateRing("StormDamageEdge", new Color(0.62f, 0.22f, 0.95f, 0.72f), 0.42f);
            Refresh();
        }

        void Update()
        {
            if (_stormRing == null) return;
            _pulse += Time.deltaTime * 1.6f;
            var alpha = 0.55f + Mathf.Sin(_pulse) * 0.15f;
            var color = _stormRing.startColor;
            color.a = alpha;
            _stormRing.startColor = color;
            _stormRing.endColor = color;
        }

        public void Refresh()
        {
            if (_playableRing == null || _stormRing == null) return;

            SetRectRing(_playableRing, PlayBounds.StormEdgeMargin + 4f);
            SetRectRing(_stormRing, PlayBounds.StormEdgeMargin);
        }

        LineRenderer CreateRing(string name, Color color, float width)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);

            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.textureMode = LineTextureMode.Stretch;
            return line;
        }

        static void SetRectRing(LineRenderer line, float margin)
        {
            PlayBounds.GetStormRectCorners(margin, out var a, out var b, out var c, out var d);
            line.SetPosition(0, a);
            line.SetPosition(1, b);
            line.SetPosition(2, c);
            line.SetPosition(3, d);
        }
    }
}
