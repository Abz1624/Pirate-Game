using PirateGame;
using PirateGame.Enemies;
using PirateGame.Progression;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SoloSceneSetup
{
    const string SoloScenePath = "Assets/Scenes/Solo.unity";

    [MenuItem("Pirate Game/Solo/Setup Solo Scene")]
    public static void SetupSoloScene()
    {
        if (!System.IO.File.Exists(SoloScenePath))
        {
            EditorUtility.DisplayDialog("Solo Scene", $"Scene not found at {SoloScenePath}", "OK");
            return;
        }

        if (EditorSceneManager.GetActiveScene().path != SoloScenePath)
            EditorSceneManager.OpenScene(SoloScenePath);

        EnsureBootstrap();
        IslandObstacleSetup.EnsureIslandLayer();
        IslandObstacleSetup.SetupIslandCollidersInScene();
        var systems = EnsureGameSystems();
        var player = PlayerShipBuilder.EnsurePlayerShipInScene(new Vector3(0f, 0.2f, 0f));
        PlayerShipBuilder.SetupCamera(player.transform);
        EnsureDraftUi();
        EnsureHealthHud();
        EnsureScoreHud();
        ConfigureSpawnerForSeaArena(systems.GetComponent<EnemySpawner>());
        ContentSetupEditor.WireEnemySpawnerIfPresent();
        AddSoloSceneToBuildSettings();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Selection.activeGameObject = player;

        EditorUtility.DisplayDialog(
            "Solo scene ready",
            "Solo.unity is configured:\n\n" +
            "• PlayerShip — item_4x8_pirategalleonred\n" +
            "• Top-down camera follow\n" +
            "• SoloRunBootstrap + wave spawner\n" +
            "• Level-up draft UI\n\n" +
            "If enemies don't spawn, run Pirate Game → Content → Build All Solo Content.\n" +
            "Save (Ctrl+S) and press Play.",
            "OK");
    }

    static void EnsureBootstrap()
    {
        var root = GameObject.Find("SoloBootstrap");
        if (root == null)
        {
            root = new GameObject("SoloBootstrap");
            Undo.RegisterCreatedObjectUndo(root, "Create SoloBootstrap");
        }

        if (root.GetComponent<SoloRunBootstrap>() == null)
            Undo.AddComponent<SoloRunBootstrap>(root);
    }

    static GameObject EnsureGameSystems()
    {
        var systems = GameObject.Find("GameSystems");
        if (systems == null)
        {
            systems = new GameObject("GameSystems");
            Undo.RegisterCreatedObjectUndo(systems, "Create GameSystems");
        }

        if (systems.GetComponent<EnemySpawner>() == null)
            Undo.AddComponent<EnemySpawner>(systems);

        if (systems.GetComponent<WaveDirector>() == null)
            Undo.AddComponent<WaveDirector>(systems);

        return systems;
    }

    static void EnsureDraftUi()
    {
        if (Object.FindFirstObjectByType<SoloDraftUI>() != null)
            return;

        var uiGo = new GameObject("SoloDraftUI");
        Undo.RegisterCreatedObjectUndo(uiGo, "Create SoloDraftUI");
        Undo.AddComponent<SoloDraftUI>(uiGo);
    }

    static void ConfigureSpawnerForSeaArena(EnemySpawner spawner)
    {
        if (spawner == null) return;

        var so = new SerializedObject(spawner);
        so.FindProperty("spawnOnStart").boolValue = false;
        so.FindProperty("autoSpawn").boolValue = false;
        so.FindProperty("spawnRadius").floatValue = 14f;
        so.FindProperty("spawnRadiusJitter").floatValue = 0f;
        so.FindProperty("minSpawnRadius").floatValue = 14f;
        so.FindProperty("maxSpawnRadius").floatValue = 14f;
        so.FindProperty("spawnHeight").floatValue = 0.2f;
        so.FindProperty("maxAlive").intValue = 36;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureHealthHud()
    {
        if (Object.FindFirstObjectByType<PirateGame.UI.PlayerHealthHud>() != null)
            return;

        var hudGo = new GameObject("PlayerHealthHud");
        Undo.RegisterCreatedObjectUndo(hudGo, "Create PlayerHealthHud");
        Undo.AddComponent<PirateGame.UI.PlayerHealthHud>(hudGo);
    }

    static void EnsureScoreHud()
    {
        if (Object.FindFirstObjectByType<PirateGame.UI.SoloScoreHud>() != null)
            return;

        var hudGo = new GameObject("SoloScoreHud");
        Undo.RegisterCreatedObjectUndo(hudGo, "Create SoloScoreHud");
        Undo.AddComponent<PirateGame.UI.SoloScoreHud>(hudGo);
    }

    static void AddSoloSceneToBuildSettings()
    {
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var hasSolo = false;
        for (var i = 0; i < scenes.Count; i++)
        {
            if (scenes[i].path == SoloScenePath)
            {
                hasSolo = true;
                if (!scenes[i].enabled)
                {
                    scenes[i] = new EditorBuildSettingsScene(SoloScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                }
                return;
            }
        }

        scenes.Insert(0, new EditorBuildSettingsScene(SoloScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
