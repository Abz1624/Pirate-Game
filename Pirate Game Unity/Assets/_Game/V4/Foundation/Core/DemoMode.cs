using RoguePirates.V4.Foundation.Data;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Core
{
    /// <summary>
    /// The demo's switches (30 Sep 2026) that reach below the presentation. <see cref="Off"/> turns every demo switch off
    /// (tests of the regular game set it); the switches themselves live on the DemoJuiceSettings asset.
    /// </summary>
    public static class DemoMode
    {
        public static bool Off;

        // V5 M3e: loaded once (every property called Resources.Load, one of them every frame).
        static DemoJuiceSettings _settings;
        static bool _loaded;

        static DemoJuiceSettings Settings
        {
            get
            {
                if (!_loaded || (_settings == null && !ReferenceEquals(_settings, null)))
                {
                    _settings = Resources.Load<DemoJuiceSettings>("DemoJuiceSettings");
                    _loaded = true;
                }
                return _settings;
            }
        }

        /// <summary>No points of interest on the stage (chests, landmarks, the Treasury Beacon).</summary>
        public static bool NoPointsOfInterest => !Off && Settings != null && Settings.hidePointsOfInterest;

        public static bool HideVoyageToast => !Off && Settings != null && Settings.hideVoyageToast;
        public static float AutoBossSeconds => Off || Settings == null ? 0f : Settings.autoBossSeconds;
        /// <summary>The boss rises out of its whirlpool after a moment (needs the whirlpool prefab); off: it spawns at once.</summary>
        public static bool BossWhirlpool => !Off && Settings != null && Settings.bossArrivalPrefab != null;
        public static float BossArrivalSeconds => Settings == null ? 2.5f : Settings.bossArrivalSeconds;
        public static float MobileHudEdgeMargin => Off || Settings == null ? 0f : Settings.mobileHudEdgeMargin;

        /// <summary>Phones run at 60 fps (Android and iOS default to 30, which makes the ship feel sluggish).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void SetFrameRate()
        {
            if (Application.isMobilePlatform) Application.targetFrameRate = 60;
        }

        /// <summary>
        /// The web build uses the mobile render settings (75% resolution) so phones keep up; a desktop browser renders at
        /// full resolution instead (set on the pipeline asset through reflection: Foundation has no URP reference).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void DesktopWebResolution()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer || Application.isMobilePlatform) return;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var scale = pipeline != null ? pipeline.GetType().GetProperty("renderScale") : null;
            if (scale != null && scale.CanWrite) scale.SetValue(pipeline, 1f);
        }

        /// <summary>UI text without em dashes (the demo's style): "A — B" reads "A: B".</summary>
        public static string Text(string s) => string.IsNullOrEmpty(s) ? s : s.Replace(" — ", ": ").Replace("—", ", ");
    }
}
