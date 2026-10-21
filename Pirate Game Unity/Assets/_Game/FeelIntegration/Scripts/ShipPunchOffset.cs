using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Springy visual offsets on the ship's model (VisualTiltPivot), applied in LateUpdate and taken back off in Update,
    /// so the model's own transform is never changed for good. Visual only; idle when settled. Two channels:
    ///  - punch: a quick scale pop or squash (MMF_ShipPunch);
    ///  - press: the ship pressing itself down into the water and flattening (e.g. before a mortar shot), then popping
    ///    back up with a springy overshoot on release (MortarPressFeel).
    /// </summary>
    // Runs FIRST (-500): the scale/offset must be off the model before the ship's own FixedUpdate/Update read it.
    // PlayerShipController sizes its island collision from the model's scale chain (RestScale), so a visual squash left
    // on during physics would widen the hull against the shore and wedge the ship. Applied again in LateUpdate (the
    // controller writes only the pivot's rotation, never its scale or position, so applying early there is safe).
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Rogue Pirates/Feel/Ship Punch Offset")]
    public sealed class ShipPunchOffset : MonoBehaviour
    {
        [Tooltip("Most scale change (0.1 = 10%) however many punches stack up.")]
        public float MaxPunch = 0.12f;

        // punch (scale)
        float _value, _velocity, _omega = 25f, _zeta = 0.35f;
        bool _squash;

        // press (heave + flatten)
        bool _pressing;
        float _pressT, _pressRamp = 0.25f, _pressDepth = 0.4f, _pressSquash = 0.1f;
        float _heave, _heaveVelocity, _heaveOmega = 2.4f * Mathf.PI * 2f, _heaveZeta = 0.3f;

        Vector3 _baseScale, _finalScale, _basePosition, _finalPosition;
        bool _applied;

        public bool IsPunching => Mathf.Abs(_value) > 0.0005f || Mathf.Abs(_velocity) > 0.01f;
        public bool IsPressing => _pressing;
        bool HeaveActive => _pressing || Mathf.Abs(_heave) > 0.0005f || Mathf.Abs(_heaveVelocity) > 0.005f;

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

        /// <summary>Start pressing down: sink <paramref name="depth"/> m and flatten by <paramref name="squash"/> (0.1 = 10%),
        /// easing in over <paramref name="rampSeconds"/>. Held until <see cref="ReleasePress"/> or <see cref="CancelPress"/>.</summary>
        public void BeginPress(float depth, float squash, float rampSeconds)
        {
            if (_pressing) return;
            _pressing = true;
            _pressT = 0f;
            _pressDepth = Mathf.Max(0.01f, depth);
            _pressSquash = Mathf.Max(0f, squash);
            _pressRamp = Mathf.Max(0.02f, rampSeconds);
        }

        /// <summary>Let go of a press with a springy pop up of about <paramref name="pop"/> m above the water line,
        /// rocking <paramref name="frequency"/> times per second.</summary>
        public void ReleasePress(float pop, float frequency = 2.4f)
        {
            if (!_pressing && _heave > -0.001f) return;
            _pressing = false;
            _heaveOmega = Mathf.Max(0.5f, frequency) * Mathf.PI * 2f;
            // Spring from the pressed depth; the extra kick sends it past the water line by about 'pop'.
            _heaveVelocity = Mathf.Max(0f, pop) * _heaveOmega * 1.6f;
        }

        /// <summary>Stop pressing without a pop (the shot never came): the ship eases back up.</summary>
        public void CancelPress()
        {
            _pressing = false;
            _heaveOmega = 1.5f * Mathf.PI * 2f;
            _heaveVelocity = 0f;
        }

        void FixedUpdate() => Restore(); // before the ship's physics step (collision size)

        void Update() => Restore();

        void Restore()
        {
            if (!_applied) return;
            if (transform.localScale == _finalScale) transform.localScale = _baseScale;
            if (transform.localPosition == _finalPosition) transform.localPosition = _basePosition;
            _applied = false;
        }

        void LateUpdate()
        {
            if (_applied) Restore();
            if (!IsPunching && !HeaveActive) return;

            var dt = Mathf.Min(Time.deltaTime, 1f / 30f);

            // punch spring
            for (var i = 0; i < 2; i++)
            {
                var h = dt * 0.5f;
                _velocity += (-_omega * _omega * _value - 2f * _zeta * _omega * _velocity) * h;
                _value += _velocity * h;
            }
            _value = Mathf.Clamp(_value, -MaxPunch, MaxPunch);
            if (!IsPunching) _value = _velocity = 0f;

            // press: ease down while held, spring back (with overshoot) after
            if (_pressing)
            {
                _pressT = Mathf.Min(1f, _pressT + dt / _pressRamp);
                var eased = _pressT * _pressT * (3f - 2f * _pressT);
                _heave = Mathf.Min(_heave, -_pressDepth * eased); // follow the eased ramp (never rise while pressing)
                _heaveVelocity = 0f;
            }
            else
            {
                for (var i = 0; i < 2; i++)
                {
                    var h = dt * 0.5f;
                    _heaveVelocity += (-_heaveOmega * _heaveOmega * _heave - 2f * _heaveZeta * _heaveOmega * _heaveVelocity) * h;
                    _heave += _heaveVelocity * h;
                }
                if (Mathf.Abs(_heave) < 0.0005f && Mathf.Abs(_heaveVelocity) < 0.005f) _heave = _heaveVelocity = 0f;
            }

            // Flatten in proportion to how deep the ship is (stretches a touch on the overshoot).
            var depthShare = _pressDepth > 0f ? Mathf.Clamp(-_heave / _pressDepth, -0.6f, 1f) : 0f;
            var flat = _pressSquash * depthShare;

            var s = _squash ? new Vector3(1f + _value, 1f - _value * 0.7f, 1f + _value) : Vector3.one * (1f + _value);
            s = Vector3.Scale(s, new Vector3(1f + flat * 0.6f, 1f - flat, 1f + flat * 0.6f));

            _baseScale = transform.localScale;
            _basePosition = transform.localPosition;
            transform.localScale = Vector3.Scale(_baseScale, s);
            transform.localPosition = _basePosition + Vector3.up * _heave;
            _finalScale = transform.localScale;
            _finalPosition = transform.localPosition;
            _applied = true;
        }

        void OnDisable()
        {
            if (_applied) Restore();
            _value = _velocity = 0f;
            _pressing = false;
            _heave = _heaveVelocity = 0f;
        }
    }
}
