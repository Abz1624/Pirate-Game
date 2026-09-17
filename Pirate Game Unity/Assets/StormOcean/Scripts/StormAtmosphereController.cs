using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Drives fog / atmospheric depth so the ocean horizon blends into the haze
    /// instead of showing a hard edge, and pushes an ambient color the water
    /// and cloud shaders use in place of baked GI (avoids depending on light probes).
    /// </summary>
    [ExecuteAlways]
    public class StormAtmosphereController : MonoBehaviour
    {
        [Header("Fog / Atmosphere")]
        [Range(0f, 0.05f)] public float fogDensity = 0.012f;
        public Color fogColor = new Color(0.20f, 0.26f, 0.30f, 1f);
        [Range(0f, 1f)] public float horizonFade = 0.6f;
        [Range(0f, 1f)] public float distanceFade = 0.5f;

        [Header("Ambient")]
        public Color ambientColor = new Color(0.12f, 0.16f, 0.21f, 1f);

        void OnEnable() { Apply(); }
        void OnValidate() { Apply(); }
        void Update() { Apply(); }

        void Apply()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;

            Shader.SetGlobalColor("_StormFogColor", fogColor);
            Shader.SetGlobalFloat("_StormHorizonFade", horizonFade);
            Shader.SetGlobalFloat("_StormDistanceFade", distanceFade);
            Shader.SetGlobalColor("_StormAmbientColor", ambientColor);
        }
    }
}
