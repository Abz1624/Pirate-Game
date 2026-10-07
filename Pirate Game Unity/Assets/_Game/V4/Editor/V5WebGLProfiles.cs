using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 M7 (I7.5): the WebGL builds. Mobile: a 512 MB heap ceiling (256 MB ran out of memory, see Build), ASTC
    /// textures, Brotli; desktop: 1 GB ceiling, DXT. Both cache data and keep the shared release settings; the project's
    /// own WebGL settings are restored after the build. <see cref="TextureDiet"/> caps every texture at 1024 on WebGL.
    /// </summary>
    public static class V5WebGLProfiles
    {
        public enum Profile { Mobile, Desktop }

        /// <summary>
        /// V5 M8z (7 Oct, D21, Q34, owner: "the 1024 texture cap on every web build, desktop too"): the largest texture any
        /// WebGL build ships, mobile and desktop alike. Applied per build through the build's max texture size override
        /// (on top of <see cref="TextureDiet"/>'s WebGL importer overrides, which already hold most textures at 1024, it
        /// also catches the few overridden higher or deliberately uncompressed) and restored after the build.
        /// </summary>
        public const int WebTextureCap = 1024;

        /// <summary>The texture cap of a profile's build (the same for every web build since D21).</summary>
        public static int TextureCapFor(Profile profile) => WebTextureCap;

        [MenuItem("RoguePirates/V5/Build WebGL (mobile)")]
        public static void BuildMobile() => Build(Profile.Mobile);

        [MenuItem("RoguePirates/V5/Build WebGL (desktop)")]
        public static void BuildDesktop() => Build(Profile.Desktop);

        /// <summary>
        /// A profile's build, with the code optimized for speed. Mobile textures are ASTC (phones cannot sample DXT, and
        /// expanding it cost ~720 MB of GPU memory and 67 MB of heap on a Pixel 9a); desktop textures are DXT.
        /// <paramref name="wasm2023"/> (off by default) needs Safari 16.4+ and showed no measured gain (phone bench, 2 Oct;
        /// the owner's question). <paramref name="bench"/> adds the V5_PHONE_BENCH define (the benchmark harness; otherwise
        /// the same build) into "&lt;folder&gt;-Bench".
        /// </summary>
        public static string Build(Profile profile, bool bench = false, bool wasm2023 = false) => WithSettings(() =>
        {
            // Mobile: 512 MB. The 256 MB ceiling (32-bit mobile Chrome) ran out of memory loading the menu on 1 Oct (the
            // game needs ~344 MB); 64-bit phones such as the reference Galaxy A15 grow past it. 256 needs asset streaming.
            PlayerSettings.WebGL.maximumMemorySize = profile == Profile.Mobile ? 512 : 1024;
            EditorUserBuildSettings.overrideMaxTextureSize = TextureCapFor(profile); // V5 M8z (D21, Q34): desktop too
            EditorUserBuildSettings.SetPlatformSettings("WebGL", "CodeOptimization", "RuntimeSpeed");
            PlayerSettings.WebGL.wasm2023 = wasm2023;
        }, (profile == Profile.Mobile ? "WebGL-Mobile" : "WebGL-Desktop") + (bench ? "-Bench" : ""), $"V5WebGL-{profile}{(bench ? "-Bench" : "")}",
            $"profile={profile} bench={bench} wasm2023={wasm2023}", bench ? new[] { "V5_PHONE_BENCH" } : null,
            (int)(profile == Profile.Mobile ? WebGLTextureSubtarget.ASTC : WebGLTextureSubtarget.DXT));

        /// <summary>
        /// V5 phone benchmark builds (1 Oct): the mobile profile plus the <c>V5_PHONE_BENCH</c> define (the DevTools pilot
        /// in a release build, see PhoneBench), with the settings that need their own build: the code optimization
        /// ("BuildTimes", "RuntimeSpeed", "RuntimeSpeedLTO", "DiskSize", "DiskSizeLTO"), WebAssembly 2023 and the texture
        /// format. Into Builds/Bench-&lt;name&gt;; the project's settings are restored afterwards.
        /// </summary>
        public static string BuildBench(string name, string codeOptimization, bool wasm2023, bool astc) => WithSettings(() =>
        {
            PlayerSettings.WebGL.maximumMemorySize = 512;
            EditorUserBuildSettings.overrideMaxTextureSize = WebTextureCap; // V5 M8z (D21, Q34)
            EditorUserBuildSettings.webGLBuildSubtarget = astc ? WebGLTextureSubtarget.ASTC : WebGLTextureSubtarget.Generic;
            if (!string.IsNullOrEmpty(codeOptimization)) EditorUserBuildSettings.SetPlatformSettings("WebGL", "CodeOptimization", codeOptimization);
            PlayerSettings.WebGL.wasm2023 = wasm2023;
        }, "Bench-" + name, "V5Bench-" + name, $"bench={name} codeOptimization={codeOptimization} wasm2023={wasm2023} astc={astc}", new[] { "V5_PHONE_BENCH" },
            (int)(astc ? WebGLTextureSubtarget.ASTC : WebGLTextureSubtarget.Generic));

        static string WithSettings(System.Action apply, string folderName, string reportName, string label, string[] defines, int subtarget)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
            var memory = PlayerSettings.WebGL.maximumMemorySize;
            var initial = PlayerSettings.WebGL.initialMemorySize;
            var savedSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            var compression = PlayerSettings.WebGL.compressionFormat;
            var caching = PlayerSettings.WebGL.dataCaching;
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            var codeOptimization = EditorUserBuildSettings.GetPlatformSettings("WebGL", "CodeOptimization");
            var wasm2023 = PlayerSettings.WebGL.wasm2023;
            var logTrace = PlayerSettings.GetStackTraceLogType(LogType.Log);
            var warningTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning);
            var textureCap = EditorUserBuildSettings.overrideMaxTextureSize; // V5 M8z (D21): restored after the build
            try
            {
                // Release logs without stack traces (each one printed to the browser console with its trace); errors keep theirs.
                PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                PlayerSettings.WebGL.initialMemorySize = 64;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.dataCaching = true;
                // Browsers decode Brotli only over HTTPS: the fallback lets a LAN test over plain HTTP load (slower first load).
                PlayerSettings.WebGL.decompressionFallback = true;
                apply();
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", folderName);
                var result = V4BuildSettings.BuildRelease(folder, false, defines, subtarget);
                // The player lands in <folder>/RoguePirates/Build: count every file under a Build folder.
                var download = Directory.Exists(folder)
                    ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "Build")
                        .Sum(f => new FileInfo(f).Length) / (1024f * 1024f) : 0f;
                result += $" {label} download={download:0.0}MB maxMemory={PlayerSettings.WebGL.maximumMemorySize}MB textures={(WebGLTextureSubtarget)subtarget} textureCap={EditorUserBuildSettings.overrideMaxTextureSize}";
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", reportName + "-report.txt"), result);
                Debug.Log("V5 WebGL build: " + result);
                return result;
            }
            finally
            {
                PlayerSettings.WebGL.maximumMemorySize = memory;
                PlayerSettings.WebGL.initialMemorySize = initial;
                EditorUserBuildSettings.webGLBuildSubtarget = savedSubtarget;
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.dataCaching = caching;
                PlayerSettings.WebGL.decompressionFallback = fallback;
                EditorUserBuildSettings.SetPlatformSettings("WebGL", "CodeOptimization", codeOptimization);
                PlayerSettings.WebGL.wasm2023 = wasm2023;
                PlayerSettings.SetStackTraceLogType(LogType.Log, logTrace);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, warningTrace);
                EditorUserBuildSettings.overrideMaxTextureSize = textureCap;
                AssetDatabase.SaveAssets(); // the build saved its own values to ProjectSettings.asset: write the restored ones
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

        /// <summary>
        /// V5 phone bench (1 Oct): sets every WebGL texture override made by <see cref="TextureDiet"/> to <paramref name="format"/>
        /// (ASTC 6x6 for phones: the build's texture setting is ignored in Unity 6, so phones got DXT, which they cannot sample
        /// and must expand to raw pixels). Returns how many changed; the same call with Automatic undoes it.
        /// </summary>
        public static int WebGLTextureFormat(TextureImporterFormat format)
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
                    var web = importer.GetPlatformTextureSettings("WebGL");
                    if (!web.overridden || web.textureCompression == TextureImporterCompression.Uncompressed || web.format == format) continue;
                    web.format = format;
                    importer.SetPlatformTextureSettings(web);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "V5TextureFormat.txt"), $"{changed} of {paths.Count} WebGL textures set to {format}");
            return changed;
        }

        /// <summary>
        /// V5 phone performance (2 Oct): WebGL overrides that name DXT outright (163 textures, mostly UI icons) become
        /// Automatic, so each build picks its own format: ASTC in the mobile build, still DXT in the desktop build (the same
        /// as before there). A fixed DXT format kept them DXT on phones, which expand it. Single-channel formats are left alone.
        /// </summary>
        [MenuItem("RoguePirates/V5/WebGL: DXT overrides to Automatic")]
        public static void DxtOverridesToAutomaticMenu() => Debug.Log($"V5: {DxtOverridesToAutomatic()} WebGL overrides set to Automatic");

        public static int DxtOverridesToAutomatic()
        {
            var changed = 0;
            var paths = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Distinct().ToList();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in paths)
                {
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                    var web = importer.GetPlatformTextureSettings("WebGL");
                    if (!web.overridden) continue;
                    if (web.format != TextureImporterFormat.DXT1 && web.format != TextureImporterFormat.DXT5
                        && web.format != TextureImporterFormat.DXT1Crunched && web.format != TextureImporterFormat.DXT5Crunched) continue;
                    web.format = TextureImporterFormat.Automatic;
                    if (web.textureCompression == TextureImporterCompression.Uncompressed) web.textureCompression = TextureImporterCompression.Compressed;
                    importer.SetPlatformTextureSettings(web);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return changed;
        }

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
