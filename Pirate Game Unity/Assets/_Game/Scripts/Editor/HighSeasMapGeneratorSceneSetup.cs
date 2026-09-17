#if UNITY_EDITOR
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using HighSeas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasMapGeneratorSceneSetup
    {
        [MenuItem("HighSeas/V3/Setup Map Generator Scene")]
        public static void SetupMapGeneratorScene()
        {
            if (!EnsureSceneAsset())
                return;

            var scene = EditorSceneManager.OpenScene(V3SceneNames.MapGeneratorPath, OpenSceneMode.Single);
            WireScene(scene);
            EditorSceneManager.SaveScene(scene);
            AppendToBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log($"Map Generator scene ready: {V3SceneNames.MapGeneratorPath}\nOpen it, focus Scene view, press M to generate islands (Shift+M = random seed).");
        }

        [MenuItem("HighSeas/V3/Open Map Generator Scene")]
        public static void OpenMapGeneratorScene()
        {
            if (!EnsureSceneAsset())
                return;
            EditorSceneManager.OpenScene(V3SceneNames.MapGeneratorPath, OpenSceneMode.Single);
        }

        static bool EnsureSceneAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(V3SceneNames.MapGeneratorPath) != null)
                return true;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(V3SceneNames.SaltwindRunPath) == null)
            {
                Debug.LogError($"Missing {V3SceneNames.SaltwindRunPath}. Run HighSeas/V3/Setup All Scenes first.");
                return false;
            }

            if (!AssetDatabase.CopyAsset(V3SceneNames.SaltwindRunPath, V3SceneNames.MapGeneratorPath))
            {
                Debug.LogError($"Failed to copy scene to {V3SceneNames.MapGeneratorPath}");
                return false;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(V3SceneNames.MapGeneratorPath) != null;
        }

        static void WireScene(Scene scene)
        {
            var systems = GameObject.Find("Systems");
            if (systems == null)
            {
                Debug.LogError("Map Generator scene: Systems object missing.");
                return;
            }

            var bootstrap = systems.GetComponent<V3RunBootstrap>();
            if (bootstrap != null)
                bootstrap.enabled = false;

            var legacy = systems.GetComponent<RunBootstrap>();
            if (legacy != null)
                legacy.enabled = false;

            var stageBuilder = systems.GetComponent<StageBuilder>();
            if (stageBuilder == null)
                stageBuilder = systems.AddComponent<StageBuilder>();

            var library = HighSeasIslandSetup.EnsureLibraryAsset();
            var islandRoot = GameObject.Find("IslandRoot");
            var stageSo = new SerializedObject(stageBuilder);
            if (islandRoot != null)
                stageSo.FindProperty("islandRoot").objectReferenceValue = islandRoot.transform;
            stageSo.FindProperty("visuals").objectReferenceValue = library;
            stageSo.FindProperty("useVisualPrefabs").boolValue = true;
            stageSo.FindProperty("usePredefinedLayout").boolValue = false;
            var proc = stageSo.FindProperty("proceduralIslands");
            proc.FindPropertyRelative("countMin").intValue = ProceduralIslandSettings.Default.countMin;
            proc.FindPropertyRelative("countMax").intValue = ProceduralIslandSettings.Default.countMax;
            proc.FindPropertyRelative("islandDiameterMeters").floatValue =
                ProceduralIslandSettings.Default.islandDiameterMeters;
            proc.FindPropertyRelative("minSeparationMeters").floatValue =
                ProceduralIslandSettings.Default.minSeparationMeters;
            stageSo.ApplyModifiedPropertiesWithoutUndo();

            var layout = AssetDatabase.LoadAssetAtPath<IslandLayoutDefinition>(V3SceneNames.V3IslandLayoutPath);
            if (layout != null)
                stageBuilder.ConfigureV3Layout(layout);
            else
                stageBuilder.ConfigureProceduralMode();

            var workbench = systems.GetComponent<MapGeneratorWorkbench>();
            if (workbench == null)
                workbench = systems.AddComponent<MapGeneratorWorkbench>();

            var wbSo = new SerializedObject(workbench);
            wbSo.FindProperty("stageBuilder").objectReferenceValue = stageBuilder;
            wbSo.FindProperty("proceduralOnly").boolValue = false;
            wbSo.FindProperty("useV3SaltwindLayout").boolValue = true;
            wbSo.FindProperty("v3IslandLayout").objectReferenceValue = layout;
            wbSo.FindProperty("seed").intValue = GameConstants.DemoRunSeed;
            wbSo.FindProperty("lastBuildSeed").intValue = 0;
            wbSo.ApplyModifiedPropertiesWithoutUndo();

            workbench.SyncSeedFromGameplay();

            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void AppendToBuildSettings()
        {
            var path = V3SceneNames.MapGeneratorPath;
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (var i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == path)
                    return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
#endif
