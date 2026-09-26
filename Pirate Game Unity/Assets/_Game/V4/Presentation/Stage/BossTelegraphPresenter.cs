using System;
using RoguePirates.V4.Config;
using RoguePirates.V4.Stage;
using UnityEngine;

namespace RoguePirates.V4.Presentation.Stage
{
    /// <summary>
    /// The boss's marked circle (M7): shown while a marked bombardment or shell slam is telegraphed, as V3's boss
    /// director drew it (a flat primitive cylinder without its collider, URP Lit else Standard, orange at alpha 0.55,
    /// scaled radius × 2 by the thickness, just above the marked point). V4: drawn from <see cref="IBossView"/> by this
    /// presenter instead of by the director; the colour, height and thickness come from <see cref="BossConfig"/>
    /// (V3's values by default) and follow live edits.
    /// </summary>
    public sealed class BossTelegraphPresenter : MonoBehaviour
    {
        static readonly Color V3Color = new Color(1f, 0.35f, 0.1f, 0.55f);
        const float V3Height = 0.08f;
        const float V3Thickness = 0.12f;

        IBossView _view;
        Func<BossConfig> _config;
        GameObject _circle;
        Material _material;

        public void Bind(IBossView view, Func<BossConfig> config)
        {
            _view = view;
            _config = config;
        }

        /// <summary>The circle object (null until first shown).</summary>
        public GameObject Circle => _circle;

        void LateUpdate() => Refresh();

        /// <summary>Shows, poses or hides the circle from the view (called every frame; public for tests).</summary>
        public void Refresh()
        {
            if (_view == null || !_view.MarkedTelegraphVisible)
            {
                if (_circle != null) _circle.SetActive(false);
                return;
            }

            var config = _config != null ? _config() : null;
            EnsureCircle();
            var color = config != null ? config.telegraphColor : V3Color;
            if (_material.color != color) _material.color = color;
            var height = config != null ? config.telegraphHeight : V3Height;
            var thickness = config != null ? config.telegraphThickness : V3Thickness;
            var radius = _view.MarkedTelegraphRadius;
            _circle.transform.position = _view.MarkedTelegraphCenter + Vector3.up * height;
            _circle.transform.localScale = new Vector3(radius * 2f, thickness, radius * 2f);
            _circle.SetActive(true);
        }

        void EnsureCircle()
        {
            if (_circle != null) return;
            _circle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _circle.name = "BarnacleKingMarkedTelegraph";
            var collider = _circle.GetComponent<Collider>();
            if (collider != null) DestroyOwned(collider);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _material = new Material(shader);
            var config = _config != null ? _config() : null;
            _material.color = config != null ? config.telegraphColor : V3Color;
            _circle.GetComponent<Renderer>().sharedMaterial = _material;
        }

        void OnDestroy()
        {
            if (_circle != null) DestroyOwned(_circle);
            if (_material != null) DestroyOwned(_material);
            _circle = null;
            _material = null;
        }

        static void DestroyOwned(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
