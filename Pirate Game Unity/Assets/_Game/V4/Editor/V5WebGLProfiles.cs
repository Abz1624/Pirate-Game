using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M7 (I7.5): the WebGL builds. Mobile: a 256 MB heap ceiling (32-bit mobile Chrome cannot grow past it), ASTC
    /// textures, Brotli; desktop: 1 GB ceiling, DXT. Both cache data and keep the shared release settings; the project's
    /// own WebGL settings are restored after the build. <see cref="TextureDiet"/> caps every texture at 1024 on WebGL.
    /// </summary>
    public static class V5WebGLProfiles
    {
        public enum Profile { Mobile, Desktop }

        [MenuItem("RoguePirates/V5/Build WebGL (mobile)")]
        public static void BuildMobile() => Build(Profile.Mobile);

        [MenuItem("RoguePirates/V5/Build WebGL (desktop)")]
        public static void BuildDesktop() => Build(Profile.Desktop);

        public static string Build(Profile profile)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            var memory = PlayerSettings.WebGL.maximumMemorySize;
            var initial = PlayerSettings.WebGL.initialMemorySize;
            var subtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            var compression = PlayerSettings.WebGL.compressionFormat;
            var caching = PlayerSettings.WebGL.dataCaching;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            try
            {
                PlayerSettings.WebGL.maximumMemorySize = profile == Profile.Mobile ? 256 : 1024;
                PlayerSettings.WebGL.initialMemorySize = 64;
                EditorUserBuildSettings.webGLBuildSubtarget = profile == Profile.Mobile ? WebGLTextureSubtarget.ASTC : WebGLTextureSubtarget.DXT;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.dataCaching = true;
                // Browsers decode Brotli only over HTTPS: the fallback lets a LAN test over plain HTTP load (slower first load).
                PlayerSettings.WebGL.decompressionFallback = true;
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", profile == Profile.Mobile ? "WebGL-Mobile" : "WebGL-Desktop");
                var result = V4BuildSettings.BuildRelease(folder);
                var download = Directory.Exists(Path.Combine(folder, "Build"))
                    ? Directory.GetFiles(Path.Combine(folder, "Build")).Sum(f => new FileInfo(f).Length) / (1024f * 1024f) : 0f;
                result += $" profile={profile} download={download:0.0}MB maxMemory={PlayerSettings.WebGL.maximumMemorySize}MB textures={EditorUserBuildSettings.webGLBuildSubtarget}";
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", $"V5WebGL-{profile}-report.txt"), result);
                Debug.Log("V5 WebGL build: " + result);
                return result;
            }
            finally
            {
                PlayerSettings.WebGL.maximumMemorySize = memory;
                PlayerSettings.WebGL.initialMemorySize = initial;
                EditorUserBuildSettings.webGLBuildSubtarget = subtarget;
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.dataCaching = caching;
                PlayerSettings.WebGL.decompressionFallback = fallback;
            }
        }

        /// <summary>Command line: -executeMethod RoguePirates.V4.Editor.V5WebGLProfiles.BuildMobileFromCommandLine</summary>
        public static void BuildMobileFromCommandLine() => Exit(Build(Profile.Mobile));
        public static void BuildDesktopFromCommandLine() => Exit(Build(Profile.Desktop));

        static void Exit(string result)
        {
            if (Application.isBatchMode) EditorApplication.Exit(result.Contains("result=Succeeded") ? 0 : 1);
        }

        /// <summary>
        /// Every texture under Assets capped at <paramref name="maxSize"/> on WebGL (a platform override; other platforms keep
        /// theirs), compressed. Returns how many changed. Reimports them, which takes a while the first time.
        /// </summary>
        [MenuItem("RoguePirates/V5/WebGL texture diet (1024)")]
        public static void TextureDietMenu() => Debug.Log($"V5 texture diet: {TextureDiet(1024)} textures capped");

        public static int TextureDiet(int maxSize)
        {
            var changed = 0;
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !p.Contains("/Editor/") && !p.StartsWith("Packages/")).Distinct().ToList();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                    if (importer.textureCompression == TextureImporterCompression.Uncompressed) continue; // deliberately uncompressed: kept as authored
                    var web = importer.GetPlatformTextureSettings("WebGL");
                    var current = web.overridden ? web.maxTextureSize : importer.maxTextureSize;
                    if (web.overridden && current <= maxSize && web.textureCompression != TextureImporterCompression.Uncompressed) continue;
                    web.overridden = true;
                    web.maxTextureSize = Mathf.Min(current, maxSize);
                    web.format = TextureImporterFormat.Automatic;
                    web.textureCompression = TextureImporterCompression.Compressed;
                    importer.SetPlatformTextureSettings(web);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "V5TextureDiet.txt"), $"{changed} of {paths.Count} textures capped at {maxSize} on WebGL");
            return changed;
        }
    }
}
