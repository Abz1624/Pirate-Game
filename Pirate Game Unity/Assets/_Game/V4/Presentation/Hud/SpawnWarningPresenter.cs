using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Events;
using RoguePirates.V4.Views;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Spawn warnings (M6): port of V3SpawnTelegraph + V3SpawnWarningHud + V3SpawnEdgeIndicator, driven by Spawned
    /// events flagged ambient. For a Captain- or General-tier ambient spawn: a toast ("Captain incoming — watch the
    /// horizon" / "Heavy contact approaching", at most one per 1.5 s), a 3 s minimap ping and a 2.5 s screen-edge chevron
    /// toward the newest off-screen spawn. Soldier-tier spawns and scripted spawns (miniboss, elite beats, boss) never
    /// telegraphed in V3 and still don't.
    /// </summary>
    public sealed class SpawnWarningPresenter : MonoBehaviour
    {
        const float ToastCooldown = 1.5f;
        const float ToastSeconds = 2.5f;
        const float PingSeconds = 3f;
        const float EdgeSeconds = 2.5f;

        struct Ping
        {
            public Vector3 world;
            public float expires;
        }

        readonly List<Ping> _edgePings = new List<Ping>(8);
        Image _arrow;
        IRunViews _views;
        ICombatEventReader _events;
        MinimapPresenter _minimap;
        float _lastWarnTime = float.NegativeInfinity;

        public int WarningsShown { get; private set; }
        public bool EdgeArrowVisible => _arrow != null && _arrow.gameObject.activeSelf;

        public static SpawnWarningPresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<SpawnWarningPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4SpawnWarnings", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var presenter = go.AddComponent<SpawnWarningPresenter>();
            presenter.BuildArrow();
            return presenter;
        }

        public void Bind(IRunViews views, ICombatEventReader events, MinimapPresenter minimap)
        {
            if (_events != null) _events.TickPublished -= OnEvents;
            _views = views;
            _events = events;
            _minimap = minimap;
            if (_events != null) _events.TickPublished += OnEvents;
        }

        void OnDestroy()
        {
            if (_events != null) _events.TickPublished -= OnEvents;
        }

        /// <summary>V3 made four chevrons and only ever showed the first; one is enough.</summary>
        void BuildArrow()
        {
            _arrow = new GameObject("Arrow0", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            _arrow.transform.SetParent(transform, false);
            _arrow.color = new Color(1f, 0.75f, 0.35f, 0.9f);
            _arrow.raycastTarget = false;
            _arrow.rectTransform.sizeDelta = new Vector2(28f, 28f);
            _arrow.gameObject.SetActive(false);
        }

        void OnEvents(IReadOnlyList<CombatEvent> batch)
        {
            for (var i = 0; i < batch.Count; i++)
            {
                var e = batch[i];
                if (e.Kind == CombatEventKind.Warning && (e.Flags & CombatEventFlags.HordeWarning) != 0)
                {
                    NotifyHorde(new Vector3(e.X, e.Y, e.Z));
                    continue;
                }
                if (e.Kind != CombatEventKind.Spawned || (e.Flags & CombatEventFlags.AmbientSpawn) == 0) continue;
                Notify(new Vector3(e.X, e.Y, e.Z), e.B);
            }
        }

        /// <summary>V3SpawnTelegraph.NotifyAmbientSpawn.</summary>
        public void Notify(Vector3 position, int definitionIndex)
        {
            var enemies = _views?.Enemies;
            if (enemies == null || !enemies.IsKnownDefinition(definitionIndex)) return;
            var tier = enemies.TierOf(definitionIndex);
            if (tier == EnemyTier.Soldier) return;

            var label = tier switch
            {
                EnemyTier.Captain => "Captain incoming — watch the horizon",
                EnemyTier.General => "Heavy contact approaching",
                _ => "Enemy incoming"
            };
            if (Time.unscaledTime - _lastWarnTime >= ToastCooldown)
            {
                _lastWarnTime = Time.unscaledTime;
                WarningsShown++;
                _views.Toasts?.Show(label, ToastSeconds);
            }
            if (_minimap != null) _minimap.RegisterSpawnPing(position, PingSeconds);
            _edgePings.Add(new Ping { world = position, expires = Time.unscaledTime + EdgeSeconds });
        }

        /// <summary>V5 M1: a horde is coming from <paramref name="position"/>: edge marker and minimap ping (its toast comes from the flow).</summary>
        public void NotifyHorde(Vector3 position)
        {
            HordeWarningsShown++;
            if (_minimap != null) _minimap.RegisterSpawnPing(position, HordeSeconds);
            _edgePings.Add(new Ping { world = position, expires = Time.unscaledTime + HordeSeconds });
        }

        public int HordeWarningsShown { get; private set; }
        const float HordeSeconds = 3.5f;

        void LateUpdate()
        {
            var cam = Camera.main;
            var ship = _views?.Ship;
            if (cam == null || ship == null || !ship.Exists || _arrow == null)
            {
                SetArrow(false);
                return;
            }

            var now = Time.unscaledTime;
            for (var i = _edgePings.Count - 1; i >= 0; i--)
                if (_edgePings[i].expires <= now) _edgePings.RemoveAt(i);
            if (_edgePings.Count == 0)
            {
                SetArrow(false);
                return;
            }

            var latest = _edgePings[_edgePings.Count - 1].world;
            var viewport = cam.WorldToViewportPoint(latest);
            if (viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f)
            {
                SetArrow(false);
                return;
            }

            var dir = latest - ship.Position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
            {
                SetArrow(false);
                return;
            }

            var screen = cam.WorldToScreenPoint(ship.Position + dir.normalized * 12f);
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var edge = center + (new Vector2(screen.x, screen.y) - center).normalized * Mathf.Min(Screen.width, Screen.height) * 0.42f;
            edge.x = Mathf.Clamp(edge.x, 40f, Screen.width - 40f);
            edge.y = Mathf.Clamp(edge.y, 40f, Screen.height - 40f);
            SetArrow(true);
            _arrow.rectTransform.position = edge;
            var angle = Mathf.Atan2(screen.y - center.y, screen.x - center.x) * Mathf.Rad2Deg;
            _arrow.rectTransform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        }

        void SetArrow(bool on)
        {
            if (_arrow != null && _arrow.gameObject.activeSelf != on) _arrow.gameObject.SetActive(on);
        }
    }
}
