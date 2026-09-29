using System.IO;
using System.Linq;
using RoguePirates.V4;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// M14.15: builds contain the V4 scenes only (user decision), with V4Boot first. The Editor's build list keeps the
    /// V3 and test scenes enabled after the V4 ones because V3's regression PlayMode tests load them by name (the Editor
    /// only loads enabled build-list scenes) until M15 retires them; every player build — the Build window included,
    /// through the registered build handler — is filtered to the V4 scenes. <see cref="BuildRelease"/> makes the release
    /// (non-development) build and checks that the DevTools assembly is absent.
    /// </summary>
    [InitializeOnLoad]
    public static class V4BuildSettings
    {
        /// <summary>The scenes of a V4 player, in build order (the trace launcher only in development builds).</summary>
        public static readonly string[] PlayerScenes =
        {
            V4SceneNames.BootPath, V4SceneNames.MainMenuPath, V4SceneNames.SaltwindRunPath, V4SceneNames.ModuleLabPath,
        };

        public const string TraceLauncherPath = V4SceneNames.Folder + "/V4TraceLauncher.unity";

        static V4BuildSettings()
        {
            BuildPlayerWindow.RegisterBuildPlayerHandler(options =>
            {
                options.scenes = ScenesFor(options.options);
                BuildPlayerWindow.DefaultBuildMethods.BuildPlayer(options);
            });
        }

        public static string[] ScenesFor(BuildOptions options)
        {
            var development = (options & BuildOptions.Development) != 0;
            return development && File.Exists(TraceLauncherPath) ? PlayerScenes.Append(TraceLauncherPath).ToArray() : PlayerScenes;
        }

        /// <summary>The Editor build list: the V4 scenes first (V4Boot is scene 0), then the rest as they were.</summary>
        [MenuItem("RoguePirates/V4/Apply Build List (V4 first)")]
        public static string ApplyBuildList()
        {
            var v4 = PlayerScenes.Append(TraceLauncherPath).Where(File.Exists).ToList();
            var rest = EditorBuildSettings.scenes.Where(s => !v4.Contains(s.path)).ToList();
            var scenes = v4.Select(p => new EditorBuildSettingsScene(p, true)).Concat(rest).ToArray();
            EditorBuildSettings.scenes = scenes;
            return string.Join("\n", scenes.Select((s, i) => $"{i}: {(s.enabled ? "on " : "off")} {s.path}"));
        }

        /// <summary>
        /// A player build with the V4 scenes (release by default) into <paramref name="folder"/>; reports the result and
        /// whether the DevTools assembly made it into the player (it must not in a release build).
        /// </summary>
        public static string BuildRelease(string folder, bool development = false)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var options = development ? BuildOptions.Development : BuildOptions.None;
            var exe = Path.Combine(folder, "RoguePirates" + (target == BuildTarget.StandaloneWindows64 || target == BuildTarget.StandaloneWindows ? ".exe" : string.Empty));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = ScenesFor(options),
                locationPathName = exe,
                target = target,
                options = options,
            });
            var summary = report.summary;
            var dataFolder = Directory.Exists(folder) ? Directory.GetDirectories(folder, "*_Data").FirstOrDefault() : null;
            var managed = dataFolder != null ? Path.Combine(dataFolder, "Managed") : null;
            // IL2CPP targets (WebGL) ship no managed DLLs: read the stripped assemblies the build compiled instead.
            if (managed == null || !Directory.Exists(managed))
                managed = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "Bee", "artifacts", target.ToString(), "ManagedStripped");
            var devTools = managed != null && File.Exists(Path.Combine(managed, "RoguePirates.V4.DevTools.dll"));
            var runtime = managed != null && File.Exists(Path.Combine(managed, "RoguePirates.V4.dll"));
            return $"result={summary.result} target={target} development={development} size={summary.totalSize / (1024 * 1024)}MB " +
                   $"time={summary.totalTime.TotalSeconds:0}s errors={summary.totalErrors} scenes={string.Join(",", ScenesFor(options).Select(Path.GetFileNameWithoutExtension))} " +
                   $"V4 runtime dll={runtime} DevTools dll={devTools}";
        }
    }
}
