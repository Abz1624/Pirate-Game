using MoreMountains.Feedbacks;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Camera feel for the ship's speed: the view zooms out as the ship gets faster (plus an extra kick while it
    /// accelerates hard), zooms in a touch while braking, and always eases back to the original camera distance as the
    /// ship slows down. The zoom runs along the camera's own view direction, so the camera angle never changes and the
    /// rest position is exactly the game's FollowCamera view. A light rumble grows with speed, braking hard gives a
    /// small jolt, and coming to a stop plays a settle (the optional Feel player).
    /// Zoom sizes are a share of the camera's distance to the ship, so they read the same at any camera height. The
    /// ship's top speed is learned from how fast it actually goes (it changes with upgrades).
    /// Reads the followed ship's existing velocity; no game code changed. Visual only (CameraKickOffset takes it off
    /// before physics and the run's ticks). Island hits are excluded: no zoom bump or settle right after one.
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Ship Motion Camera Feel")]
    public sealed class ShipMotionCameraFeel : MonoBehaviour
    {
        [Header("Zoom (share of the camera's distance to the ship)")]
        [Tooltip("Zoom out at top speed (0.06 = 6% farther).")]
        [Range(0f, 0.2f)] public float SpeedZoomOut = 0f; // 0 = cruising keeps the default camera distance
        [Tooltip("Extra zoom out while accelerating hard.")]
        [Range(0f, 0.2f)] public float AccelZoomOut = 0.05f;
        [Tooltip("Zoom in while braking hard (0.04 = 4% closer).")]
        [Range(0f, 0.2f)] public float BrakeZoomIn = 0.04f;
        [Tooltip("How quickly the zoom follows (zooms per second). Lower = smoother and slower.")]
        public float ZoomSpeed = 0.9f;

        [Header("Speed and acceleration")]
        [Tooltip("Forward acceleration (m/s per second) that counts as 'hard'.")]
        public float FullAcceleration = 14f;
        [Tooltip("Smallest top speed assumed (m/s) before the ship has shown how fast it goes.")]
        public float MinTopSpeed = 10f;
        [Tooltip("Smooths the acceleration reading (higher = reacts faster).")]
        public float Responsiveness = 6f;

        [Header("Shake")]
        [Tooltip("Sideways rumble (m) at top speed. 0 = none.")]
        public float SpeedRumble = 0f;
        [Tooltip("Extra rumble (m) while accelerating hard.")]
        public float AccelRumble = 0.015f;
        [Tooltip("Rumble jitter speed.")]
        public float RumbleFrequency = 16f;
        [Tooltip("A small jolt (m) when the ship starts braking hard.")]
        public float BrakeJolt = 0.035f;

        [Header("Coming to a stop")]
        [Tooltip("Feel player to play when the ship stops after moving. Defaults to the MMF_Player on this object.")]
        public MMF_Player StopFeedback;
        [Tooltip("The ship counts as stopped below this speed (m/s)...")]
        public float StopSpeed = 1.5f;
        [Tooltip("...after having been faster than this (m/s).")]
        public float MovingSpeed = 8f;

        PlayerShipController _ship;
        CameraKickOffset _kick;
        float _lastSpeed, _accel, _peakSpeed, _topSpeed;
        bool _hasLast, _braking;

        void Awake()
        {
            if (StopFeedback == null) StopFeedback = GetComponent<MMF_Player>();
            _topSpeed = MinTopSpeed;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var follow = cam.GetComponent<FollowCamera>();
            var ship = follow != null && follow.target != null ? follow.target.GetComponent<PlayerShipController>() : null;
            if (ship != _ship) { _ship = ship; _hasLast = false; _accel = 0f; _peakSpeed = 0f; _topSpeed = MinTopSpeed; }
            if (_kick == null || _kick.gameObject != cam.gameObject)
            {
                _kick = cam.GetComponent<CameraKickOffset>();
                if (_kick == null) _kick = cam.gameObject.AddComponent<CameraKickOffset>();
            }
            if (_ship == null) { _kick.SetMotionDolly(0f); return; }

            var dt = Time.deltaTime;
            if (dt <= 0f) return; // paused

            // Speed along the ship's heading, and its smoothed change.
            var forward = _ship.MovementForward;
            forward.y = 0f;
            var speed = forward.sqrMagnitude > 1e-4f ? Vector3.Dot(_ship.Velocity, forward.normalized) : _ship.Velocity.magnitude;
            if (!_hasLast) { _lastSpeed = speed; _hasLast = true; }
            var raw = (speed - _lastSpeed) / dt;
            _lastSpeed = speed;
            if (Mathf.Abs(raw) > FullAcceleration * 20f) raw = 0f; // teleport/respawn, not a real speed change
            _accel = Mathf.Lerp(_accel, raw, 1f - Mathf.Exp(-Responsiveness * dt));

            // Learn the top speed (rises at once, slowly forgets), so the zoom scales with upgrades.
            var abs = Mathf.Abs(speed);
            _topSpeed = Mathf.Max(MinTopSpeed, Mathf.Max(abs, _topSpeed - dt * 0.5f));

            // Right after a hit or an island bump the sudden stop is not braking: hold the zoom on speed alone.
            var recentlyHit = FeelPriority.HitWithin(0.6f);
            if (recentlyHit) { _accel = 0f; _peakSpeed = 0f; }

            var speedShare = Mathf.Clamp01(Mathf.Max(0f, speed) / _topSpeed);
            var a = Mathf.Clamp(_accel / Mathf.Max(0.1f, FullAcceleration), -1f, 1f);

            // Zoom as a share of the camera's distance: + = farther, - = closer. 0 = exactly the original view.
            var share = speedShare * SpeedZoomOut + (a > 0f ? a * AccelZoomOut : a * BrakeZoomIn);
            var distance = follow.target != null ? Vector3.Distance(cam.transform.position, follow.target.position) : 15f;
            _kick.DollyFrequency = Mathf.Max(0.1f, ZoomSpeed);
            _kick.DollyDamping = 1f; // critically damped: eases in and back with no overshoot
            _kick.MaxDolly = Mathf.Max(0.5f, distance * 0.25f);
            _kick.SetMotionDolly(share * distance);

            // Rumble with speed, more while pushing hard.
            var rumble = speedShare * speedShare * SpeedRumble + Mathf.Max(0f, a - 0.25f) / 0.75f * AccelRumble;
            if (rumble > 0.0005f) _kick.Kick(rumble, 0.12f, RumbleFrequency, true);

            // A jolt the moment hard braking starts.
            var brakingNow = !recentlyHit && a < -0.5f;
            if (brakingNow && !_braking && BrakeJolt > 0f) _kick.Kick(BrakeJolt, 0.22f, 14f, true, Vector3.zero, 0f, 0.3f);
            _braking = brakingNow;

            // A settle when the ship comes to a stop after moving.
            _peakSpeed = Mathf.Max(_peakSpeed, abs);
            if (!recentlyHit && abs < StopSpeed && _peakSpeed > MovingSpeed)
            {
                _peakSpeed = 0f;
                if (StopFeedback != null) StopFeedback.PlayFeedbacks(_ship.transform.position, 1f);
            }
        }

        void OnDisable()
        {
            if (_kick != null) _kick.SetMotionDolly(0f);
        }
    }
}
