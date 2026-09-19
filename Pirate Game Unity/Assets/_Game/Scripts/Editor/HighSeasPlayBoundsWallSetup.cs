#if UNITY_EDITOR
using HighSeas.V3;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasPlayBoundsWallSetup
    {
        const string PrefabFolder = "Assets/_Game/Prefabs/V3";
        const string MaterialFolder = "Assets/_Game/Materials/V3";
        const string PrefabPath = PrefabFolder + "/PlayBoundsWallSegment.prefab";
        const string MaterialPath = MaterialFolder + "/PlayBoundsWall.mat";
        const string ResourcesPrefabPath = "Assets/_Game/Resources/V3PlayBoundsWallSegment.prefab";

        [MenuItem("HighSeas/V3/Setup Play Bounds Wall Prefab")]
        public static void SetupPlayBoundsWallPrefab()
        {
            EnsureFolder("Assets/_Game", "Prefabs");
            EnsureFolder("Assets/_Game/Prefabs", "V3");
            EnsureFolder("Assets/_Game", "Materials");
            EnsureFolder("Assets/_Game/Materials", "V3");
            EnsureFolder("Assets/_Game", "Resources");

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null)
                    shader = Shader.Find("Sprites/Default");
                material = new Material(shader)
                {
                    color = new Color(0.78f, 0.18f, 0.22f, 0.28f)
                };
                V3PlayBoundsWallMaterial.ConfigureTransparent(material);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            temp.name = "PlayBoundsWallSegment";
            Object.DestroyImmediate(temp.GetComponent<Collider>());

            var renderer = temp.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            var segment = temp.AddComponent<V3PlayBoundsWallSegment>();
            segment.materialTemplate = material;
            segment.tint = new Color(0.78f, 0.18f, 0.22f, 0.28f);
            segment.textureScrollDirection = new Vector2(0f, 1f);
            segment.textureScrollSpeed = 0.25f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);

            if (AssetDatabase.LoadAssetAtPath<GameObject>(ResourcesPrefabPath) != null)
                AssetDatabase.DeleteAsset(ResourcesPrefabPath);
            AssetDatabase.CopyAsset(PrefabPath, ResourcesPrefabPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Play bounds wall prefab ready:\n  Edit: {PrefabPath}\n  Runtime load: {ResourcesPrefabPath}\nAssign texture/scroll on V3PlayBoundsWallSegment.");
            Selection.activeObject = prefab;
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
#endif
