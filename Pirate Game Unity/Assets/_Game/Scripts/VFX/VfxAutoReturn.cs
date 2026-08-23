using System;
using UnityEngine;

namespace HighSeas.VFX
{
    public sealed class VfxAutoReturn : MonoBehaviour
    {
        float _timer;
        Action<GameObject> _onReturn;

        public void Play(float lifetime, Action<GameObject> onReturn)
        {
            _timer = Mathf.Max(0.05f, lifetime);
            _onReturn = onReturn;
            enabled = true;
        }

        public static float EstimateLifetime(GameObject prefab, float fallback = 1.5f)
        {
            if (prefab == null) return fallback;

            var particles = prefab.GetComponentsInChildren<ParticleSystem>(true);
            var longest = fallback;
            for (var i = 0; i < particles.Length; i++)
            {
                var main = particles[i].main;
                var duration = main.duration + main.startLifetime.constantMax;
                if (duration > longest) longest = duration;
            }

            return longest;
        }

        void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            enabled = false;
            _onReturn?.Invoke(gameObject);
        }
    }
}
