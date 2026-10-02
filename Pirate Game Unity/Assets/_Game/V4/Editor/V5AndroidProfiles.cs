using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// V5 Android builds (2 Oct): IL2CPP, 64-bit ARM, ASTC textures (through the build option), release logs without stack
    /// traces; variants for what needs its own build: the graphics API and Optimized Frame Pacing. <c>bench</c> adds the
    /// V5_PHONE_BENCH define (the benchmark harness, started with "-v5bench &lt;query&gt;") and builds under its own package
    /// name (<see cref="BenchPackage"/>), so a benchmark never touches a playtest install's data. The project's settings, the
    /// Unity Connect file (Unity turns services on during Android builds) and, unless told otherwise, the active build
    /// target are restored after the build. Signed with the debug key (adb install); a store keystore is the owner's to set up.
    /// </summary>
    public static class V5AndroidProfiles
    {
        public enum Graphics { ProjectDefault, GlesOnly, VulkanOnly }

        public const string BenchPackage = "com.x2c.roguepirates.bench";

        [MenuItem("RoguePirates/V5/Build Android APK (playtest)")]
        public static void BuildPlaytestMenu() => Build("RoguePirates-Playtest", false, Graphics.ProjectDefault, false);

        [MenuItem("RoguePirates/V5/Build Android APK (bench)")]
        public static void BuildBenchMenu() => Build("Android-Bench", true, Graphics.ProjectDefault, false);

        /// <summary>Command line: -executeMethod RoguePirates.V4.Editor.V5AndroidProfiles.BuildPlaytestFromCommandLine</summary>
        public static void BuildPlaytestFromCommandLine()
        {
            var result = Build("RoguePirates-Playtest", false, Graphics.ProjectDefault, false, restoreTarget: false);
            if (Application.isBatchMode) EditorApplication.Exit(result.Contains("result=Succeeded") ? 0 : 1);
        }

        // ---- Texture sizes on Android ---------------------------------------------------------------------------------

        /// <summary>
        /// The Android size cap for one texture, or null to leave it alone. Uncompressed textures (deliberately so) and
        /// text atlases (SDF: soft edges if compressed or downscaled) are skipped; a texture never grows; sprites (UI) get
        /// <paramref name="uiMax"/>, everything else <paramref name="worldMax"/>.
        /// </summary>
        public static int? CapFor(string path, TextureImporterType type, TextureImporterCompression compression, int currentMax, int sourceMax,
            int worldMax, int uiMax)
        {
            if (compression == TextureImporterCompression.Uncompressed) return null;
            if (path.IndexOf("SDF", System.StringComparison.OrdinalIgnoreCase) >= 0) return null;
            var cap = type == TextureImporterType.Sprite ? uiMax : worldMax;
            if (Mathf.Min(currentMax, sourceMax) <= cap) return null;
            return Mathf.Min(currentMax, cap);
        }

        /// <summary>The textures a player build uses: the build scenes' dependencies and everything under a Resources folder.</summary>
        public static HashSet<string> UsedTextures()
        {
            var roots = V4BuildSettings.ScenesFor(BuildOptions.None).ToList();
            roots.AddRange(AssetDatabase.FindAssets(string.Empty, new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)));
            return new HashSet<string>(AssetDatabase.GetDependencies(roots.Distinct().ToArray(), true)
                .Where(p => AssetImporter.GetAtPath(p) is TextureImporter));
        }

        /// <summary>
        /// Android texture sizes (2 Oct): without an Android override, 440 textures loaded above 1024 (224 at 2048, a few
        /// sources up to 4949). For the textures the build uses (<see cref="UsedTextures"/>), world textures are capped at
        /// <paramref name="worldMax"/> as in the web build and UI sprites at <paramref name="uiMax"/> (portraits and
        /// backgrounds fill a 2424-pixel-wide screen). An Android override only; a format already chosen there is kept.
        /// </summary>
        public static int AndroidTextureCap(int worldMax, int uiMax)
        {
            var changed = 0;
            var used = UsedTextures();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in used)
                {
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) continue;
                    var android = importer.GetPlatformTextureSettings("Android");
                    var compression = android.overridden ? android.textureCompression : importer.textureCompression;
                    var current = android.overridden ? android.maxTextureSize : importer.maxTextureSize;
                    importer.GetSourceTextureWidthAndHeight(out var w, out var h);
                    var cap = CapFor(path, importer.textureType, compression, current, Mathf.Max(w, h), worldMax, uiMax);
                    if (!cap.HasValue) continue;
                    if (!android.overridden) android.format = TextureImporterFormat.Automatic;
                    android.overridden = true;
                    android.maxTextureSize = cap.Value;
                    android.textureCompression = compression;
                    importer.SetPlatformTextureSettings(android);
                    importer.SaveAndReimport();
                    changed++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return changed;
        }

        // ---- Builds ----------------------------------------------------------------------------------------------------

        public static string Build(string name, bool bench, Graphics graphics, bool framePacing, bool restoreTarget = true)
        {
            var previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var previousGroup = BuildPipeline.GetBuildTargetGroup(previousTarget);
            var connectPath = Path.Combine(Path.GetDirectoryName(Application.dataPath), "ProjectSettings", "UnityConnectSettings.asset");
            var connect = File.Exists(connectPath) ? File.ReadAllBytes(connectPath) : null;
            if (previousTarget != BuildTarget.Android)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            var target = NamedBuildTarget.Android;
            var backend = PlayerSettings.GetScriptingBackend(target);
            var architectures = PlayerSettings.Android.targetArchitectures;
            var autoApis = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android);
            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            var pacing = PlayerSettings.Android.optimizedFramePacing;
            var subtarget = EditorUserBuildSettings.androidBuildSubtarget;
            var logTrace = PlayerSettings.GetStackTraceLogType(LogType.Log);
            var warningTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning);
            var package = PlayerSettings.GetApplicationIdentifier(target);
            var frameTiming = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                if (graphics != Graphics.ProjectDefault)
                {
                    PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                    PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                        graphics == Graphics.GlesOnly ? new[] { GraphicsDeviceType.OpenGLES3 } : new[] { GraphicsDeviceType.Vulkan });
                }
                PlayerSettings.Android.optimizedFramePacing = framePacing;
                EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
                // Release logs without stack traces (the phone's log showed one per line); errors keep theirs.
                PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                if (bench)
                {
                    PlayerSettings.SetApplicationIdentifier(target, BenchPackage);
                    PlayerSettings.enableFrameTimingStats = true; // the benchmark reads CPU and GPU time per frame
                }
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Builds", "Android");
                Directory.CreateDirectory(folder);
                var apk = Path.Combine(folder, name + ".apk");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = V4BuildSettings.ScenesFor(BuildOptions.None),
                    locationPathName = apk,
                    target = BuildTarget.Android,
                    targetGroup = BuildTargetGroup.Android,
                    options = BuildOptions.None,
                    subtarget = (int)MobileTextureSubtarget.ASTC,
                    extraScriptingDefines = bench ? new[] { "V5_PHONE_BENCH" } : null,
                });
                var s = report.summary;
                var size = File.Exists(apk) ? new FileInfo(apk).Length / (1024f * 1024f) : 0f;
                var result = $"result={s.result} target=Android apk={apk} size={size:0.0}MB time={s.totalTime.TotalSeconds:0}s errors={s.totalErrors} " +
                             $"bench={bench} package={(bench ? BenchPackage : package)} graphics={graphics} framePacing={framePacing} backend=IL2CPP arch=ARM64 textures=ASTC";
                File.WriteAllText(Path.Combine(folder, name + "-report.txt"), result);
                Debug.Log("V5 Android build: " + result);
                return result;
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(target, backend);
                PlayerSettings.Android.targetArchitectures = architectures;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, autoApis);
                if (!autoApis) PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, apis);
                PlayerSettings.Android.optimizedFramePacing = pacing;
                EditorUserBuildSettings.androidBuildSubtarget = subtarget;
                PlayerSettings.SetStackTraceLogType(LogType.Log, logTrace);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, warningTrace);
                PlayerSettings.SetApplicationIdentifier(target, package);
                PlayerSettings.enableFrameTimingStats = frameTiming;
                AssetDatabase.SaveAssets();
                if (connect != null) File.WriteAllBytes(connectPath, connect); // Unity turns services on during Android builds
                if (restoreTarget && previousTarget != BuildTarget.Android)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
            }
        }
    }
}
