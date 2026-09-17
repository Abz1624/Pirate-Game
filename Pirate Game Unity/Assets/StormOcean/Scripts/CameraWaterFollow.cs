using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Keeps the camera a fixed height above the LOCAL water surface rather than a
    /// fixed world Y. Uses OceanController.SampleSwellHeightSmoothed (broad
    /// large+medium swell only, averaged over a small footprint) instead of the
    /// full per-vertex displacement: at the amplitudes this ocean uses, the exact
    /// rendered surface can be a near-vertical wave face a moment after a deep
    /// trough, and anchoring the camera to that single exact point can plant it at
    /// the bottom of an extreme, highly local dip right beside a towering crest,
    /// blocking the entire view. Riding the broader swell keeps the camera close to
    /// the water while still seeing over/around the sharper foreground wave faces.
    /// </summary>
    [ExecuteAlways]
    public class CameraWaterFollow : MonoBehaviour
    {
        public OceanController ocean;

        [Tooltip("Height above the local swell surface, in meters.")]
        public float heightOffset = 2.0f;

        [Tooltip("Footprint radius (meters) the swell height is averaged over.")]
        public float sampleRadius = 8f;

        [Range(0f, 1f)] public float followSmoothing = 0.08f;

        float smoothedHeight;
        bool initialized;

        void LateUpdate()
        {
            if (ocean == null) return;

            Vector3 p = transform.position;
            float t = Time.time;
            float h = ocean.SampleSwellHeightSmoothed(p.x, p.z, t, sampleRadius);

            if (!initialized || !Application.isPlaying)
            {
                smoothedHeight = h;
                initialized = true;
            }
            else
            {
                smoothedHeight = Mathf.Lerp(smoothedHeight, h, followSmoothing);
            }

            p.y = smoothedHeight + heightOffset;
            transform.position = p;
        }
    }
}
