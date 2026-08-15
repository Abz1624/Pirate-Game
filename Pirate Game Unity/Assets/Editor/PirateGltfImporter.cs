using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Configures glTFast import defaults and provides scene instantiation helpers.
/// Unity cannot place raw .gltf files in a scene without the glTFast package.
/// </summary>
[InitializeOnLoad]
public static class PirateGltfImporter
{
    const string PirateAssetsRoot = "Assets/Pirate Assets";
    const string GameModelsRoot = "Assets/3D Models";
    static readonly string[] GltfShaderNames =
    {
        "Shader Graphs/glTF-pbrMetallicRoughness",
        "Shader Graphs/glTF-unlit",
        "glTF/PbrMetallicRoughness",
        "Universal Render Pipeline/Lit"
    };

    static PirateGltfImporter()
    {
        EditorApplication.delayCall += EnsureGltfPackagePresent;
    }

    static void EnsureGltfPackagePresent()
    {
        if (Type.GetType("GLTFast.Editor.GltfImporter, Unity.Cloud.gltfast.Editor") == null &&
            Type.GetType("GLTFast.Editor.GltfImporter, glTFast.Editor") == null)
        {
            Debug.LogWarning(
                "[Pirate Game] glTFast is not installed yet. Open Package Manager and confirm " +
                "'com.unity.cloud.gltfast' resolves, then reimport GLTF files. " +
                "Until then, dragging .gltf into the scene will not work.");
        }
    }

    [MenuItem("Pirate Game/GLTF/Reimport All Pirate GLTFs")]
    static void ReimportAllGltfs() => ReimportGltfsUnder(PirateAssetsRoot);

    [MenuItem("Pirate Game/GLTF/Reimport All Game GLTFs")]
    static void ReimportAllGameGltfs()
    {
        EnsureGltfShadersIncluded();
        ReimportGltfsUnder(GameModelsRoot);
    }

    [MenuItem("Pirate Game/GLTF/Add glTFast Shaders To Graphics Settings")]
    static void AddGltfShadersToGraphicsSettings()
    {
        var added = EnsureGltfShadersIncluded();
        if (added > 0)
            Debug.Log($"[Pirate Game] Added {added} glTFast shader(s) to Always Included Shaders.");
        else
            Debug.Log("[Pirate Game] glTFast shaders were already included, or their shader assets were not found yet.");
    }

