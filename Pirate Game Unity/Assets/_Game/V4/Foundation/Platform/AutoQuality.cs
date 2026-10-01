using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.V4.Foundation.Platform
{
    /// <summary>
    /// V5 M7: the automatic quality tier. With the Quality setting on Auto, the frame time from 2 s to 10 s into a run picks
    /// the tier (above 30 ms Low, above 20 ms Mid, else High) and, on a phone, the render scale (0.6, 0.7, 0.75); the tier
    /// is kept for the next runs (pools are sized at run start). Desktop browsers keep full resolution.
    /// </summary>
    public sealed class AutoQuality : MonoBehaviour
    {
        public const string Key = "v5_auto_quality";
        const float Start = 20f, End = 30f; // under load (the first seconds are the quietest)

        float _age, _sum;
        int _frames;

        /// <summary>The tier for an average frame time (s).</summary>
        public static QualityTier TierFor(float averageFrameSeconds) =>
            averageFrameSeconds > 0.030f ? QualityTier.Low : averageFrameSeconds > 0.020f ? QualityTier.Mid : QualityTier.High;

        /// <summary>The render scale a phone uses at a tier.</summary>
        public static float PhoneRenderScale(QualityTier tier) => tier == QualityTier.Low ? 0.6f : tier == QualityTier.Mid ? 0.7f : 0.75f;

        /// <summary>Starts a measurement for this run (Auto only); applies the tier measured in an earlier run at once.</summary>
        public static void Begin(GameObject host)
        {
            if (GameSettings.Current.quality >= 0) { Apply((QualityTier)GameSettings.Current.quality); return; }
            if (host == null) return;
            if (PlayerPrefs.HasKey(Key)) Apply((QualityTier)Mathf.Clamp(PlayerPrefs.GetInt(Key), 0, 2));
            if (host.GetComponent<AutoQuality>() == null) host.AddComponent<AutoQuality>();
        }

        static void Apply(QualityTier tier)
        {
            var config = AppRoot.Instance != null ? AppRoot.Instance.Config : null;
            if (config != null) config.Quality = tier;
            if (!Application.isMobilePlatform) return;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            var scale = pipeline != null ? pipeline.GetType().GetProperty("renderScale") : null;
            if (scale != null && scale.CanWrite) scale.SetValue(pipeline, PhoneRenderScale(tier));
            else Debug.LogWarning("AutoQuality: the render pipeline has no writable renderScale; the phone render scale is unchanged");
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            if (_age < Start) return;
            if (_age < End) { _sum += Time.unscaledDeltaTime; _frames++; return; }
            if (_frames > 0)
            {
                var tier = TierFor(_sum / _frames);
                // Steps down only, so the render scale does not flip between runs.
                if (PlayerPrefs.HasKey(Key)) tier = (QualityTier)Mathf.Min((int)tier, PlayerPrefs.GetInt(Key));
                PlayerPrefs.SetInt(Key, (int)tier);
                PlayerPrefs.Save(); // WebGL keeps PlayerPrefs only after a save
                Apply(tier);
            }
            Destroy(this);
        }
    }
}
