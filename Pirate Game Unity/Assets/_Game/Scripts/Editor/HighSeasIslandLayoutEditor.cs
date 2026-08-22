#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HighSeas.Editor
{
    public static class HighSeasIslandLayoutEditor
    {
        const string LayoutAssetPath = "Assets/_Game/Data/DemoIslandLayout.asset";
        const string ResourcesLayoutPath = "Assets/_Game/Resources/DemoIslandLayout.asset";
        const string IslandPrefabFolder = "Assets/_Game/Prefabs/Environment/Islands";
        const string RunScenePath = "Assets/_Game/Scenes/Run.unity";

        [MenuItem("HighSeas/Islands/Spawn Demo Clusters (Edit Scene)")]
        public static void SpawnDemoClusters()
        {
            EnsureRunSceneOpen();

            var stageBuilder = Object.FindFirstObjectByType<StageBuilder>();
            if (stageBuilder == null)
            {
                Debug.LogError("No StageBuilder found in Run scene. Add StageBuilder to Systems first.");
                return;
            }

            var settings = ReadProceduralSettings(stageBuilder);
            var library = HighSeasIslandSetup.EnsureLibraryAsset();
            if (library == null || !library.HasPrefabs)
            {
                Debug.LogError("Island visual library is missing. Run HighSeas/Setup Island Visuals first.");
                return;
            }

            var islandRoot = EnsureIslandRoot(stageBuilder);
            ClearChildren(islandRoot);

            var layout = new StageGenerator().Generate(GameConstants.DemoRunSeed, 0, settings);
            var centers = layout.islandCenters;
            if (centers == null || centers.Count == 0)
            {
                Debug.LogError(
                    $"StageGenerator produced 0 island positions (target {settings.countMin}-{settings.countMax}). " +
                    "Check Procedural Islands settings on Systems/StageBuilder.");
                return;
            }

            var capsuleRadius = settings.islandDiameterMeters * 0.5f;
            for (var i = 0; i < centers.Count; i++)
            {
                CreateIslandTemplate(
                    islandRoot,
                    $"Island_{i}",
                    i,
                    i,
                    centers[i],
                    capsuleRadius,
                    library);
            }

            MarkSceneDirty();
            Debug.Log(
                $"Spawned {centers.Count} full demo islands under IslandRoot " +
                $"(seed {GameConstants.DemoRunSeed}, target {settings.countMin}-{settings.countMax}, " +
                $"dia {settings.islandDiameterMeters:F0}m, sep {settings.minSeparationMeters:F0}m). " +
                "Edit them, then run HighSeas/Islands/Bake Layout From IslandRoot to use at Play.");
        }

        [MenuItem("HighSeas/Islands/Bake Layout From IslandRoot")]
        public static void BakeLayoutFromScene()
        {
            var islandRoot = GameObject.Find("IslandRoot");
            if (islandRoot == null)
            {
                Debug.LogError("No IslandRoot found. Run Spawn Demo Clusters first.");
                return;
            }

            var markers = islandRoot.GetComponentsInChildren<DemoIslandMarker>(true);
            if (markers.Length == 0)
            {
                Debug.LogError("No DemoIslandMarker components found under IslandRoot.");
                return;
            }

            EnsureFolder(IslandPrefabFolder);

            var grouped = markers
                .GroupBy(m => m.clusterId)
                .OrderBy(g => g.Key)
                .ToList();

            var clusters = new List<IslandClusterDefinition>();

            foreach (var group in grouped)
            {
                var clusterId = group.Key;
                var positions = group.Select(m => m.transform.position).ToList();
                var anchor = AveragePosition(positions);

                var entries = new List<IslandPrefabEntry>();
                var entryIndex = 0;
                foreach (var marker in group)
                {
                    var prefabName = group.Count() == 1
                        ? $"Island_{clusterId}"
                        : $"Island_{clusterId}_{entryIndex}";
                    var prefabPath = $"{IslandPrefabFolder}/{prefabName}.prefab";

                    var prefab = SaveIslandAsPrefab(marker.gameObject, prefabPath);
                    if (prefab == null) continue;

                    var t = marker.transform;
                    entries.Add(new IslandPrefabEntry
                    {
                        prefab = prefab,
                        offsetFromAnchor = t.position - anchor,
                        yRotation = t.eulerAngles.y,
                        scale = t.localScale,
                        positionJitter = marker.positionJitter,
                        rotationJitter = marker.rotationJitter
                    });
                    entryIndex++;
                }

                clusters.Add(new IslandClusterDefinition
                {
                    clusterId = clusterId,
                    anchorPosition = anchor,
                    clusterJitterRadius = 0f,
                    islands = entries.ToArray()
                });
            }

            var layout = LoadOrCreateLayoutAsset();
            layout.clusters = clusters.ToArray();
            layout.overridePlayerSpawn = true;
            layout.playerSpawn = StageGenerator.CellToWorld(GameConstants.GridCells / 2, 2);

            EditorUtility.SetDirty(layout);
            AssetDatabase.SaveAssets();

            CopyLayoutToResources(layout);
            WireRunSceneLayout(layout);

            Debug.Log($"Baked {markers.Length} islands into {LayoutAssetPath} ({clusters.Count} clusters). Assigned to Run scene StageBuilder.");
        }

        [MenuItem("HighSeas/Islands/Clear IslandRoot")]
        public static void ClearIslandRootMenu()
        {
            var islandRoot = GameObject.Find("IslandRoot");
            if (islandRoot == null) return;
            ClearChildren(islandRoot.transform);
            MarkSceneDirty();
            Debug.Log("Cleared IslandRoot.");
        }

        [MenuItem("HighSeas/Islands/Quick Setup (Spawn + Bake)")]
        public static void QuickSetup()
        {
            EnsureRunSceneOpen();
            SpawnDemoClusters();
            BakeLayoutFromScene();
        }

        static void EnsureRunSceneOpen()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isLoaded && scene.path == RunScenePath)
                return;

            EditorSceneManager.OpenScene(RunScenePath, OpenSceneMode.Single);
        }

        static ProceduralIslandSettings ReadProceduralSettings(StageBuilder stageBuilder)
        {
            var so = new SerializedObject(stageBuilder);
            var prop = so.FindProperty("proceduralIslands");
            var settings = new ProceduralIslandSettings
            {
                countMin = prop.FindPropertyRelative("countMin").intValue,
                countMax = prop.FindPropertyRelative("countMax").intValue,
                islandDiameterMeters = prop.FindPropertyRelative("islandDiameterMeters").floatValue,
                minSeparationMeters = prop.FindPropertyRelative("minSeparationMeters").floatValue,
                maxPlacementAttempts = prop.FindPropertyRelative("maxPlacementAttempts").intValue
            };
            return settings.Clamped();
        }

        static GameObject CreateIslandTemplate(
            Transform parent,
            string name,
            int clusterId,
            int islandIndex,
            Vector3 worldPosition,
            float capsuleRadius,
            IslandVisualLibrary library)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Spawn demo island");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPosition;

            var marker = go.AddComponent<DemoIslandMarker>();
            marker.clusterId = clusterId;
            marker.capsuleRadius = capsuleRadius;
            marker.positionJitter = 0f;
            marker.rotationJitter = 0f;

            var islandDiameter = capsuleRadius * 2f;
            var decorationSeed = unchecked(GameConstants.DemoRunSeed + clusterId * 73856093 + islandIndex * 19349663);
            IslandVisualBuilder.BuildInEditor(go.transform, library, islandDiameter, decorationSeed);

            return go;
        }

        static GameObject SaveIslandAsPrefab(GameObject island, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);

            var temp = Object.Instantiate(island);
            temp.name = island.name;

            var marker = temp.GetComponent<DemoIslandMarker>();
            if (marker != null)
                Object.DestroyImmediate(marker);

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return prefab;
        }

        static IslandLayoutDefinition LoadOrCreateLayoutAsset()
        {
            var layout = AssetDatabase.LoadAssetAtPath<IslandLayoutDefinition>(LayoutAssetPath);
            if (layout != null) return layout;

            EnsureFolder("Assets/_Game/Data");
            layout = ScriptableObject.CreateInstance<IslandLayoutDefinition>();
            AssetDatabase.CreateAsset(layout, LayoutAssetPath);
            return layout;
        }

        static void CopyLayoutToResources(IslandLayoutDefinition layout)
        {
            EnsureFolder("Assets/_Game/Resources");
            if (AssetDatabase.LoadAssetAtPath<IslandLayoutDefinition>(ResourcesLayoutPath) != null)
                AssetDatabase.DeleteAsset(ResourcesLayoutPath);
            AssetDatabase.CopyAsset(LayoutAssetPath, ResourcesLayoutPath);
        }

        static void WireRunSceneLayout(IslandLayoutDefinition layout)
        {
            var stageBuilder = Object.FindFirstObjectByType<StageBuilder>();
            if (stageBuilder == null) return;

            var so = new SerializedObject(stageBuilder);
            so.FindProperty("usePredefinedLayout").boolValue = true;
            so.FindProperty("predefinedLayout").objectReferenceValue = layout;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static Transform EnsureIslandRoot(StageBuilder stageBuilder = null)
        {
            var existing = GameObject.Find("IslandRoot");
            Transform root;
            if (existing != null)
            {
                root = existing.transform;
            }
            else
            {
                var go = new GameObject("IslandRoot");
                Undo.RegisterCreatedObjectUndo(go, "Create IslandRoot");
                root = go.transform;
            }

            if (stageBuilder != null)
            {
                var so = new SerializedObject(stageBuilder);
                var islandRootProp = so.FindProperty("islandRoot");
                if (islandRootProp.objectReferenceValue == null)
                {
                    islandRootProp.objectReferenceValue = root;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return root;
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }

        static Vector3 AveragePosition(IReadOnlyList<Vector3> positions)
        {
            var sum = Vector3.zero;
            for (var i = 0; i < positions.Count; i++)
                sum += positions[i];
            return sum / positions.Count;
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

        static void MarkSceneDirty()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.IsValid())
                EditorSceneManager.MarkSceneDirty(scene);
        }
    }
}
#endif
