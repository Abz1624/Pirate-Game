using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "DemoJuiceSettings", menuName = "HighSeas/Demo Juice Settings")]
    public sealed class DemoJuiceSettings : ScriptableObject
    {
        [Header("Combat VFX")]
        public GameObject enemyHitPrefab;
        public GameObject enemyKillPrefab;
        public GameObject eliteKillExplosionPrefab;
        public GameObject bossKillExplosionPrefab;
        public GameObject critHitPrefab;
        public GameObject levelUpBurstPrefab;
        public GameObject waterImpactSplashPrefab;
        public GameObject damageHighlightPrefab;

        [Header("Combat VFX Scale")]
        public float enemyHitScale = 0.55f;
        public float enemyKillScale = 1f;
        public float eliteKillExplosionScale = 1.35f;
        public float bossKillExplosionScale = 2.2f;
        public float critHitScale = 0.75f;
        public float levelUpBurstScale = 1.6f;
        public float waterImpactSplashScale = 0.85f;
        public float damageHighlightScale = 1.15f;
        public int bossDeathBurstCount = 3;

        [Header("Projectiles")]
        public GameObject projectilePrefab;
        public GameObject shootingVfxPrefab;
        [Min(0.5f)] public float projectileSizeMultiplier = 1f;
        [Min(0.5f)] public float projectileRadiusMultiplier = 1f;
        public float shootingVfxScale = 0.35f;

        [Header("Weapon VFX")]
        public GameObject mineBarrelPrefab;
        public GameObject lightningStrikePrefab;
        public GameObject burningTrailPrefab;
        public GameObject flamethrowerPrefab;
        public GameObject cutlassPrefab;
        public float mineBarrelScale = 0.55f;
        public float lightningStrikeScale = 1f;
        public float burningTrailScale = 1f;
        public float flamethrowerScale = 1f;
        public float cutlassScale = 0.325f;

        [Header("Player VFX")]
        public GameObject seaFoamTrailPrefab;
        public float seaFoamTrailScale = 1f;

        [Header("Pickups")]
        public GameObject infamyPickupPrefab;
        public GameObject goldPickupPrefab;
        [Tooltip("3D mesh coin used for flying gold pickups. Falls back to goldPickupPrefab if unset.")]
        public GameObject goldCoinMeshPrefab;
        public GameObject healthPickupPrefab;
        public GameObject goldBarrelPickupPrefab;
        public float infamyPickupScale = 1f;
        public float goldPickupScale = 1.8f;
        [Tooltip("Scale for loose / barrel-burst gold coins.")]
        public float goldFlyingCoinScale = 0.85f;
        public float healthPickupScale = 0.65f;
        public float healthFlyingPickupScale = 0.85f;
        public Material healthBurstMaterial;
        public Color healthBurstColor = new(0.184f, 1f, 0.42f, 1f);
        public float goldBarrelPickupScale = 0.7f;
        public Color infamyColor = new(0.2f, 0.95f, 1f);
        public Color goldColor = new(1f, 0.85f, 0.15f);
        [Min(1)] public int goldBarrelMinValue = 12;

        [Header("Enemy UI")]
        public GameObject enemyHealthBarPrefab;
        public float enemyHealthBarHeight = 2.2f;
        public float playerHealthBarHeight = 6f;
        public bool showHealthOnElitesOnly = false;

        [Header("POI Visuals")]
        public GameObject salvageChestPrefab;
        public GameObject lighthousePrefab;
        public GameObject trialBuoyPrefab;
        public GameObject bossBeaconPrefab;
        public GameObject bossTelegraphRingPrefab;
        public float salvageChestScale = 1f;
        public float lighthouseScale = 1f;
        public float trialBuoyScale = 1f;
        public float bossBeaconScale = 1f;
        public float bossTelegraphScale = 2.4f;

        [Header("Player Feedback")]
        public Color playerDamageFlashColor = new(1f, 0.25f, 0.2f, 1f);
        [Range(0.05f, 0.5f)] public float playerDamageFlashDuration = 0.12f;
        [Range(0.05f, 0.5f)] public float damageHighlightDuration = 0.14f;
    }
}
