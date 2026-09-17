using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using UnityEngine;

namespace HighSeas.Pickups
{
    public sealed class PickupViewPool
    {
        readonly Transform _root;
        readonly DemoJuiceSettings _settings;
        readonly Dictionary<int, Stack<Transform>> _pools = new();
        readonly Stack<Transform> _flyingHealthPool = new();
        Transform _flyingLootSparkleTemplate;

        public PickupViewPool(Transform root, DemoJuiceSettings settings)
        {
            _root = root;
            _settings = settings;
        }

        public Transform Acquire(PickupType type, Vector3 position, int value = 1)
        {
            if (type == PickupType.FlyingHealth)
                return AcquireFlyingHealth(position);

            var prefab = GetPrefab(type, value);
            var scale = GetScale(type, value);
            var color = GetColor(type);

            if (prefab != null)
            {
                var key = prefab.GetInstanceID();
                if (!_pools.TryGetValue(key, out var stack))
                {
                    stack = new Stack<Transform>(32);
                    _pools[key] = stack;
                }

                Transform view;
                if (stack.Count > 0)
                {
                    view = stack.Pop();
                    view.gameObject.SetActive(true);
                }
                else
                {
                    var instance = Object.Instantiate(prefab, _root);
                    instance.name = prefab.name;
                    view = instance.transform;
                    StripPhysics(instance);
                    if (IsFlyingBurstPickup(type) && instance.GetComponent<PickupViewVfxCache>() == null)
                        instance.AddComponent<PickupViewVfxCache>();
                }

                view.position = position;
                ApplyPickupViewScale(view, type, scale);
                if (type is PickupType.FlyingXp or PickupType.Infamy)
                    EnsureFlyingLootSparkle(view);
                if (IsFlyingBurstPickup(type))
                    PlayFlyingBurstVfx(view);
                return view;
            }

            return CreatePrimitive(position, color, scale);
        }

        public void Release(Transform view, PickupType type, int value = 1)
        {
            if (view == null) return;

            if (type == PickupType.FlyingHealth)
            {
                view.gameObject.SetActive(false);
                view.SetParent(_root, false);
                _flyingHealthPool.Push(view);
                return;
            }

            var prefab = GetPrefab(type, value);
            if (prefab != null && view.name == prefab.name)
            {
                view.gameObject.SetActive(false);
                view.SetParent(_root, false);
                var key = prefab.GetInstanceID();
                if (!_pools.TryGetValue(key, out var stack))
                {
                    stack = new Stack<Transform>(32);
                    _pools[key] = stack;
                }

                stack.Push(view);
                return;
            }

            Object.Destroy(view.gameObject);
        }

        Transform AcquireFlyingHealth(Vector3 position)
        {
            Transform view;
            if (_flyingHealthPool.Count > 0)
            {
                view = _flyingHealthPool.Pop();
                view.gameObject.SetActive(true);
            }
            else
            {
                var go = new GameObject("FlyingHealthPlus");
                go.transform.SetParent(_root, false);
                var billboard = go.AddComponent<FlyingHealthBillboard>();
                if (_settings != null)
                    billboard.Configure(_settings.healthBurstMaterial, _settings.healthBurstColor);
                view = go.transform;
            }

            var billboardView = view.GetComponent<FlyingHealthBillboard>();
            if (billboardView != null && _settings != null)
            {
                billboardView.Configure(_settings.healthBurstMaterial, _settings.healthBurstColor);
                billboardView.ResetLifetime();
            }

            var scale = _settings != null ? _settings.healthFlyingPickupScale : 0.85f;
            view.position = position;
            view.localScale = Vector3.one * scale;
            return view;
        }

        void ApplyPickupViewScale(Transform view, PickupType type, float uniformScale)
        {
            if (IsBarrelPickup(type) && _settings != null)
            {
                var y = _settings.barrelVerticalScale;
                view.localScale = new Vector3(uniformScale, uniformScale * y, uniformScale);
                return;
            }

            view.localScale = Vector3.one * uniformScale;
        }

        static bool IsBarrelPickup(PickupType type) =>
            type is PickupType.GoldBarrel or PickupType.XpBarrel or PickupType.Powerup;

