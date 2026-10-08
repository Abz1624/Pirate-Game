using System;
using System.IO;
using System.Linq;
using RoguePirates.V4.Editor;
using UnityEditor;
using UnityEngine;

namespace RoguePirates.V5.Net.Editor
{
    /// <summary>
    /// V5 M9b part 2b: the server voyage's browser client, a desktop WebGL build (the desktop profile of
    /// <see cref="V5WebGLProfiles"/>) with the <c>V5_NET</c> define given to this build only (its build options'
    /// <c>extraScriptingDefines</c>, as <see cref="V5NetServerBuild"/> does): its main menu has the "Server voyage (test)"
    /// entry (<see cref="NetVoyageEntry"/>). The game's own WebGL, Windows and Android builds never compile the net assembly
    /// (<c>V5NetEditModeTests.NetAssembly_IsKeptOutOfReleaseBuilds</c>). Output: <c>Builds/WebGL-Desktop-Net/</c>; the report
    /// (with the build's file sizes and its stripped Netcode and Transport assemblies) in
    /// <c>Builds/V5NetWebGL-report.json</c>. The project's WebGL settings are put back by the profile.
    /// </summary>
    public static class V5NetWebGLBuild
    {
        public const string Define = V5NetServerBuild.Define;
        public const string VariantName = "Net";
        static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        public static string OutputFolder => Path.Combine(ProjectRoot, "Builds", "WebGL-Desktop-" + VariantName);
        public static string ReportPath => Path.Combine(ProjectRoot, "Builds", "V5NetWebGL-report.json");

        [MenuItem("RoguePirates/V5/Build WebGL server voyage client (desktop, V5_NET)")]
        public static void BuildMenu() => Debug.Log("V5 net WebGL build: " + Build());

        /// <summary>For the live Editor (unity command eval): runs on the next Editor tick, the report at <see cref="ReportPath"/>.</summary>
        public static string Schedule(bool normalBuildToo = false)
        {
            if (File.Exists(ReportPath)) File.Delete(ReportPath);
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var net = Build();
                    if (normalBuildToo) MeasureNormalDesktop();
                    Debug.Log("V5 net WebGL build done: " + net);
                }
                catch (Exception e) { File.WriteAllText(ReportPath, "{\"result\":\"Exception\",\"error\":\"" + e.Message.Replace("\"", "'") + "\"}"); }
            };
            return "scheduled; report at " + ReportPath;
        }

        public static void BuildFromCommandLine()
        {
            var json = Build();
            if (Application.isBatchMode) EditorApplication.Exit(json.Contains("result=Succeeded") ? 0 : 1);
        }

        public static string Build()
        {
            var result = V5WebGLProfiles.BuildDesktopVariant(VariantName, new[] { Define });
            var json = Describe("net", OutputFolder, result);
            File.WriteAllText(ReportPath, json);
            return json;
        }

        /// <summary>The normal desktop build (no V5_NET) measured the same way (Builds/V5WebGL-Desktop-measure.json).</summary>
        public static string MeasureNormalDesktop()
        {
            var result = V5WebGLProfiles.Build(V5WebGLProfiles.Profile.Desktop);
            var json = Describe("normal", Path.Combine(ProjectRoot, "Builds", "WebGL-Desktop"), result);
            File.WriteAllText(Path.Combine(ProjectRoot, "Builds", "V5WebGL-Desktop-measure.json"), json);
            return json;
        }

        /// <summary>The build's files and the stripped managed assemblies it compiled (IL2CPP: the code itself is in the wasm).</summary>
        static string Describe(string kind, string folder, string result)
        {
            var build = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Where(f => Path.GetFileName(Path.GetDirectoryName(f)) == "Build").ToArray()
                : new string[0];
            var files = string.Join(",", build.Select(f => $"\"{Path.GetFileName(f)}\":{new FileInfo(f).Length}"));
            var stripped = Path.Combine(ProjectRoot, "Library", "Bee", "artifacts", "WebGL", "ManagedStripped");
            long Size(string dll) { var p = Path.Combine(stripped, dll); return File.Exists(p) ? new FileInfo(p).Length : 0; }
            var netDlls = new[] { "Unity.Netcode.Runtime.dll", "Unity.Networking.Transport.dll", "Unity.Collections.dll", "Unity.Burst.dll", "RoguePirates.V5.Net.dll" };
            var managed = string.Join(",", netDlls.Select(d => $"\"{d}\":{Size(d)}"));
            var total = Directory.Exists(stripped) ? Directory.GetFiles(stripped, "*.dll").Sum(f => new FileInfo(f).Length) : 0;
            return "{" +
                   $"\"kind\":\"{kind}\",\"report\":\"{result.Replace("\"", "'")}\",\"downloadBytes\":{build.Sum(f => new FileInfo(f).Length)}," +
                   $"\"files\":{{{files}}},\"strippedManagedBytes\":{{{managed}}},\"strippedManagedTotalBytes\":{total}," +
                   $"\"netAssemblyInBuild\":{(Size("RoguePirates.V5.Net.dll") > 0 ? "true" : "false")},\"utc\":\"{DateTime.UtcNow:o}\"}}";
        }
    }
}
