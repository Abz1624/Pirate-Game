using RoguePirates.V4.Foundation.Core;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Camera shake for the game's FollowCamera, driven by the Feel feedback MMF_ShipCameraShake. Three layers:
    ///  - shake: Perlin jitter (sideways by default) that decays smoothly; new kicks add to it, capped by Max Shake;
    ///  - recoil: a sideways spring push (e.g. away from a broadside), snappy and settling on its own;
    ///  - roll: a tiny Perlin roll of the camera, for weight on hits.
    /// Applied in LateUpdate after FollowCamera, and taken back off in Update before FollowCamera runs, so the follow
    /// (and its smoothing) never sees the shake. Respects the game's Screen Shake setting. No allocations; idle when calm.
    /// </summary>
    [DefaultExecutionOrder(250)] // after FollowCamera (0) and the game's CameraShakePresenter (200)
    [DisallowMultipleComponent]
    [AddComponentMenu("Rogue Pirates/Feel/Camera Kick Offset")]
    public sealed class CameraKickOffset : MonoBehaviour
    {
        [Tooltip("Most shake offset (m) however many kicks stack up.")]
        public float MaxShake = 0.12f;
        [Tooltip("Most recoil push (m).")]
        public float MaxRecoil = 0.2f;
        [Tooltip("Most camera roll (degrees).")]
        public float MaxRoll = 1f;
        [Tooltip("Recoil spring: pushes per second and damping (0..1).")]
        public float RecoilFrequency = 5f;
        [Range(0.05f, 1f)] public float RecoilDamping = 0.55f;

        float _shake, _shakeDecay = 20f, _frequency = 30f, _seed;
        bool _sideways = true;
        float _roll, _rollDecay = 15f;
        Vector3 _recoil, _recoilVelocity;

        Vector3 _appliedPosition, _finalPosition;
        Quaternion _appliedRotation = Quaternion.identity, _finalRotation;
        bool _applied;

        public bool IsShaking => _shake > 0.0005f || _roll > 0.005f || _recoil.sqrMagnitude > 1e-6f || _recoilVelocity.sqrMagnitude > 1e-4f;

        /// <summary>Back-compatible kick (shake only).</summary>
        public void Kick(float amplitude, float duration, float frequency, bool sidewaysOnly = true) =>
            Kick(amplitude, duration, frequency, sidewaysOnly, Vector3.zero, 0f);

        /// <summary>Add a shake of <paramref name="amplitude"/> m fading over <paramref name="duration"/> s, an optional
        /// sideways recoil push of <paramref name="recoil"/> m along <paramref name="recoilDirection"/> (world, flat), and an
        /// optional roll of <paramref name="rollDegrees"/>.</summary>
        public void Kick(float amplitude, float duration, float frequency, bool sidewaysOnly, Vector3 recoilDirection, float recoil, float rollDegrees = 0f)
        {
            duration = Mathf.Max(0.02f, duration);
            if (amplitude > 0f)
            {
                if (_shake < 0.0005f) _seed = Random.value * 100f;
                _shake = Mathf.Min(MaxShake, _shake * 0.5f + amplitude); // soft add: stacks, never explodes
                _shakeDecay = 4.6f / duration;                            // ~1% left at 'duration'
                _frequency = Mathf.Max(1f, frequency);
                _sideways = sidewaysOnly;
            }
            if (rollDegrees > 0f)
            {
                _roll = Mathf.Min(MaxRoll, _roll * 0.5f + rollDegrees);
                _rollDecay = 4.6f / duration;
            }
            if (recoil > 0f && recoilDirection.sqrMagnitude > 1e-6f)
            {
                // Sideways only: keep the part of the push along the screen's left-right axis.
                var right = FlatRight();
                var side = Vector3.Dot(recoilDirection.normalized, right);
                if (Mathf.Abs(side) > 0.15f)
                {
                    var w = RecoilFrequency * Mathf.PI * 2f;
                    _recoilVelocity += right * (Mathf.Sign(side) * recoil * w * 1.6f); // peak ~ recoil at this damping
                }
            }
        }

        Vector3 FlatRight()
        {
            var r = transform.right;
            r.y = 0f;
            return r.sqrMagnitude > 1e-4f ? r.normalized : transform.right;
        }

        void Update()
        {
            // Take last frame's shake back off before FollowCamera works from the camera's transform.
            if (!_applied) return;
            if (transform.position == _finalPosition) transform.position -= _appliedPosition;
            if (transform.rotation == _finalRotation) transform.rotation *= Quaternion.Inverse(_appliedRotation);
            _applied = false;
        }

        void LateUpdate()
        {
            if (_applied) Update(); // nothing moved it since our last frame (e.g. the follow had no target)
            if (!IsShaking) return;

            var dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            _shake *= Mathf.Exp(-_shakeDecay * dt);
            _roll *= Mathf.Exp(-_rollDecay * dt);
            if (_shake < 0.0005f) _shake = 0f;
            if (_roll < 0.005f) _roll = 0f;

            var w = RecoilFrequency * Mathf.PI * 2f;
            for (var i = 0; i < 2; i++)
            {
                var h = dt * 0.5f;
                _recoilVelocity += (-w * w * _recoil - 2f * RecoilDamping * w * _recoilVelocity) * h;
                _recoil += _recoilVelocity * h;
            }
            if (_recoil.magnitude > MaxRecoil) _recoil = _recoil.normalized * MaxRecoil;
            if (_recoil.sqrMagnitude < 1e-6f && _recoilVelocity.sqrMagnitude < 1e-4f) _recoil = _recoilVelocity = Vector3.zero;

            var config = AppRoot.Instance != null ? AppRoot.Instance.Config : null;
            if (config != null && !config.ScreenShakeEnabled) return;

            var t = Time.unscaledTime * _frequency;
            var x = (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f;
            var y = _sideways ? 0f : (Mathf.PerlinNoise(_seed + 37.1f, t) - 0.5f) * 2f;
            var r = (Mathf.PerlinNoise(_seed + 71.3f, t * 0.8f) - 0.5f) * 2f;

            _appliedPosition = FlatRight() * (x * _shake) + transform.up * (y * _shake) + _recoil;
            _appliedRotation = Quaternion.AngleAxis(r * _roll, Vector3.forward);
            transform.position += _appliedPosition;
            transform.rotation *= _appliedRotation;
            _finalPosition = transform.position;
            _finalRotation = transform.rotation;
            _applied = true;
        }

        void OnDisable()
        {
            if (_applied) Update();
            _shake = _roll = 0f;
            _recoil = _recoilVelocity = Vector3.zero;
        }
    }
}
