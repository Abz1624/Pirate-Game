using System.Collections.Generic;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Pickups;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Progression;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Pickup models (M14.9b; port of V3's PickupViewPool and the view half of PickupWorld, was LegacyPickupViews): the
    /// prefab and scale per pickup type, barrels squashed and floated to their submerged depth from the model's renderer
    /// bounds (register R-74), XP gems (no coin sparkle since the hackathon playtest), the burst replayed on flying pickups, the spin, and
    /// the flying health billboard. Every model comes from the V4 view pool, keys "pickup/&lt;model&gt;", prewarmed at
    /// bind; the gem with its sparkle and the health billboard are built once as pool templates, so nothing is created
    /// while the run plays.
    /// </summary>
    public sealed class PickupPresenter : IPickupViews
    {
        public const string KeyPrefix = "pickup/";

        readonly PoolService _pools;
        readonly DemoJuiceSettings _s;
        readonly List<GameObject> _templates = new List<GameObject>();
        readonly Dictionary<GameObject, string> _keys = new Dictionary<GameObject, string>();
        readonly Dictionary<int, ParticleSystem[]> _burst = new Dictionary<int, ParticleSystem[]>();
        readonly HashSet<int> _prepared = new HashSet<int>();
        GameObject _gem, _flyingHealth, _primitive;

        public int LiveCount { get; private set; }

        public PickupPresenter(PoolService pools, DemoJuiceSettings settings)
        {
            _pools = pools;
            _s = settings;
            if (_s != null)
            {
                // Prewarm for peak load (M14.15: six weapons, ~70 enemies peaked at 101 XP, 75 gold and 15 health barrels).
                Register(_s.goldBarrelPickupPrefab != null ? _s.goldBarrelPickupPrefab : _s.goldPickupPrefab, 80);
                Register(_s.xpBarrelPickupPrefab != null ? _s.xpBarrelPickupPrefab : _s.infamyPickupPrefab, 112);
                Register(_s.goldPickupPrefab, 24);
                Register(_s.healthPickupPrefab, 16);
                Register(_s.infamyPickupPrefab, 4);
                var gemSource = _s.xpFlyingGemPrefab != null ? _s.xpFlyingGemPrefab : _s.infamyPickupPrefab;
                if (gemSource != null) _gem = RegisterTemplate(GemTemplate(gemSource), 64);
                _flyingHealth = RegisterTemplate(FlyingHealthTemplate(), 12);
            }
            else _primitive = RegisterTemplate(PrimitiveTemplate(), 16);
        }

        public object Acquire(PickupType type, Vector3 position, int value)
        {
            var prefab = PrefabFor(type);
            if (prefab == null || !_keys.TryGetValue(prefab, out var key)) return null;
            var go = _pools.Acquire(key, position, Quaternion.identity);
            if (go == null) return null;
            var view = go.transform;
            var firstUse = _prepared.Add(go.GetInstanceID());
            if (firstUse && prefab != _gem && prefab != _flyingHealth) StripColliders(go);
            if (firstUse && IsBarrel(type) && _s != null && !_s.barrelSparkles) RemoveSparkles(go); // V5 M8z (7 Oct, D21, Q16)

            if (prefab == _flyingHealth)
            {
                var billboard = go.GetComponent<FlyingHealthBillboard>();
                if (billboard != null)
                {
                    // V3 re-configured (a new material) on every use; once per pooled billboard is the same look.
                    if (firstUse) billboard.Configure(_s.healthBurstMaterial, _s.healthBurstColor);
                    billboard.ResetLifetime();
                }
                view.localScale = Vector3.one * _s.healthFlyingPickupScale;
            }
            else if (prefab == _primitive)
            {
                view.localScale = Vector3.one * 0.6375f;
                TintPrimitive(go, ColorFor(type));
            }
            else
            {
                var scale = ScaleFor(type);
                view.localScale = IsBarrel(type) ? new Vector3(scale, scale * _s.barrelVerticalScale, scale) : Vector3.one * scale;
                if (IsFlyingBurst(type)) PlayBurst(go);
            }
            UniformScale(type, view);
            if (firstUse) NoShadows(go); // after the billboard has made its renderer
            LiveCount++;
            return view;
        }

        /// <summary>V3 AlignBarrelSubmerged: the barrel floats with the configured fraction of its height under water.</summary>
        public Vector3 AlignBarrel(object view)
        {
            var t = (Transform)view;
            var submerged = _s != null ? _s.barrelSubmergedFraction : 0.15f;
            var waterY = _s != null ? _s.pickupWaterSurfaceY : 0f;
            if (!TryGetBounds(t, out var bounds)) return t.position;
            var aligned = t.position + Vector3.up * (waterY - submerged * bounds.size.y - bounds.min.y);
            t.position = aligned;
            return aligned;
        }

        /// <summary>V3 BeginHomingPickup: coins and gems take their flying scale.</summary>
        public void BeginHoming(object view, PickupType type)
        {
            var t = (Transform)view;
            if ((type == PickupType.Gold || type == PickupType.FlyingGold || type == PickupType.FlyingXp) && _s != null)
                t.localScale = Vector3.one * (type == PickupType.FlyingXp ? GemScale : _s.goldFlyingCoinScale);
            else if (type == PickupType.Infamy)
                UniformScale(type, t);
        }

        /// <summary>V3 PickupWorld.Tick's view step: position, the crystal's uniform scale, the spin (not barrels).</summary>
        public void Update(object view, PickupType type, Vector3 position, bool homing, float dt)
        {
            var t = (Transform)view;
            t.position = position;
            UniformScale(type, t);
            if (!homing && type != PickupType.GoldBarrel && type != PickupType.XpBarrel && type != PickupType.Powerup)
                t.Rotate(0f, 180f * dt, 0f, Space.World);
        }

        public void Release(object view, PickupType type, int value)
        {
            var t = view as Transform;
            if (t == null) return;
            if (!_pools.Release(t.gameObject)) Object.Destroy(t.gameObject);
            LiveCount = Mathf.Max(0, LiveCount - 1);
        }

        public void Dispose()
        {
            foreach (var template in _templates) if (template != null) Remove(template);
            _templates.Clear();
        }

        // ---- V3 PickupViewPool tables ----------------------------------------------------------------------------

        float GemScale => _s != null ? _s.xpFlyingGemScale : 0.6375f;

        void UniformScale(PickupType type, Transform view)
        {
            if (view != null && type is PickupType.Infamy or PickupType.FlyingXp) view.localScale = Vector3.one * GemScale;
        }

        GameObject PrefabFor(PickupType type)
        {
            if (_s == null) return _primitive;
            return type switch
            {
                PickupType.FlyingHealth => _flyingHealth,
                PickupType.GoldBarrel => _s.goldBarrelPickupPrefab != null ? _s.goldBarrelPickupPrefab : _s.goldPickupPrefab,
                PickupType.XpBarrel => _s.xpBarrelPickupPrefab != null ? _s.xpBarrelPickupPrefab : _s.infamyPickupPrefab,
                PickupType.Gold or PickupType.FlyingGold => _s.goldPickupPrefab,
                PickupType.FlyingXp or PickupType.Infamy => _gem,
                PickupType.Powerup => _s.healthPickupPrefab,
                _ => _s.infamyPickupPrefab
            };
        }

        float ScaleFor(PickupType type) => type switch
        {
            PickupType.GoldBarrel => _s.goldBarrelPickupScale,
            PickupType.XpBarrel => _s.xpBarrelPickupScale,
            PickupType.Gold or PickupType.FlyingGold => _s.goldFlyingCoinScale,
            PickupType.FlyingXp or PickupType.Infamy => GemScale,
            PickupType.Powerup => _s.healthPickupScale,
            _ => GemScale
        };

        static Color ColorFor(PickupType type) => type switch
        {
            PickupType.Gold or PickupType.GoldBarrel or PickupType.FlyingGold => new Color(1f, 0.85f, 0.15f),
            PickupType.Powerup or PickupType.FlyingHealth => new Color(0.184f, 1f, 0.42f),
            _ => new Color(0.2f, 0.95f, 1f)
        };

        static bool IsBarrel(PickupType type) => type is PickupType.GoldBarrel or PickupType.XpBarrel or PickupType.Powerup;

        static bool IsFlyingBurst(PickupType type) =>
            type is PickupType.FlyingGold or PickupType.FlyingXp or PickupType.FlyingHealth or PickupType.Infamy or PickupType.Gold;

        /// <summary>V3 PickupViewVfxCache.PlayBurstVfx: the collect sparkle if the model has one, else all its particles.</summary>
        void PlayBurst(GameObject go)
        {
            var id = go.GetInstanceID();
            if (!_burst.TryGetValue(id, out var particles))
            {
                var sparkle = go.transform.Find("CollectSparkle");
                particles = (sparkle != null ? sparkle : go.transform).GetComponentsInChildren<ParticleSystem>(true);
                _burst[id] = particles;
            }
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }
        }

        // ---- Pools and templates ---------------------------------------------------------------------------------

        void Register(GameObject prefab, int prewarm)
        {
            if (prefab == null || _keys.ContainsKey(prefab)) return;
            var key = KeyPrefix + prefab.name;
            for (var n = 2; _pools.Has(key); n++) key = KeyPrefix + prefab.name + "#" + n;
            _pools.Register(new PoolDefinition(key, prefab, prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow, "pickups"));
            _pools.Prewarm(key);
            _keys[prefab] = key;
        }

        GameObject RegisterTemplate(GameObject template, int prewarm)
        {
            if (template == null) return null;
            template.transform.SetParent(_pools.Staging, false);
            _templates.Add(template);
            Register(template, prewarm);
            return template;
        }

        /// <summary>The flying XP gem, done once: colliders removed. Hackathon playtest (12 Oct, owner: "in the XP pickup we're
        /// also showing gold coins, please remove those"): V3's EnsureFlyingLootSparkle gave the gem the gold coin's
        /// CollectSparkle (its particles use the CoinParticles material), so every XP gem burst gold coins; the gem now keeps
        /// only its own particles, if it has any.</summary>
        GameObject GemTemplate(GameObject source)
        {
            var template = Object.Instantiate(source);
            template.SetActive(false);
            template.name = source.name;
            StripColliders(template);
            return template;
        }

        static GameObject FlyingHealthTemplate()
        {
            var go = new GameObject("FlyingHealthPlus");
            go.SetActive(false);
            go.AddComponent<FlyingHealthBillboard>();
            return go;
        }

        static GameObject PrimitiveTemplate()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pickup";
            go.SetActive(false);
            StripColliders(go);
            return go;
        }

        static void TintPrimitive(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            renderer.material.color = color;
            renderer.material.SetColor("_EmissionColor", color * 0.8f);
            renderer.material.EnableKeyword("_EMISSION");
        }

        /// <summary>V5 M3e (owner notes f, i): pickups cast no shadows (up to ~200 barrels drew each again into the shadow map).</summary>
        static void NoShadows(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// V5 M8z (7 Oct, D21, Q16, owner: "remove them"): a loot barrel's sparkle particles (star glow and twinkle, the
        /// health barrel's blessing) are switched off once per pooled barrel: each particle object is stopped, cleared and
        /// deactivated. The barrel's model stays.
        /// </summary>
        static void RemoveSparkles(GameObject root)
        {
            foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps == null) continue;
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (ps.gameObject != root) ps.gameObject.SetActive(false);
                else { var r = ps.GetComponent<ParticleSystemRenderer>(); if (r != null) r.enabled = false; }
            }
        }

        /// <summary>V5 M8z (D21, Q16): active particle systems under a pickup view (tests: none on a barrel).</summary>
        public static int ActiveParticleSystems(Transform view)
        {
            var n = 0;
            if (view == null) return 0;
            foreach (var ps in view.GetComponentsInChildren<ParticleSystem>(false))
                if (ps != null && ps.gameObject.activeInHierarchy) n++;
            return n;
        }

        static void StripColliders(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Remove(collider);
        }

        // V3 PickupWorld.TryGetBarrelVisualBounds: mesh renderers (non-empty), else skinned renderers, else mesh filters.
        static bool TryGetBounds(Transform view, out Bounds bounds)
        {
            bounds = default;
            var has = false;
            var root = view.Find("Visuals");
            if (root == null) root = view;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var b = r.bounds;
                if (b.size.sqrMagnitude < 0.0001f) continue;
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!has) { bounds = r.bounds; has = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (has) return true;
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                var b = MeshBounds(f.transform, f.sharedMesh.bounds);
                if (!has) { bounds = b; has = true; }
                else bounds.Encapsulate(b);
            }
            return has;
        }

        static Bounds MeshBounds(Transform t, Bounds local)
        {
            var e = local.extents;
            var x = t.TransformVector(e.x, 0f, 0f);
            var y = t.TransformVector(0f, e.y, 0f);
            var z = t.TransformVector(0f, 0f, e.z);
            var world = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(t.TransformPoint(local.center), world * 2f);
        }

        /// <summary>Components go at once (templates are stripped before the pool clones them; edit-mode safe).</summary>
        static void Remove(Object o)
        {
            if (Application.isPlaying && o is GameObject) Object.Destroy(o);
            else Object.DestroyImmediate(o);
        }
    }
}
