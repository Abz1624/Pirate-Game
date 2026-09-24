using System.Collections.Generic;
using HighSeas.Player;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Screen-edge chevrons for short-lived off-screen spawn hints.</summary>
    public sealed class V3SpawnEdgeIndicator : MonoBehaviour
    {
        struct Ping
        {
            public Vector3 world;
            public float expires;
        }

        public static V3SpawnEdgeIndicator Instance { get; private set; }

        static readonly List<Ping> Pings = new(8);
        readonly List<Image> _arrows = new(4);

        void Awake()
        {
            Instance = this;
            EnsureArrows();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void Notify(Vector3 worldPosition, float durationSeconds = 2.5f)
        {
            Pings.Add(new Ping { world = worldPosition, expires = Time.unscaledTime + durationSeconds });
            if (Instance == null)
            {
                var canvas = Object.FindFirstObjectByType<Canvas>();
                if (canvas == null) return;
                var go = new GameObject("V3SpawnEdgeIndicator", typeof(RectTransform));
                go.transform.SetParent(canvas.transform, false);
                go.AddComponent<V3SpawnEdgeIndicator>();
            }
        }

        void EnsureArrows()
        {
            if (_arrows.Count > 0) return;
            var rt = gameObject.GetComponent<RectTransform>();
            if (rt == null) rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            for (var i = 0; i < 4; i++)
            {
                var img = new GameObject($"Arrow{i}", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(transform, false);
                img.color = new Color(1f, 0.75f, 0.35f, 0.9f);
                img.raycastTarget = false;
                var art = img.rectTransform;
                art.sizeDelta = new Vector2(28f, 28f);
                _arrows.Add(img);
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            var player = Object.FindFirstObjectByType<PlayerShipController>();
            if (cam == null || player == null)
            {
                HideAll();
                return;
            }

            var now = Time.unscaledTime;
            for (var i = Pings.Count - 1; i >= 0; i--)
            {
                if (Pings[i].expires <= now)
                    Pings.RemoveAt(i);
            }

            if (Pings.Count == 0)
            {
                HideAll();
                return;
            }

            var latest = Pings[Pings.Count - 1].world;
            var viewport = cam.WorldToViewportPoint(latest);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
            {
                HideAll();
                return;
            }

            var dir = latest - player.Position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                HideAll();
                return;
            }

            var screen = cam.WorldToScreenPoint(player.Position + dir.normalized * 12f);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var edge = center + (new Vector2(screen.x, screen.y) - center).normalized * Mathf.Min(Screen.width, Screen.height) * 0.42f;
            edge.x = Mathf.Clamp(edge.x, 40f, Screen.width - 40f);
            edge.y = Mathf.Clamp(edge.y, 40f, Screen.height - 40f);

            EnsureArrows();
            for (var i = 0; i < _arrows.Count; i++)
                _arrows[i].gameObject.SetActive(i == 0);
            var arrow = _arrows[0];
            arrow.rectTransform.position = edge;
            var angle = Mathf.Atan2(screen.y - center.y, screen.x - center.x) * Mathf.Rad2Deg;
            arrow.rectTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        void HideAll()
        {
            for (var i = 0; i < _arrows.Count; i++)
                _arrows[i].gameObject.SetActive(false);
        }
    }
}
