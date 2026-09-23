#if UNITY_EDITOR
using System.IO;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.Playtest;
using HighSeas.POI;
using HighSeas.V3;
using HighSeas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasWeaponEnemyPlaytestSceneSetup
    {
        const string Root = "Assets/_Game";
        const string V3Template = Root + "/Scenes/v3/V3SaltwindRun.unity";
        const string TargetScene = Root + "/Scenes/Tests/Test_WeaponEnemy.unity";
        const string DatabasePath = Root + "/Data/GameDatabase.asset";

        [MenuItem("HighSeas/Scenes/Open Weapon+Enemy Playtest")]
        public static void OpenScene()
        {
            EnsureSceneExists();
            if (File.Exists(TargetScene))
                EditorSceneManager.OpenScene(TargetScene);
        }

        [MenuItem("HighSeas/Scenes/Create or Update Weapon+Enemy Playtest Scene")]
        public static void CreateOrUpdateScene()
        {
            RebuildSceneFromV3Template();
            var scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            ConfigurePlaytestScene();
            EditorSceneManager.SaveScene(scene);
            AddToBuildSettingsIfMissing(TargetScene);
            Debug.Log(
                "Weapon+Enemy playtest scene rebuilt from V3SaltwindRun.\n" +
                "Play: [ ] weapons, ; ' enemies, P spawn, Shift+P Barnacle King, F1 help.");
        }

        public static void CreateOrUpdateSceneBatch() => CreateOrUpdateScene();

        static void EnsureSceneExists()
        {
            if (!File.Exists(TargetScene))
                RebuildSceneFromV3Template();
        }

        static void RebuildSceneFromV3Template()
        {
            if (!File.Exists(V3Template))
            {
                Debug.LogError($"Missing V3 template scene: {V3Template}");
                return;
            }

            if (File.Exists(TargetScene))
                AssetDatabase.DeleteAsset(TargetScene);

            AssetDatabase.CopyAsset(V3Template, TargetScene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void ConfigurePlaytestScene()
        {
            CleanupLegacySandbox();

            foreach (var v3Boot in Object.FindObjectsByType<V3RunBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                v3Boot.enabled = false;

            foreach (var runBoot in Object.FindObjectsByType<RunBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(runBoot);

            var systems = GameObject.Find("Systems");
            if (systems == null)
            {
                Debug.LogError("Playtest scene has no Systems object.");
                return;
            }

            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(systems);

            var boot = systems.GetComponent<WeaponEnemyPlaytestBootstrap>();
            if (boot == null)
                boot = systems.AddComponent<WeaponEnemyPlaytestBootstrap>();

            WireBootstrap(boot, systems);

            var director = systems.GetComponent<WeaponEnemyPlaytestDirector>();
            if (director == null)
                director = systems.AddComponent<WeaponEnemyPlaytestDirector>();

            var player = Object.FindFirstObjectByType<PlayerShipController>();
            if (player != null)
            {
                var ship = new SerializedObject(player);
                ship.FindProperty("modelForwardIsNegativeZ").boolValue = false;
                ship.ApplyModifiedPropertiesWithoutUndo();
            }

            DisableSaltwindOnlyBehaviours(systems);
        }

        static void WireBootstrap(WeaponEnemyPlaytestBootstrap boot, GameObject systems)
        {
            var so = new SerializedObject(boot);
            so.FindProperty("database").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameDatabase>(DatabasePath);
            so.FindProperty("stageBuilder").objectReferenceValue = systems.GetComponent<StageBuilder>();
            so.FindProperty("player").objectReferenceValue = Object.FindFirstObjectByType<PlayerShipController>();
            so.FindProperty("islandLayout").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<IslandLayoutDefinition>(Root + "/Data/V3/V3SaltwindIslandLayout.asset");
            if (so.FindProperty("islandLayout").objectReferenceValue == null)
                so.FindProperty("islandLayout").objectReferenceValue =
                    Resources.Load<IslandLayoutDefinition>("V3SaltwindIslandLayout");
            so.FindProperty("runSeed").intValue = GameConstants.DemoRunSeed;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void DisableSaltwindOnlyBehaviours(GameObject systems)
        {
            foreach (var dir in Object.FindObjectsByType<V3PressureBeatDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                dir.enabled = false;
            foreach (var dir in Object.FindObjectsByType<V3RouteEventDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                dir.enabled = false;
            foreach (var dir in Object.FindObjectsByType<V3OvertimeDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                dir.enabled = false;
            foreach (var poi in Object.FindObjectsByType<POIManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                poi.enabled = false;
        }

        static void CleanupLegacySandbox()
        {
            var tuningHud = GameObject.Find("TuningSandboxHud");
            if (tuningHud != null)
                Object.DestroyImmediate(tuningHud);

            var systems = GameObject.Find("Systems");
            if (systems != null)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(systems);
        }

        static void AttachDirector()
        {
            ConfigurePlaytestScene();
        }

        static void AddToBuildSettingsIfMissing(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var entry in scenes)
            {
                if (entry.path == scenePath)
                    return;
            }

            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
