using HighSeas.Data;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class DamageHighlightPulse : MonoBehaviour
    {
        Transform _target;
        float _timer;
        VfxPool _pool;
        GameObject _prefab;
        float _scale;

        public void Begin(Transform target, DemoJuiceSettings settings, VfxPool pool, GameObject prefab)
        {
            _target = target;
            _timer = settings.damageHighlightDuration;
            _scale = settings.damageHighlightScale;
            _pool = pool;
            _prefab = prefab;
            transform.SetParent(target, false);
            transform.localPosition = Vector3.up * 0.5f;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one * _scale;
            gameObject.SetActive(true);
            enabled = true;
        }

        void LateUpdate()
        {
            if (_target == null)
            {
                Finish();
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
                Finish();
        }

        void Finish()
        {
            enabled = false;
            transform.SetParent(null, false);
            if (_pool != null && _prefab != null)
                _pool.ReleasePublic(_prefab, gameObject);
            else
                Destroy(gameObject);
        }
    }
}
