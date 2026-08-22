using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Player
{
    /// <summary>
    /// Camera follow ported from the original PirateGame TopDownCameraFollow.
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
        [SerializeField] float positionSmoothing = 6f;
        [SerializeField] float rotationSmoothing = 6f;
        [SerializeField] bool snapOnStart = true;

        bool _snapped;
        PlayerShipController _ship;

        void Start()
        {
            if (target == null)
                TryFindPlayer();

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

            if (_ship == null)
                _ship = target.GetComponent<PlayerShipController>();

            if (snapOnStart && !_snapped)
            {
                SnapToTarget();
                return;
            }

            var desiredPosition = GetDesiredPosition();
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                SmoothFactor(positionSmoothing));

            ApplyRotation(desiredPosition, smooth: true);
        }

        public void SnapToTarget()
        {
            if (target == null) return;

            var desiredPosition = GetDesiredPosition();
            transform.position = desiredPosition;
            ApplyRotation(desiredPosition, smooth: false);
            _snapped = true;
        }

        Vector3 GetDesiredPosition()
        {
            if (target == null)
                return transform.position;

            if (followShipRotation && TryGetMovementRelativeOffset(out var movementOffset))
                return target.position + movementOffset;

            return followShipRotation
                ? target.position + target.rotation * offset
                : target.position + offset;
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

            var lookPoint = target.position;
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

        static float SmoothFactor(float smoothing) =>
            1f - Mathf.Exp(-smoothing * Time.deltaTime);
    }
}
