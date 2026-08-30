using HighSeas.Combat;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.VFX;
using UnityEngine;

namespace HighSeas.VFX
{
    [RequireComponent(typeof(DamageReceiver))]
    public sealed class PlayerHealthBarBinder : MonoBehaviour
    {
        WorldHealthBar _bar;
        DamageReceiver _damage;
        Transform _anchor;
        DemoJuiceSettings _settings;

        void Start()
        {
            _damage = GetComponent<DamageReceiver>();
            _settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            var player = GetComponent<PlayerShipController>();
            _anchor = player != null && player.VisualModel != null ? player.VisualModel : transform;
            CreateBar();
            _damage.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (_damage != null)
                _damage.Damaged -= OnDamaged;
        }

        void CreateBar()
        {
            if (_settings?.enemyHealthBarPrefab == null || _anchor == null)
                return;

            var barGo = Instantiate(_settings.enemyHealthBarPrefab);
            _bar = barGo.GetComponent<WorldHealthBar>();
            if (_bar == null)
                _bar = barGo.AddComponent<WorldHealthBar>();
            _bar.Bind(_anchor, _settings.playerHealthBarHeight);
            UpdateBar();
        }

        void OnDamaged(float _, bool __) => UpdateBar();

        void LateUpdate() => UpdateBar();

        void UpdateBar()
        {
            if (_bar == null || _damage == null)
                return;
            _bar.SetFill(_damage.MaxHP > 0f ? _damage.CurrentHP / _damage.MaxHP : 0f);
        }
    }
}
