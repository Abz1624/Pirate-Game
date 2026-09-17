#if UNITY_EDITOR
using HighSeas.V3;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasPlayerInfoHudArtSetup
    {
        const string ArtRoot = "Assets/_Game/Art/Captain icon, HP & XP";
        const string DataPath = "Assets/_Game/Data/V3PlayerInfoHudArtSettings.asset";
        const string ResourcesPath = "Assets/_Game/Resources/V3PlayerInfoHudArtSettings.asset";

        [MenuItem("HighSeas/V3/Setup Player Info HUD Art")]
        public static void SetupPlayerInfoHudArt()
        {
            SetupPlayerInfoHudArtInternal();
        }

        static void SetupPlayerInfoHudArtInternal()
        {
            EnsureSpriteImport($"{ArtRoot}/empty hp & xp bar.png");
            EnsureSpriteImport($"{ArtRoot}/full HP bar.png");
            EnsureSpriteImport($"{ArtRoot}/Full XP bar.png");
            EnsureSpriteImport($"{ArtRoot}/HP icon.png");
            EnsureSpriteImport($"{ArtRoot}/XP icon.png");
            EnsureSpriteImport($"{ArtRoot}/Rook Icon.png");
            EnsureSpriteImport($"{ArtRoot}/hank icon.png");
            EnsureSpriteImport($"{ArtRoot}/Sparky Icon.png");
            EnsureSpriteImport($"{ArtRoot}/Richie icon.png");
            EnsureSpriteImport($"{ArtRoot}/Ember Icon.png");
            EnsureSpriteImport($"{ArtRoot}/Roxie Icon.png");
            EnsureSpriteImport($"{ArtRoot}/Level Indicator overlay.png");
            EnsureSpriteImport($"{ArtRoot}/Sheild overlay pattern.png");

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data"))
                AssetDatabase.CreateFolder("Assets/_Game", "Data");

            var settings = AssetDatabase.LoadAssetAtPath<V3PlayerInfoHudArtSettings>(DataPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<V3PlayerInfoHudArtSettings>();
                AssetDatabase.CreateAsset(settings, DataPath);
            }

            settings.emptyBar = LoadSprite($"{ArtRoot}/empty hp & xp bar.png");
            settings.fullHpBar = LoadSprite($"{ArtRoot}/full HP bar.png");
            settings.fullXpBar = LoadSprite($"{ArtRoot}/Full XP bar.png");
            settings.hpIcon = LoadSprite($"{ArtRoot}/HP icon.png");
            settings.xpIcon = LoadSprite($"{ArtRoot}/XP icon.png");
            settings.levelBadgeOverlay = LoadSprite($"{ArtRoot}/Level Indicator overlay.png");
            settings.shieldOverlayPattern = LoadSprite($"{ArtRoot}/Sheild overlay pattern.png");

            settings.captainPortraits = new[]
            {
                Portrait(V3CaptainId.Rook, LoadSprite($"{ArtRoot}/Rook Icon.png")),
                Portrait(V3CaptainId.Brine, LoadSprite($"{ArtRoot}/hank icon.png")),
                Portrait(V3CaptainId.Flint, LoadSprite($"{ArtRoot}/Sparky Icon.png")),
                Portrait(V3CaptainId.Richie, LoadSprite($"{ArtRoot}/Richie icon.png")),
                Portrait(V3CaptainId.Ember, LoadSprite($"{ArtRoot}/Ember Icon.png")),
                Portrait(V3CaptainId.Roxie, LoadSprite($"{ArtRoot}/Roxie Icon.png"))
            };

            EditorUtility.SetDirty(settings);

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");

            var resourcesCopy = AssetDatabase.LoadAssetAtPath<V3PlayerInfoHudArtSettings>(ResourcesPath);
            if (resourcesCopy == null)
            {
                if (!AssetDatabase.CopyAsset(DataPath, ResourcesPath))
                    Debug.LogWarning("Could not copy V3PlayerInfoHudArtSettings to Resources.");
            }
            else
            {
                EditorUtility.CopySerialized(settings, resourcesCopy);
                EditorUtility.SetDirty(resourcesCopy);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Player info HUD art settings wired (portrait, HP/XP bars, level badge).");
        }

        static V3PlayerInfoHudArtSettings.CaptainPortrait Portrait(V3CaptainId captain, Sprite icon) =>
            new() { captain = captain, icon = icon };

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
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string assetPath) =>
            AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }
}
#endif
