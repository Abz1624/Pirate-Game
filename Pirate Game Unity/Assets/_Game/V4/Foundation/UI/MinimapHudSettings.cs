using UnityEngine;

namespace RoguePirates.V4.Foundation.UI
{
    /// <summary>
    /// Project-wide minimap tuning. Edit in the Project window (not in the scene).
    /// Runtime HUD copies these values when the run starts.
    /// </summary>
    [CreateAssetMenu(fileName = "V3MinimapHudSettings", menuName = "RoguePirates/Minimap HUD Settings")]
    public sealed class MinimapHudSettings : ScriptableObject
    {
        [Header("Screen placement")]
        public float cornerInset = 24f;
        [Range(0.08f, 0.35f)] public float heightFraction = 0.18f;
        public float minMapSize = 140f;
        public float maxMapSize = 220f;

        [Header("Frame mask")]
        [Range(0.05f, 0.25f)] public float ringInsetFraction = 0.135f;

        [Header("Zoom (Megabonk ortho ratio)")]
        public float megabonkNormalZoom = 100f;
        public float megabonkFullMapZoom = 300f;

        [Header("Icons")]
        public float headingOffsetDegrees = 0f;

        [Header("Fog of war")]
        [Tooltip("Fog mask texels across the playable width. Bilinear upscale keeps edges smooth.")]
        [Range(64, 256)] public int fogMaskResolution = 160;
        public Color fogTint = new(0.1f, 0.16f, 0.26f, 0.88f);
        [Tooltip("World-units fade width at explored/unexplored borders.")]
        [Range(2f, 24f)] public float fogEdgeSoftnessWorld = 10f;

        static MinimapHudSettings _cached;

        public static MinimapHudSettings Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<MinimapHudSettings>("V3MinimapHudSettings");
            return _cached;
        }

    }
}
