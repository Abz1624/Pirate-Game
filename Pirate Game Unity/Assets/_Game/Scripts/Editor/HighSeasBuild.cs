#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HighSeas.Editor
{
    public static class HighSeasBuild
    {
        const string WebGLOutput = "Build/WebGL";

        [MenuItem("HighSeas/Build/WebGL")]
        public static void BuildWebGL()
        {
            HighSeasBootstrap.BootstrapAll();
            Directory.CreateDirectory(WebGLOutput);
            var scenes = EditorBuildSettings.scenes;
            if (scenes == null || scenes.Length == 0)
            {
                scenes = new[]
                {
                    new EditorBuildSettingsScene("Assets/_Game/Scenes/Boot.unity", true),
                    new EditorBuildSettingsScene("Assets/_Game/Scenes/MainMenu.unity", true),
                    new EditorBuildSettingsScene("Assets/_Game/Scenes/Run.unity", true)
                };
            }

            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(scenes, s => s.path),
                locationPathName = WebGLOutput,
                target = BuildTarget.WebGL,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"WebGL build failed: {report.summary.result}");
            Debug.Log($"WebGL build succeeded: {WebGLOutput}");
        }

        [MenuItem("HighSeas/Build/Android")]
        public static void BuildAndroid()
        {
            HighSeasBootstrap.BootstrapAll();
            var outPath = "Build/Android/highseas.apk";
            Directory.CreateDirectory(Path.GetDirectoryName(outPath));
            var scenes = EditorBuildSettings.scenes;
            var options = new BuildPlayerOptions
            {
                scenes = System.Array.ConvertAll(scenes, s => s.path),
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Android build failed: {report.summary.result}");
            Debug.Log($"Android build succeeded: {outPath}");
        }
    }
}
#endif
