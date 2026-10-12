using UnityEditor;
using UnityEngine;

namespace RoguePirates.Hackathon.Editor
{
    /// <summary>Hackathon: batch-mode APK build (package fun.roguepirates.hackathon, Solana MWA dependencies patched).
    /// -executeMethod RoguePirates.Hackathon.Editor.HackathonApkCli.Build</summary>
    public static class HackathonApkCli
    {
        public static void Build()
        {
            var target = UnityEditor.Build.NamedBuildTarget.Android;
            var pkg = PlayerSettings.GetApplicationIdentifier(target);
            var ok = false;
            try
            {
                PlayerSettings.SetApplicationIdentifier(target, "fun.roguepirates.hackathon");
                EditorApplication.ExecuteMenuItem("Solana/Fix Android Dependencies");
                var r = RoguePirates.V4.Editor.V5AndroidProfiles.Build("RoguePirates-hackathon", false, RoguePirates.V4.Editor.V5AndroidProfiles.Graphics.ProjectDefault, false, restoreTarget: false);
                Debug.Log("[HackathonApkCli] " + r);
                ok = r.Contains("result=Succeeded");
            }
            catch (System.Exception e) { Debug.LogError("[HackathonApkCli] " + e); }
            finally { PlayerSettings.SetApplicationIdentifier(target, pkg); AssetDatabase.SaveAssets(); }
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
