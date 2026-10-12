using System.Linq;
using UnityEditor;

namespace RoguePirates.Hackathon.Editor
{
    /// <summary>
    /// Hackathon (12 Oct): the Solana Unity SDK adds its MagicBlock logo to the splash screen on every script reload when
    /// the project has no splash logos (SetDefaultSplashScreen in the package). This runs after it and takes the logo out
    /// again, so the SDK does not change ProjectSettings.
    /// </summary>
    static class SolanaSdkSplashGuard
    {
        [UnityEditor.Callbacks.DidReloadScripts(1000)]
        static void OnScriptsReloaded()
        {
            var logos = PlayerSettings.SplashScreen.logos;
            if (logos == null || logos.Length == 0) return;
            var kept = logos.Where(l => l.logo == null || l.logo.texture == null || l.logo.texture.name != "magicblock-logo").ToArray();
            if (kept.Length == logos.Length) return;
            PlayerSettings.SplashScreen.logos = kept;
            var bg = PlayerSettings.SplashScreen.background;
            if (bg != null && bg.texture != null && bg.texture.name == "background" && !AssetDatabase.Contains(bg)) PlayerSettings.SplashScreen.background = null;
        }
    }
}
