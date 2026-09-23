#if UNITY_EDITOR
using HighSeas.Data;
using HighSeas.UI;
using HighSeas.VFX;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasDemoSetup
    {
        const string JuicePath = "Assets/_Game/Data/DemoJuiceSettings.asset";
        const string JuiceResourcesPath = "Assets/_Game/Resources/DemoJuiceSettings.asset";
        const string PacingPath = "Assets/_Game/Data/DemoPacingSettings.asset";
        const string PacingResourcesPath = "Assets/_Game/Resources/DemoPacingSettings.asset";
        const string LevelUpPath = "Assets/_Game/Data/LevelUpSettings.asset";
        const string LevelUpResourcesPath = "Assets/_Game/Resources/LevelUpSettings.asset";
        const string VfxRoot = "Assets/Prefabs/VFX";

        static void EnsureLevelUpUiInScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.Contains("Run.unity")) return;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var modals = Object.FindObjectsByType<LevelUpModal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            LevelUpModal keeper = null;
            for (var i = 0; i < modals.Length; i++)
            {
                var modal = modals[i];
                if (modal.transform.childCount == 0 && modal.GetComponent<RectTransform>() == null)
                {
                    Object.DestroyImmediate(modal.gameObject);
                    continue;
                }

                if (keeper == null || modal.transform.childCount > keeper.transform.childCount)
                    keeper = modal;
            }

            if (keeper == null)
            {
                var panel = new GameObject("LevelUpPanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                panel.transform.SetParent(canvas.transform, false);
                keeper = panel.AddComponent<LevelUpModal>();
            }

            keeper.RebuildUiLayout();

            var runHud = canvas.GetComponent<RunHud>();
            if (runHud == null)
                runHud = canvas.gameObject.AddComponent<RunHud>();
            runHud.RebuildUiLayout();

            var levelSystem = Object.FindFirstObjectByType<LevelSystem>();
            if (levelSystem != null)
            {
                var so = new SerializedObject(levelSystem);
                if (so.FindProperty("levelUpSettings").objectReferenceValue == null)
                {
                    so.FindProperty("levelUpSettings").objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<LevelUpSettings>(LevelUpPath);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        [MenuItem("HighSeas/Setup Run HUD")]
        public static void SetupRunHud()
        {
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("No Canvas found in the active scene.");
                return;
            }

            UiCanvasConfigurator.Configure(canvas);

            var hud = canvas.GetComponent<RunHud>();
            if (hud == null)
                hud = canvas.gameObject.AddComponent<RunHud>();

            hud.RebuildUiLayout();

            var modal = Object.FindFirstObjectByType<LevelUpModal>(FindObjectsInactive.Include);
            modal?.RebuildUiLayout();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Run HUD and canvas scaling rebuilt.");
        }

        [MenuItem("HighSeas/Setup Level Up UI")]
        public static void SetupLevelUpUi()
        {
            EnsureFolder("Assets/_Game/Data");
            EnsureFolder("Assets/_Game/Resources");
            WireLevelUpSettings();
            PopulateWeaponUpgradeAxes();
            EnsureLevelUpUiInScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Level-up settings and weapon upgrade axes configured.");
        }

        [MenuItem("HighSeas/Setup Demo Juice (Phases 6-9)")]
        public static void SetupDemoJuice()
        {
            EnsureFolder("Assets/_Game/Data");
            EnsureFolder("Assets/_Game/Resources");
            WireJuiceSettings();
            WirePacingSettings();
            WireLevelUpSettings();
            TuneDeckCannons();
            EnsureJuiceSystemsInScene();
            AssetDatabase.SaveAssets();
            Debug.Log("Demo juice configured with artist VFX prefabs from Assets/Prefabs/VFX.");
        }

        [MenuItem("HighSeas/Integrate Artist VFX")]
        public static void IntegrateArtistVfx() => SetupDemoJuice();

        public static void WireJuiceSettings()
        {
            var juice = LoadOrCreate<DemoJuiceSettings>(JuicePath);

            juice.enemyHitPrefab = LoadPrefab($"{VfxRoot}/02_FishEnemy_DamageVFX.prefab");
            juice.enemyKillPrefab = juice.enemyHitPrefab;
            var explosionLarge = LoadPrefab("Assets/GameObject/Explosion_Large.prefab");
            juice.eliteKillExplosionPrefab = explosionLarge != null ? explosionLarge : juice.enemyHitPrefab;
            juice.bossKillExplosionPrefab = explosionLarge != null ? explosionLarge : juice.enemyHitPrefab;
            juice.critHitPrefab = juice.enemyHitPrefab;
            juice.levelUpBurstPrefab = juice.enemyHitPrefab;
            juice.waterImpactSplashPrefab = LoadPrefab($"{VfxRoot}/19_WaterImpactSplash.prefab");
            juice.damageHighlightPrefab = LoadPrefab($"{VfxRoot}/06_DamageHighlightShader.prefab");

            juice.projectilePrefab = LoadPrefab("Assets/GameObject/Guns/Cannonball.prefab");
            if (juice.projectilePrefab == null)
                juice.projectilePrefab = LoadPrefab($"{VfxRoot}/01_Cannonball_ShootingVFX.prefab");
            juice.shootingVfxPrefab = juice.projectilePrefab;
            juice.smallProjectilePrefab = LoadPrefab("Assets/GameObject/Guns/Cannonball small.prefab");

            WireGunCombatVfx(juice);
            WireWeaponVfxBindings(juice);

            juice.mineBarrelPrefab = LoadPrefab("Assets/GameObject/FireBarrel.prefab");
            juice.lightningStrikePrefab = LoadPrefab("Assets/GameObject/Guns/Lightning Mast.prefab");
            if (juice.lightningStrikePrefab == null)
                juice.lightningStrikePrefab = LoadPrefab($"{VfxRoot}/13_LightningStrike_VFX.prefab");
            juice.burningTrailPrefab = LoadPrefab($"{VfxRoot}/14_BurningTrail_VFX.prefab");
            juice.flamethrowerPrefab = LoadPrefab("Assets/GameObject/Guns/Flamethrower.prefab");
            if (juice.flamethrowerPrefab == null)
                juice.flamethrowerPrefab = LoadPrefab($"{VfxRoot}/10_Flamethrower_VFX.prefab");
            juice.cutlassPrefab = LoadPrefab("Assets/GameObject/Guns/Flying Cutlasses.prefab");
            if (juice.cutlassPrefab == null)
                juice.cutlassPrefab = LoadPrefab($"{VfxRoot}/12_CutlassSword.prefab");
            juice.seaFoamTrailPrefab = LoadPrefab($"{VfxRoot}/11_SeaFoamTrail.prefab");

            const string PickupBarrelRoot = "Assets/GameObject";
            juice.xpFlyingGemPrefab = LoadPrefab($"{VfxRoot}/04_XPCrystal.prefab");
            juice.infamyPickupPrefab = juice.xpFlyingGemPrefab;
            juice.goldPickupPrefab = LoadPrefab($"{VfxRoot}/03_GoldCoin.prefab");
            juice.goldCoinMeshPrefab = null;
            juice.healthPickupPrefab = LoadPrefab($"{PickupBarrelRoot}/HealthBarrel.prefab");
            juice.healthBurstMaterial = LoadMaterial("Assets/Material/HealthParticles.mat");
            juice.goldBarrelPickupPrefab = LoadPrefab($"{PickupBarrelRoot}/LootBarrel.prefab");
            juice.xpBarrelPickupPrefab = LoadPrefab($"{PickupBarrelRoot}/GemBarrel.prefab");

            juice.infamyPickupScale = 1f;
            juice.eliteKillExplosionScale = 0.35f;
            juice.powderKegExplosionScale = 0.525f;
            juice.bossKillExplosionScale = 0.45f;

            juice.enemyHealthBarPrefab = LoadPrefab($"{VfxRoot}/05_EnemyHealthIndicator.prefab");

            juice.salvageChestPrefab = LoadPrefab("Assets/StylizedCharacterPack/Prefabs/Props/Chest_Wood.prefab")
                ?? LoadPrefab($"{VfxRoot}/15_SalvageChest.prefab");
            juice.salvageChestGlowPrefab = LoadPrefab("Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Light/CFXR3 LightGlow A (Loop).prefab");
            juice.salvageChestGlowScale = 0.55f;
            juice.lighthousePrefab = LoadPrefab($"{VfxRoot}/16_Lighthouse.prefab");
            juice.trialBuoyPrefab = LoadPrefab($"{VfxRoot}/17_TrialBuoy.prefab");
            juice.bossBeaconPrefab = LoadPrefab($"{VfxRoot}/18_BossBeacon.prefab");
            juice.bossTelegraphRingPrefab = juice.mineBarrelPrefab;

            EditorUtility.SetDirty(juice);
            CopyAsset(JuicePath, JuiceResourcesPath);
        }

        static void WirePacingSettings()
        {
            var pacing = LoadOrCreate<DemoPacingSettings>(PacingPath);
            pacing.earlyLevelXpMultiplier = 0.62f;
            pacing.earlyLevelCap = 6;
            pacing.xpDropMultiplier = 1.35f;
            pacing.upgradePowerMultiplier = 1.2f;
            pacing.startingDamageMultiplier = 1.1f;
            pacing.bossBeaconMinSeconds = 180f;
            pacing.bossTelegraphSeconds = 0.7f;
            pacing.firstChestDiscount = 0.5f;
            pacing.postScriptedThreatMultiplier = 0.75f;
            EditorUtility.SetDirty(pacing);
            CopyAsset(PacingPath, PacingResourcesPath);
        }

        static void WireLevelUpSettings()
        {
            var settings = LoadOrCreate<LevelUpSettings>(LevelUpPath);
            settings.cardsPerLevel = 3;
            settings.weaponOfferWeight = 0.55f;
            settings.newWeaponWeightMultiplier = 2.5f;
            settings.excludeMaxedWeapons = true;
            settings.preventDuplicateCards = true;
            settings.scriptFirstLevelUp = true;
            settings.startingWeaponId = "swivel_guns";
            settings.commonStatLines = 1;
            settings.uncommonStatLines = 1;
            settings.rareStatLines = 2;
            settings.epicStatLines = 2;
            settings.legendaryStatLines = 3;
            settings.upgradePowerMultiplier = 1.2f;
            EditorUtility.SetDirty(settings);
            CopyAsset(LevelUpPath, LevelUpResourcesPath);
        }

        static void PopulateWeaponUpgradeAxes()
        {
            var guids = AssetDatabase.FindAssets("t:WeaponDefinition", new[] { "Assets/_Game/Data/Weapons" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
                if (weapon == null) continue;
                weapon.upgradeAxisConfigs = null;
                var defaults = weapon.GetUpgradeConfigs();
                weapon.upgradeAxisConfigs = new WeaponUpgradeAxisConfig[defaults.Count];
                for (var j = 0; j < defaults.Count; j++)
                    weapon.upgradeAxisConfigs[j] = defaults[j];
                EditorUtility.SetDirty(weapon);
            }
        }

        static void TuneDeckCannons()
        {
            var weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>("Assets/_Game/Data/Weapons/deck_cannons.asset");
            if (weapon == null) return;
            weapon.projectileColor = new Color(1f, 0.82f, 0.25f);
            weapon.size = 1.15f;
            weapon.baseDamage = 11f;
            weapon.baseCooldown = 1.05f;
            EditorUtility.SetDirty(weapon);
        }

        static void EnsureJuiceSystemsInScene()
        {
            EnsureJuiceSystemsInActiveScene();
        }

        public static void EnsureJuiceSystemsInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid())
                return;

            var systems = GameObject.Find("Systems");
            if (systems == null) return;

            if (systems.GetComponent<JuiceVfxBroker>() == null)
                systems.AddComponent<JuiceVfxBroker>();
            if (systems.GetComponent<CombatVfxController>() == null)
                systems.AddComponent<CombatVfxController>();
            if (systems.GetComponent<WeaponJuiceController>() == null)
                systems.AddComponent<WeaponJuiceController>();
            if (systems.GetComponent<FloatingDamageNumbers>() == null)
                systems.AddComponent<FloatingDamageNumbers>();
            if (systems.GetComponent<PlayerJuiceController>() == null)
                systems.AddComponent<PlayerJuiceController>();

            var vfxRoot = GameObject.Find("VFXRoot");
            if (vfxRoot == null)
            {
                vfxRoot = new GameObject("VFXRoot");
                vfxRoot.transform.SetParent(systems.transform, false);
            }

            var broker = systems.GetComponent<JuiceVfxBroker>();
            if (broker != null)
            {
                var so = new SerializedObject(broker);
                so.FindProperty("vfxRoot").objectReferenceValue = vfxRoot.transform;
                so.FindProperty("settings").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<DemoJuiceSettings>(JuicePath);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void CopyAsset(string source, string destination)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(destination) != null)
                AssetDatabase.DeleteAsset(destination);
            AssetDatabase.CopyAsset(source, destination);
        }

        static GameObject LoadPrefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>(path);

        static Material LoadMaterial(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        static void WireGunCombatVfx(DemoJuiceSettings juice)
        {
            const string gunsRoot = "Assets/GameObject/Guns";
            const string vfxRoot = gunsRoot + "/VFX";
            juice.laserBeamPrefab = LoadPrefab($"{gunsRoot}/LaserBeam.prefab");
            juice.mortarTelegraphPrefab = LoadPrefab($"{gunsRoot}/Lightning Mast.prefab");
            juice.weaponExplosionPrefab = LoadPrefab($"{gunsRoot}/Explosion_VFX.prefab");
            juice.cannonImpactPrefab = LoadPrefab($"{vfxRoot}/PowderKeg_Explosion_VFX.prefab") ?? juice.weaponExplosionPrefab;
            juice.cannonImpactScale = 0.22f;
            juice.cannonMuzzleVfxPrefab = LoadPrefab($"{gunsRoot}/Deck Cannons.prefab");
            juice.harpoonProjectilePrefab = LoadPrefab($"{gunsRoot}/Harpoon.prefab");
            juice.powderKegExplosionPrefab = LoadPrefab($"{vfxRoot}/PowderKeg_Explosion_VFX.prefab");
            juice.mortarExplosionPrefab = LoadPrefab($"{vfxRoot}/Mortar_Explosion_VFX.prefab");
            juice.mortarExplosionScaleMult = 3f;
            juice.flamethrowerStreamPrefab = LoadPrefab($"{vfxRoot}/Flamethrower_Stream_VFX.prefab");
            juice.swivelProjectileSizeMult = 0.55f;
            juice.swivelDamageMult = 0.55f;
            juice.swivelBetweenShotDelay = 0.18f;
            juice.harpoonProjectileScale = 0.55f;
        }

        static void WireWeaponVfxBindings(DemoJuiceSettings juice)
        {
            const string gunsRoot = "Assets/GameObject/Guns";
            const string vfxRoot = gunsRoot + "/VFX";
            var cannonball = LoadPrefab($"{gunsRoot}/Cannonball.prefab");
            var broadsideBall = LoadPrefab($"{vfxRoot}/Broadside_Cannonball_VFX.prefab");
            var deckMuzzle = LoadPrefab($"{vfxRoot}/DeckCannons_Muzzle_VFX.prefab");
            var broadsideMuzzle = LoadPrefab($"{vfxRoot}/Broadside_Muzzle_VFX.prefab");
            var powderExplosion = LoadPrefab($"{vfxRoot}/PowderKeg_Explosion_VFX.prefab");
            var mortarExplosion = LoadPrefab($"{vfxRoot}/Mortar_Explosion_VFX.prefab");
            var smallBall = LoadPrefab($"{gunsRoot}/Cannonball small.prefab");
            var harpoon = LoadPrefab($"{gunsRoot}/Harpoon.prefab");
            var lightning = LoadPrefab($"{gunsRoot}/Lightning Mast.prefab");
            var cutlass = LoadPrefab($"{gunsRoot}/Flying Cutlasses.prefab");
            var longNine = LoadPrefab($"{gunsRoot}/Long Nine.prefab");
            var swivel = LoadPrefab($"{gunsRoot}/Swivel Guns.prefab");
            var mortarGun = LoadPrefab($"{gunsRoot}/Mortar.prefab");

            juice.weaponVfxBindings = new[]
            {
                Binding("deck_cannons", cannonball, deckMuzzle, null, 1f, 1f, 1f, 1f),
                Binding("broadside_battery", broadsideBall, broadsideMuzzle, null, 1.15f, 1.08f, 1f, 1f),
                Binding("powder_kegs", null, null, powderExplosion, 1f, 1f, 1f, 1f),
                Binding("flying_cutlasses", cutlass, null, null, 1f, 1f, 1f, 1f),
                Binding("flamethrower", null, null, null, 1f, 1f, 1f, 1f),
                Binding("lightning_mast", null, lightning, lightning, 1f, 1f, 1f, 1f),
                Binding("long_nine", null, longNine, null, 1f, 1f, 1f, 1f),
                Binding("swivel_guns", smallBall, swivel, null, 1f, 1f, 1f, 1f),
                Binding("harpoon_gun", harpoon, null, null, 1f, 1f, 1f, 1f),
                Binding("mortar", null, mortarGun, mortarExplosion, 1f, 1f, 1f, 1f),
            };
        }

        static WeaponJuiceVfxBinding Binding(
            string weaponId,
            GameObject projectile,
            GameObject muzzle,
            GameObject impact,
            float projectileScale,
            float muzzleScale,
            float impactScale,
            float lifeMult) =>
            new()
            {
                weaponId = weaponId,
                projectilePrefab = projectile,
                muzzleVfxPrefab = muzzle,
                impactVfxPrefab = impact,
                projectileScaleMult = projectileScale,
                muzzleScaleMult = muzzleScale,
                impactScaleMult = impactScale,
                projectileLifeMult = lifeMult
            };

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            var leaf = System.IO.Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
