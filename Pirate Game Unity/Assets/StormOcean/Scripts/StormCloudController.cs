using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Drives the procedural storm-cloud sky dome material from the Inspector.
    /// </summary>
    [ExecuteAlways]
    public class StormCloudController : MonoBehaviour
    {
        public MeshRenderer skyRenderer;

        [Header("Clouds")]
        [Range(0f, 1f)] public float cloudDensity = 0.85f;
        public float cloudScale = 0.02f;
        public float cloudDetail = 3.7f;
        public float cloudSpeed = 0.015f;
        [Range(0f, 1f)] public float cloudCoverageBias = 0.22f;
        public float cloudHeightCenter = 0.35f;
        public float cloudHeightSpread = 0.9f;
        public Color cloudColorDark = new Color(0.05f, 0.07f, 0.09f, 1f);
        public Color cloudColorLit = new Color(0.5f, 0.58f, 0.64f, 1f);

        [Header("Sky")]
        public Color skyHorizonColor = new Color(0.18f, 0.24f, 0.28f, 1f);
        public Color skyZenithColor = new Color(0.02f, 0.03f, 0.05f, 1f);

        Material mat;

        void OnEnable() { Bind(); Apply(); }
        void OnValidate() { Bind(); Apply(); }
        void Update() { Apply(); }

        void Bind()
        {
            if (skyRenderer != null)
            {
                mat = Application.isPlaying ? skyRenderer.material : skyRenderer.sharedMaterial;
            }
        }

        void Apply()
        {
            if (mat == null) Bind();
            if (mat == null) return;

            mat.SetFloat("_CloudDensity", cloudDensity);
            mat.SetFloat("_CloudScale", cloudScale);
            mat.SetFloat("_CloudDetail", cloudDetail);
            mat.SetFloat("_CloudSpeed", cloudSpeed);
            mat.SetFloat("_CloudCoverageBias", cloudCoverageBias);
            mat.SetFloat("_CloudHeightCenter", cloudHeightCenter);
            mat.SetFloat("_CloudHeightSpread", cloudHeightSpread);
            mat.SetColor("_CloudColorDark", cloudColorDark);
            mat.SetColor("_CloudColorLit", cloudColorLit);
            mat.SetColor("_SkyHorizonColor", skyHorizonColor);
            mat.SetColor("_SkyZenithColor", skyZenithColor);
        }
    }
}
