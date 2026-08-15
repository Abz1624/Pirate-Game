using PirateGame;
using PirateGame.Enemies;
using PirateGame.Net;
using PirateGame.Ship;
using UnityEditor;
using UnityEngine;

public static class SoloSurvivorSetup
{
    const string AnglerfishGltfPath = "Assets/3D Models/Enemies/anglerfish.gltf";
    const string EnemyPrefabPath = "Assets/Prefabs/Enemies/AnglerfishAir.prefab";

    [MenuItem("Pirate Game/Solo/Setup Survivor Mode (Main Scene)")]
    static void SetupMain() => SetupCore(useCoopBootstrap: true);

    static void SetupCore(bool useCoopBootstrap)
    {
        GameObject root;
        if (useCoopBootstrap)
        {
            var bootstrap = Object.FindFirstObjectByType<CoopMatchBootstrap>();
            root = bootstrap != null ? bootstrap.gameObject : GameObject.Find("SoloBootstrap");
        }
        else
        {
            root = GameObject.Find("SoloBootstrap");
        }

        if (root == null)
        {
            root = new GameObject("SoloBootstrap");
            Undo.RegisterCreatedObjectUndo(root, "Create SoloBootstrap");
        }

        if (root.GetComponent<SoloRunBootstrap>() == null)
            Undo.AddComponent<SoloRunBootstrap>(root);

        var systems = GameObject.Find("GameSystems");
        if (systems == null)
        {
            systems = new GameObject("GameSystems");
            Undo.RegisterCreatedObjectUndo(systems, "Create GameSystems");
        }

        var spawner = systems.GetComponent<EnemySpawner>();
        if (spawner == null)
            spawner = Undo.AddComponent<EnemySpawner>(systems);

        var director = systems.GetComponent<WaveDirector>();
        if (director == null)
            director = Undo.AddComponent<WaveDirector>(systems);

        var enemyPrefab = EnsureEnemyPrefab();
        WireSpawner(spawner, enemyPrefab);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            var shipMovement = player.GetComponent<ShipMovement>();
            if (shipMovement != null)
                shipMovement.enabled = true;

            var netShip = player.GetComponent<PirateGame.Ship.NetworkedShipController>();
            if (netShip != null)
                netShip.enabled = false;
        }

        var remote = GameObject.Find("RemoteShip");
        if (remote != null)
            remote.SetActive(false);

        EditorSceneManagerMarkDirty();
        Selection.activeGameObject = systems;
        EditorGUIUtility.PingObject(systems);

        EditorUtility.DisplayDialog(
            "Solo survivor mode",
            "Solo mode configured.\n\n" +
            "• SoloRunBootstrap on bootstrap (disables co-op at runtime)\n" +
            "• GameSystems with EnemySpawner + WaveDirector\n" +
            "• Player uses ShipMovement (network ship off)\n\n" +
            "Save scene (Ctrl+S), then Press Play — WASD to sail.\n\n" +
            "Run Pirate Game → Content → Build All Solo Content once to generate cards, enemy prefabs, and pickups.",
            "OK");
    }

    static GameObject EnsureEnemyPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath);
        if (existing != null)
            return existing;

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(AnglerfishGltfPath);
        if (model == null)
            return null;

        EnsureFolder("Assets/Prefabs");
        EnsureFolder("Assets/Prefabs/Enemies");

        var instance = Object.Instantiate(model);
        instance.name = "AnglerfishAir";
        instance.tag = "Enemy";
        instance.transform.localScale = Vector3.one * 0.015f;

        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, EnemyPrefabPath);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    static void WireSpawner(EnemySpawner spawner, GameObject enemyPrefab)
    {
        if (enemyPrefab == null) return;

        var so = new SerializedObject(spawner);
        var types = so.FindProperty("enemyTypes");
        types.arraySize = 1;
        var row = types.GetArrayElementAtIndex(0);
        row.FindPropertyRelative("overrideDefinition").boolValue = true;
        row.FindPropertyRelative("prefab").objectReferenceValue = enemyPrefab;
        row.FindPropertyRelative("maxHealth").floatValue = 30f;
        row.FindPropertyRelative("moveSpeed").floatValue = 4f;
        row.FindPropertyRelative("spawnWeight").floatValue = 1f;
        so.FindProperty("spawnOnStart").boolValue = false;
        so.FindProperty("autoSpawn").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static void EditorSceneManagerMarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
