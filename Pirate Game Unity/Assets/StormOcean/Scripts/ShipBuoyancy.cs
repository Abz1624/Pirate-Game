using UnityEngine;
using UnityEngine.Serialization;

namespace StormOcean
{
    /// <summary>
    /// Floats a ship hull on the storm ocean by sampling OceanController's exact
    /// Gerstner surface height (SampleSurfaceHeight - the SAME math the ocean
    /// shader renders, not an approximation) at six hull points (front-left,
    /// front-right, center-left, center-right, rear-left, rear-right), then
    /// drives heave, pitch and roll toward those samples with a per-axis
    /// critically-damped spring/damper (mass-spring-damper, integrated with
    /// Time.deltaTime so the response is identical at any frame rate).
    ///
    /// This replaced a version that blended straight to the target height with
    /// a fixed per-frame Lerp fraction (Vector3.Lerp(pos, target, 0.15f) every
    /// frame). That is NOT frame-rate independent: the same 0.15 fraction
    /// converges far slower in real seconds at low FPS than at high FPS, which
    /// is exactly what produced the reported bug - the hull visibly lagging
    /// behind the true wave surface (floating above a falling wave, then
    /// diving into a rising one) whenever the frame rate dipped, e.g. during
    /// heavy storm rendering. The spring/damper model below reacts
    /// proportionally to how far the hull actually is from the sampled water
    /// (bigger error -> bigger corrective acceleration, clamped by
    /// maxBuoyancyForce so it never snaps violently), which keeps it tracking
    /// the wave surface at any frame rate while still feeling like a heavy
    /// hull rather than a cork.
    ///
    /// Yaw (heading) is left untouched - this only makes the hull sit
    /// correctly on the current wave shape, it does not steer or drift the
    /// ship.
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

        [Header("Buoyancy")]
        [FormerlySerializedAs("heightOffset")]
        [Tooltip("Raises (+) or lowers (-) the hull relative to the sampled water surface, e.g. for draft/keel depth.")]
        public float floatHeight = 0f;
        [Tooltip("Spring stiffness pulling the hull toward the sampled water height/attitude. Higher = snappier response to wave changes; lower = softer, sluggish (\"heavy\") response.")]
        public float buoyancyStrength = 18f;
        [Tooltip("Damping on the pitch/roll spring. Near buoyancyStrength's critical value the hull settles cleanly; lower values let it rock more before settling; 0 oscillates indefinitely.")]
        [Range(0f, 40f)] public float damping = 6f;
        [Tooltip("Damping on the vertical (heave) spring. Near buoyancyStrength's critical value the hull settles cleanly without bobbing like a cork.")]
        [Range(0f, 40f)] public float verticalDamping = 8f;
        [Tooltip("Extra velocity resistance simulating water drag on the hull, applied on top of the spring damping. Bleeds off residual motion so the ship settles rather than drifting or lightly oscillating forever.")]
        [Range(0f, 10f)] public float waterDrag = 1.5f;
        [Tooltip("Clamps the vertical acceleration the buoyancy spring can apply (m/s^2). Prevents violent snapping/explosive motion on extreme waves; rotation uses a proportional clamp derived from this value.")]
        public float maxBuoyancyForce = 40f;
        public float maxPitchDegrees = 25f;
        public float maxRollDegrees = 25f;

        // -- spring/damper state (world Y for heave, degrees for pitch/roll) --
        Vector3 smoothedPos;
        float heaveVelocity;
        float smoothedPitchDeg;
        float pitchVelocity;
        float smoothedRollDeg;
        float rollVelocity;
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
            // Clamp dt so a stall/hitch (or the Editor going idle between frames)
            // can never inject a huge single-step velocity kick into the spring.
            float dt = Application.isPlaying ? Mathf.Min(Time.deltaTime, 0.1f) : 0f;

            Vector3 flatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();
            Vector3 flatRight = new Vector3(flatFwd.z, 0f, -flatFwd.x); // 90 deg from flatFwd on the horizontal plane

            // Six-point hull footprint: front-left, front-right, center-left,
            // center-right, rear-left, rear-right.
            Vector3 frontL = pos + flatFwd * (length * 0.5f) - flatRight * (width * 0.5f);
            Vector3 frontR = pos + flatFwd * (length * 0.5f) + flatRight * (width * 0.5f);
            Vector3 midL = pos - flatRight * (width * 0.5f);
            Vector3 midR = pos + flatRight * (width * 0.5f);
            Vector3 rearL = pos - flatFwd * (length * 0.5f) - flatRight * (width * 0.5f);
            Vector3 rearR = pos - flatFwd * (length * 0.5f) + flatRight * (width * 0.5f);

