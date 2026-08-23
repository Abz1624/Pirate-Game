using HighSeas.Data;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class PlayerDamageFlash : MonoBehaviour
    {
        DemoJuiceSettings _settings;
        Renderer[] _renderers;
        Color[] _baseColors;
        float _timer;

        public void Configure(DemoJuiceSettings settings)
        {
            _settings = settings;
            CacheRenderers();
        }

        void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
            {
                var material = _renderers[i].sharedMaterial;
                _baseColors[i] = material != null && material.HasProperty("_Color")
                    ? material.color
                    : Color.white;
            }
        }

        public void Flash()
        {
            if (_settings == null) return;
            if (_renderers == null || _renderers.Length == 0)
                CacheRenderers();

            _timer = _settings.playerDamageFlashDuration;
            ApplyFlash(1f);
        }

        void Update()
        {
            if (_timer <= 0f) return;
            _timer -= Time.deltaTime;
            var t = _settings != null && _settings.playerDamageFlashDuration > 0f
                ? Mathf.Clamp01(_timer / _settings.playerDamageFlashDuration)
                : 0f;
            ApplyFlash(t);
        }

        void ApplyFlash(float strength)
        {
            if (_renderers == null) return;
            var flashColor = _settings != null ? _settings.playerDamageFlashColor : Color.red;
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;
                var material = renderer.material;
                if (!material.HasProperty("_Color")) continue;
                material.color = Color.Lerp(_baseColors[i], flashColor, strength);
            }
        }
    }
}
