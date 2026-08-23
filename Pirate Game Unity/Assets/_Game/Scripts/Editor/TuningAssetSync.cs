#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class TuningAssetSync
    {
        public const string Root = "Assets/_Game";

        static readonly (string data, string resources)[] SyncPairs =
        {
            (Root + "/Data/DemoJuiceSettings.asset", Root + "/Resources/DemoJuiceSettings.asset"),
            (Root + "/Data/DemoPacingSettings.asset", Root + "/Resources/DemoPacingSettings.asset"),
            (Root + "/Data/LevelUpSettings.asset", Root + "/Resources/LevelUpSettings.asset"),
            (Root + "/Data/GameDatabase.asset", Root + "/Resources/GameDatabase.asset"),
            (Root + "/Data/DemoIslandLayout.asset", Root + "/Resources/DemoIslandLayout.asset"),
            (Root + "/Data/SpawnDirectorSettings.asset", Root + "/Resources/SpawnDirectorSettings.asset")
        };

        public static int SyncDataToResources(bool saveAssets = true, bool recordUndo = false)
        {
            EnsureFolder(Root + "/Resources");
            var synced = 0;

            for (var i = 0; i < SyncPairs.Length; i++)
            {
                var pair = SyncPairs[i];
                if (AssetDatabase.LoadAssetAtPath<Object>(pair.data) == null)
                    continue;

                if (CopySerialized(pair.data, pair.resources, recordUndo))
                    synced++;
            }

            if (saveAssets)
                AssetDatabase.SaveAssets();

            return synced;
        }

        public static bool CopySerialized(string sourcePath, string destinationPath, bool recordUndo = false)
        {
            var source = AssetDatabase.LoadAssetAtPath<ScriptableObject>(sourcePath);
            if (source == null)
            {
                Debug.LogWarning($"Missing tuning source asset: {sourcePath}");
                return false;
            }

            var destination = AssetDatabase.LoadAssetAtPath<ScriptableObject>(destinationPath);
            if (destination == null)
            {
                var parent = System.IO.Path.GetDirectoryName(destinationPath)?.Replace("\\", "/");
                if (!string.IsNullOrEmpty(parent))
                    EnsureFolder(parent);

                if (!AssetDatabase.CopyAsset(sourcePath, destinationPath))
                {
                    Debug.LogWarning($"Failed to create runtime copy: {destinationPath}");
                    return false;
                }

                destination = AssetDatabase.LoadAssetAtPath<ScriptableObject>(destinationPath);
            }

            if (destination == null)
                return false;

            if (recordUndo)
                Undo.RecordObject(destination, "Sync Tuning");

            EditorUtility.CopySerialized(source, destination);
            EditorUtility.SetDirty(destination);
            EditorUtility.SetDirty(source);
            return true;
        }

        public static T LoadDataAsset<T>(string fileName) where T : Object
        {
            return AssetDatabase.LoadAssetAtPath<T>($"{Root}/Data/{fileName}");
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
