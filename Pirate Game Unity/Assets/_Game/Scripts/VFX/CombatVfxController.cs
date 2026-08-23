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
        [SerializeField] Transform vfxRoot;

        VfxPool _pool;
        PlayerDamageFlash _playerFlash;

        void Awake()
        {
            if (settings == null)
                settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");

            _pool = new VfxPool(vfxRoot != null ? vfxRoot : transform);
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

        void OnHit(float damage, bool crit, Vector3 position)
        {
            if (!crit || settings == null || settings.hitSparkPrefab == null) return;
            _pool.Play(
                settings.hitSparkPrefab,
                position + Vector3.up * 0.35f,
                Quaternion.identity,
                settings.hitSparkScale);
        }

        void OnKill(bool elite, bool boss, Vector3 position)
        {
            if (settings == null) return;

            if (boss)
            {
                var prefab = settings.bossKillExplosionPrefab != null
                    ? settings.bossKillExplosionPrefab
                    : settings.killExplosionPrefab;
                var scale = settings.bossKillExplosionScale;
                var count = Mathf.Max(1, settings.bossDeathBurstCount);
                for (var i = 0; i < count; i++)
                {
                    var offset = Random.insideUnitSphere * 1.5f;
                    offset.y = Mathf.Abs(offset.y) * 0.5f;
                    _pool.Play(prefab, position + offset, Quaternion.identity, scale * (0.85f + i * 0.15f));
                }

                return;
            }

            var explosion = elite && settings.eliteKillExplosionPrefab != null
                ? settings.eliteKillExplosionPrefab
                : settings.killExplosionPrefab;
            if (explosion == null) return;

            var explosionScale = elite ? settings.eliteKillExplosionScale : settings.killExplosionScale;
            _pool.Play(explosion, position + Vector3.up * 0.25f, Quaternion.identity, explosionScale);
        }

        void OnDamageTaken(float amount)
        {
            if (amount <= 0f) return;
            _playerFlash?.Flash();
        }

        void OnLevelChanged(int level)
        {
            if (settings == null || settings.levelUpBurstPrefab == null) return;
            var player = FindFirstObjectByType<PlayerShipController>();
            if (player == null) return;
            _pool.Play(
                settings.levelUpBurstPrefab,
                player.Position + Vector3.up * 0.5f,
                Quaternion.identity,
                settings.levelUpBurstScale);
        }
    }
}
