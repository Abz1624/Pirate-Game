using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.VFX
{
    public sealed class WorldHealthBar : MonoBehaviour
    {
        Image _fill;
        Transform _target;
        float _heightOffset;

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
        }

        public void SetFill(float normalized)
        {
            if (_fill != null)
                _fill.fillAmount = Mathf.Clamp01(normalized);
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                Destroy(gameObject);
                return;
            }

            var pos = _target.position + Vector3.up * _heightOffset;
            transform.position = pos;

            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
