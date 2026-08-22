using UnityEngine;

namespace HighSeas.Data
{
    [CreateAssetMenu(fileName = "IslandVisualLibrary", menuName = "HighSeas/Island Visual Library")]
    public sealed class IslandVisualLibrary : ScriptableObject
    {
        public GameObject groundTile;
        public GameObject[] rocks;
        public GameObject[] trees;
        public Material fallbackIslandMaterial;
        public float groundScale = 1f;
        public float groundYOffset;

        public bool HasPrefabs => groundTile != null;

        public static IslandVisualLibrary Resolve(IslandVisualLibrary assigned)
        {
            if (assigned != null && assigned.HasPrefabs)
                return assigned;

            var fromResources = Resources.Load<IslandVisualLibrary>("IslandVisualLibrary");
            if (fromResources != null && fromResources.HasPrefabs)
                return fromResources;

#if UNITY_EDITOR
            return CreateFromProjectDefaults();
#else
            return assigned;
#endif
        }

#if UNITY_EDITOR
        public static IslandVisualLibrary CreateFromProjectDefaults()
        {
            var lib = CreateInstance<IslandVisualLibrary>();
            lib.groundTile = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/3D Models/Ground/Ground_Sand_Small.prefab");
            lib.rocks = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Rocks/Rock_Small.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Rocks/Rock_Meduim.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Rocks/Rock_Large.prefab")
            };
            lib.trees = new[]
            {
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Tree/Tree_01.prefab"),
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Models/Tree/Tree_02.prefab")
            };
            lib.groundScale = 1f;
            return lib.HasPrefabs ? lib : null;
        }
#endif
    }
}
