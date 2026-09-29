using HighSeas.Data;
using HighSeas.VFX;
using RoguePirates.V4.Config;
using RoguePirates.V4.Presentation.Pooling;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.Presentation.World
{
    /// <summary>
    /// The health bar over the player ship (M14.9b; port of V3's PlayerHealthBarBinder, a component V3 added to the ship):
    /// the enemy health bar model bound above the ship's model at the player bar height, its fill = hull / max hull, read
    /// from the ship view every frame. The bar comes from the V4 view pool (key "world/ship_health_bar").
    /// </summary>
    public sealed class ShipHealthBarPresenter : MonoBehaviour
    {
        public const string Key = "world/ship_health_bar";

        PoolService _pools;
        IShipView _ship;
        DemoJuiceSettings _settings;
        GameObject _barGo;
        WorldHealthBar _bar;
        Transform _anchor;

        public float Fill { get; private set; }
        public bool HasBar => _bar != null;

        public void Bind(PoolService pools, IShipView ship, DemoJuiceSettings settings)
        {
            Unbind();
            _pools = pools;
            _ship = ship;
            _settings = settings;
            var prefab = settings != null ? settings.enemyHealthBarPrefab : null;
            if (_pools == null || prefab == null) return;
            if (!_pools.Has(Key))
            {
                _pools.Register(new PoolDefinition(Key, prefab, 1, 0, ConfigEnums.PoolOverflowPolicy.Grow, "ship health bar"));
                _pools.Prewarm(Key);
            }
        }

        void Unbind()
        {
            if (_barGo != null && _pools != null) _pools.Release(_barGo);
            _barGo = null;
            _bar = null;
            _anchor = null;
        }

        void LateUpdate()
        {
            if (_ship == null || !_ship.Exists) return;
            var anchor = _ship.VisualModel;
            if (anchor == null) return;
            if (_bar == null || anchor != _anchor) Attach(anchor);
            if (_bar == null) return;
            Fill = _ship.MaxHp > 0f ? _ship.Hp / _ship.MaxHp : 0f;
            _bar.SetFill(Fill);
        }

        void Attach(Transform anchor)
        {
            if (_barGo == null)
            {
                _barGo = _pools != null && _pools.Has(Key) ? _pools.Acquire(Key, anchor.position, Quaternion.identity) : null;
                if (_barGo == null) return;
                _bar = _barGo.GetComponent<WorldHealthBar>();
                if (_bar == null) _bar = _barGo.AddComponent<WorldHealthBar>();
            }
            _anchor = anchor;
            _bar.Bind(anchor, _settings.playerHealthBarHeight);
        }

        void OnDestroy() => Unbind();
    }
}
