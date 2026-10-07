using System;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Data
{
    [CreateAssetMenu(fileName = "DemoJuiceSettings", menuName = "RoguePirates/Demo Juice Settings")]
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
        [Tooltip("LineRenderer beam from Assets/GameObject/Guns/LaserBeam.prefab.")]
        public GameObject laserBeamPrefab;
        [Tooltip("Ground telegraph circles (Lightning Mast WaterReticle).")]
        public GameObject mortarTelegraphPrefab;
        [Tooltip("AoE burst from Assets/GameObject/Guns/Explosion_VFX.prefab.")]
        public GameObject weaponExplosionPrefab;
        [Tooltip("Small burst on cannon projectile hits.")]
        public GameObject cannonImpactPrefab;
        public float cannonImpactScale = 0.22f;
        [Tooltip("CannonSmoke burst shared by gunnery weapons.")]
        public GameObject cannonMuzzleVfxPrefab;
        [Tooltip("Dedicated powder keg detonation (single burst; not elite kill VFX).")]
        public GameObject powderKegExplosionPrefab;
        [Tooltip("Dedicated mortar impact burst prefab.")]
        public GameObject mortarExplosionPrefab;
        [Tooltip("Extra scale on mortar impact bursts (+200% = 3).")]
        [Min(0.1f)] public float mortarExplosionScaleMult = 3f;
        [Tooltip("Continuous flamethrower stream (particle-only; not the full gun rig).")]
        public GameObject flamethrowerStreamPrefab;
        [Tooltip("Per-weapon projectile / muzzle / impact prefabs for the playtest pool.")]
        public WeaponJuiceVfxBinding[] weaponVfxBindings;
        [Tooltip("Optional smaller projectile mesh for rapid-fire guns.")]
        public GameObject smallProjectilePrefab;
        [Tooltip("Projectile mesh for harpoon_gun.")]
        public GameObject harpoonProjectilePrefab;
        public float harpoonProjectileScale = 0.55f;
        public float mineBarrelScale = 0.55f;
        public float powderKegExplosionScale = 0.525f;
        public float lightningStrikeScale = 1f;
        public float burningTrailScale = 1f;
        public float mortarTelegraphScale = 1f;
        [Tooltip("Tint for mortar ground telegraph (WaterReticle).")]
        public Color mortarTelegraphColor = new(0.92f, 0.12f, 0.08f, 1f);
        [Range(0f, 1f)] public float mortarTelegraphTintSaturation = 0.19f;
        [Range(0f, 1f)] public float mortarTelegraphTintAlpha = 0.21f;
        public float weaponExplosionScale = 0.55f;
        public float laserBeamDuration = 0.22f;
        public float longNineBeamHalfWidth = 0.32f;
        public float swivelProjectileSizeMult = 0.55f;
        public float swivelDamageMult = 0.55f;
        public float swivelBetweenShotDelay = 0.18f;
        public float flamethrowerScale = 1f;
        [Tooltip("Scale when flamethrower VFX is attached to burning enemies.")]
        public float enemyBurnVfxScale = 0.42f;
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
        [Tooltip("World XP barrel (gem barrel). Burst pickups use xpFlyingGemPrefab.")]
        public GameObject xpBarrelPickupPrefab;
        [Tooltip("Loose / barrel-burst XP gem mesh. Falls back to infamyPickupPrefab if unset.")]
        public GameObject xpFlyingGemPrefab;
        public float infamyPickupScale = 1f;
        public float goldPickupScale = 1.8f;
        [Tooltip("Scale for loose / barrel-burst gold coins.")]
        public float goldFlyingCoinScale = 0.85f;
        public float healthPickupScale = 0.65f;
        public float healthFlyingPickupScale = 0.85f;
        public Material healthBurstMaterial;
        public Color healthBurstColor = new(0.184f, 1f, 0.42f, 1f);
        public float goldBarrelPickupScale = 0.7f;
        [Range(0.25f, 1.5f)]
        [Tooltip("Y scale multiplier for loot / health / XP barrels (1 = full height).")]
        public float barrelVerticalScale = 0.75f;
        [Tooltip("World Y of the water surface for floating pickups.")]
        public float pickupWaterSurfaceY = 0f;
        [Range(0f, 0.9f)]
        [Tooltip("Fraction of barrel height below the water surface (0.15 = 15% submerged).")]
        public float barrelSubmergedFraction = 0.15f;
        [Tooltip("V5 M8z (7 Oct, D21, Q16): the sparkle particles on the loot barrels (gold, XP and health). Off = removed (owner); the barrels stay.")]
        public bool barrelSparkles;
        public float xpBarrelPickupScale = 1.05f;
        [Tooltip("Scale for gems that fly out of an XP barrel.")]
        public float xpFlyingGemScale = 0.6375f;
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
        public GameObject salvageChestGlowPrefab;
        public float salvageChestGlowScale = 0.28f;
        public GameObject lighthousePrefab;
        public GameObject trialBuoyPrefab;
        public GameObject bossBeaconPrefab;
        public GameObject bossTelegraphRingPrefab;
        [Tooltip("Multiplier on top of the prefab's authored scale (~1.57). 1.5 ≈ 1.8 m footprint.")]
        public float salvageChestScale = 1.5f;
        public float lighthouseScale = 1f;
        public float trialBuoyScale = 1f;
        public float bossBeaconScale = 1f;
        public float bossTelegraphScale = 2.4f;
        [Header("POI Visuals (art team, demo)")]
        [Tooltip("Repair Wreck landmark. Shown even while placeholder POI visuals are on.")]
        public GameObject gildedWreckPrefab;
        public float gildedWreckScale = 1f;
        [Tooltip("Treasure Chart landmark. Shown even while placeholder POI visuals are on.")]
        public GameObject cursedIdolPrefab;
        public float cursedIdolScale = 1f;
        [Tooltip("Powder Cache landmark. Shown even while placeholder POI visuals are on.")]
        public GameObject merchantSkiffPrefab;
        public float merchantSkiffScale = 1f;
        [Tooltip("Demo: no points of interest on the stage (chests, landmarks, the Treasury Beacon).")]
        public bool hidePointsOfInterest = true;
        [Tooltip("Playtest (4-5 Oct, owner): of the points of interest only the salvage chests (no landmarks, Beacon or Powder Cache).")]
        public bool chestsOnlyPointsOfInterest;
        [Tooltip("V5 M8z (6 Oct, owner): every weapon, Captain, Logbook and item is open (locks come back later).")]
        public bool unlockEverything;
        // V5 M8z (7 Oct, D20): session files are for playtest builds only; switch off for a public build.
        [Tooltip("V5 M8z (7 Oct, D20, playtest builds only): a finished voyage records its session file and saves or downloads it, and the 'Download voyage data' / 'Download last voyage' buttons show. Off for a public build: nothing is recorded or downloaded.")]
        public bool downloadSessionFiles = true;
        [Tooltip("Demo: no voyage message at run start (it shows the seed and mentions chests and the Beacon).")]
        public bool hideVoyageToast = true;
        [Tooltip("Demo: the boss rises on its own at this run time (seconds; 0 = only at the Treasury Beacon).")]
        [Min(0f)] public float autoBossSeconds = 540f;
        [Tooltip("Demo: the whirlpool that swirls where the boss is about to rise, and for how long before it does.")]
        public GameObject bossArrivalPrefab;
        [Min(0f)] public float bossArrivalScale = 1.6f;
        [Range(0.5f, 5f)] public float bossArrivalSeconds = 2.5f;
        [Tooltip("Plays instead of VFX Graph effects (the whirlpool) where the device can't run them (WebGL: no compute shaders).")]
        public GameObject vfxGraphFallbackPrefab;
        [Min(0.01f)] public float vfxGraphFallbackScale = 0.6f;
        [Tooltip("Demo: HUD margin from the screen edges on phones (fraction of the screen height), on top of the safe area, for rounded corners.")]
        [Range(0f, 0.1f)] public float mobileHudEdgeMargin = 0.035f;

        [Header("Player Feedback")]
        public Color playerDamageFlashColor = new(1f, 0.25f, 0.2f, 1f);
        [Range(0.05f, 0.5f)] public float playerDamageFlashDuration = 0.12f;
        [Range(0.05f, 0.5f)] public float damageHighlightDuration = 0.14f;

        public bool TryGetWeaponVfx(string weaponId, out WeaponJuiceVfxBinding binding)
        {
            binding = null;
            if (string.IsNullOrEmpty(weaponId) || weaponVfxBindings == null)
                return false;

            for (var i = 0; i < weaponVfxBindings.Length; i++)
            {
                var entry = weaponVfxBindings[i];
                if (entry == null || entry.weaponId != weaponId)
                    continue;
                binding = entry;
                return true;
            }

            return false;
        }
    }
}
