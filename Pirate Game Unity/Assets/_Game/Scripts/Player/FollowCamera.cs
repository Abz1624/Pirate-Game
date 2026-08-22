using UnityEngine;

namespace HighSeas.Player
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
        [SerializeField] Transform target;

        [Header("Position")]
        [SerializeField] Vector3 offset = new(0f, 18f, 12f);
        [SerializeField] bool followShipRotation;
        [SerializeField] Vector3 lookAtOffset;

        [Header("Rotation")]
        [SerializeField] RotationMode rotationMode = RotationMode.LookAtTarget;
        [SerializeField] Vector3 rotationEuler = new(56f, 0f, 0f);
        [SerializeField] Vector3 rotationOffset;

        [Header("Smoothing")]
        [Tooltip("0 = locked to ship. Higher = snappier follow (8 ≈ 0.125s lag).")]
        [SerializeField] float positionSmoothing;
        [SerializeField] float rotationSmoothing = 6f;
        [SerializeField] bool snapOnStart = true;

        bool _snapped;
        bool _anchorInitialized;
        PlayerShipController _ship;
        Rigidbody _targetBody;
        Vector3 _smoothedAnchor;
        Vector3 _anchorVelocity;

        void Start()
        {
            if (target == null)
                TryFindPlayer();

            CacheTargetBody();

            if (snapOnStart && target != null)
                SnapToTarget();
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
            UpdateSmoothedAnchor(anchorPosition);

            var desiredPosition = GetDesiredPosition(_smoothedAnchor);
            transform.position = desiredPosition;
            ApplyRotation(desiredPosition, smooth: rotationSmoothing > 0f);
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
            if (positionSmoothing <= 0f)
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
                GetPositionSmoothTime(),
                Mathf.Infinity,
                Time.deltaTime);
        }

        float GetPositionSmoothTime() => 1f / Mathf.Max(positionSmoothing, 0.01f);

        Vector3 GetAnchorPosition()
        {
            // In LateUpdate, transform holds the interpolated pose; rigidbody.position does not.
            return target.position;
        }

        Vector3 GetDesiredPosition(Vector3 anchorPosition)
        {
            if (target == null)
                return transform.position;

            if (followShipRotation && TryGetMovementRelativeOffset(out var movementOffset))
                return anchorPosition + movementOffset;

            return followShipRotation
                ? anchorPosition + target.rotation * offset
                : anchorPosition + offset;
        }

        bool TryGetMovementRelativeOffset(out Vector3 worldOffset)
        {
            worldOffset = default;
            if (_ship == null)
                return false;

            var forward = _ship.MovementForward;
            worldOffset = Vector3.up * offset.y - forward * Mathf.Abs(offset.z);
            return true;
        }

        void ApplyRotation(Vector3 cameraPosition, bool smooth)
        {
            var desiredRotation = GetDesiredRotation(cameraPosition);
            transform.rotation = smooth
                ? Quaternion.Slerp(transform.rotation, desiredRotation, SmoothFactor(rotationSmoothing))
                : desiredRotation;
        }

        Quaternion GetDesiredRotation(Vector3 cameraPosition)
        {
            if (rotationMode == RotationMode.Manual)
                return Quaternion.Euler(rotationEuler);

            var lookPoint = _anchorInitialized ? _smoothedAnchor : GetAnchorPosition();
            if (followShipRotation && _ship != null)
                lookPoint += _ship.MovementForward * lookAtOffset.z + Vector3.up * lookAtOffset.y + target.right * lookAtOffset.x;
            else if (followShipRotation)
                lookPoint += target.rotation * lookAtOffset;
            else
                lookPoint += lookAtOffset;

            var direction = lookPoint - cameraPosition;
            if (direction.sqrMagnitude < 0.0001f)
                return Quaternion.Euler(rotationEuler);

            return Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(rotationOffset);
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
