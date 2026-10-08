using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using RoguePirates.V4;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RoguePirates.V5.Net.Editor
{
    /// <summary>
    /// V5 M9b part 2a: the match server's build. A Linux Dedicated Server player (StandaloneLinux64, Server subtarget,
    /// Mono, release) with the <c>V5_NET</c> define given to this build only (<c>extraScriptingDefines</c>: no build
    /// profile, PlayerSettings or build list carries it, so the WebGL, Windows and Android builds never compile the net
    /// assembly; <c>V5NetEditModeTests.NetAssembly_IsKeptOutOfReleaseBuilds</c> checks that). Its scenes are the
    /// V5NetSpike scene first (its Server role in batch mode, its Client role with <c>-rpClient</c>) and the run scene.
    /// Output: <c>Builds/LinuxServer/</c> (the Docker build context of <c>server/match/Dockerfile</c>), the report in
    /// <c>Builds/V5NetServer-report.json</c>. The project's build target, server subtarget, scripting backend and log
    /// settings are put back afterwards.
    /// Mono by default: an IL2CPP Linux build from Windows needs the Linux toolchain packages (Unity adds
    /// com.unity.toolchain.win-x86_64-linux and com.unity.sdk.linux-x86_64 1.1.0 on the first try, and the build still
    /// found no sysroot on 8 Oct without an Editor restart); <c>Build(il2cpp: true)</c> once they are set up.
    /// </summary>
    public static class V5NetServerBuild
    {
        public const string Define = "V5_NET";
        public const string SpikeScenePath = "Assets/_Game/V5/Net/Scenes/V5NetSpike.unity";
        public const string Executable = "RoguePirates.x86_64";
        public static readonly string[] Scenes = { SpikeScenePath, V4SceneNames.SaltwindRunPath };

        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string OutputFolder => Path.Combine(ProjectRoot, "Builds", "LinuxServer");
        public static string ReportPath => Path.Combine(ProjectRoot, "Builds", "V5NetServer-report.json");

        [MenuItem("RoguePirates/V5/Build Linux match server (V5_NET)")]
        public static void BuildMenu() => Debug.Log("V5 net server build: " + Build());

        /// <summary>For the live Editor (unity command eval): the build runs on the next Editor tick and writes its report
        /// to <see cref="ReportPath"/>, so the command does not wait on it.</summary>
        public static string Schedule(bool il2cpp = false, bool restoreTarget = true)
        {
            if (File.Exists(ReportPath)) File.Delete(ReportPath);
            EditorApplication.delayCall += () =>
            {
                try { Build(il2cpp, restoreTarget); }
                catch (Exception e) { File.WriteAllText(ReportPath, "{\"result\":\"Exception\",\"error\":\"" + e.Message.Replace("\"", "'") + "\"}"); }
            };
            return "scheduled; report at " + ReportPath;
        }

        /// <summary>Command line: -executeMethod RoguePirates.V5.Net.Editor.V5NetServerBuild.BuildFromCommandLine</summary>
        public static void BuildFromCommandLine()
        {
            var json = Build();
            if (Application.isBatchMode) EditorApplication.Exit(json.Contains("\"result\":\"Succeeded\"") ? 0 : 1);
        }

        static bool RestoreIfChanged(string path, byte[] original)
        {
            if (original == null || File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(original)) return false;
            File.WriteAllBytes(path, original);
            return true;
        }

        public static string Build(bool il2cpp = false, bool restoreTarget = true)
        {
            var total = Stopwatch.StartNew();
            var previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var previousGroup = BuildPipeline.GetBuildTargetGroup(previousTarget);
            var previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Server);
            var logTrace = PlayerSettings.GetStackTraceLogType(LogType.Log);
            var warningTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning);
            double switchSeconds = 0, buildSeconds = 0, restoreSeconds = 0;
            // A Linux build makes Unity add its Linux toolchain packages to the manifest (com.unity.toolchain.win-x86_64-linux,
            // com.unity.sdk.linux-x86_64), even for Mono: the manifest and lock are put back afterwards (build churn).
            var manifestPath = Path.Combine(ProjectRoot, "Packages", "manifest.json");
            var lockPath = Path.Combine(ProjectRoot, "Packages", "packages-lock.json");
            var manifest = File.Exists(manifestPath) ? File.ReadAllBytes(manifestPath) : null;
            var packagesLock = File.Exists(lockPath) ? File.ReadAllBytes(lockPath) : null;
            string json;
            try
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
                // Server logs without stack traces (each Debug.Log would print one); errors keep theirs.
                PlayerSettings.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
                var sw = Stopwatch.StartNew();
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
                if (previousTarget != BuildTarget.StandaloneLinux64)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneLinux64);
                switchSeconds = sw.Elapsed.TotalSeconds;

                if (Directory.Exists(OutputFolder)) Directory.Delete(OutputFolder, true);
                sw.Restart();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = Scenes,
                    locationPathName = Path.Combine(OutputFolder, Executable),
                    target = BuildTarget.StandaloneLinux64,
                    subtarget = (int)StandaloneBuildSubtarget.Server,
                    options = BuildOptions.None,
                    extraScriptingDefines = new[] { Define },
                });
                buildSeconds = sw.Elapsed.TotalSeconds;
                var files = Directory.Exists(OutputFolder) ? Directory.GetFiles(OutputFolder, "*", SearchOption.AllDirectories) : new string[0];
                var bytes = files.Sum(f => new FileInfo(f).Length);
                // Debug symbols ship next to the player in a release build too; the image leaves them out.
                var symbols = files.Where(f => f.Contains("_BackUpThisFolder_ButDontShipItWithYourGame") || f.EndsWith(".debug") || f.EndsWith(".dbg"))
                    .Sum(f => new FileInfo(f).Length);
                var managed = files.Any(f => f.EndsWith("RoguePirates.V5.Net.dll")) ? "managed dll present" : il2cpp ? "IL2CPP (no managed dlls)" : "MISSING V5.Net dll";
                var summary = report.summary;
                json = "{" +
                       $"\"result\":\"{summary.result}\",\"target\":\"{summary.platform}\",\"subtarget\":\"Server\"," +
                       $"\"scriptingBackend\":\"{(il2cpp ? "IL2CPP" : "Mono")}\",\"define\":\"{Define}\"," +
                       $"\"scenes\":\"{string.Join(",", Scenes.Select(Path.GetFileNameWithoutExtension))}\"," +
                       $"\"errors\":{summary.totalErrors},\"warnings\":{summary.totalWarnings}," +
                       $"\"switchSeconds\":{switchSeconds:0.0},\"buildSeconds\":{buildSeconds:0.0},\"unityReportedSeconds\":{summary.totalTime.TotalSeconds:0.0}," +
                       $"\"folderMB\":{bytes / 1048576.0:0.0},\"symbolsMB\":{symbols / 1048576.0:0.0},\"shippedMB\":{(bytes - symbols) / 1048576.0:0.0}," +
                       $"\"files\":{files.Length},\"assemblies\":\"{managed}\",\"output\":\"{OutputFolder.Replace("\\", "/")}\",\"utc\":\"{DateTime.UtcNow:o}\"";
            }
            finally
            {
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, backend);
                PlayerSettings.SetStackTraceLogType(LogType.Log, logTrace);
                PlayerSettings.SetStackTraceLogType(LogType.Warning, warningTrace);
                var sw = Stopwatch.StartNew();
                if (restoreTarget && EditorUserBuildSettings.activeBuildTarget != previousTarget)
                    EditorUserBuildSettings.SwitchActiveBuildTarget(previousGroup, previousTarget);
                EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
                restoreSeconds = sw.Elapsed.TotalSeconds;
                AssetDatabase.SaveAssets();
                var packagesChanged = RestoreIfChanged(manifestPath, manifest) | RestoreIfChanged(lockPath, packagesLock);
                if (packagesChanged) UnityEditor.PackageManager.Client.Resolve();
            }
            json += $",\"restoreSeconds\":{restoreSeconds:0.0},\"totalSeconds\":{total.Elapsed.TotalSeconds:0.0}}}";
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, json);
            Debug.Log("V5 net server build: " + json);
            return json;
        }
    }
}
