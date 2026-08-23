#if UNITY_EDITOR
using HighSeas.Data;
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

        const string ExplosionPath = "Assets/GameObject/Explosion_Simple.prefab";
        const string ChestPath = "Assets/StylizedCharacterPack/Prefabs/Props/Chest_Wood.prefab";
        const string BarrelPath = "Assets/StylizedCharacterPack/Prefabs/Props/Barrel_Wood.prefab";
        const string RockPath = "Assets/3D Models/Rocks/Rock_Meduim.prefab";

        [MenuItem("HighSeas/Setup Demo Juice (Phases 6-9)")]
        public static void SetupDemoJuice()
        {
            EnsureFolder("Assets/_Game/Data");
            EnsureFolder("Assets/_Game/Resources");
            EnsureFolder("Assets/_Game/Prefabs/VFX");

            var juice = LoadOrCreate<DemoJuiceSettings>(JuicePath);
            var explosion = LoadPrefab(ExplosionPath);
            juice.killExplosionPrefab = explosion;
            juice.eliteKillExplosionPrefab = explosion;
            juice.bossKillExplosionPrefab = explosion;
            juice.hitSparkPrefab = explosion;
            juice.levelUpBurstPrefab = explosion;
            juice.salvageChestPrefab = LoadPrefab(ChestPath);
            juice.trialBuoyPrefab = LoadPrefab(BarrelPath);
            juice.lighthousePrefab = LoadPrefab(RockPath);
            juice.bossBeaconPrefab = null;

            EditorUtility.SetDirty(juice);
            CopyAsset(JuicePath, JuiceResourcesPath);

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

            TuneDeckCannons();
            EnsureCombatVfxInScene();

            AssetDatabase.SaveAssets();
            Debug.Log("Demo juice + pacing configured. Explosion on kill, brighter projectiles, POI props, and demo pacing are ready.");
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

        static void EnsureCombatVfxInScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.path.Contains("Run.unity"))
            {
                Debug.Log("CombatVfxController will be added automatically at runtime. Open Run.unity to bake it into the scene.");
                return;
            }

            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<CombatVfxController>() == null)
                systems.AddComponent<CombatVfxController>();
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
