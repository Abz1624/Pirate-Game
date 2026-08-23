using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "DemoJuiceSettings", menuName = "HighSeas/Demo Juice Settings")]
    public sealed class DemoJuiceSettings : ScriptableObject
    {
        [Header("Combat VFX")]
        public GameObject killExplosionPrefab;
        public GameObject eliteKillExplosionPrefab;
        public GameObject bossKillExplosionPrefab;
        public GameObject hitSparkPrefab;
        public GameObject levelUpBurstPrefab;

        [Header("Explosion Scale")]
        public float killExplosionScale = 1f;
        public float eliteKillExplosionScale = 1.35f;
        public float bossKillExplosionScale = 2.2f;
        public float hitSparkScale = 0.45f;
        public float levelUpBurstScale = 1.6f;
        public int bossDeathBurstCount = 3;

        [Header("Projectiles")]
        public GameObject projectilePrefab;
        [Min(0.5f)] public float projectileSizeMultiplier = 1.3f;
        [Min(0.5f)] public float projectileRadiusMultiplier = 1.2f;

        [Header("Pickups")]
        public GameObject infamyPickupPrefab;
        public GameObject goldPickupPrefab;
        public float infamyPickupScale = 0.42f;
        public float goldPickupScale = 0.38f;
        public Color infamyColor = new(0.2f, 0.95f, 1f);
        public Color goldColor = new(1f, 0.85f, 0.15f);

        [Header("POI Visuals")]
        public GameObject salvageChestPrefab;
        public GameObject lighthousePrefab;
        public GameObject trialBuoyPrefab;
        public GameObject bossBeaconPrefab;
        public GameObject bossTelegraphRingPrefab;
        public float salvageChestScale = 0.55f;
        public float lighthouseScale = 0.7f;
        public float trialBuoyScale = 0.5f;
        public float bossBeaconScale = 1.1f;
        public float bossTelegraphScale = 2.4f;

        [Header("Player Feedback")]
        public Color playerDamageFlashColor = new(1f, 0.25f, 0.2f, 1f);
        [Range(0.05f, 0.5f)] public float playerDamageFlashDuration = 0.12f;
    }
}
