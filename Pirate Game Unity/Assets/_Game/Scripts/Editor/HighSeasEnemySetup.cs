#if UNITY_EDITOR
using System.IO;
using HighSeas.Core;
using HighSeas.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasEnemySetup
    {
        const string EnemyFolder = "Assets/_Game/Data/Enemies";
        const string DatabasePath = "Assets/_Game/Data/GameDatabase.asset";

        const string CannonboatPrefabPath = "Assets/GameObject/Cannonboat.prefab";
        const string SharkPrefabPath = "Assets/GameObject/Shark.prefab";
        const string SwordfishPrefabPath = "Assets/GameObject/SwordFish.prefab";
        const string HammerheadPrefabPath = "Assets/GameObject/HammerHead.prefab";
        const string KrakenPrefabPath = "Assets/GameObject/Kraken.prefab";

        [MenuItem("HighSeas/Setup Enemy Visuals")]
        public static void SetupEnemyVisuals()
        {
            EnsureFolder(EnemyFolder);

            var cannonboat = LoadPrefab(CannonboatPrefabPath);
            var shark = LoadPrefab(SharkPrefabPath);
            var swordfish = LoadPrefab(SwordfishPrefabPath);
            var hammerhead = LoadPrefab(HammerheadPrefabPath);
            var kraken = LoadPrefab(KrakenPrefabPath);

            var enemies = new[]
            {
                MakeEnemy(
                    "cannonboat", "Cannonboat", EnemyCategory.Ship, cannonboat,
                    hp: 18f, dmg: 8f, speed: 3.8f, threat: 3, radius: 1.2f, scale: 1f, yaw: 0f,
                    behavior: EnemyBehavior.DirectChaser, xp: 3, gold: 1),
                MakeEnemy(
                    "shark", "Shark", EnemyCategory.Creature, shark,
                    hp: 16f, dmg: 9f, speed: 5f, threat: 4, radius: 1.1f, scale: 1f, yaw: 0f,
                    behavior: EnemyBehavior.DirectChaser, xp: 4, gold: 1),
                MakeEnemy(
                    "swordfish", "Swordfish", EnemyCategory.Creature, swordfish,
                    hp: 10f, dmg: 6f, speed: 6.5f, threat: 2, radius: 0.9f, scale: 1f, yaw: 0f,
                    behavior: EnemyBehavior.FastSwarm, xp: 2, gold: 1),
                MakeEnemy(
                    "hammerhead", "Hammerhead", EnemyCategory.Creature, hammerhead,
                    hp: 14f, dmg: 7f, speed: 5f, threat: 3, radius: 1.1f, scale: 1f, yaw: 0f,
                    behavior: EnemyBehavior.FlankingChaser, xp: 3, gold: 1),
                MakeEnemy(
                    "kraken_spawn", "Kraken Spawn", EnemyCategory.Creature, kraken,
                    hp: 28f, dmg: 11f, speed: 2.5f, threat: 6, radius: 1.6f, scale: 0.65f, yaw: 0f,
                    behavior: EnemyBehavior.TankChaser, xp: 6, gold: 2),
                MakeEnemy(
                    "reef_crab_miniboss", "Reef Kraken", EnemyCategory.Creature, kraken,
                    hp: 120f, dmg: 14f, speed: 2f, threat: 8, radius: 2.2f, scale: 1.15f, yaw: 0f,
                    behavior: EnemyBehavior.MaintainRange, xp: 35, gold: 50, isMiniboss: true),
                MakeEnemy(
                    "barnacle_king", "Barnacle Kraken", EnemyCategory.Creature, kraken,
                    hp: 800f, dmg: 18f, speed: 2.5f, threat: 9, radius: 2.8f, scale: 1.75f, yaw: 0f,
                    behavior: EnemyBehavior.TankChaser, xp: 100, gold: 150, isBoss: true)
            };

            var database = AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            if (database == null)
            {
                Debug.LogError($"GameDatabase not found at {DatabasePath}. Run HighSeas/Bootstrap Project first.");
                return;
            }

            database.enemies = enemies;
            EditorUtility.SetDirty(database);
            AssetDatabase.SaveAssets();
            Debug.Log(
                "Enemy visuals configured: 1 ship (Cannonboat) + 4 sea creatures + miniboss/boss. " +
                "Play Run scene to verify spawns.");
            SetupSpawnSettings();
        }

        static EnemyDefinition MakeEnemy(
            string id,
            string displayName,
            EnemyCategory category,
            GameObject prefab,
            float hp,
            float dmg,
            float speed,
            int threat,
            float radius,
            float scale,
            float yaw,
            EnemyBehavior behavior,
            int xp,
            int gold,
            bool isBoss = false,
            bool isMiniboss = false)
        {
            var path = $"{EnemyFolder}/{id}.asset";
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (enemy == null)
            {
                enemy = ScriptableObject.CreateInstance<EnemyDefinition>();
                AssetDatabase.CreateAsset(enemy, path);
            }

            enemy.id = id;
            enemy.displayName = displayName;
            enemy.category = category;
            enemy.visualPrefab = prefab;
            enemy.visualScale = scale;
            enemy.visualYawOffset = yaw;
            enemy.baseHP = hp;
            enemy.baseDamage = dmg;
            enemy.moveSpeed = speed;
            enemy.radius = radius;
            enemy.threatCost = threat;
            enemy.behavior = behavior;
            enemy.xpValue = xp;
            enemy.goldValue = gold;
            enemy.isBoss = isBoss;
            enemy.isMiniboss = isMiniboss;
            enemy.placeholderColor = category == EnemyCategory.Ship
                ? new Color(0.75f, 0.45f, 0.2f)
                : new Color(0.2f, 0.65f, 0.85f);
            EditorUtility.SetDirty(enemy);
            return enemy;
        }

        static GameObject LoadPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                Debug.LogWarning($"Missing enemy prefab: {path}");
            return prefab;
        }

        [MenuItem("HighSeas/Setup Spawn Settings")]
        public static void SetupSpawnSettings()
        {
            const string path = "Assets/_Game/Data/SpawnDirectorSettings.asset";
            const string resourcesPath = "Assets/_Game/Resources/SpawnDirectorSettings.asset";

            var settings = AssetDatabase.LoadAssetAtPath<Enemies.SpawnDirectorSettings>(path);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<Enemies.SpawnDirectorSettings>();
                settings.timingMode = Enemies.SpawnTimingMode.ScriptedWaves;
                settings.threatMultiplier = 0.85f;
                settings.creatureChanceAtLateGame = 0.5f;
                settings.waves = Enemies.SpawnDirectorSettings.CreateDefaultWaveScript();
                settings.enemies = Enemies.SpawnDirectorSettings.CreateDefaultEnemyTuning();
                EnsureFolder("Assets/_Game/Data");
                AssetDatabase.CreateAsset(settings, path);
            }
            else
            {
                settings.timingMode = Enemies.SpawnTimingMode.ScriptedWaves;
                if (settings.waves == null || settings.waves.Length == 0)
                    settings.waves = Enemies.SpawnDirectorSettings.CreateDefaultWaveScript();
                EditorUtility.SetDirty(settings);
            }

            EnsureFolder("Assets/_Game/Resources");
            if (AssetDatabase.LoadAssetAtPath<Enemies.SpawnDirectorSettings>(resourcesPath) != null)
                AssetDatabase.DeleteAsset(resourcesPath);
            AssetDatabase.CopyAsset(path, resourcesPath);

            var director = Object.FindFirstObjectByType<Enemies.SpawnDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                so.FindProperty("settings").objectReferenceValue = settings;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Spawn settings configured. Select SpawnDirectorSettings to edit waves, counts, and enemy speed.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
#endif
