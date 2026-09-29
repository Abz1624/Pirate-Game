using System;
using System.IO;
using RoguePirates.V4.DevTools.Trace;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Builds the Windows development player used to run trace seeds in parallel. It contains only the empty launcher
    /// scene (index 0) and the two run scenes; a request on the command line (<c>-v4trace request.json</c>) picks the
    /// scene. Meant for a batch-mode Unity on a mirror copy of the project, so the user's Editor keeps its build target:
    /// <c>Unity -batchmode -quit -projectPath &lt;copy&gt; -buildTarget Win64 -executeMethod
    /// RoguePirates.V4.Editor.V4TraceBuild.BuildFromCommandLine -v4BuildOut &lt;dir&gt;</c>.
    /// </summary>
    public static class V4TraceBuild
    {
        public const string LauncherPath = V4SceneNames.Folder + "/" + TraceRequest.LauncherSceneName + ".unity";
        public const string PlayerExeName = "PirateV4Trace.exe";

        static readonly string[] Scenes =
        {
            LauncherPath,
            V4SceneNames.SaltwindRunPath
        };

        /// <summary>Creates the empty launcher scene once (a camera-less scene; TraceBoot loads the run scene from it).</summary>
        public static string EnsureLauncherScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(LauncherPath) != null) return "exists: " + LauncherPath;
            var previous = EditorSceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, LauncherPath);
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            return "created: " + LauncherPath;
        }

        public static BuildReport Build(string outputDirectory)
        {
            EnsureLauncherScene();
            var options = new BuildPlayerOptions
            {
                scenes = Scenes,
                locationPathName = Path.Combine(outputDirectory, PlayerExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.Development
            };
            return BuildPipeline.BuildPlayer(options);
        }

        /// <summary>Batch entry point; exits with 0 on success, 1 on failure.</summary>
        public static void BuildFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var output = "Builds/V4Trace";
            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == "-v4BuildOut") output = args[i + 1];

            var started = DateTime.UtcNow;
            var report = Build(output);
            var ok = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"V4TraceBuild: {report.summary.result} in {(DateTime.UtcNow - started).TotalSeconds:0}s, " +
                      $"{report.summary.totalErrors} errors -> {output}");
            EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
