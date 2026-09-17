using HighSeas.World;
using UnityEngine;

namespace HighSeas.V3
{
    /// <summary>Legacy world-space boundary rings (disabled in normal play).</summary>
    public sealed class V3StormEdgeVisual : MonoBehaviour
    {
        static readonly Color PlayableEdgeColor = new(0.62f, 0.08f, 0.1f, 0.5f);
        static readonly Color StormEdgeColor = new(0.22f, 0.04f, 0.38f, 0.78f);

        LineRenderer _playableRing;
        LineRenderer _stormRing;
        float _pulse;

        public static V3StormEdgeVisual Ensure()
        {
            DisableWorldRings();
            return null;
        }

        public static void DisableWorldRings()
        {
            var visuals = FindObjectsByType<V3StormEdgeVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (var i = 0; i < visuals.Length; i++)
            {
                if (visuals[i] != null)
                    visuals[i].gameObject.SetActive(false);
            }
        }

        void Awake()
        {
            _playableRing = CreateRing("PlayableWaterEdge", PlayableEdgeColor, 0.28f);
            _stormRing = CreateRing("StormDamageEdge", StormEdgeColor, 0.42f);
            Refresh();
            gameObject.SetActive(false);
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
