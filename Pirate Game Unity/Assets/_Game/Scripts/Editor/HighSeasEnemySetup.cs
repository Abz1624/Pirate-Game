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
        const string CrabPrefabPath = "Assets/GameObject/Crab.prefab";
        const string CrabElitePrefabPath = "Assets/GameObject/CrabElite.prefab";
        const string MantaRayPrefabPath = "Assets/GameObject/MantaRay.prefab";

        [MenuItem("HighSeas/Setup Enemy Visuals")]
        public static void SetupEnemyVisuals()
        {
            EnsureFolder(EnemyFolder);

            var cannonboat = LoadPrefab(CannonboatPrefabPath);
            var shark = LoadPrefab(SharkPrefabPath);
            var swordfish = LoadPrefab(SwordfishPrefabPath);
            var hammerhead = LoadPrefab(HammerheadPrefabPath);
            var kraken = LoadPrefab(KrakenPrefabPath);
            var crab = LoadPrefab(CrabPrefabPath);
            var crabElite = LoadPrefab(CrabElitePrefabPath);
            var mantaRay = LoadPrefab(MantaRayPrefabPath);

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
                    "manta_ray", "Manta Ray", EnemyCategory.Creature, mantaRay,
                    hp: 22f, dmg: 8f, speed: 4.5f, threat: 4, radius: 1.4f, scale: 1.05f, yaw: 0f,
                    behavior: EnemyBehavior.FlankingChaser, xp: 5, gold: 2, heightOffset: 0.82f),
                MakeEnemy(
                    "reef_crab_miniboss", "Reef Crab", EnemyCategory.Creature, crab,
                    hp: 120f, dmg: 14f, speed: 2f, threat: 8, radius: 2.2f, scale: 1f, yaw: 0f,
                    behavior: EnemyBehavior.MaintainRange, xp: 35, gold: 50, isMiniboss: true),
                MakeEnemy(
                    "barnacle_king", "Barnacle King", EnemyCategory.Creature,
                    crabElite != null ? crabElite : crab,
                    hp: 800f, dmg: 18f, speed: 2.5f, threat: 9, radius: 2.8f, scale: 2.4f, yaw: 0f,
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
                "Enemy visuals configured: ship + sea creatures + manta ray + reef crab miniboss/boss. " +
                "Play Run or test scenes to verify spawns.");
            HighSeasEncounterSpawnSetup.SyncScheduleToResources();
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
            float heightOffset = 0f,
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
            enemy.visualHeightOffset = heightOffset;
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
