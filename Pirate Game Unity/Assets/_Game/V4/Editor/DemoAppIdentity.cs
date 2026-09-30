using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace RoguePirates.V4.Editor
{
    /// <summary>
    /// Demo (30 Sep 2026): the player's name and icon. "Rogue Pirates" as the product name, and the logo icon from
    /// Assets/_Game/Art/AppIcon (default icon, Android legacy and round icons, and the adaptive icon's background and
    /// foreground layers). The package id is left as it is (changing it installs as a different app).
    /// </summary>
    public static class DemoAppIdentity
    {
        const string Folder = "Assets/_Game/Art/AppIcon/";

        [MenuItem("Tools/Rogue Pirates/Demo/Apply App Name and Icon")]
        public static void Apply()
        {
            PlayerSettings.productName = "Rogue Pirates";
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "AppIcon_1024.png");
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "AppIcon_Adaptive_Background.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "AppIcon_Adaptive_Foreground.png");
            if (icon == null) { Debug.LogError("[DemoAppIdentity] no icon at " + Folder); return; }

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            var android = NamedBuildTarget.Android;
            foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round })
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var i in icons) i.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            var adaptive = PlayerSettings.GetPlatformIcons(android, AndroidPlatformIconKind.Adaptive);
            foreach (var i in adaptive)
            {
                i.SetTexture(background != null ? background : icon, 0);
                i.SetTexture(foreground != null ? foreground : icon, 1);
            }
            PlayerSettings.SetPlatformIcons(android, AndroidPlatformIconKind.Adaptive, adaptive);
            AssetDatabase.SaveAssets();
            Debug.Log("[DemoAppIdentity] product name and icons set: " + PlayerSettings.productName);
        }
    }
}
