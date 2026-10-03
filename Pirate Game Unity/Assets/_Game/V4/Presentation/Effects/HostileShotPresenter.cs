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
    /// <para>Playtest (3 Oct, owner: enemy cannonballs looked like the player's): an enemy shot is no longer the player's
    /// cannonball model. It is a glowing danger orb built once here: a hot red-orange core, a translucent crimson glow
    /// around it and a dark red trail, so it reads as "dodge this" at a glance. Every shot shares the same three
    /// materials (no per-shot material instances).</para>
    /// </summary>
    public sealed class HostileShotPresenter : IHostileShotViews
    {
        public const string KeyPrefix = "hostile/";
        const int Prewarm = 24;
        const float GlowScale = 1.75f;
        const float TrailWidth = 0.7f; // of the shot's diameter

        static readonly Color CoreColor = new Color(1f, 0.32f, 0.08f, 1f) * 2.2f; // HDR: blooms where bloom is on
        static readonly Color GlowColor = new Color(1f, 0.12f, 0.3f, 0.38f);
        static readonly Color TrailHead = new Color(0.85f, 0.12f, 0.08f, 0.85f);
        static readonly Color TrailTail = new Color(0.18f, 0.02f, 0.06f, 0f);

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
            var size = Mathf.Max(0.2f, radius * 2f * sizeMult);
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

        /// <summary>The danger orb: a collider-less core sphere, a glow shell and a trail, as the pool template.</summary>
        GameObject BuildTemplate()
        {
            var root = new GameObject("HostileShotOrb");
            root.SetActive(false);

            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "Core";
            Object.DestroyImmediate(core.GetComponent<Collider>()); // before the pool clones the template
            core.transform.SetParent(root.transform, false);
            var coreRenderer = core.GetComponent<MeshRenderer>();
            coreRenderer.sharedMaterial = Unlit(CoreColor);
            coreRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            glow.name = "Glow";
            Object.DestroyImmediate(glow.GetComponent<Collider>());
            glow.transform.SetParent(root.transform, false);
            glow.transform.localScale = Vector3.one * GlowScale;
            var glowRenderer = glow.GetComponent<MeshRenderer>();
            glowRenderer.sharedMaterial = Translucent(GlowColor);
            glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glowRenderer.receiveShadows = false;

            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = Translucent(Color.white);
            trail.time = 0.32f;
            trail.minVertexDistance = 0.12f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f));
            trail.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(TrailHead, 0f), new GradientColorKey(TrailTail, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(TrailHead.a, 0f), new GradientAlphaKey(0f, 1f) },
            };
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.emitting = false;
            return root;
        }

        Material Unlit(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            var m = new Material(shader) { name = "HostileShotCore", hideFlags = HideFlags.DontSave };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            _materials.Add(m);
            return m;
        }

        Material Translucent(Color color)
        {
            var m = new Material(Shader.Find("Sprites/Default")) { name = "HostileShotGlow", hideFlags = HideFlags.DontSave, color = color };
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
