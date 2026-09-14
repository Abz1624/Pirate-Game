#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasV3Setup
    {
        const string ScenesV3Folder = "Assets/_Game/Scenes/v3";
        const string DataV3Folder = "Assets/_Game/Data/V3";
        const string ResourcesV3Layout = "Assets/_Game/Resources/V3SaltwindIslandLayout.asset";

        [MenuItem("HighSeas/V3/Setup All Scenes")]
        public static void SetupAll()
        {
            EnsureFolders();
            var layout = CreateOrUpdateIslandLayout();
            CreateBootScene();
            CreateMainMenuScene();
            CreateSaltwindRunScene(layout);
            UpdateBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rogue Pirates v3 setup complete. Start from V3Boot scene.");
        }

        [MenuItem("HighSeas/V3/Regenerate Island Layout Only")]
        public static void RegenerateIslandLayoutMenu()
        {
            EnsureFolders();
            CreateOrUpdateIslandLayout();
            AssetDatabase.SaveAssets();
        }

        static void EnsureFolders()
        {
            CreateFolderIfMissing("Assets/_Game/Scenes");
            CreateFolderIfMissing(ScenesV3Folder);
            CreateFolderIfMissing("Assets/_Game/Data");
            CreateFolderIfMissing(DataV3Folder);
        }

        static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            var leaf = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(leaf))
                AssetDatabase.CreateFolder(parent, leaf);
        }

        public static IslandLayoutDefinition CreateOrUpdateIslandLayout()
        {
            var demo = AssetDatabase.LoadAssetAtPath<IslandLayoutDefinition>("Assets/_Game/Data/DemoIslandLayout.asset");
            if (demo == null)
                throw new InvalidDataException("DemoIslandLayout.asset is required to position v3 islands.");

            var tropicalPrefabs = LoadTropicalIslandPrefabs();
            if (tropicalPrefabs.Count == 0)
                throw new InvalidDataException(
                    $"No island prefabs found. Searched: {string.Join(", ", IslandPrefabSearchFolders)}");

            var measuredFootprint = MeasureAverageFootprint(tropicalPrefabs);
            var targetFootprint = Mathf.Clamp(measuredFootprint > 0f ? measuredFootprint : 20f, 14f, 28f);
            Debug.Log($"V3 island auto-scale target footprint: {targetFootprint:F1}m (measured avg {measuredFootprint:F1}m)");

            var layout = AssetDatabase.LoadAssetAtPath<IslandLayoutDefinition>(V3SceneNames.V3IslandLayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<IslandLayoutDefinition>();
                AssetDatabase.CreateAsset(layout, V3SceneNames.V3IslandLayoutPath);
            }

            layout.overridePlayerSpawn = demo.overridePlayerSpawn;
            layout.playerSpawn = demo.playerSpawn;
            layout.clusters = CloneClustersWithTropicalPrefabs(demo.clusters, tropicalPrefabs, targetFootprint);

            EditorUtility.SetDirty(layout);

            var resourcesCopy = AssetDatabase.LoadAssetAtPath<IslandLayoutDefinition>(ResourcesV3Layout);
            if (resourcesCopy == null)
            {
                if (!AssetDatabase.CopyAsset(V3SceneNames.V3IslandLayoutPath, ResourcesV3Layout))
                    Debug.LogWarning("Could not copy island layout to Resources; assign manually.");
            }
            else
            {
                EditorUtility.CopySerialized(layout, resourcesCopy);
                EditorUtility.SetDirty(resourcesCopy);
            }

            return layout;
        }

        static readonly string[] IslandPrefabSearchFolders =
        {
            V3SceneNames.TropicalIslandsFolder,
            "Assets/_Game/Prefabs/Environment/Islands"
        };

        static List<GameObject> LoadTropicalIslandPrefabs()
        {
            var result = new List<GameObject>();
            foreach (var folder in IslandPrefabSearchFolders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;

                CollectIslandPrefabs(result, folder, "Island_Tropical t:Prefab");
                if (result.Count > 0)
                    break;

                CollectIslandPrefabs(result, folder, "Island_ t:Prefab");
                if (result.Count > 0)
                    break;

                CollectIslandPrefabs(result, folder, "t:Prefab");
                if (result.Count > 0)
                    break;
            }

            result.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return result;
        }

        static void CollectIslandPrefabs(List<GameObject> result, string folder, string filter)
        {
            var guids = AssetDatabase.FindAssets(filter, new[] { folder });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !prefab.name.StartsWith("Island", StringComparison.Ordinal))
                    continue;

                if (!result.Contains(prefab))
                    result.Add(prefab);
            }
        }

        static float MeasureAverageFootprint(List<GameObject> prefabs)
        {
            if (prefabs.Count == 0) return 20f;
            var total = 0f;
            for (var i = 0; i < prefabs.Count; i++)
                total += IslandVisualUtility.GetPrefabXZExtent(prefabs[i]);
            return total / prefabs.Count;
        }

        static IslandClusterDefinition[] CloneClustersWithTropicalPrefabs(
            IslandClusterDefinition[] source,
            List<GameObject> tropicalPrefabs,
            float targetFootprint)
        {
            if (source == null) return System.Array.Empty<IslandClusterDefinition>();
            var clusters = new IslandClusterDefinition[source.Length];
            for (var c = 0; c < source.Length; c++)
            {
                var src = source[c];
                clusters[c] = new IslandClusterDefinition
                {
                    clusterId = src.clusterId,
                    anchorPosition = src.anchorPosition,
                    clusterJitterRadius = src.clusterJitterRadius,
                    islands = CloneIslandEntries(src.islands, tropicalPrefabs, c, targetFootprint)
                };
            }

            return clusters;
        }

        static IslandPrefabEntry[] CloneIslandEntries(
            IslandPrefabEntry[] source,
            List<GameObject> tropicalPrefabs,
            int clusterIndex,
            float targetFootprint)
        {
            if (source == null || source.Length == 0)
                return System.Array.Empty<IslandPrefabEntry>();

            var entries = new IslandPrefabEntry[source.Length];
            for (var i = 0; i < source.Length; i++)
            {
                var prefab = tropicalPrefabs[(clusterIndex + i) % tropicalPrefabs.Count];
                entries[i] = new IslandPrefabEntry
                {
                    prefab = prefab,
                    offsetFromAnchor = source[i].offsetFromAnchor,
                    yRotation = source[i].yRotation,
                    scale = Vector3.one,
                    positionJitter = source[i].positionJitter,
                    rotationJitter = source[i].rotationJitter,
                    autoScaleToFootprint = true,
                    targetFootprintMeters = targetFootprint
                };
            }

            return entries;
        }

        static void CreateBootScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("V3BootLoader");
            boot.AddComponent<V3BootLoader>();
            SaveScene($"{ScenesV3Folder}/V3Boot.unity");
        }

        static void CreateMainMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.1f, 0.16f, 1f);
            camera.orthographic = false;
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 12f, -18f);
            cameraGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);

            var menu = new GameObject("V3MainMenu");
            menu.AddComponent<V3MainMenuController>();
            SaveScene($"{ScenesV3Folder}/V3MainMenu.unity");
        }

        static void CreateSaltwindRunScene(IslandLayoutDefinition layout)
        {
            if (!File.Exists(V3SceneNames.RunReferencePath))
                throw new FileNotFoundException($"Reference scene missing: {V3SceneNames.RunReferencePath}");

            var dest = V3SceneNames.SaltwindRunPath;
            if (!File.Exists(dest))
            {
                if (!AssetDatabase.CopyAsset(V3SceneNames.RunReferencePath, dest))
                    throw new IOException($"Failed to copy {V3SceneNames.RunReferencePath} to {dest}");
            }

            var scene = EditorSceneManager.OpenScene(dest, OpenSceneMode.Single);

            var bootstrap = UnityEngine.Object.FindFirstObjectByType<RunBootstrap>();
            if (bootstrap != null)
            {
                var go = bootstrap.gameObject;
                UnityEngine.Object.DestroyImmediate(bootstrap);
                var v3 = go.GetComponent<V3RunBootstrap>();
                if (v3 == null)
                    v3 = go.AddComponent<V3RunBootstrap>();
                SetSerializedField(v3, "islandLayout", layout);
            }
            else
            {
                var systems = GameObject.Find("Systems");
                if (systems != null)
                {
                    var v3 = systems.GetComponent<V3RunBootstrap>();
                    if (v3 == null)
                        v3 = systems.AddComponent<V3RunBootstrap>();
                    SetSerializedField(v3, "islandLayout", layout);
                }
            }

            var poi = UnityEngine.Object.FindFirstObjectByType<POIManager>();
            if (poi != null)
                poi.SetV3Options(continueAfterBossVictory: true, usePlaceholderVisuals: true);

            var stageBuilder = UnityEngine.Object.FindFirstObjectByType<StageBuilder>();
            stageBuilder?.ConfigureV3Layout(layout);

            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                if (canvas.GetComponent<V3RunResultsScreen>() == null)
                    canvas.gameObject.AddComponent<V3RunResultsScreen>();

                foreach (var legacyEnd in canvas.GetComponentsInChildren<RunEndScreen>(true))
                    legacyEnd.enabled = false;
                foreach (var tutorial in canvas.GetComponentsInChildren<TutorialOverlay>(true))
                    tutorial.enabled = false;
            }

            EditorSceneManager.SaveScene(scene);
        }

        static void UpdateBuildSettings()
        {
            var v3Scenes = new[]
            {
                $"{ScenesV3Folder}/V3Boot.unity",
                $"{ScenesV3Folder}/V3MainMenu.unity",
                V3SceneNames.SaltwindRunPath
            };

            var ordered = new List<EditorBuildSettingsScene>();
            foreach (var path in v3Scenes)
            {
                if (!File.Exists(path)) continue;
                ordered.Add(new EditorBuildSettingsScene(path, true));
            }

            var existing = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var scene in existing)
            {
                var duplicate = false;
                for (var i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i].path == scene.path)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                    ordered.Add(scene);
            }

            EditorBuildSettings.scenes = ordered.ToArray();
        }

        static void SaveScene(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), path);
        }

        static void SetSerializedField(UnityEngine.Object target, string fieldName, UnityEngine.Object value)
        {
            if (target == null) return;
            var so = new SerializedObject(target);
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"Field '{fieldName}' not found on {target.GetType().Name}");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
