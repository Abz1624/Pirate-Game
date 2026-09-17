#if UNITY_EDITOR
using HighSeas.V3;
using UnityEditor;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasCharacterCardArtSetup
    {
        const string ArtRoot = "Assets/_Game/Art/Character Card";
        const string PoseRoot = "Assets/_Game/Art/Character Poses";
        const string DataPath = "Assets/_Game/Data/V3CharacterCardArtSettings.asset";
        const string ResourcesPath = "Assets/_Game/Resources/V3CharacterCardArtSettings.asset";
        const string ResourcesPoseRoot = "Assets/_Game/Resources/CharacterPoses";
        const string TitleFontSrc = "Assets/FPSFont/FPS Gaming Font/Square-Black SDF.asset";
        const string BodyFontSrc = "Assets/FPSFont/FPS Gaming Font/ToonySans_PERSONAL_USE_ONLY SDF.asset";
        const string TypographySettingsPath = "Assets/_Game/Resources/V3MainMenuTypographySettings.asset";

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

        [MenuItem("HighSeas/V3/Setup Character Select Poses")]
        public static void SetupCharacterSelectPoses()
        {
            EnsureSpriteImport($"{PoseRoot}/Rook.png");
            EnsureSpriteImport($"{PoseRoot}/Hank.png");
            EnsureSpriteImport($"{PoseRoot}/Richie.png");
            EnsureSpriteImport($"{PoseRoot}/Sparky.png");
            EnsureSpriteImport($"{PoseRoot}/Amber.png");
            EnsureSpriteImport($"{PoseRoot}/Roxy.png");

            var settings = AssetDatabase.LoadAssetAtPath<V3CharacterCardArtSettings>(DataPath);
            if (settings == null)
            {
                Debug.LogWarning("Run HighSeas/V3/Setup Character Card Art first.");
                return;
            }

            settings.entries = MergePoseEntries(settings.entries);
            EditorUtility.SetDirty(settings);

            EnsureResourcesFolder();
            MirrorPoseToResources("Rook.png");
            MirrorPoseToResources("Hank.png");
            MirrorPoseToResources("Richie.png");
            MirrorPoseToResources("Sparky.png");
            MirrorPoseToResources("Amber.png");
            MirrorPoseToResources("Roxy.png");
            EnsureMenuTypographySettings();

            var resourcesCopy = AssetDatabase.LoadAssetAtPath<V3CharacterCardArtSettings>(ResourcesPath);
            if (resourcesCopy != null)
            {
                EditorUtility.CopySerialized(settings, resourcesCopy);
                EditorUtility.SetDirty(resourcesCopy);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Character select poses, Resources mirrors, and menu fonts wired.");
        }

        static void EnsureResourcesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Resources"))
                AssetDatabase.CreateFolder("Assets/_Game", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourcesPoseRoot))
                AssetDatabase.CreateFolder("Assets/_Game/Resources", "CharacterPoses");
        }

        static void MirrorPoseToResources(string fileName)
        {
            var src = $"{PoseRoot}/{fileName}";
            var dst = $"{ResourcesPoseRoot}/{fileName}";
            if (AssetDatabase.LoadAssetAtPath<Object>(dst) != null)
                AssetDatabase.DeleteAsset(dst);
            if (!AssetDatabase.CopyAsset(src, dst))
                Debug.LogWarning($"Could not copy pose {src} to Resources.");
            else
                EnsureSpriteImport(dst);
        }

        static void EnsureMenuTypographySettings()
        {
            var title = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(TitleFontSrc);
            var body = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(BodyFontSrc);
            if (title == null || body == null)
            {
                Debug.LogWarning("Missing Square-Black or ToonySans font under Assets/FPSFont.");
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<V3MainMenuTypographySettings>(TypographySettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<V3MainMenuTypographySettings>();
                AssetDatabase.CreateAsset(settings, TypographySettingsPath);
            }

            settings.titleFont = title;
            settings.bodyFont = body;
            EditorUtility.SetDirty(settings);
        }

        [MenuItem("HighSeas/V3/Setup Main Menu Typography")]
        public static void SetupMainMenuTypography()
        {
            EnsureResourcesFolder();
            EnsureMenuTypographySettings();
            AssetDatabase.SaveAssets();
            Debug.Log("Main menu typography settings wired (title + body font references).");
        }

        static V3CharacterCardArtSettings.Entry[] MergePoseEntries(V3CharacterCardArtSettings.Entry[] existing)
        {
            var map = new System.Collections.Generic.Dictionary<V3CaptainId, V3CharacterCardArtSettings.Entry>();
            if (existing != null)
            {
                for (var i = 0; i < existing.Length; i++)
                    map[existing[i].captain] = existing[i];
            }

            void Upsert(V3CaptainId captain, string poseFile, Color theme)
            {
                map.TryGetValue(captain, out var entry);
                entry.captain = captain;
                entry.menuPose = LoadSprite($"{PoseRoot}/{poseFile}");
                entry.menuThemeColor = theme;
                map[captain] = entry;
            }

            Upsert(V3CaptainId.Rook, "Rook.png", V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Rook));
            Upsert(V3CaptainId.Brine, "Hank.png", V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Brine));
            Upsert(V3CaptainId.Richie, "Richie.png", V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Richie));
            Upsert(V3CaptainId.Flint, "Sparky.png", V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Flint));
            Upsert(V3CaptainId.Ember, "Amber.png", V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Ember));
            Upsert(V3CaptainId.Roxie, "Roxy.png", V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Roxie));

            var order = V3CaptainCatalog.MenuOrder;
            var list = new V3CharacterCardArtSettings.Entry[order.Length];
            for (var i = 0; i < order.Length; i++)
                list[i] = map.TryGetValue(order[i], out var e) ? e : new V3CharacterCardArtSettings.Entry { captain = order[i] };
            return list;
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
