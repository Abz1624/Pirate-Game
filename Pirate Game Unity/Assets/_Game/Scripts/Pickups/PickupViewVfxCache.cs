using UnityEngine;

namespace HighSeas.Pickups
{
    [DisallowMultipleComponent]
    sealed class PickupViewVfxCache : MonoBehaviour
    {
        ParticleSystem[] _particles;

        public void PlayBurstVfx()
        {
            var sparkleRoot = transform.Find("CollectSparkle");
            if (sparkleRoot != null)
            {
                PlayOnTransform(sparkleRoot);
                return;
            }

            EnsureCached();
            PlayCached();
        }

        void EnsureCached()
        {
            if (_particles != null)
                return;
            _particles = GetComponentsInChildren<ParticleSystem>(true);
        }

        void PlayCached()
        {
            if (_particles == null)
                return;

            for (var i = 0; i < _particles.Length; i++)
            {
                _particles[i].Clear(true);
                _particles[i].Play(true);
            }
        }

        static void PlayOnTransform(Transform root)
        {
            var particles = root.GetComponentsInChildren<ParticleSystem>(true);
            for (var i = 0; i < particles.Length; i++)
            {
                particles[i].Clear(true);
                particles[i].Play(true);
            }
        }
    }
}
