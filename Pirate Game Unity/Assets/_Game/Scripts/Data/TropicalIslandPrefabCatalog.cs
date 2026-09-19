using System;
using System.Collections.Generic;
using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "TropicalIslandPrefabCatalog", menuName = "HighSeas/Tropical Island Prefab Catalog")]
    public sealed class TropicalIslandPrefabCatalog : ScriptableObject
    {
        const string ResourcesName = "TropicalIslandPrefabCatalog";

        public GameObject[] prefabs;

        public static bool IsAllowedPrefabName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName) ||
                !prefabName.StartsWith("Island_Tropical_", StringComparison.Ordinal))
                return false;

            if (prefabName.Contains("_cursed", StringComparison.OrdinalIgnoreCase) ||
                prefabName.Contains("_default", StringComparison.OrdinalIgnoreCase))
                return false;

            var suffix = prefabName.Substring("Island_Tropical_".Length);
            return int.TryParse(suffix, out var index) && index > 0;
        }

        public static GameObject[] GetRuntimePrefabs()
        {
            var catalog = Resources.Load<TropicalIslandPrefabCatalog>(ResourcesName);
            if (catalog?.prefabs != null && catalog.prefabs.Length > 0)
                return FilterAndSort(catalog.prefabs);

#if UNITY_EDITOR
            return FilterAndSort(LoadEditorPrefabs());
#else
            return Array.Empty<GameObject>();
#endif
        }

        public static GameObject ResolvePrefab(IslandPrefabEntry entry, GameObject[] pool, RunRng rng)
        {
            if (entry?.prefab != null && IsAllowedPrefabName(entry.prefab.name))
                return entry.prefab;

            if (pool == null || pool.Length == 0)
                return entry?.prefab;

            return pool[rng.MapInt(0, pool.Length)];
        }

        public static GameObject[] FilterAndSort(GameObject[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<GameObject>();

            var list = new List<GameObject>(source.Length);
            for (var i = 0; i < source.Length; i++)
            {
                var prefab = source[i];
                if (prefab == null || !IsAllowedPrefabName(prefab.name))
                    continue;
                if (!list.Contains(prefab))
                    list.Add(prefab);
            }

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list.ToArray();
        }

#if UNITY_EDITOR
        public static TropicalIslandPrefabCatalog CreateOrUpdateAsset(string assetPath, IReadOnlyList<GameObject> tropicalPrefabs)
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<TropicalIslandPrefabCatalog>(assetPath);
            if (catalog == null)
            {
                catalog = CreateInstance<TropicalIslandPrefabCatalog>();
                UnityEditor.AssetDatabase.CreateAsset(catalog, assetPath);
            }

            catalog.prefabs = FilterAndSort(tropicalPrefabs as GameObject[] ?? CopyToArray(tropicalPrefabs));
            UnityEditor.EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static GameObject[] CopyToArray(IReadOnlyList<GameObject> list)
        {
            var arr = new GameObject[list.Count];
            for (var i = 0; i < list.Count; i++)
                arr[i] = list[i];
            return arr;
        }

        static GameObject[] LoadEditorPrefabs()
        {
            var result = new List<GameObject>();
            var folder = HighSeas.V3.V3SceneNames.TropicalIslandsFolder;
            if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                return Array.Empty<GameObject>();

            var guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null && IsAllowedPrefabName(prefab.name) && !result.Contains(prefab))
                    result.Add(prefab);
            }

            return FilterAndSort(result.ToArray());
        }
#endif
    }
}
