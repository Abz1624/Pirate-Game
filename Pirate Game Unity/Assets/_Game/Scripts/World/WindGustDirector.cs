using HighSeas.Core;
using HighSeas.Player;
using HighSeas.V3;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class WindGustDirector : MonoBehaviour
    {
        [SerializeField] float gustIntervalSeconds = 45f;
        [SerializeField] float gustDurationSeconds = 8f;
        [SerializeField] float gustStrength = 2.2f;

        float _nextGust;
        float _gustTimer;
        Vector3 _gustDir;

        public void BeginRun(int seed)
        {
            var rng = new RunRng(seed ^ 0x57494E44);
            _nextGust = 20f + rng.DirectorFloat() * 15f;
            _gustTimer = 0f;
        }

        void OnEnable()
        {
            if (EnvironmentalHazardsDisabled.Value)
                Destroy(this);
        }

        void Update()
        {
            if (EnvironmentalHazardsDisabled.Value) return;
            if (RunManager.Instance == null || RunManager.Instance.IsRunEnded) return;
            var elapsed = RunManager.Instance.ElapsedSeconds;
            if (_gustTimer > 0f)
            {
                _gustTimer -= Time.deltaTime;
                ApplyWind(_gustDir * gustStrength);
                return;
            }

            if (elapsed < _nextGust) return;
            _nextGust = elapsed + gustIntervalSeconds;
            _gustTimer = gustDurationSeconds;
            var angle = RunManager.Instance.Rng.DirectorFloat() * Mathf.PI * 2f;
            _gustDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            V3ToastHud.Instance?.Show("Tailwind gust — ride the swell", 2f);
        }

        static void ApplyWind(Vector3 targetDriftMetersPerSecond)
        {
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            if (ship == null) return;
            var receiver = ship.GetComponent<ExternalForceReceiver>();
            receiver?.BlendToward(targetDriftMetersPerSecond, 2.5f);
        }
    }
}
