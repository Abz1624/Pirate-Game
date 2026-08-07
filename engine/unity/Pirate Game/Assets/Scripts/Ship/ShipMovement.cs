using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateGame.Ship
{
    [RequireComponent(typeof(Rigidbody))]
    public class ShipMovement : MonoBehaviour
    {
        [Header("Handling")]
        [SerializeField] float maxSpeed = 8f;
        [SerializeField] float acceleration = 18f;
        [SerializeField] float turnRate = 6f;
        [SerializeField] float waterDrag = 3f;
        [SerializeField] float idleDrag = 4f;

        [Header("Bounds (optional)")]
        [SerializeField] bool clampToArena;
        [SerializeField] float arenaRadius = 80f;

        Rigidbody body;
        Vector3 velocity;

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
            var moveInput = ReadMoveInput();

            if (moveInput.sqrMagnitude > 0.01f)
            {
                var targetHeading = Mathf.Atan2(moveInput.x, moveInput.y) * Mathf.Rad2Deg;
                var currentY = transform.eulerAngles.y;
                var newY = Mathf.LerpAngle(currentY, targetHeading, turnRate * dt);
                transform.rotation = Quaternion.Euler(0f, newY, 0f);

                var forward = transform.forward;
                forward.y = 0f;
                forward.Normalize();

                velocity += forward * (acceleration * dt);
            }
            else
            {
                velocity *= Mathf.Max(0f, 1f - idleDrag * dt);
            }

            velocity *= Mathf.Max(0f, 1f - waterDrag * dt);

            var speed = velocity.magnitude;
            if (speed > maxSpeed)
                velocity = velocity / speed * maxSpeed;

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

        static Vector2 ReadMoveInput()
        {
            var move = Vector2.zero;

            if (Gamepad.current != null)
                move += Gamepad.current.leftStick.ReadValue();

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) move.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) move.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) move.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) move.x += 1f;
            }

            return move.sqrMagnitude > 1f ? move.normalized : move;
        }
    }
}
