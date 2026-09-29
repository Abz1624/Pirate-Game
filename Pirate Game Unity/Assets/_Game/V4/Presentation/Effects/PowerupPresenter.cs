using System;
using System.Collections.Generic;
using HighSeas.V3;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Progression;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RoguePirates.V4.Presentation.Effects
{
    /// <summary>
    /// Floor powerup models (M14.9b; V3 built a placeholder marker per drop with V3PlaceholderFactory.CreateFloorPowerup
    /// and destroyed it on pickup). The same placeholder look, built once per powerup kind as a pool template (keys
    /// "powerup/&lt;kind&gt;") and prewarmed at bind, so a drop takes a pooled model and a pickup returns it.
    /// </summary>
    public sealed class PowerupPresenter : IPowerupViews
    {
        public const string KeyPrefix = "powerup/";
        const int Prewarm = 2; // up to 6 powerups a stage, rarely two of a kind at once

        readonly PoolService _pools;
        readonly Dictionary<PowerupKind, string> _keys = new Dictionary<PowerupKind, string>();
        readonly List<GameObject> _templates = new List<GameObject>();

        public int LiveCount { get; private set; }

        public PowerupPresenter(PoolService pools)
        {
            _pools = pools;
            foreach (PowerupKind kind in Enum.GetValues(typeof(PowerupKind)))
            {
                var template = V3PlaceholderFactory.CreateFloorPowerup((V3FloorPowerupKind)(int)kind, Vector3.zero, null);
                if (template == null) continue;
                template.SetActive(false);
                template.transform.SetParent(_pools.Staging, true);
                _templates.Add(template);
                var key = KeyPrefix + kind;
                if (!_pools.Has(key))
                {
                    _pools.Register(new PoolDefinition(key, template, Prewarm, 0, ConfigEnums.PoolOverflowPolicy.Grow, "floor powerups"));
                    _pools.Prewarm(key);
                }
                _keys[kind] = key;
            }
        }

        public object Create(PowerupKind kind, Vector3 position)
        {
            if (!_keys.TryGetValue(kind, out var key)) return null;
            var go = _pools.Acquire(key, position, Quaternion.identity);
            if (go == null) return null;
            LiveCount++;
            return go.transform;
        }

        public void Move(object view, Vector3 position)
        {
            if (view is Transform t && t != null) t.position = position;
        }

        public void Destroy(object view)
        {
            if (!(view is Transform t) || t == null) return;
            if (!_pools.Release(t.gameObject)) Object.Destroy(t.gameObject);
            LiveCount = Mathf.Max(0, LiveCount - 1);
        }

        public void Dispose()
        {
            foreach (var template in _templates) if (template != null) Object.Destroy(template);
            _templates.Clear();
        }
    }
}
