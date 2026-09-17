#if UNITY_EDITOR
using HighSeas.V3;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasCharacterCardArtSetup
    {
        const string ArtRoot = "Assets/_Game/Art/Character Card";
        const string DataPath = "Assets/_Game/Data/V3CharacterCardArtSettings.asset";
        const string ResourcesPath = "Assets/_Game/Resources/V3CharacterCardArtSettings.asset";

        [MenuItem("HighSeas/V3/Setup Character Card Art")]
        public static void SetupCharacterCardArt()
        {
            EnsureSpriteImport($"{ArtRoot}/hank card.png");
            EnsureSpriteImport($"{ArtRoot}/Hank card Selected.png");
            EnsureSpriteImport($"{ArtRoot}/Rook card.png");
            EnsureSpriteImport($"{ArtRoot}/Rook card selected.png");
            EnsureSpriteImport($"{ArtRoot}/Richie Card.png");
            EnsureSpriteImport($"{ArtRoot}/Character menu ref.png");

            var settings = AssetDatabase.LoadAssetAtPath<V3CharacterCardArtSettings>(DataPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<V3CharacterCardArtSettings>();
                AssetDatabase.CreateAsset(settings, DataPath);
            }

            settings.menuBackground = LoadSprite($"{ArtRoot}/Character menu ref.png");
            settings.entries = new[]
            {
                Entry(V3CaptainId.Rook, "Rook card.png", "Rook card selected.png"),
                Entry(V3CaptainId.Brine, "hank card.png", "Hank card Selected.png"),
                Entry(V3CaptainId.Richie, "Richie Card.png", null),
                Entry(V3CaptainId.Flint, null, null),
                Entry(V3CaptainId.Ember, null, null),
                Entry(V3CaptainId.Roxie, null, null)
            };

            EditorUtility.SetDirty(settings);

            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");

            var resourcesCopy = AssetDatabase.LoadAssetAtPath<V3CharacterCardArtSettings>(ResourcesPath);
            if (resourcesCopy == null)
            {
                if (!AssetDatabase.CopyAsset(DataPath, ResourcesPath))
                    Debug.LogWarning("Could not copy V3CharacterCardArtSettings to Resources.");
            }
            else
            {
                EditorUtility.CopySerialized(settings, resourcesCopy);
                EditorUtility.SetDirty(resourcesCopy);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Character card art settings wired for V3 main menu.");
        }

        static V3CharacterCardArtSettings.Entry Entry(V3CaptainId captain, string scrollFile, string selectedFile) =>
            new()
            {
                captain = captain,
                scrollCard = scrollFile != null ? LoadSprite($"{ArtRoot}/{scrollFile}") : null,
                selectedCard = selectedFile != null ? LoadSprite($"{ArtRoot}/{selectedFile}") : null
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
