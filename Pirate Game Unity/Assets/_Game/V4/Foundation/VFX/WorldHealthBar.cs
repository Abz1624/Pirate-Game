using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.VFX
{
    /// <summary>
    /// A health bar over a ship. V5 phone performance (2 Oct): bars no longer each keep their own world-space canvas and
    /// LateUpdate (with 80 enemies that was 80 canvases re-batched and drawn every frame, about 60% of what the crowd
    /// cost). <see cref="Bind"/> turns the bar's own canvas off and moves it under one shared canvas
    /// (<see cref="WorldHealthBarBatch"/>) next to where it was, which places and billboards every bar in one LateUpdate.
    /// The prefab, its sprites and the fill are unchanged.
    /// </summary>
    public sealed class WorldHealthBar : MonoBehaviour
    {
        Image _fill;
        Transform _target;
        float _heightOffset;

        internal Transform Target => _target;
        internal WorldHealthBarBatch Batch;
        internal float HeightOffset => _heightOffset;
        /// <summary>V5 M8z (7 Oct, playtest): the fill shown (0 to 1; 0 without a fill image) (tests).</summary>
        public float Fill => _fill != null ? _fill.fillAmount : 0f;

        public void Bind(Transform target, float heightOffset)
        {
            _target = target;
            _heightOffset = heightOffset;
            if (_fill == null)
            {
                var fillTransform = transform.Find("Fill");
                if (fillTransform != null)
                    _fill = fillTransform.GetComponent<Image>();
                if (_fill == null)
                    _fill = GetComponentInChildren<Image>(true);
            }
            WorldHealthBarBatch.Add(this);
        }

        public void SetFill(float normalized)
        {
            if (_fill != null)
                _fill.fillAmount = Mathf.Clamp01(normalized);
        }

        void OnDisable() => WorldHealthBarBatch.Remove(this); // back in its pool: no longer placed
    }
}
