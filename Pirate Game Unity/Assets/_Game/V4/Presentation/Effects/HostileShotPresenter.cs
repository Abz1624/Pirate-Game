using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Presentation.Pooling;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Enemy shots (M14.9b; port of V3's ProjectileViewPool as the hostile shots used it: V3's scale formula) through the
    /// V4 view pool, key "hostile/&lt;model&gt;", prewarmed at bind. The impact splash is the weapon presenter's water splash
    /// (V3 shared the broker's splash and its merge window).
    /// <para>Playtest (3 Oct, owner, round 7: the glowing red-orange orbs "look like powerups and are easy to miss"): an
    /// enemy shot is a dark iron cannonball (a little larger than the player's) trailing bright orange-red fire and smoke,
    /// so its path reads as danger (owner: no red marker under it). Every shot shares the same two materials (no
    /// per-shot material instances).</para>
    /// </summary>
    public sealed class HostileShotPresenter : IHostileShotViews
    {
        public const string KeyPrefix = "hostile/";
        const int Prewarm = 24;
        const float SizeBoost = 1.6f;   // of the old size: a little larger than the player's cannonball
        const float MinSize = 0.45f;
        const float TrailWidth = 0.8f;  // of the ball's diameter

        static readonly Color IronColor = new Color(0.09f, 0.09f, 0.1f, 1f);
        static readonly Color TrailHead = new Color(1f, 0.55f, 0.12f, 1f);   // fire at the ball
        static readonly Color TrailMid = new Color(1f, 0.2f, 0.05f, 0.85f);  // red-orange
        static readonly Color TrailTail = new Color(0.25f, 0.22f, 0.2f, 0f); // grey smoke fading out

        readonly PoolService _pools;
        readonly DemoJuiceSettings _settings;
        readonly Action<Vector3> _splash;
        readonly string _key;
        readonly List<Material> _materials = new List<Material>(3);
        readonly Dictionary<int, TrailRenderer> _trails = new Dictionary<int, TrailRenderer>();
        GameObject _template;

        public int LiveCount { get; private set; }

        public HostileShotPresenter(PoolService pools, DemoJuiceSettings settings, Action<Vector3> splash)
        {
            _pools = pools;
            _settings = settings;
            _splash = splash;
            _template = BuildTemplate();
            if (_pools != null) _template.transform.SetParent(_pools.Staging, false);
            _key = KeyPrefix + _template.name;
            if (_pools != null && !_pools.Has(_key))
            {
                _pools.Register(new PoolDefinition(_key, _template, Prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow, "hostile shots"));
                _pools.Prewarm(_key);
            }
        }

        /// <summary>V3 ProjectileViewPool.Acquire (no override prefab, scale x1). The colour is the shooter's shot tint,
        /// which the danger orb no longer shows (every enemy shot reads the same).</summary>
        public Transform Acquire(Vector3 position, Color color, float visualRadius)
        {
            var go = _pools?.Acquire(_key, position, Quaternion.identity);
            if (go == null) return null;
            var sizeMult = _settings != null ? _settings.projectileSizeMultiplier : 1.2f;
            var radius = visualRadius * (_settings != null ? _settings.projectileRadiusMultiplier : 1f);
            var size = Mathf.Max(MinSize, radius * 2f * sizeMult * SizeBoost);
            go.transform.localScale = Vector3.one * size;
            var trail = Trail(go);
            if (trail != null)
            {
                trail.Clear(); // a reused shot does not draw a line from where its last life ended
                trail.widthMultiplier = size * TrailWidth;
                trail.emitting = true;
            }
            LiveCount++;
            return go.transform;
        }

        public void Release(Transform view)
        {
            if (view == null) return;
            var trail = Trail(view.gameObject);
            if (trail != null) { trail.emitting = false; trail.Clear(); }
            _pools?.Release(view.gameObject);
            LiveCount = Mathf.Max(0, LiveCount - 1);
        }

        public void Splash(Vector3 position) => _splash?.Invoke(position);

        public void Dispose()
        {
            if (_template != null) Remove(_template);
            _template = null;
            foreach (var m in _materials) if (m != null) Remove(m);
            _materials.Clear();
            _trails.Clear();
        }

        TrailRenderer Trail(GameObject go)
        {
            var id = go.GetInstanceID();
            if (!_trails.TryGetValue(id, out var trail))
                _trails[id] = trail = go.GetComponentInChildren<TrailRenderer>(true);
            return trail;
        }

        /// <summary>The iron ball: a collider-less sphere, its fire-and-smoke trail, as the pool template.</summary>
        GameObject BuildTemplate()
        {
            var root = new GameObject("HostileCannonball");
            root.SetActive(false);

            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Iron";
            Object.DestroyImmediate(ball.GetComponent<Collider>()); // before the pool clones the template
            ball.transform.SetParent(root.transform, false);
            var ballRenderer = ball.GetComponent<MeshRenderer>();
            ballRenderer.sharedMaterial = Iron();
            ballRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Translucent(Color.white, "HostileShotTrail");
            trail.time = 0.5f;
            trail.minVertexDistance = 0.1f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.4f, 0.75f), new Keyframe(1f, 0.25f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(TrailHead, 0f), new GradientColorKey(TrailMid, 0.35f), new GradientColorKey(TrailTail, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(TrailMid.a, 0.35f), new GradientAlphaKey(0f, 1f) },
            };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
            return root;
        }

        /// <summary>Dark iron with a little shine (lit, so it reads as a solid ball, not a glowing pickup).</summary>
        Material Iron()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { name = "HostileShotIron", hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", IronColor);
            if (m.HasProperty("_Color")) m.SetColor("_Color", IronColor);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.7f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.55f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.55f);
            _materials.Add(m);
            return m;
        }

        Material Translucent(Color color, string name)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { name = name, hideFlags = HideFlags.DontSave, color = color };
            _materials.Add(m);
            return m;
        }

        /// <summary>Objects go at once in edit mode (tests), at the end of the frame in play.</summary>
        static void Remove(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
