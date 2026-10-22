using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// A 1-2 frame warm screen flash for big moments (elite and boss kills). Built in code at runtime: one full-screen
    /// UI Image on its own overlay canvas, no new assets and no post-processing. The canvas is switched off whenever no
    /// flash is showing, so it costs nothing in between (mobile friendly). Respects the game's Screen Shake setting as a
    /// "reduce motion" switch.
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Screen Flash Feel")]
    public sealed class ScreenFlashFeel : MonoBehaviour
    {
        [Tooltip("Flash colour (alpha = strength at full intensity).")]
        public Color FlashColor = new Color(1f, 0.82f, 0.55f, 0.32f);
        [Tooltip("How long the flash takes to fade (seconds).")]
        public float Duration = 0.12f;
        [Tooltip("Draw order of the flash canvas (above the HUD by default).")]
        public int SortingOrder = 900;

        Canvas _canvas;
        Image _image;
        float _timeLeft, _strength;

        /// <summary>Flash the screen at <paramref name="intensity"/> (0..1).</summary>
        public void Flash(float intensity = 1f)
        {
            var config = RoguePirates.V4.Foundation.Core.AppRoot.Instance != null ? RoguePirates.V4.Foundation.Core.AppRoot.Instance.Config : null;
            if (config != null && !config.ScreenShakeEnabled) return;
            if (!Build()) return;
            _strength = Mathf.Max(_timeLeft > 0f ? _strength * (_timeLeft / Duration) : 0f, Mathf.Clamp01(intensity));
            _timeLeft = Mathf.Max(0.02f, Duration);
            _canvas.enabled = true;
            Apply(1f);
        }

        void LateUpdate()
        {
            if (_timeLeft <= 0f) return;
            _timeLeft -= Time.unscaledDeltaTime;
            if (_timeLeft <= 0f) { _canvas.enabled = false; return; }
            var n = _timeLeft / Mathf.Max(0.02f, Duration);
            Apply(n * n);
        }

        void Apply(float k)
        {
            var c = FlashColor;
            c.a *= _strength * k;
            _image.color = c;
        }

        bool Build()
        {
            if (_canvas != null) return true;
            var go = new GameObject("ScreenFlash");
            go.transform.SetParent(transform, false);
            _canvas = go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            _canvas.enabled = false;
            var imgGo = new GameObject("Flash");
            imgGo.transform.SetParent(go.transform, false);
            _image = imgGo.AddComponent<Image>();
            _image.raycastTarget = false; // never blocks taps
            var rt = _image.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return true;
        }

        void OnDisable()
        {
            _timeLeft = 0f;
            if (_canvas != null) _canvas.enabled = false;
        }
    }
}
