#if UNITY_EDITOR
using HighSeas.World;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasIslandColliderEditor
    {
        const string TropicalIslandFolder = "Assets/GameObject/Islands";

        [MenuItem("HighSeas/Islands/Prepare Mesh Collider Islands (Selected)")]
        public static void PrepareSelected()
        {
            PreparePrefabs(Selection.objects);
        }

        [MenuItem("HighSeas/Islands/Prepare Mesh Collider Islands (All Tropical)")]
        public static void PrepareAllTropical()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { TropicalIslandFolder });
            var objects = new Object[guids.Length];
            for (var i = 0; i < guids.Length; i++)
                objects[i] = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
            PreparePrefabs(objects);
        }

        static void PreparePrefabs(Object[] objects)
        {
            if (objects == null || objects.Length == 0)
            {
                Debug.LogWarning("Select one or more island prefabs in the Project window.");
                return;
            }

            var prepared = 0;
            for (var i = 0; i < objects.Length; i++)
            {
                var path = AssetDatabase.GetAssetPath(objects[i]);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab"))
                    continue;
                if (PreparePrefab(path))
                    prepared++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Prepared mesh collider setup on {prepared} island prefab(s).");
        }

        static bool PreparePrefab(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
                return false;

            try
            {
                var authoring = root.GetComponent<IslandCollisionAuthoring>();
                if (authoring == null)
                    authoring = root.AddComponent<IslandCollisionAuthoring>();

                MeshCollider ground = null;
                MeshCollider foliage = null;
                var meshColliders = root.GetComponentsInChildren<MeshCollider>(true);
                for (var i = 0; i < meshColliders.Length; i++)
                {
                    var meshCollider = meshColliders[i];
                    if (meshCollider == null)
                        continue;

                    if (meshCollider.convex && ground == null)
                        ground = meshCollider;
                    else if (!meshCollider.convex && foliage == null)
                        foliage = meshCollider;
                }

                authoring.groundCollider = ground;
                authoring.foliageCollider = foliage;

                if (ground != null)
                {
                    ground.convex = true;
                    ground.enabled = true;
                    SetIslandLayer(ground.gameObject);
                }

                if (foliage != null)
                {
                    foliage.enabled = true;
                    SetIslandLayer(foliage.gameObject);
                }

                SetIslandLayer(root);

                var capsuleColliders = root.GetComponentsInChildren<CapsuleCollider>(true);
                for (var i = 0; i < capsuleColliders.Length; i++)
                {
                    if (capsuleColliders[i] != null)
                        capsuleColliders[i].enabled = false;
                }

                var collisionRoot = root.transform.Find(IslandCompoundColliderBuilder.CollisionRootName);
                if (collisionRoot != null)
                    Object.DestroyImmediate(collisionRoot.gameObject);

                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log(
                    $"[{root.name}] ground={(ground != null ? "ok" : "missing")}, foliage={(foliage != null ? "ok" : "missing")}.",
                    AssetDatabase.LoadAssetAtPath<GameObject>(path));
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetIslandLayer(GameObject target)
        {
            var layer = LayerMask.NameToLayer(IslandObstacles.LayerName);
            if (layer >= 0)
                target.layer = layer;
        }
    }
}
#endif
