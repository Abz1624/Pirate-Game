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

        [Header("Visual Tilt")]
        [Tooltip("Tilts the whole ship model when turning. Auto-built from child meshes if empty.")]
        [SerializeField] Transform visualModel;
        [SerializeField] float maxRollAngle = 26f;
        [SerializeField] float rollSmoothing = 12f;
        [Tooltip("How strongly banking scales with current speed (0 = none at crawl, 1 = full bank at max speed).")]
        [SerializeField] float bankSpeedExponent = 1f;

        Rigidbody body;
        Vector3 velocity;
        float moveSpeedMultiplier = 1f;
        float turnRateMultiplier = 1f;
        float slowMultiplier = 1f;
        float slowTimer;
        float targetRoll;
        float currentRoll;
        Quaternion visualBaseLocalRotation = Quaternion.identity;

        public float MoveSpeedMultiplier
        {
            get => moveSpeedMultiplier;
            set => moveSpeedMultiplier = Mathf.Max(0.1f, value);
        }

        public float TurnRateMultiplier
        {
            get => turnRateMultiplier;
            set => turnRateMultiplier = Mathf.Max(0.1f, value);
        }

        public void ApplySlow(float multiplier, float durationSeconds)
        {
            slowMultiplier = Mathf.Clamp(multiplier, 0.2f, 1f);
            slowTimer = Mathf.Max(slowTimer, durationSeconds);
        }

        public Vector3 MovementForward => GetShipForward();

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezePositionY |
                               RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationZ;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (visualModel == null)
                visualModel = EnsureVisualPivot();

            if (visualModel != null)
                visualBaseLocalRotation = visualModel.localRotation;
        }

        void LateUpdate()
        {
            if (visualModel == null) return;

            currentRoll = Mathf.Lerp(currentRoll, targetRoll, 1f - Mathf.Exp(-rollSmoothing * Time.deltaTime));
            var rollAxis = modelForwardIsNegativeZ ? Vector3.back : Vector3.forward;
            visualModel.localRotation = visualBaseLocalRotation * Quaternion.AngleAxis(currentRoll, rollAxis);
        }

        void FixedUpdate()
        {
            if (slowTimer > 0f)
            {
                slowTimer -= Time.fixedDeltaTime;
                if (slowTimer <= 0f)
                    slowMultiplier = 1f;
            }

            var dt = Time.fixedDeltaTime;
            var input = ReadShipInput();
            var forward = GetShipForward();
            var forwardSpeed = Vector3.Dot(velocity, forward);
            var turnReferenceSpeed = forwardSpeed >= 0f ? maxSpeed : reverseMaxSpeed;
            var turnEffectiveness = turnReferenceSpeed > 0f
                ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / turnReferenceSpeed)
                : 0f;

            if (Mathf.Abs(input.steer) > 0.01f && turnEffectiveness > 0f)
                transform.Rotate(0f, input.steer * turnSpeed * turnRateMultiplier * turnEffectiveness * dt, 0f);

            var maxForward = maxSpeed * moveSpeedMultiplier * slowMultiplier;
            var speedRatio = maxForward > 0.01f
                ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / maxForward)
                : 0f;
            if (bankSpeedExponent != 1f)
                speedRatio = Mathf.Pow(speedRatio, bankSpeedExponent);

            targetRoll = -input.steer * maxRollAngle * speedRatio;

            if (Mathf.Abs(input.throttle) > 0.01f)
            {
                var thrust = input.throttle > 0f ? acceleration : reverseAcceleration;
                velocity += forward * (input.throttle * thrust * slowMultiplier * dt);

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
            var maxAllowed = (forwardSpeed >= 0f ? maxSpeed : reverseMaxSpeed) * moveSpeedMultiplier * slowMultiplier;

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

        Transform EnsureVisualPivot()
        {
            var existing = transform.Find("VisualTiltPivot");
            if (existing != null)
                return existing;

            var pivot = new GameObject("VisualTiltPivot").transform;
            pivot.SetParent(transform, false);

            var children = new Transform[transform.childCount];
            for (var i = 0; i < children.Length; i++)
                children[i] = transform.GetChild(i);

            foreach (var child in children)
                child.SetParent(pivot, true);

            return pivot;
        }
    }
}
