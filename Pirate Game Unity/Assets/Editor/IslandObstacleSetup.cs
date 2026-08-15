using PirateGame.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IslandObstacleSetup
{
    [MenuItem("Pirate Game/Environment/Setup Island Colliders")]
    public static void SetupIslandCollidersInScene()
    {
        EnsureIslandLayer();
        IslandObstacle.ConfigureScene();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[Island] Configured island, rock, and tree obstacles on the Island layer.");
    }

    public static void EnsureIslandLayer()
    {
        var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManager == null || tagManager.Length == 0)
            return;

        var serialized = new SerializedObject(tagManager[0]);
        var layers = serialized.FindProperty("layers");

        for (var i = 0; i < 32; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == IslandObstacles.LayerName)
                return;
        }

        for (var i = 6; i < 32; i++)
        {
            var slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue))
                continue;

            slot.stringValue = IslandObstacles.LayerName;
            serialized.ApplyModifiedProperties();
            return;
        }

        Debug.LogWarning("[Island] No free user layer slot found for the Island layer.");
    }
}
