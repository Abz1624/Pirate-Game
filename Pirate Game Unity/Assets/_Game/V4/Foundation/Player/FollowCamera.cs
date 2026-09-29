using UnityEngine;
using UnityEngine.InputSystem;

namespace RoguePirates.V4.Foundation.Player
{
    /// <summary>
    /// Camera follow ported from the original PirateGame TopDownCameraFollow.
    /// Smooths a virtual anchor toward the rigidbody, then applies offset — avoids
    /// lerping the camera transform itself, which fights physics interpolation.
    /// </summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public enum RotationMode
        {
            LookAtTarget,
            Manual
        }

        [Header("Target")]
        public Transform target;

        [Header("Top-down — press C to cycle here")]
        public Vector3 offset = new(0f, 18f, 12f);
        public Vector3 lookAtOffset;
        public bool followShipRotation;
        public RotationMode rotationMode = RotationMode.LookAtTarget;
        public Vector3 rotationEuler = new(56f, 0f, 0f);
        public Vector3 rotationOffset;

        public Vector3 WorldOffset => ActiveOffset;

        [Header("Top-down smoothing")]
        [Tooltip("0 = locked to ship. Higher = snappier follow (8 ≈ 0.125s lag).")]
        public float positionSmoothing;
        public float rotationSmoothing = 6f;
        public bool snapOnStart = true;

        [Header("Third person — edit THESE while in third person (Play mode)")]
        [Tooltip("When enabled, run starts in high third-person. When off, starts top-down. Press C to cycle views.")]
        public bool defaultThirdPerson = false;

        [Tooltip("Camera position in ship-local space when Follow Ship Rotation is on:\n" +
                 "X = right (+) / left (-)\n" +
                 "Y = height above anchor\n" +
                 "Z = distance behind ship (sign ignored; 14 = 14m aft)")]
        public Vector3 thirdPersonOffset = new(0f, 6f, 14f);

        [Tooltip("Look-at point offset from ship anchor (ship-local when Follow Ship Rotation is on):\n" +
                 "X = look right of ship\n" +
                 "Y = look height\n" +
                 "Z = look ahead of ship (+Z = further in front)")]
        public Vector3 thirdPersonLookAtOffset = new(0f, 1.5f, 6f);

        [Tooltip("When on, offset/look-at use ship movement forward (recommended for third person).")]
        public bool thirdPersonFollowShipRotation = true;

        [Tooltip("LookAtTarget: extra euler applied after aiming at look point. Manual: sets camera rotation directly.")]
        public RotationMode thirdPersonRotationMode = RotationMode.LookAtTarget;

        [Tooltip("Used when Third Person Rotation Mode is Manual.")]
        public Vector3 thirdPersonRotationEuler = new(12f, 0f, 0f);

        [Tooltip("Extra euler twist after look-at aim (pitch/yaw roll tweak).")]
        public Vector3 thirdPersonRotationOffset;

        public float thirdPersonPositionSmoothing;
        public float thirdPersonRotationSmoothing = 6f;

        [Header("Third person (high) — press C from top-down to reach this view")]
        [Tooltip("Higher / farther rig. Used at run start only when Default Third Person is enabled.")]
        public Vector3 thirdPersonHighOffset = new(0f, 8f, -15f);

        [Tooltip("Leave at (0,0,0) to reuse Third Person Look At Offset.")]
        public Vector3 thirdPersonHighLookAtOffset;

        enum CameraViewMode
        {
            TopDown,
            ThirdPerson,
            ThirdPersonHigh
        }

        CameraViewMode _viewMode = CameraViewMode.TopDown;
        Vector3 _savedOffset;
        Vector3 _savedLookAtOffset;
        bool _savedFollowShipRotation;
        RotationMode _savedRotationMode;
        Vector3 _savedRotationEuler;
        Vector3 _savedRotationOffset;
        float _savedPositionSmoothing;
        float _savedRotationSmoothing;

        bool _snapped;
        bool _anchorInitialized;
        PlayerShipController _ship;
        Rigidbody _targetBody;
        Vector3 _smoothedAnchor;
        Vector3 _anchorVelocity;

        Vector3 ActiveOffset => _viewMode switch
        {
            CameraViewMode.ThirdPersonHigh => thirdPersonHighOffset,
            CameraViewMode.ThirdPerson => thirdPersonOffset,
            _ => offset
        };

        Vector3 ActiveLookAtOffset => _viewMode switch
        {
            CameraViewMode.ThirdPersonHigh when thirdPersonHighLookAtOffset.sqrMagnitude > 0.0001f =>
                thirdPersonHighLookAtOffset,
            CameraViewMode.ThirdPersonHigh => thirdPersonLookAtOffset,
            CameraViewMode.ThirdPerson => thirdPersonLookAtOffset,
            _ => lookAtOffset
        };

