#if UNITY_EDITOR
using HighSeas.Core;
using HighSeas.UI;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasLevelUpCardArtSetup
    {
        const string ArtRoot = "Assets/_Game/Art/Upgrade Cards";
        const string DataPath = "Assets/_Game/Data/LevelUpCardArtSettings.asset";
        const string ResourcesPath = "Assets/_Game/Resources/LevelUpCardArtSettings.asset";

        [MenuItem("HighSeas/V3/Setup Level Up Card Art")]
        public static void SetupLevelUpCardArt()
        {
            EnsureSpriteImport($"{ArtRoot}/Common card bg.png");
            EnsureSpriteImport($"{ArtRoot}/Common card effect.png");
            EnsureSpriteImport($"{ArtRoot}/Uncommon card bg.png");
            EnsureSpriteImport($"{ArtRoot}/Uncommon Card effect.png");
            EnsureSpriteImport($"{ArtRoot}/Rare card bg.png");
            EnsureSpriteImport($"{ArtRoot}/rare card effect.png");
            EnsureSpriteImport($"{ArtRoot}/Epic card bg.png");
            EnsureSpriteImport($"{ArtRoot}/Epic card effect.png");
            EnsureSpriteImport($"{ArtRoot}/Legendary card bg.png");
            EnsureSpriteImport($"{ArtRoot}/Legendary Card effect.png");

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data"))
                AssetDatabase.CreateFolder("Assets/_Game", "Data");

            var settings = AssetDatabase.LoadAssetAtPath<LevelUpCardArtSettings>(DataPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LevelUpCardArtSettings>();
                AssetDatabase.CreateAsset(settings, DataPath);
            }

            settings.rarityArt = new[]
            {
                Entry(Rarity.Common, "Common card bg.png", "Common card effect.png"),
                Entry(Rarity.Uncommon, "Uncommon card bg.png", "Uncommon Card effect.png"),
                Entry(Rarity.Rare, "Rare card bg.png", "rare card effect.png"),
                Entry(Rarity.Epic, "Epic card bg.png", "Epic card effect.png"),
                Entry(Rarity.Legendary, "Legendary card bg.png", "Legendary Card effect.png")
            };

            EditorUtility.SetDirty(settings);

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");

            var resourcesCopy = AssetDatabase.LoadAssetAtPath<LevelUpCardArtSettings>(ResourcesPath);
            if (resourcesCopy == null)
            {
                if (!AssetDatabase.CopyAsset(DataPath, ResourcesPath))
                    Debug.LogWarning("Could not copy LevelUpCardArtSettings to Resources.");
            }
            else
            {
                EditorUtility.CopySerialized(settings, resourcesCopy);
                EditorUtility.SetDirty(resourcesCopy);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Level up card art settings wired (rarity backgrounds + effect overlays).");
        }

        static LevelUpCardArtSettings.RarityArt Entry(Rarity rarity, string bgFile, string effectFile) =>
            new()
            {
                rarity = rarity,
                background = LoadSprite($"{ArtRoot}/{bgFile}"),
                effectOverlay = LoadSprite($"{ArtRoot}/{effectFile}")
            };

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
