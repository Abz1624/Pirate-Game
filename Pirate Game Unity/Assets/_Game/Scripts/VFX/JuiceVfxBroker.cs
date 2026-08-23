using HighSeas.Data;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class JuiceVfxBroker : MonoBehaviour
    {
        public static JuiceVfxBroker Instance { get; private set; }

        [SerializeField] DemoJuiceSettings settings;
        [SerializeField] Transform vfxRoot;

        VfxPool _pool;

        public DemoJuiceSettings Settings => settings;
        public VfxPool Pool => _pool;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            if (settings == null)
                settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
            _pool = new VfxPool(vfxRoot != null ? vfxRoot : transform);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Play(GameObject prefab, Vector3 position, Quaternion rotation, float scale, float lifetimeOverride = 0f)
        {
            if (prefab == null) return;
            _pool.Play(prefab, position, rotation, scale, lifetimeOverride);
        }

        public void PlayHit(Vector3 position, bool crit)
        {
            if (settings == null) return;
            var prefab = crit && settings.critHitPrefab != null
                ? settings.critHitPrefab
                : settings.enemyHitPrefab;
            var scale = crit ? settings.critHitScale : settings.enemyHitScale;
            Play(prefab, position + Vector3.up * 0.35f, Quaternion.identity, scale);
        }

        public void PlayKill(Vector3 position, bool elite, bool boss)
        {
            if (settings == null) return;

            if (boss)
            {
                var prefab = settings.bossKillExplosionPrefab != null
                    ? settings.bossKillExplosionPrefab
                    : settings.enemyKillPrefab;
                var count = Mathf.Max(1, settings.bossDeathBurstCount);
                for (var i = 0; i < count; i++)
                {
                    var offset = Random.insideUnitSphere * 1.5f;
                    offset.y = Mathf.Abs(offset.y) * 0.5f;
                    Play(prefab, position + offset, Quaternion.identity, settings.bossKillExplosionScale * (0.85f + i * 0.15f));
                }

                return;
            }

            var explosion = elite && settings.eliteKillExplosionPrefab != null
                ? settings.eliteKillExplosionPrefab
                : settings.enemyKillPrefab;
            if (explosion == null) return;
            var scale = elite ? settings.eliteKillExplosionScale : settings.enemyKillScale;
            Play(explosion, position + Vector3.up * 0.25f, Quaternion.identity, scale);
        }

        public void PlayWaterSplash(Vector3 position)
        {
            if (settings?.waterImpactSplashPrefab == null) return;
            var splashPos = position;
            splashPos.y = 0.35f;
            Play(settings.waterImpactSplashPrefab, splashPos, Quaternion.identity, settings.waterImpactSplashScale);
        }

        public void PlayDamageHighlight(Transform target)
        {
            if (settings?.damageHighlightPrefab == null || target == null) return;
            var instance = _pool.AcquirePublic(settings.damageHighlightPrefab);
            var pulse = instance.GetComponent<DamageHighlightPulse>();
            if (pulse == null)
                pulse = instance.AddComponent<DamageHighlightPulse>();
            pulse.Begin(target, settings, _pool, settings.damageHighlightPrefab);
        }
    }
}