        bool ActiveFollowShipRotation => _viewMode != CameraViewMode.TopDown
            ? thirdPersonFollowShipRotation
            : followShipRotation;

        RotationMode ActiveRotationMode => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationMode
            : rotationMode;

        Vector3 ActiveRotationEuler => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationEuler
            : rotationEuler;

        Vector3 ActiveRotationOffset => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationOffset
            : rotationOffset;

        float ActivePositionSmoothing => _viewMode != CameraViewMode.TopDown
            ? thirdPersonPositionSmoothing
            : positionSmoothing;

        float ActiveRotationSmoothing => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationSmoothing
            : rotationSmoothing;

        bool IsThirdPersonView => _viewMode != CameraViewMode.TopDown;

        void Start()
        {
            if (target == null)
                TryFindPlayer();

            CacheTargetBody();
            CacheDefaultCameraSettings();

            if (defaultThirdPerson)
                SetViewMode(CameraViewMode.ThirdPersonHigh);
            else
                SetViewMode(CameraViewMode.TopDown);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                CycleViewMode();
        }

        void LateUpdate()
        {
            if (target == null)
            {
                TryFindPlayer();
                if (target == null) return;
            }

            CacheTargetBody();

            if (_ship == null)
                _ship = target.GetComponent<PlayerShipController>();

            if (snapOnStart && !_snapped)
            {
                SnapToTarget();
                return;
            }

            var anchorPosition = GetAnchorPosition();
            if (IsThirdPersonView)
            {
                _smoothedAnchor = anchorPosition;
                _anchorVelocity = Vector3.zero;
                _anchorInitialized = true;

                var thirdPersonPosition = GetDesiredPosition(anchorPosition);
                if (ActivePositionSmoothing > 0f)
                {
                    transform.position = Vector3.SmoothDamp(
                        transform.position,
                        thirdPersonPosition,
                        ref _anchorVelocity,
                        1f / Mathf.Max(ActivePositionSmoothing, 0.01f),
                        Mathf.Infinity,
                        Time.deltaTime);
                }
                else
                    transform.position = thirdPersonPosition;

                ApplyRotation(transform.position, smooth: ActiveRotationSmoothing > 0f);
                return;
            }

            UpdateSmoothedAnchor(anchorPosition);

            var desiredPosition = GetDesiredPosition(_smoothedAnchor);
            transform.position = desiredPosition;
            ApplyRotation(desiredPosition, smooth: ActiveRotationSmoothing > 0f);
        }

        public void SnapToTarget()
        {
            if (target == null) return;

            CacheTargetBody();
            var anchorPosition = GetAnchorPosition();
            _smoothedAnchor = anchorPosition;
            _anchorVelocity = Vector3.zero;
            _anchorInitialized = true;

            var desiredPosition = GetDesiredPosition(_smoothedAnchor);
            transform.position = desiredPosition;
            ApplyRotation(desiredPosition, smooth: false);
            _snapped = true;
        }

        void UpdateSmoothedAnchor(Vector3 anchorPosition)
        {
            if (ActivePositionSmoothing <= 0f)
            {
                _smoothedAnchor = anchorPosition;
                _anchorVelocity = Vector3.zero;
                _anchorInitialized = true;
                return;
            }

            if (!_anchorInitialized)
            {
                _smoothedAnchor = anchorPosition;
                _anchorVelocity = Vector3.zero;
                _anchorInitialized = true;
                return;
            }

            _smoothedAnchor = Vector3.SmoothDamp(
                _smoothedAnchor,
                anchorPosition,
                ref _anchorVelocity,
                1f / Mathf.Max(ActivePositionSmoothing, 0.01f),
                Mathf.Infinity,
                Time.deltaTime);
        }

        Vector3 GetAnchorPosition() => target.position;

        Vector3 GetDesiredPosition(Vector3 anchorPosition)
        {
            if (target == null)
                return transform.position;

            var activeOffset = ActiveOffset;
            if (ActiveFollowShipRotation && TryGetShipLocalCameraOffset(activeOffset, out var movementOffset))
                return anchorPosition + movementOffset;

            return ActiveFollowShipRotation
                ? anchorPosition + target.rotation * activeOffset
                : anchorPosition + activeOffset;
        }

        /// <summary>Camera rig: X lateral, Y height, |Z| = distance aft of ship.</summary>
        bool TryGetShipLocalCameraOffset(Vector3 shipLocalOffset, out Vector3 worldOffset)
        {
            worldOffset = default;
            if (_ship == null)
                return false;

            GetShipBasis(out var forward, out var right);
            var distanceAft = Mathf.Abs(shipLocalOffset.z);
            worldOffset = right * shipLocalOffset.x + Vector3.up * shipLocalOffset.y - forward * distanceAft;
            return true;
        }

