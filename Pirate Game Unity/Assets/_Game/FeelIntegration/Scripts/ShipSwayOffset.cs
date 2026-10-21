using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// A slight side-to-side roll added on top of the ship's visual tilt (PlayerShipController writes the
    /// VisualTiltPivot rotation every frame; this runs after it, so nothing accumulates). Driven by MMF_ShipSway.
    /// A damped spring: each kick adds to the motion already running, so quick repeated shots sway smoothly
    /// instead of popping. Visual only: physics, aim and the simulation never see it. Idle when settled.
    /// </summary>
    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rogue Pirates/Feel/Ship Sway Offset")]
    public sealed class ShipSwayOffset : MonoBehaviour
    {
        [Tooltip("Hard limit on the total roll (degrees), however many kicks stack up.")]
        public float MaxRoll = 7f;

        float _roll, _velocity, _omega = 12f, _zeta = 0.3f;
        Quaternion _lastApplied = Quaternion.identity, _lastFinal;
        bool _active, _applying;

        public bool IsSwaying => _active;
        public float CurrentRoll => _roll;

        /// <summary>Kick the sway: about <paramref name="angle"/> degrees of first lean, settling over about
        /// <paramref name="duration"/> seconds, rocking <paramref name="frequency"/> times per second.</summary>
        /// <param name="directionSign">+1 / -1 for the first lean's side, 0 for a random side.</param>
        public void Sway(float angle, float duration, float frequency, float directionSign = 0f)
        {
            if (angle <= 0f || duration <= 0f) return;
            _omega = Mathf.Max(0.5f, frequency) * Mathf.PI * 2f;
            _zeta = Mathf.Clamp(4f / (_omega * duration), 0.05f, 0.95f); // settles (~2%) in about 'duration'
            // No side given: keep pushing the way the ship is already swaying (rapid shots build up instead of cancelling).
            var sign = directionSign > 0f ? 1f : directionSign < 0f ? -1f
                : _active && Mathf.Abs(_velocity) > 0.5f ? Mathf.Sign(_velocity)
                : _active && Mathf.Abs(_roll) > 0.05f ? Mathf.Sign(_roll)
                : (Random.value < 0.5f ? -1f : 1f);
            // Kick so the first lean peaks at 'angle' (underdamped impulse response: v0/wd * e^(-zeta*w*t) * sin(wd*t)).
            var root = Mathf.Sqrt(1f - _zeta * _zeta);
            var wd = _omega * root;
            var tPeak = Mathf.Atan2(root, _zeta) / wd;
            var peakPerVelocity = Mathf.Exp(-_zeta * _omega * tPeak) * Mathf.Sin(wd * tPeak) / wd;
            _velocity += sign * angle / Mathf.Max(1e-4f, peakPerVelocity);
            _active = true;
        }

        void LateUpdate()
        {
            if (!_active && !_applying) return;

            // If nothing rewrote the pivot since our last frame, take our sway back off first.
            var baseRotation = transform.localRotation;
            if (_applying && baseRotation == _lastFinal) baseRotation *= Quaternion.Inverse(_lastApplied);
            _applying = false;

            if (_active)
            {
                var dt = Mathf.Min(Time.deltaTime, 1f / 30f);
                // Semi-implicit Euler, two sub-steps for stability at low frame rates.
                for (var i = 0; i < 2; i++)
                {
                    var h = dt * 0.5f;
                    _velocity += (-_omega * _omega * _roll - 2f * _zeta * _omega * _velocity) * h;
                    _roll += _velocity * h;
                }
                _roll = Mathf.Clamp(_roll, -MaxRoll, MaxRoll);
                if (Mathf.Abs(_roll) < 0.01f && Mathf.Abs(_velocity) < 0.05f)
                {
                    _roll = _velocity = 0f;
                    _active = false;
                }
            }

            if (!_active)
            {
                transform.localRotation = baseRotation;
                return;
            }

            _lastApplied = Quaternion.AngleAxis(_roll, Vector3.forward);
            _lastFinal = baseRotation * _lastApplied;
            transform.localRotation = _lastFinal;
            _applying = true;
        }

        void OnDisable()
        {
            if (_applying && transform.localRotation == _lastFinal)
                transform.localRotation *= Quaternion.Inverse(_lastApplied);
            _applying = false;
            _active = false;
            _roll = _velocity = 0f;
        }
    }
}
