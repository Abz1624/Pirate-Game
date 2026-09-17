#if UNITY_EDITOR
using HighSeas.V3;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasMinimapArtSetup
    {
        const string MinimapArtFolder = "Assets/_Game/Art/Minimap";
        const string BackgroundPath = MinimapArtFolder + "/Mini Map Background.png";
        const string OverlayPath = MinimapArtFolder + "/Mini Map Overlay.png";
        const string DataPath = "Assets/_Game/Data/V3MinimapArtSettings.asset";
        const string ResourcesPath = "Assets/_Game/Resources/V3MinimapArtSettings.asset";
        const string HudDataPath = "Assets/_Game/Data/V3MinimapHudSettings.asset";
        const string HudResourcesPath = "Assets/_Game/Resources/V3MinimapHudSettings.asset";

        [MenuItem("HighSeas/V3/Setup Minimap Art")]
        public static void SetupMinimapArt()
        {
            EnsureSpriteImport(BackgroundPath);
            EnsureSpriteImport(OverlayPath);

            var settings = AssetDatabase.LoadAssetAtPath<V3MinimapArtSettings>(DataPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<V3MinimapArtSettings>();
                AssetDatabase.CreateAsset(settings, DataPath);
            }

            settings.background = LoadSprite(BackgroundPath);
            settings.overlay = LoadSprite(OverlayPath);
            EditorUtility.SetDirty(settings);

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");

            var resourcesCopy = AssetDatabase.LoadAssetAtPath<V3MinimapArtSettings>(ResourcesPath);
            if (resourcesCopy == null)
            {
                if (!AssetDatabase.CopyAsset(DataPath, ResourcesPath))
                    Debug.LogWarning("Could not copy V3MinimapArtSettings to Resources.");
            }
            else
            {
                EditorUtility.CopySerialized(settings, resourcesCopy);
                EditorUtility.SetDirty(resourcesCopy);
            }

            EnsureHudSettingsAsset();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Minimap art settings wired (background + overlay).");
        }

        [MenuItem("HighSeas/V3/Setup Minimap HUD Settings Asset")]
        public static void SetupMinimapHudSettingsMenu()
        {
            EnsureHudSettingsAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("V3MinimapHudSettings ready at Assets/_Game/Data/V3MinimapHudSettings.asset");
        }

        static void EnsureHudSettingsAsset()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data"))
                AssetDatabase.CreateFolder("Assets/_Game", "Data");
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");

            var settings = AssetDatabase.LoadAssetAtPath<V3MinimapHudSettings>(HudDataPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<V3MinimapHudSettings>();
                AssetDatabase.CreateAsset(settings, HudDataPath);
            }

            var resourcesCopy = AssetDatabase.LoadAssetAtPath<V3MinimapHudSettings>(HudResourcesPath);
            if (resourcesCopy == null)
            {
                if (!AssetDatabase.CopyAsset(HudDataPath, HudResourcesPath))
                    Debug.LogWarning("Could not copy V3MinimapHudSettings to Resources.");
            }
            else
            {
                EditorUtility.CopySerialized(settings, resourcesCopy);
                EditorUtility.SetDirty(resourcesCopy);
            }
        }

        static void EnsureSpriteImport(string assetPath)
        {
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"Missing texture at {assetPath}");
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string assetPath) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
#endif
