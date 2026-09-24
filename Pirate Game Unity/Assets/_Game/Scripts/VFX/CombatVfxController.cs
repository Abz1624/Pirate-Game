using HighSeas.Audio;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Meta;
using HighSeas.Player;
using HighSeas.UI;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class CombatVfxController : MonoBehaviour
    {
        [SerializeField] DemoJuiceSettings settings;

        PlayerDamageFlash _playerFlash;
        JuiceVfxBroker _broker;

        void Awake()
        {
            if (settings == null)
                settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            _broker = JuiceVfxBroker.Instance;
        }

        void Start()
        {
            Subscribe();
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player != null)
            {
                _playerFlash = player.GetComponent<PlayerDamageFlash>();
                if (_playerFlash == null)
                    _playerFlash = player.gameObject.AddComponent<PlayerDamageFlash>();
                _playerFlash.Configure(settings);
            }
        }

        void Subscribe()
        {
            if (TriggerBus.Instance != null)
            {
                TriggerBus.Instance.Hit -= OnHit;
                TriggerBus.Instance.Kill -= OnKill;
                TriggerBus.Instance.DamageTaken -= OnDamageTaken;
                TriggerBus.Instance.Hit += OnHit;
                TriggerBus.Instance.Kill += OnKill;
                TriggerBus.Instance.DamageTaken += OnDamageTaken;
            }

            if (LevelSystem.Instance != null)
            {
                LevelSystem.Instance.LevelChanged -= OnLevelChanged;
                LevelSystem.Instance.LevelChanged += OnLevelChanged;
            }
        }

        void OnDisable()
        {
            if (TriggerBus.Instance != null)
            {
                TriggerBus.Instance.Hit -= OnHit;
                TriggerBus.Instance.Kill -= OnKill;
                TriggerBus.Instance.DamageTaken -= OnDamageTaken;
            }

            if (LevelSystem.Instance != null)
                LevelSystem.Instance.LevelChanged -= OnLevelChanged;
        }

        void OnHit(float damage, bool crit, Vector3 position, string weaponId)
        {
            if (weaponId == "powder_kegs")
                return;
            if (_broker == null) _broker = JuiceVfxBroker.Instance;
            if (CannonImpactVfx.IsCannonWeapon(weaponId))
                _broker?.PlayCannonImpact(position, crit);
            else
                _broker?.PlayHit(position, crit);
        }

        void OnKill(bool elite, bool boss, Vector3 position, string weaponId)
        {
            if (weaponId == "powder_kegs")
                return;
            if (_broker == null) _broker = JuiceVfxBroker.Instance;
            _broker?.PlayKill(position, elite, boss);
        }

        void OnDamageTaken(float amount)
        {
            if (amount <= 0f) return;
            _playerFlash?.Flash();
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player != null)
            {
                if (_broker == null) _broker = JuiceVfxBroker.Instance;
                _broker?.PlayDamageHighlight(player.transform);
            }
        }

        void OnLevelChanged(int level)
        {
            if (settings == null || settings.levelUpBurstPrefab == null) return;
            if (_broker == null) _broker = JuiceVfxBroker.Instance;
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;
            var scale = settings.levelUpBurstScale > 0f ? settings.levelUpBurstScale : 1f;
            _broker.Play(
                settings.levelUpBurstPrefab,
                player.Position + Vector3.up * 0.5f,
                Quaternion.identity,
                scale);
            CameraShakeService.Shake(0.12f, 0.25f);
            SfxService.PlayPlaceholder("level_up");
        }
    }
}
