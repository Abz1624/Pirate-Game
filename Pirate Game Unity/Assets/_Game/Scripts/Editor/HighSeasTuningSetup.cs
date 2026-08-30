#if UNITY_EDITOR
using HighSeas.Tuning;
using HighSeas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasTuningSetup
    {
        const string Root = "Assets/_Game";
        const string TuningScenePath = Root + "/Scenes/Tests/Test_Tuning.unity";
        const string RunScenePath = Root + "/Scenes/Run.unity";

        [MenuItem("HighSeas/Sync Tuning To Runtime")]
        public static void SyncTuningToRuntime()
        {
            var synced = TuningAssetSync.SyncDataToResources(saveAssets: true);
            Debug.Log(synced > 0
                ? $"Synced {synced} tuning asset(s) from Assets/_Game/Data/ to Assets/_Game/Resources/. Enter Play Mode to test changes."
                : "No tuning assets were synced. Check Assets/_Game/Data/.");
        }

        [MenuItem("HighSeas/Create Tuning Sandbox Scene")]
        public static void CreateTuningSandboxScene()
        {
            if (!System.IO.File.Exists(RunScenePath))
            {
                Debug.LogError("Run.unity not found. Run HighSeas/Bootstrap Project first.");
                return;
            }

            EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
            WireTuningComponents();
            MinimizeIslands();
            EnsureFolder(Root + "/Scenes/Tests");
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), TuningScenePath);
            AddSceneToBuildSettings(TuningScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Tuning sandbox saved to {TuningScenePath}. Open it and press Play to tune weapons/VFX.");
        }

        [MenuItem("HighSeas/Open Tuning Sandbox Scene")]
        public static void OpenTuningSandboxScene()
        {
            if (!System.IO.File.Exists(TuningScenePath))
                CreateTuningSandboxScene();
            else
                EditorSceneManager.OpenScene(TuningScenePath);
        }

        public static void WireTuningComponents()
        {
            var systems = GameObject.Find("Systems");
            if (systems == null)
            {
                Debug.LogWarning("Systems object not found in scene.");
                return;
            }

            if (systems.GetComponent<TuningSandbox>() == null)
                systems.AddComponent<TuningSandbox>();

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null && canvas.GetComponentInChildren<TuningSandboxHud>(true) == null)
            {
                var hudGo = new GameObject("TuningSandboxHud", typeof(RectTransform));
                hudGo.transform.SetParent(canvas.transform, false);
                hudGo.AddComponent<TuningSandboxHud>();
            }
        }

        public static void MinimizeIslands()
        {
            var stageBuilder = Object.FindFirstObjectByType<StageBuilder>();
            if (stageBuilder == null) return;

            var so = new SerializedObject(stageBuilder);
            var proc = so.FindProperty("proceduralIslands");
            if (proc == null) return;

            proc.FindPropertyRelative("countMin").intValue = 2;
            proc.FindPropertyRelative("countMax").intValue = 4;
            proc.FindPropertyRelative("islandDiameterMeters").floatValue = 10f;
            proc.FindPropertyRelative("minSeparationMeters").floatValue = 22f;
            proc.FindPropertyRelative("maxPlacementAttempts").intValue = 2000;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddSceneToBuildSettings(string scenePath)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (var i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == scenePath)
                    return;
            }

            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

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
