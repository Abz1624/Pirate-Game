using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace PirateGame.Ship
{
    [RequireComponent(typeof(Rigidbody))]
    public class ShipMovement : MonoBehaviour
    {
        [Header("Handling")]
        [SerializeField] float maxSpeed = 50f;
        [SerializeField] float reverseMaxSpeed = 15f;
        [SerializeField] float acceleration = 15f;
        [SerializeField] float reverseAcceleration = 4f;
        [FormerlySerializedAs("turnRate")]
        [SerializeField] float turnSpeed = 800f;
        [SerializeField] float waterDrag = 3f;
        [SerializeField] float brakeDrag = 6f;

        [Header("Model")]
        [Tooltip("These ship models face -Z instead of Unity's +Z forward axis.")]
        [SerializeField] bool modelForwardIsNegativeZ = true;

        [Header("Bounds (optional)")]
        [SerializeField] bool clampToArena;
        [SerializeField] float arenaRadius = 80f;

        Rigidbody body;
        Vector3 velocity;

        public Vector3 MovementForward => GetShipForward();

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY |
                               RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;
        }

        void FixedUpdate()
        {
            var dt = Time.fixedDeltaTime;
            var input = ReadShipInput();
            var forward = GetShipForward();
            var forwardSpeed = Vector3.Dot(velocity, forward);
            var turnReferenceSpeed = forwardSpeed >= 0f ? maxSpeed : reverseMaxSpeed;
            var turnEffectiveness = turnReferenceSpeed > 0f
                ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / turnReferenceSpeed)
                : 0f;

            if (Mathf.Abs(input.steer) > 0.01f && turnEffectiveness > 0f)
                transform.Rotate(0f, input.steer * turnSpeed * turnEffectiveness * dt, 0f);

            if (Mathf.Abs(input.throttle) > 0.01f)
            {
                var thrust = input.throttle > 0f ? acceleration : reverseAcceleration;
                velocity += forward * (input.throttle * thrust * dt);

                forwardSpeed = Vector3.Dot(velocity, forward);
                if (Mathf.Sign(input.throttle) != Mathf.Sign(forwardSpeed) && Mathf.Abs(forwardSpeed) > 0.1f)
                    velocity *= Mathf.Max(0f, 1f - brakeDrag * dt);
            }

            velocity *= Mathf.Max(0f, 1f - waterDrag * dt);
            velocity = ClampSpeed(velocity, forward);

            var nextPosition = body.position + velocity * dt;
            nextPosition.y = body.position.y;

            if (clampToArena)
            {
                var flat = new Vector2(nextPosition.x, nextPosition.z);
                if (flat.magnitude > arenaRadius)
                    flat = flat.normalized * arenaRadius;

                nextPosition.x = flat.x;
                nextPosition.z = flat.y;
            }

            body.MovePosition(nextPosition);
        }

        Vector3 GetShipForward()
        {
            var forward = transform.forward;
            if (modelForwardIsNegativeZ)
                forward = -forward;

            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        Vector3 ClampSpeed(Vector3 currentVelocity, Vector3 forward)
        {
            var speed = currentVelocity.magnitude;
            if (speed < 0.0001f)
                return Vector3.zero;

            var forwardSpeed = Vector3.Dot(currentVelocity, forward);
            var maxAllowed = forwardSpeed >= 0f ? maxSpeed : reverseMaxSpeed;

            if (speed <= maxAllowed)
                return currentVelocity;

            return currentVelocity / speed * maxAllowed;
        }

        static ShipInput ReadShipInput()
        {
            var input = new ShipInput();

            if (Gamepad.current != null)
            {
                var stick = Gamepad.current.leftStick.ReadValue();
                input.throttle += stick.y;
                input.steer += stick.x;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    input.throttle += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    input.throttle -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    input.steer -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    input.steer += 1f;
            }

            input.throttle = Mathf.Clamp(input.throttle, -1f, 1f);
            input.steer = Mathf.Clamp(input.steer, -1f, 1f);
            return input;
        }

        struct ShipInput
        {
            public float throttle;
            public float steer;
        }
    }
}
