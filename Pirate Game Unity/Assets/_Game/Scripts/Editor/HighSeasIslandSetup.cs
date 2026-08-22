#if UNITY_EDITOR
using System.Linq;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasIslandSetup
    {
        const string LibraryPath = "Assets/_Game/Resources/IslandVisualLibrary.asset";
        const string RunScenePath = "Assets/_Game/Scenes/Run.unity";

        [MenuItem("HighSeas/Setup Island Visuals")]
        public static void SetupIslandVisuals()
        {
            RepairRunSceneScripts();
            var library = EnsureLibraryAsset();
            WireRunScene(library);
            AssetDatabase.SaveAssets();
            Debug.Log("Island visuals configured. Play Run scene with seed 12345 to verify.");
        }

        [MenuItem("HighSeas/Repair Run Scene Scripts")]
        public static void RepairRunSceneScripts()
        {
            var scene = EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            var repaired = 0;

            foreach (var root in scene.GetRootGameObjects())
                repaired += RemoveMissingScriptsRecursive(root);

            repaired += DeduplicateStageBuilders();
            repaired += ClearBakedDemoIslands();

            if (repaired > 0)
                Debug.Log($"Repaired Run scene ({repaired} fix(es)).");
            else
                Debug.Log("Run scene script check complete — no issues found.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static int RemoveMissingScriptsRecursive(GameObject go)
        {
            var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (count > 0)
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);

            var total = count;
            for (var i = 0; i < go.transform.childCount; i++)
                total += RemoveMissingScriptsRecursive(go.transform.GetChild(i).gameObject);
            return total;
        }

        static int DeduplicateStageBuilders()
        {
            var builders = Object.FindObjectsByType<StageBuilder>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (builders.Length <= 1) return 0;

            var keep = builders
                .OrderByDescending(b => b.isActiveAndEnabled)
                .ThenByDescending(b => b.islandRootAssignedForEditor())
                .First();

            var removed = 0;
            for (var i = 0; i < builders.Length; i++)
            {
                if (builders[i] == keep) continue;
                Object.DestroyImmediate(builders[i]);
                removed++;
            }

            return removed;
        }

        static int ClearBakedDemoIslands()
        {
            var islandRoot = GameObject.Find("IslandRoot");
            if (islandRoot == null) return 0;

            var removed = 0;
            for (var i = islandRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = islandRoot.transform.GetChild(i);
                if (!child.name.StartsWith("Island_C") && !child.name.StartsWith("Island_")) continue;
                Object.DestroyImmediate(child.gameObject);
                removed++;
            }

            if (removed > 0)
                Debug.Log($"Removed {removed} baked demo island(s) from IslandRoot. Procedural islands spawn at Play.");

            return removed;
        }

        public static IslandVisualLibrary EnsureLibraryAsset()
        {
            var library = AssetDatabase.LoadAssetAtPath<IslandVisualLibrary>(LibraryPath);
            if (library == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                    AssetDatabase.CreateFolder("Assets/_Game", "Resources");

                library = ScriptableObject.CreateInstance<IslandVisualLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            library.groundTile = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/3D Models/Ground/Ground_Sand_Small.prefab");
            library.rocks = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Rocks/Rock_Small.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Rocks/Rock_Meduim.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Rocks/Rock_Large.prefab")
            };
            library.trees = new[]
            {
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Tree/Tree_01.prefab"),
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Tree/Tree_02.prefab")
            };
            library.groundScale = 1f;
            library.groundYOffset = 0f;
            EditorUtility.SetDirty(library);
            return library;
        }

        static void WireRunScene(IslandVisualLibrary library)
        {
            var scene = EditorSceneManager.GetActiveScene();
            var stageBuilder = Object.FindFirstObjectByType<StageBuilder>();
            if (stageBuilder == null)
            {
                Debug.LogError("Run scene has no StageBuilder on Systems. Re-run HighSeas/Bootstrap Project or add StageBuilder manually.");
                return;
            }

            var islandRoot = GameObject.Find("IslandRoot");
            var so = new SerializedObject(stageBuilder);
            if (islandRoot != null)
                so.FindProperty("islandRoot").objectReferenceValue = islandRoot.transform;
            so.FindProperty("visuals").objectReferenceValue = library;
            so.FindProperty("useVisualPrefabs").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            var bootstrap = Object.FindFirstObjectByType<RunBootstrap>();
            if (bootstrap != null)
            {
                var bootstrapSo = new SerializedObject(bootstrap);
                bootstrapSo.FindProperty("stageBuilder").objectReferenceValue = stageBuilder;
                bootstrapSo.FindProperty("runSeed").intValue = GameConstants.DemoRunSeed;
                bootstrapSo.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }

    static class StageBuilderEditorExtensions
    {
        public static bool islandRootAssignedForEditor(this StageBuilder builder)
        {
            var so = new SerializedObject(builder);
            return so.FindProperty("islandRoot").objectReferenceValue != null;
        }
    }
}
#endif