    static void ReimportGltfsUnder(string root)
    {
        if (!AssetDatabase.IsValidFolder(root))
        {
            Debug.LogWarning($"[Pirate Game] Folder not found: {root}");
            return;
        }

        var paths = FindGltfAssetPaths(root).ToList();
        if (paths.Count == 0)
        {
            Debug.LogWarning($"[Pirate Game] No .gltf/.glb files found under {root}.");
            return;
        }

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var path in paths)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        Debug.Log($"[Pirate Game] Reimported {paths.Count} GLTF files under {root}.");
    }

    static IEnumerable<string> FindGltfAssetPaths(string assetsRoot)
    {
        var diskRoot = Path.Combine(Application.dataPath, assetsRoot["Assets/".Length..]);
        if (!Directory.Exists(diskRoot)) yield break;

        foreach (var ext in new[] { "*.gltf", "*.glb" })
        {
            foreach (var file in Directory.GetFiles(diskRoot, ext, SearchOption.AllDirectories))
            {
                var path = "Assets" + file[Application.dataPath.Length..].Replace('\\', '/');
                yield return path;
            }
        }
    }

    static int EnsureGltfShadersIncluded()
    {
        var graphicsSettings = Unsupported.GetSerializedAssetInterfaceSingleton("UnityEngine.Rendering.GraphicsSettings");
        if (graphicsSettings == null)
        {
            Debug.LogWarning("[Pirate Game] Could not access GraphicsSettings.");
            return 0;
        }

        var serialized = new SerializedObject(graphicsSettings);
        var shadersProp = serialized.FindProperty("m_AlwaysIncludedShaders");
        if (shadersProp == null)
        {
            Debug.LogWarning("[Pirate Game] Could not find m_AlwaysIncludedShaders on GraphicsSettings.");
            return 0;
        }

        var added = 0;
        foreach (var shaderName in GltfShaderNames)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) continue;
            if (ShaderAlreadyIncluded(shadersProp, shader)) continue;

            shadersProp.InsertArrayElementAtIndex(shadersProp.arraySize);
            shadersProp.GetArrayElementAtIndex(shadersProp.arraySize - 1).objectReferenceValue = shader;
            added++;
        }

        if (added > 0)
        {
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        return added;
    }

    static bool ShaderAlreadyIncluded(SerializedProperty shadersProp, Shader shader)
    {
        for (var i = 0; i < shadersProp.arraySize; i++)
        {
            if (shadersProp.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                return true;
        }

        return false;
    }

    [MenuItem("Pirate Game/GLTF/Instantiate Selected In Scene %#g")]
    static void InstantiateSelectedInScene()
    {
        var path = GetSelectedGltfPath();
        if (path == null) return;

        var instance = InstantiateGltfAtPath(path, Vector3.zero);
        if (instance == null)
        {
            EditorUtility.DisplayDialog(
                "GLTF Instantiate",
                "Could not instantiate the selected GLTF.\n\n" +
                "1. Confirm com.unity.cloud.gltfast is installed (Package Manager).\n" +
                "2. Select the .gltf in Project and run Pirate Game > GLTF > Reimport All Pirate GLTFs.\n" +
                "3. Try again, or expand the imported asset and drag the generated prefab sub-asset.",
                "OK");
            return;
        }

        Undo.RegisterCreatedObjectUndo(instance, "Instantiate GLTF");
        Selection.activeGameObject = instance;
        SceneView.lastActiveSceneView?.FrameSelected();
    }

    [MenuItem("Pirate Game/GLTF/Instantiate Selected In Scene %#g", true)]
    static bool InstantiateSelectedInSceneValidate() => GetSelectedGltfPath() != null;

    [MenuItem("Assets/Pirate Game/Instantiate GLTF In Scene", false, 2100)]
    static void ContextInstantiateGltf()
    {
        InstantiateSelectedInScene();
    }

    [MenuItem("Assets/Pirate Game/Instantiate GLTF In Scene", true)]
    static bool ContextInstantiateGltfValidate() => GetSelectedGltfPath() != null;

    static string GetSelectedGltfPath()
    {
        if (Selection.activeObject == null) return null;
        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        return IsGltfPath(path) ? path : null;
    }

    static bool IsGltfPath(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var ext = Path.GetExtension(path);
        return ext.Equals(".gltf", StringComparison.OrdinalIgnoreCase) ||
               ext.Equals(".glb", StringComparison.OrdinalIgnoreCase);
    }

    static GameObject InstantiateGltfAtPath(string assetPath, Vector3 position)
    {
        // glTFast creates GameObject sub-assets on import — prefer those.
        var subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        var prefab = subAssets.OfType<GameObject>()
            .FirstOrDefault(go => go != null && !go.name.StartsWith("$"));

        if (prefab != null)
        {
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.transform.position = position;
                instance.name = prefab.name;
                return instance;
            }
        }

        // Fallback: load via glTFast runtime API (works before sub-assets exist).
        return InstantiateViaGltFast(assetPath, position);
    }

    static GameObject InstantiateViaGltFast(string assetPath, Vector3 position)
    {
        var gltfImportType = Type.GetType("GLTFast.GltfImport, Unity.Cloud.gltfast") ??
                             Type.GetType("GLTFast.GltfImport, glTFast");
        if (gltfImportType == null) return null;

        try
        {
            var gltf = Activator.CreateInstance(gltfImportType);
            var loadMethod = gltfImportType.GetMethod("Load", new[] { typeof(string) });
            if (loadMethod == null) return null;

            var loadTask = loadMethod.Invoke(gltf, new object[] { assetPath });
            if (loadTask == null) return null;
            WaitForTask(loadTask);
            if (!GetTaskResult<bool>(loadTask)) return null;

            var parent = new GameObject(Path.GetFileNameWithoutExtension(assetPath));
            parent.transform.position = position;

            var instantiateMethod = gltfImportType.GetMethod("InstantiateMainSceneAsync");
            if (instantiateMethod == null) return parent;

            var instantiateTask = instantiateMethod.Invoke(gltf, new object[] { parent.transform });
            if (instantiateTask != null) WaitForTask(instantiateTask);

            return parent;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Pirate Game] GLTF runtime instantiate failed for {assetPath}: {ex.Message}");
            return null;
        }
    }

    static void WaitForTask(object task)
    {
        var taskType = task.GetType();
        while (!(bool)taskType.GetProperty("IsCompleted").GetValue(task))
        {
            System.Threading.Thread.Sleep(10);
        }

        if (taskType.GetProperty("IsFaulted")?.GetValue(task) is true)
        {
            var ex = taskType.GetProperty("Exception")?.GetValue(task) as AggregateException;
            throw ex?.InnerException ?? new Exception("GLTF task failed.");
        }
    }

    static T GetTaskResult<T>(object task)
    {
        return (T)task.GetType().GetProperty("Result").GetValue(task);
    }
}

/// <summary>
/// Applies sensible default import settings to GLTF files under Pirate Assets.
/// </summary>
public class PirateGltfImportPostprocessor : AssetPostprocessor
{
    void OnPreprocessAsset()
    {
        if (!assetPath.StartsWith("Assets/Pirate Assets/", StringComparison.OrdinalIgnoreCase) &&
            !assetPath.StartsWith("Assets/3D Models/", StringComparison.OrdinalIgnoreCase)) return;
        if (!assetPath.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase) &&
            !assetPath.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) return;

        var importerType = Type.GetType("GLTFast.Editor.GltfImporter, Unity.Cloud.gltfast.Editor") ??
                           Type.GetType("GLTFast.Editor.GltfImporter, glTFast.Editor");
        if (importerType == null || !importerType.IsInstanceOfType(assetImporter)) return;

        TrySetProperty(assetImporter, "ScaleFactor", 1f);
        TrySetProperty(assetImporter, "GenerateMipMaps", false);
    }

    static void TrySetProperty(object target, string propertyName, object value)
    {
        var prop = target.GetType().GetProperty(propertyName);
        if (prop != null && prop.CanWrite) prop.SetValue(target, value);
    }
}