            float hFL = ocean.SampleSurfaceHeight(frontL.x, frontL.z, t);
            float hFR = ocean.SampleSurfaceHeight(frontR.x, frontR.z, t);
            float hML = ocean.SampleSurfaceHeight(midL.x, midL.z, t);
            float hMR = ocean.SampleSurfaceHeight(midR.x, midR.z, t);
            float hRL = ocean.SampleSurfaceHeight(rearL.x, rearL.z, t);
            float hRR = ocean.SampleSurfaceHeight(rearR.x, rearR.z, t);

            float heaveTarget = (hFL + hFR + hML + hMR + hRL + hRR) / 6f + floatHeight;

            float frontAvg = (hFL + hFR) * 0.5f;
            float rearAvg = (hRL + hRR) * 0.5f;
            float leftAvg = (hFL + hML + hRL) / 3f;
            float rightAvg = (hFR + hMR + hRR) / 3f;

            float pitchTargetDeg = Mathf.Clamp(Mathf.Atan2(frontAvg - rearAvg, length) * Mathf.Rad2Deg, -maxPitchDegrees, maxPitchDegrees);
            float rollTargetDeg = Mathf.Clamp(Mathf.Atan2(rightAvg - leftAvg, width) * Mathf.Rad2Deg, -maxRollDegrees, maxRollDegrees);

            Quaternion yawOnly = Quaternion.LookRotation(flatFwd, Vector3.up);

            if (!initialized || !Application.isPlaying)
            {
                // Edit mode (and the very first frame): snap instantly so the hull
                // always previews the true current wave shape with no lag.
                smoothedPos = new Vector3(pos.x, heaveTarget, pos.z);
                smoothedPitchDeg = pitchTargetDeg;
                smoothedRollDeg = rollTargetDeg;
                heaveVelocity = 0f;
                pitchVelocity = 0f;
                rollVelocity = 0f;
                initialized = true;
            }
            else
            {
                float dragMul = Mathf.Clamp01(1f - waterDrag * dt);

                // ---- heave: damped spring toward heaveTarget ----
                float heaveError = heaveTarget - smoothedPos.y;
                float heaveAccel = Mathf.Clamp(heaveError * buoyancyStrength - heaveVelocity * verticalDamping, -maxBuoyancyForce, maxBuoyancyForce);
                heaveVelocity = (heaveVelocity + heaveAccel * dt) * dragMul;
                float newY = smoothedPos.y + heaveVelocity * dt;
                smoothedPos = new Vector3(pos.x, newY, pos.z);

                // ---- pitch: damped spring toward pitchTargetDeg (same model, degree units) ----
                float rotAccelLimit = maxBuoyancyForce * 6f;
                float pitchAccel = Mathf.Clamp((pitchTargetDeg - smoothedPitchDeg) * buoyancyStrength - pitchVelocity * damping, -rotAccelLimit, rotAccelLimit);
                pitchVelocity = (pitchVelocity + pitchAccel * dt) * dragMul;
                smoothedPitchDeg = Mathf.Clamp(smoothedPitchDeg + pitchVelocity * dt, -maxPitchDegrees, maxPitchDegrees);

                // ---- roll: damped spring toward rollTargetDeg ----
                float rollAccel = Mathf.Clamp((rollTargetDeg - smoothedRollDeg) * buoyancyStrength - rollVelocity * damping, -rotAccelLimit, rotAccelLimit);
                rollVelocity = (rollVelocity + rollAccel * dt) * dragMul;
                smoothedRollDeg = Mathf.Clamp(smoothedRollDeg + rollVelocity * dt, -maxRollDegrees, maxRollDegrees);
            }

            Quaternion targetRot = yawOnly * Quaternion.Euler(-smoothedPitchDeg, 0f, smoothedRollDeg);

            // Brigg ships from the pirate-game project carry a kinematic Rigidbody +
            // NavMeshAgent for gameplay movement; move it through the Rigidbody so
            // physics/NavMesh stay in sync instead of desyncing from a raw Transform write.
            if (rb != null && rb.isKinematic)
            {
                rb.MovePosition(smoothedPos);
                rb.MoveRotation(targetRot);
            }
            else
            {
                transform.position = smoothedPos;
                transform.rotation = targetRot;
            }
        }
    }
}
