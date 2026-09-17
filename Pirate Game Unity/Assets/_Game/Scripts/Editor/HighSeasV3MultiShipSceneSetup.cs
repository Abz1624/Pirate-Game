#if UNITY_EDITOR

using System.IO;

using HighSeas.V3;

using HighSeas.V3.MultiShip;

using UnityEditor;

using UnityEditor.SceneManagement;

using UnityEngine;



namespace HighSeas.Editor

{

    public static class HighSeasV3MultiShipSceneSetup

    {

        [MenuItem("HighSeas/V3/Open Co-op Scene (V3CoOpRun)")]

        public static void OpenCoOpScene() => OpenPrototypeScene(V3SceneNames.CoOpRunPath, V3RunSessionKind.CoOp);



        [MenuItem("HighSeas/V3/Open Split-Screen Co-op Scene")]

        public static void OpenSplitScreenCoOpScene() =>
            OpenPrototypeScene(V3SceneNames.SplitScreenCoOpRunPath, V3RunSessionKind.SplitScreenCoOp);



        [MenuItem("HighSeas/V3/Open PvP Scene (V3PvPBattle)")]

        public static void OpenPvPScene() => OpenPrototypeScene(V3SceneNames.PvPBattlePath, V3RunSessionKind.PvP);



        [MenuItem("HighSeas/V3/Setup PvP Scene (V3PvPBattle)")]

        public static void SetupPvPSceneOnly()

        {

            if (!CreateOrUpdateScene(V3SceneNames.PvPBattlePath, V3RunSessionKind.PvP))

                return;

            AppendScenesToBuildSettings();

            AssetDatabase.SaveAssets();

            Debug.Log($"PvP scene ready at {V3SceneNames.PvPBattlePath}");

        }



        [MenuItem("HighSeas/V3/Setup Co-op Scene (V3CoOpRun)")]

        public static void SetupCoOpSceneOnly()

        {

            if (!CreateOrUpdateScene(V3SceneNames.CoOpRunPath, V3RunSessionKind.CoOp))

                return;

            AppendScenesToBuildSettings();

            AssetDatabase.SaveAssets();

            Debug.Log($"Co-op scene ready at {V3SceneNames.CoOpRunPath}");

        }



        [MenuItem("HighSeas/V3/Setup Split-Screen Co-op Scene")]

        public static void SetupSplitScreenCoOpSceneOnly()

        {

            if (!CreateOrUpdateScene(V3SceneNames.SplitScreenCoOpRunPath, V3RunSessionKind.SplitScreenCoOp))

                return;

            AppendScenesToBuildSettings();

            AssetDatabase.SaveAssets();

            Debug.Log($"Split-screen co-op scene ready at {V3SceneNames.SplitScreenCoOpRunPath}");

        }



        [MenuItem("HighSeas/V3/Setup PvP + Co-op Scenes")]

        public static void SetupMultiShipScenes()

        {

            if (!CreateOrUpdateScene(V3SceneNames.PvPBattlePath, V3RunSessionKind.PvP))

                return;

            if (!CreateOrUpdateScene(V3SceneNames.CoOpRunPath, V3RunSessionKind.CoOp))

                return;

            if (!CreateOrUpdateScene(V3SceneNames.SplitScreenCoOpRunPath, V3RunSessionKind.SplitScreenCoOp))

                return;

            AppendScenesToBuildSettings();

            AssetDatabase.SaveAssets();

            Debug.Log("V3 PvP, Co-op, and Split-screen co-op scenes ready (under Assets/_Game/Scenes/v3/).");

        }



        static void OpenPrototypeScene(string scenePath, V3RunSessionKind kind)

        {

            if (!EnsureSceneAsset(scenePath))

            {

                Debug.LogWarning($"{scenePath} was missing — run Setup for {kind} first.");

                if (kind == V3RunSessionKind.PvP)

                    SetupPvPSceneOnly();

                else if (kind == V3RunSessionKind.SplitScreenCoOp)

                    SetupSplitScreenCoOpSceneOnly();

                else

                    SetupCoOpSceneOnly();

            }



            if (!EnsureSceneAsset(scenePath))

                return;



            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);

            if (sceneAsset != null)

            {

                EditorGUIUtility.PingObject(sceneAsset);

                Selection.activeObject = sceneAsset;

            }



            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Debug.Log($"Opened {scenePath} ({kind}).");

        }



        /// <summary>Ensure Unity has imported <paramref name="destPath"/> as a scene asset.</summary>

        static bool EnsureSceneAsset(string destPath)

        {

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(destPath) != null)

                return true;



            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(V3SceneNames.SaltwindRunPath) == null)

            {

                Debug.LogError($"Missing base scene {V3SceneNames.SaltwindRunPath}. Run HighSeas/V3/Setup All Scenes first.");

                return false;

            }



            RemoveUnimportedSceneFiles(destPath);

            if (!AssetDatabase.CopyAsset(V3SceneNames.SaltwindRunPath, destPath))

            {

                Debug.LogError($"AssetDatabase.CopyAsset failed: {V3SceneNames.SaltwindRunPath} -> {destPath}");

                return false;

            }



            AssetDatabase.SaveAssets();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return AssetDatabase.LoadAssetAtPath<SceneAsset>(destPath) != null;

        }



        static void RemoveUnimportedSceneFiles(string assetPath)

        {

            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)

            {

                AssetDatabase.DeleteAsset(assetPath);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                return;

            }



            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

            var fullPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar)));

            if (File.Exists(fullPath))

            {

                File.Delete(fullPath);

                var meta = fullPath + ".meta";

                if (File.Exists(meta))

                    File.Delete(meta);

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            }

        }



        static bool CreateOrUpdateScene(string destPath, V3RunSessionKind kind)

        {

            if (!EnsureSceneAsset(destPath))

                return false;



            var scene = EditorSceneManager.OpenScene(destPath, OpenSceneMode.Single);

            var systems = GameObject.Find("Systems");

            if (systems == null)

            {

                Debug.LogError($"Systems object missing in {destPath}");

                return false;

            }



            var config = systems.GetComponent<V3MultiShipSceneConfig>();

            if (config == null)

                config = systems.AddComponent<V3MultiShipSceneConfig>();



            var so = new SerializedObject(config);

            so.FindProperty("sessionKind").enumValueIndex = (int)kind;

            so.FindProperty("shipSpacing").floatValue = 22f;

            so.ApplyModifiedPropertiesWithoutUndo();



            EditorSceneManager.SaveScene(scene);

            return true;

        }



        static void AppendScenesToBuildSettings()

        {

            var paths = new[]

            {

                V3SceneNames.PvPBattlePath,

                V3SceneNames.CoOpRunPath,

                V3SceneNames.SplitScreenCoOpRunPath

            };



            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var path in paths)

            {

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)

                    continue;



                var exists = false;

                foreach (var s in scenes)

                {

                    if (s.path == path)

                    {

                        exists = true;

                        break;

                    }

                }



                if (!exists)

                    scenes.Add(new EditorBuildSettingsScene(path, true));

            }



            EditorBuildSettings.scenes = scenes.ToArray();

        }

    }

}

#endif


