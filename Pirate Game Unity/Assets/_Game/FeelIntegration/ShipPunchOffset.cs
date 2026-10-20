using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// A quick springy scale "pop" or squash of the ship's visual model, driven by MMF_ShipPunch. Applied in LateUpdate
    /// and taken back off in Update, so the model's own scale is never changed for good. Visual only; idle when settled.
    /// </summary>
    [DefaultExecutionOrder(261)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rogue Pirates/Feel/Ship Punch Offset")]
    public sealed class ShipPunchOffset : MonoBehaviour
    {
        [Tooltip("Most scale change (0.1 = 10%) however many punches stack up.")]
        public float MaxPunch = 0.12f;

        float _value, _velocity, _omega = 25f, _zeta = 0.35f;
        bool _squash;
        Vector3 _baseScale, _finalScale;
        bool _applied;

        public bool IsPunching => Mathf.Abs(_value) > 0.0005f || Mathf.Abs(_velocity) > 0.01f;

        /// <summary>Pop the model by about <paramref name="amount"/> (0.05 = 5%), settling over <paramref name="duration"/> s.
        /// <paramref name="squash"/>: widen and flatten (an impact) instead of a uniform pop.</summary>
        public void Punch(float amount, float duration, float frequency, bool squash)
        {
            if (amount <= 0f || duration <= 0f) return;
            _omega = Mathf.Max(0.5f, frequency) * Mathf.PI * 2f;
            _zeta = Mathf.Clamp(4f / (_omega * duration), 0.05f, 0.95f);
            var root = Mathf.Sqrt(1f - _zeta * _zeta);
            var wd = _omega * root;
            var tPeak = Mathf.Atan2(root, _zeta) / wd;
            var peakPerVelocity = Mathf.Exp(-_zeta * _omega * tPeak) * Mathf.Sin(wd * tPeak) / wd;
            _velocity += amount / Mathf.Max(1e-4f, peakPerVelocity);
            _squash = squash;
        }

        void Update()
        {
            if (!_applied) return;
            if (transform.localScale == _finalScale) transform.localScale = _baseScale;
            _applied = false;
        }

        void LateUpdate()
        {
            if (_applied) Update();
            if (!IsPunching) return;

            var dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            for (var i = 0; i < 2; i++)
            {
                var h = dt * 0.5f;
                _velocity += (-_omega * _omega * _value - 2f * _zeta * _omega * _velocity) * h;
                _value += _velocity * h;
            }
            _value = Mathf.Clamp(_value, -MaxPunch, MaxPunch);
            if (!IsPunching) { _value = _velocity = 0f; return; }

            _baseScale = transform.localScale;
            var s = _squash
                ? new Vector3(1f + _value, 1f - _value * 0.7f, 1f + _value)
                : Vector3.one * (1f + _value);
            transform.localScale = Vector3.Scale(_baseScale, s);
            _finalScale = transform.localScale;
            _applied = true;
        }

        void OnDisable()
        {
            if (_applied) Update();
            _value = _velocity = 0f;
        }
    }
}