        static bool IsFlyingBurstPickup(PickupType type) =>
            type is PickupType.FlyingGold or PickupType.FlyingXp or PickupType.FlyingHealth
                or PickupType.Infamy or PickupType.Gold;

        GameObject GetPrefab(PickupType type, int value)
        {
            if (_settings == null) return null;
            return type switch
            {
                PickupType.GoldBarrel => _settings.goldBarrelPickupPrefab != null
                    ? _settings.goldBarrelPickupPrefab
                    : _settings.goldPickupPrefab,
                PickupType.XpBarrel => _settings.xpBarrelPickupPrefab != null
                    ? _settings.xpBarrelPickupPrefab
                    : _settings.infamyPickupPrefab,
                PickupType.Gold or PickupType.FlyingGold => _settings.goldPickupPrefab,
                PickupType.FlyingXp or PickupType.Infamy => GetXpFlyingGemPrefab(),
                PickupType.Powerup => _settings.healthPickupPrefab,
                _ => _settings.infamyPickupPrefab
            };
        }

        GameObject GetXpFlyingGemPrefab()
        {
            if (_settings == null) return null;
            return _settings.xpFlyingGemPrefab != null
                ? _settings.xpFlyingGemPrefab
                : _settings.infamyPickupPrefab;
        }

        public float GetXpFlyingGemScale()
        {
            if (_settings == null) return 0.6375f;
            return _settings.xpFlyingGemScale;
        }

        public float GetXpCrystalVisualScale() => GetXpFlyingGemScale();

        float GetScale(PickupType type, int value)
        {
            if (_settings == null) return GetXpCrystalVisualScale();
            return type switch
            {
                PickupType.GoldBarrel => _settings.goldBarrelPickupScale,
                PickupType.XpBarrel => _settings.xpBarrelPickupScale,
                PickupType.Gold or PickupType.FlyingGold => _settings.goldFlyingCoinScale,
                PickupType.FlyingXp or PickupType.Infamy => GetXpFlyingGemScale(),
                PickupType.Powerup => _settings.healthPickupScale,
                _ => GetXpCrystalVisualScale()
            };
        }

        static Color GetColor(PickupType type) => type switch
        {
            PickupType.Gold or PickupType.GoldBarrel or PickupType.FlyingGold => new Color(1f, 0.85f, 0.15f),
            PickupType.XpBarrel or PickupType.FlyingXp or PickupType.Infamy => new Color(0.2f, 0.95f, 1f),
            PickupType.Powerup or PickupType.FlyingHealth => new Color(0.184f, 1f, 0.42f),
            _ => new Color(0.2f, 0.95f, 1f)
        };

        void EnsureFlyingLootSparkle(Transform view)
        {
            if (view.Find("CollectSparkle") != null)
                return;

            var template = GetFlyingLootSparkleTemplate();
            if (template == null)
                return;

            var sparkle = Object.Instantiate(template, view);
            sparkle.name = "CollectSparkle";
            sparkle.localPosition = Vector3.zero;
            sparkle.localRotation = Quaternion.identity;
            sparkle.localScale = Vector3.one;
        }

        Transform GetFlyingLootSparkleTemplate()
        {
            if (_flyingLootSparkleTemplate != null)
                return _flyingLootSparkleTemplate;

            if (_settings?.goldPickupPrefab == null)
                return null;

            _flyingLootSparkleTemplate = _settings.goldPickupPrefab.transform.Find("CollectSparkle");
            return _flyingLootSparkleTemplate;
        }

        static void PlayFlyingBurstVfx(Transform view)
        {
            if (view == null)
                return;

            var cache = view.GetComponent<PickupViewVfxCache>();
            if (cache != null)
            {
                cache.PlayBurstVfx();
                return;
            }

            cache = view.gameObject.AddComponent<PickupViewVfxCache>();
            cache.PlayBurstVfx();
        }

        Transform CreatePrimitive(Vector3 position, Color color, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Pickup";
            go.transform.SetParent(_root, false);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * scale;
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = color;
                renderer.material.SetColor("_EmissionColor", color * 0.8f);
                renderer.material.EnableKeyword("_EMISSION");
            }

            return go.transform;
        }

        static void StripPhysics(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
                Object.Destroy(colliders[i]);
        }
    }
}
