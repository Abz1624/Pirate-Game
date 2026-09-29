using UnityEngine;

namespace RoguePirates.V4.Foundation.VFX
{
    public sealed class EnemyBurnFlash : MonoBehaviour
    {
        static readonly Color BurnFlashColor = new(1f, 0.15f, 0.08f, 1f);
        const float FlashInterval = 0.28f;
        const float FlashDuration = 0.14f;

        Renderer[] _renderers;
        Color[] _baseColors;
        bool[] _useBaseColor;
        bool _burning;
        float _intervalTimer;
        float _flashTimer;

        public static void SetBurning(Transform view, bool burning)
        {
            if (view == null) return;
            var flash = view.GetComponent<EnemyBurnFlash>();
            if (burning)
            {
                if (flash == null)
                    flash = view.gameObject.AddComponent<EnemyBurnFlash>();
                flash.SetBurning(true);
                return;
            }

            if (flash != null)
                flash.SetBurning(false);
        }

        public void SetBurning(bool burning)
        {
            if (_burning == burning) return;
            _burning = burning;
            _intervalTimer = 0f;
            _flashTimer = 0f;
            if (!burning)
                RestoreColors();
            else
            {
                CacheRenderers();
                _flashTimer = FlashDuration;
            }
        }

        void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseColors = new Color[_renderers.Length];
            _useBaseColor = new bool[_renderers.Length];
            for (var i = 0; i < _renderers.Length; i++)
            {
                var material = _renderers[i].sharedMaterial;
                if (material != null && material.HasProperty("_BaseColor"))
                {
                    _useBaseColor[i] = true;
                    _baseColors[i] = material.GetColor("_BaseColor");
                }
                else if (material != null && material.HasProperty("_Color"))
                {
                    _useBaseColor[i] = false;
                    _baseColors[i] = material.color;
                }
                else
                {
                    _baseColors[i] = Color.white;
                }
            }
        }

        void Update()
        {
            if (!_burning) return;

            _intervalTimer -= Time.deltaTime;
            if (_intervalTimer <= 0f)
            {
                _intervalTimer = FlashInterval;
                _flashTimer = FlashDuration;
            }

            _flashTimer -= Time.deltaTime;
            var strength = _flashTimer > 0f ? Mathf.Clamp01(_flashTimer / FlashDuration) : 0f;
            ApplyTint(strength);
        }

        void ApplyTint(float strength)
        {
            if (_renderers == null) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;
                var material = renderer.material;
                var color = Color.Lerp(_baseColors[i], BurnFlashColor, strength);
                if (_useBaseColor[i] && material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                else if (material.HasProperty("_Color"))
                    material.color = color;
            }
        }

        void RestoreColors()
        {
            if (_renderers == null) return;
            for (var i = 0; i < _renderers.Length; i++)
            {
                var renderer = _renderers[i];
                if (renderer == null) continue;
                var material = renderer.material;
                if (_useBaseColor[i] && material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", _baseColors[i]);
                else if (material.HasProperty("_Color"))
                    material.color = _baseColors[i];
            }
        }
    }
}
