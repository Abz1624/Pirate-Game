using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Plain-script third-person chase camera (no Cinemachine dependency, to avoid
    /// fighting CameraWaterFollow for control of the camera transform - see that
    /// script's header). Follows the target's horizontal (X/Z) position with a
    /// damped-spring lag (Vector3.SmoothDamp) at a fixed distance behind its flat
    /// (yaw-only) heading, and smoothly looks toward it. Deliberately NEVER
    /// touches camera Y - that stays fully owned by CameraWaterFollow, which runs
    /// afterward in LateUpdate (this script runs in Update(), which Unity always
    /// finishes before any LateUpdate the same frame, so CameraWaterFollow always
    /// sees this script's finished X/Z placement before it floats Y above the
    /// local swell there). Only the target's flat heading is used for the follow
    /// offset - its pitch/roll wave-rocking is ignored, so the chase framing stays
    /// steady rather than swinging with every wave the hull rides.
    /// </summary>
    [ExecuteAlways]
    public class ShipChaseCamera : MonoBehaviour
    {
        [Tooltip("The ship (or other target) transform to chase.")]
        public Transform target;

        [Tooltip("Distance behind the target, along its flat heading.")]
        public float followDistance = 14f;
        [Tooltip("Sideways offset from the target's centerline (0 = directly behind).")]
        public float sideOffset = 0f;
        [Tooltip("SmoothDamp time (seconds) for the X/Z chase. Lower = snappier, higher = more trailing lag.")]
        public float positionSmoothTime = 1.1f;
        [Tooltip("Exponential smoothing rate for the look-rotation. Higher = snappier.")]
        public float rotationSmoothing = 4f;
        [Tooltip("World-space height above the target the camera looks toward, so the ship sits a bit below center instead of dead-on.")]
        public float lookHeightOffset = 2f;

        Vector3 chaseVelocity;

        void Update()
        {
            if (target == null) return;

            Vector3 flatFwd = new Vector3(target.forward.x, 0f, target.forward.z);
            if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            Vector3 flatRight = new Vector3(flatFwd.z, 0f, -flatFwd.x);

            Vector3 desired = target.position - flatFwd * followDistance + flatRight * sideOffset;
            Vector3 current = transform.position;

            if (!Application.isPlaying)
            {
                // Edit-mode: snap instantly so the Scene/Game view always previews the
                // true current chase framing with no lag, same convention as
                // ShipBuoyancy/CameraWaterFollow's edit-mode snap behavior.
                transform.position = new Vector3(desired.x, current.y, desired.z);
                chaseVelocity = Vector3.zero;
            }
            else
            {
                Vector3 currentFlat = new Vector3(current.x, 0f, current.z);
                Vector3 desiredFlat = new Vector3(desired.x, 0f, desired.z);
                Vector3 newFlat = Vector3.SmoothDamp(currentFlat, desiredFlat, ref chaseVelocity, Mathf.Max(0.01f, positionSmoothTime));
                transform.position = new Vector3(newFlat.x, current.y, newFlat.z);
            }

            Vector3 lookPoint = target.position + Vector3.up * lookHeightOffset;
            Vector3 toLook = lookPoint - transform.position;
            if (toLook.sqrMagnitude > 0.0001f)
            {
                Quaternion desiredRot = Quaternion.LookRotation(toLook.normalized, Vector3.up);
                if (!Application.isPlaying)
                {
                    transform.rotation = desiredRot;
                }
                else
                {
                    float rt = 1f - Mathf.Exp(-rotationSmoothing * Time.deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rt);
                }
            }
        }
    }
}
