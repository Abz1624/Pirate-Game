using UnityEngine;

namespace PirateGame.Camera
{
    public class TopDownCameraFollow : MonoBehaviour
    {
        public enum RotationMode
        {
            LookAtTarget,
            Manual
        }

        [Header("Target")]
        [Tooltip("Ship transform to follow. Auto-finds GameObject tagged Player if left empty.")]
        public Transform target;

        [Header("Position")]
        [Tooltip("World-space offset from the ship. Y = height, Z = distance behind.")]
        public Vector3 offset = new(0f, 18f, -12f);

        [Tooltip("Extra world-space point added to the look-at position (e.g. look slightly ahead of the ship).")]
        public Vector3 lookAtOffset = Vector3.zero;

        [Header("Rotation")]
        [Tooltip("LookAtTarget = aim at the ship. Manual = use Rotation Euler directly.")]
        public RotationMode rotationMode = RotationMode.LookAtTarget;

        [Tooltip("World-space euler angles used when Rotation Mode is Manual.")]
        public Vector3 rotationEuler = new(56f, 0f, 0f);

        [Tooltip("Extra euler angles applied on top of the look-at rotation for fine-tuning.")]
        public Vector3 rotationOffset = Vector3.zero;

        [Header("Smoothing")]
        [Tooltip("How quickly the camera catches up to the ship. Higher = snappier.")]
        public float positionSmoothing = 6f;

        [Tooltip("How quickly the camera rotates toward the target rotation.")]
        public float rotationSmoothing = 6f;

        [Tooltip("Snap behind the ship on the first frame instead of easing in from the editor position.")]
        public bool snapOnStart = true;

        bool snapped;

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

            if (snapOnStart && !snapped)
            {
                SnapToTarget();
                return;
            }

            var desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                SmoothFactor(positionSmoothing));

            ApplyRotation(desiredPosition, smooth: true);
        }

        void OnValidate()
        {
            if (Application.isPlaying || target == null) return;
            transform.position = target.position + offset;
            ApplyRotation(transform.position, smooth: false);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (snapOnStart)
                snapped = false;
        }

        public void SnapToTarget()
        {
            if (target == null) return;

            var desiredPosition = target.position + offset;
            transform.position = desiredPosition;
            ApplyRotation(desiredPosition, smooth: false);
            snapped = true;
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

            var lookPoint = target.position + lookAtOffset;
            var direction = lookPoint - cameraPosition;
            if (direction.sqrMagnitude < 0.0001f)
                return Quaternion.Euler(rotationEuler);

            var lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            return lookRotation * Quaternion.Euler(rotationOffset);
        }

        void TryFindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                target = player.transform;
        }

        float SmoothFactor(float smoothing) =>
            1f - Mathf.Exp(-smoothing * Time.deltaTime);
    }
}
