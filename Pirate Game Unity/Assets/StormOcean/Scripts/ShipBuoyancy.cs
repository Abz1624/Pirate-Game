using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Floats a ship hull on the storm ocean by sampling OceanController's exact
    /// Gerstner surface height (SampleSurfaceHeight - same math the shader renders,
    /// not an approximation) at bow/stern/port/starboard, then driving heave, pitch
    /// and roll from those four points. Yaw (heading) is left untouched - this only
    /// makes the hull sit correctly on the current wave shape, it does not steer or
    /// drift the ship.
    /// </summary>
    [ExecuteAlways]
    public class ShipBuoyancy : MonoBehaviour
    {
        public OceanController ocean;

        [Header("Hull Sample Footprint (meters)")]
        [Tooltip("Bow-to-stern length used to place the fore/aft sample points.")]
        public float length = 12f;
        [Tooltip("Port-to-starboard width used to place the left/right sample points.")]
        public float width = 4f;

        [Header("Response")]
        [Tooltip("Raises (+) or lowers (-) the hull relative to the sampled water surface, e.g. for draft/keel depth.")]
        public float heightOffset = 0f;
        [Range(0f, 1f)] public float heaveSmoothing = 0.15f;
        [Range(0f, 1f)] public float tiltSmoothing = 0.1f;
        public float maxPitchDegrees = 25f;
        public float maxRollDegrees = 25f;

        Vector3 smoothedPos;
        Quaternion smoothedRot = Quaternion.identity;
        bool initialized;
        Rigidbody rb;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }


        void LateUpdate()
        {
            if (ocean == null) return;

            Vector3 pos = transform.position;
            float t = Time.time;

            Vector3 flatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            Vector3 flatRight = new Vector3(flatFwd.z, 0f, -flatFwd.x); // 90 deg from flatFwd on the horizontal plane

            Vector3 bowWS = pos + flatFwd * (length * 0.5f);
            Vector3 sternWS = pos - flatFwd * (length * 0.5f);
            Vector3 starWS = pos + flatRight * (width * 0.5f);
            Vector3 portWS = pos - flatRight * (width * 0.5f);

            float hCenter = ocean.SampleSurfaceHeight(pos.x, pos.z, t);
            float hBow = ocean.SampleSurfaceHeight(bowWS.x, bowWS.z, t);
            float hStern = ocean.SampleSurfaceHeight(sternWS.x, sternWS.z, t);
            float hStar = ocean.SampleSurfaceHeight(starWS.x, starWS.z, t);
            float hPort = ocean.SampleSurfaceHeight(portWS.x, portWS.z, t);

            float heave = (hCenter + hBow + hStern + hStar + hPort) / 5f + heightOffset;

            float pitchDeg = Mathf.Clamp(Mathf.Atan2(hBow - hStern, length) * Mathf.Rad2Deg, -maxPitchDegrees, maxPitchDegrees);
            float rollDeg = Mathf.Clamp(Mathf.Atan2(hStar - hPort, width) * Mathf.Rad2Deg, -maxRollDegrees, maxRollDegrees);

            Quaternion yawOnly = Quaternion.LookRotation(flatFwd, Vector3.up);
            Quaternion targetRot = yawOnly * Quaternion.Euler(-pitchDeg, 0f, rollDeg);
            Vector3 targetPos = new Vector3(pos.x, heave, pos.z);

            if (!initialized || !Application.isPlaying)
            {
                smoothedPos = targetPos;
                smoothedRot = targetRot;
                initialized = true;
            }
            else
            {
                smoothedPos = Vector3.Lerp(smoothedPos, targetPos, heaveSmoothing);
                smoothedRot = Quaternion.Slerp(smoothedRot, targetRot, tiltSmoothing);
            }

            // Brigg ships from the pirate-game project carry a kinematic Rigidbody +
            // NavMeshAgent for gameplay movement; move it through the Rigidbody so
            // physics/NavMesh stay in sync instead of desyncing from a raw Transform write.
            if (rb != null && rb.isKinematic)
            {
                rb.MovePosition(smoothedPos);
                rb.MoveRotation(smoothedRot);
            }
            else
            {
                transform.position = smoothedPos;
                transform.rotation = smoothedRot;
            }
        }
    }
}
