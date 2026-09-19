using UnityEngine;

namespace StormOcean
{
    /// <summary>
    /// Data-only forward-sail speed model: ramps a speed value toward sailSpeed
    /// (or 0 when not sailing) and hands out a per-frame horizontal delta along
    /// the ship's current flat (yaw-only) heading. Deliberately never touches
    /// Transform or Rigidbody itself - ShipBuoyancy.LateUpdate calls
    /// ComputeFrameDelta() once per frame and folds the result into the SAME
    /// single rb.MovePosition call it already makes for heave/pitch/roll.
    ///
    /// Why not move independently: this ship's Rigidbody is kinematic, and on a
    /// kinematic body a raw Transform.position write in the same frame as a
    /// Rigidbody.MovePosition call causes Unity to silently drop the MovePosition
    /// (and this recurs every subsequent frame once it happens once) - so a
    /// second, independent mover fighting ShipBuoyancy for control of the body
    /// either gets its own move silently discarded, or permanently breaks
    /// ShipBuoyancy's own moves. Routing everything through ShipBuoyancy's one
    /// official per-frame move avoids that entirely and keeps a single source of
    /// truth for how this kinematic hull gets moved, exactly like the existing
    /// heave/pitch/roll spring.
    ///
    /// Never touches Y, pitch, roll or rotation - those stay fully owned by
    /// ShipBuoyancy. Heading is whatever it already is (set elsewhere, e.g. by
    /// hand in the scene). This is real per-frame position integration, not an
    /// animated/faked move.
    /// </summary>
    [ExecuteAlways]
    public class ShipAutoSail : MonoBehaviour
    {
        [Tooltip("Cruising forward speed in meters/second.")]
        public float sailSpeed = 4f;

        [Tooltip("Seconds to ramp up from a standstill to full sailSpeed when this starts sailing. 0 = snap to full speed instantly.")]
        public float accelerationTime = 3f;

        [Tooltip("If false, the ship holds station (no forward movement) - useful to pause sailing without removing the component.")]
        public bool isSailing = true;

        float currentSpeed;

        /// <summary>
        /// Called once per frame, by ShipBuoyancy, with the SAME clamped dt it uses
        /// for its own spring integration. Returns how far to advance horizontally
        /// this frame; Vector3.zero while not playing (dt &lt;= 0) or at a standstill.
        /// </summary>
        public Vector3 ComputeFrameDelta(float dt)
        {
            float targetSpeed = isSailing ? sailSpeed : 0f;
            float accel = accelerationTime > 0.001f ? Mathf.Max(sailSpeed, 0.01f) / accelerationTime : 9999f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Mathf.Max(0f, dt));

            if (dt <= 0f || Mathf.Abs(currentSpeed) < 0.0001f) return Vector3.zero;

            Vector3 flatFwd = new Vector3(transform.forward.x, 0f, transform.forward.z);
            if (flatFwd.sqrMagnitude < 0.0001f) flatFwd = Vector3.forward;
            flatFwd.Normalize();

            return flatFwd * currentSpeed * dt;
        }
    }
}