        /// <summary>Look target: X lateral, Y height, +Z = ahead of ship.</summary>
        bool TryGetShipLocalLookOffset(Vector3 shipLocalOffset, out Vector3 worldOffset)
        {
            worldOffset = default;
            if (_ship == null)
                return false;

            GetShipBasis(out var forward, out var right);
            worldOffset = right * shipLocalOffset.x + Vector3.up * shipLocalOffset.y + forward * shipLocalOffset.z;
            return true;
        }

        void GetShipBasis(out Vector3 forward, out Vector3 right)
        {
            forward = _ship != null ? _ship.MovementForward : target.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.0001f)
                right = target.right;
            right.y = 0f;
            right.Normalize();
        }

        void ApplyRotation(Vector3 cameraPosition, bool smooth)
        {
            var desiredRotation = GetDesiredRotation(cameraPosition);
            var rotSmoothing = ActiveRotationSmoothing;
            transform.rotation = smooth && rotSmoothing > 0f
                ? Quaternion.Slerp(transform.rotation, desiredRotation, SmoothFactor(rotSmoothing))
                : desiredRotation;
        }

        Quaternion GetDesiredRotation(Vector3 cameraPosition)
        {
            if (ActiveRotationMode == RotationMode.Manual)
                return Quaternion.Euler(ActiveRotationEuler);

            var lookPoint = _anchorInitialized ? _smoothedAnchor : GetAnchorPosition();
            var lookOffset = ActiveLookAtOffset;

            if (ActiveFollowShipRotation && _ship != null &&
                TryGetShipLocalLookOffset(lookOffset, out var worldLookOffset))
            {
                lookPoint += worldLookOffset;
            }
            else if (ActiveFollowShipRotation)
            {
                lookPoint += target.rotation * lookOffset;
            }
            else
            {
                lookPoint += lookOffset;
            }

            var direction = lookPoint - cameraPosition;
            if (direction.sqrMagnitude < 0.0001f)
                return Quaternion.Euler(ActiveRotationEuler);

            return Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(ActiveRotationOffset);
        }

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            _ship = newTarget != null ? newTarget.GetComponent<PlayerShipController>() : null;
            _targetBody = null;
            CacheTargetBody();
            _snapped = false;
            _anchorInitialized = false;
            if (snap && target != null)
                SnapToTarget();
        }

        public bool IsThirdPerson => IsThirdPersonView;

        public void ToggleThirdPerson() => CycleViewMode();

        public void CycleViewMode()
        {
            var next = _viewMode switch
            {
                CameraViewMode.ThirdPersonHigh => CameraViewMode.ThirdPerson,
                CameraViewMode.ThirdPerson => CameraViewMode.TopDown,
                _ => CameraViewMode.ThirdPersonHigh
            };

            SetViewMode(next);
        }

        void SetViewMode(CameraViewMode mode)
        {
            if (mode != CameraViewMode.TopDown && _viewMode == CameraViewMode.TopDown)
                CacheDefaultCameraSettings();

            if (mode == CameraViewMode.TopDown && _viewMode != CameraViewMode.TopDown)
                RestoreTopDownSettings();

            _viewMode = mode;
            SnapToTarget();
        }

        void RestoreTopDownSettings()
        {
            offset = _savedOffset;
            lookAtOffset = _savedLookAtOffset;
            followShipRotation = _savedFollowShipRotation;
            rotationMode = _savedRotationMode;
            rotationEuler = _savedRotationEuler;
            rotationOffset = _savedRotationOffset;
            positionSmoothing = _savedPositionSmoothing;
            rotationSmoothing = _savedRotationSmoothing;
        }

        void CacheDefaultCameraSettings()
        {
            _savedOffset = offset;
            _savedLookAtOffset = lookAtOffset;
            _savedFollowShipRotation = followShipRotation;
            _savedRotationMode = rotationMode;
            _savedRotationEuler = rotationEuler;
            _savedRotationOffset = rotationOffset;
            _savedPositionSmoothing = positionSmoothing;
            _savedRotationSmoothing = rotationSmoothing;
        }

        void TryFindPlayer()
        {
            _ship = FindFirstObjectByType<PlayerShipController>();
            if (_ship != null)
                target = _ship.transform;
        }

        void CacheTargetBody()
        {
            if (target == null)
            {
                _targetBody = null;
                return;
            }

            if (_targetBody == null || _targetBody.transform != target)
                _targetBody = target.GetComponent<Rigidbody>();
        }

        static float SmoothFactor(float smoothing) =>
            1f - Mathf.Exp(-smoothing * Time.deltaTime);
    }
}
