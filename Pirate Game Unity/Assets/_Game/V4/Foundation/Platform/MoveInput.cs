using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RoguePirates.V4.Foundation.Platform
{
    public interface IMoveInput
    {
        Vector2 ReadMove();
    }

    /// <summary>
    /// Ship controls: x = steer, y = throttle. Axes are independent (no diagonal normalization).
    /// </summary>
    public interface IShipSteerInput
    {
        Vector2 ReadSteerThrottle();
    }

    public sealed class CompositeShipSteerInput : IShipSteerInput
    {
        readonly ShipKeyboardInput _keyboard = new();
        readonly VirtualJoystick _joystick;
        readonly System.Func<Vector3> _shipForward;

        // Touch stick as a heading (demo, 30 Sep 2026): full turn rate at this angle off the heading, none inside the dead zone.
        const float FullSteerAngle = 35f;
        const float SteerDeadZoneAngle = 4f;
        const float StickDeadZone = 0.12f;

        /// <param name="shipForward">The ship's heading on the water; with it the stick points where the ship should go
        /// (the ship turns towards the stick's direction and sails at the stick's reach), without it the stick is raw
        /// steer (x) and throttle (y).</param>
        public CompositeShipSteerInput(VirtualJoystick joystick, System.Func<Vector3> shipForward = null)
        {
            _joystick = joystick;
            _shipForward = shipForward;
        }

        /// <summary>
        /// Steer and throttle that take the ship towards a stick direction on screen: the direction is read against the
        /// camera (stick up = screen up), the steer is the signed angle from the ship's heading (full at 35°), the throttle
        /// is the stick's reach, eased while the ship is turned far away so it swings round in an arc rather than
        /// running off the other way.
        /// </summary>
        public static Vector2 HeadingToSteerThrottle(Vector2 stick, Vector3 shipForward, Transform camera)
        {
            var reach = Mathf.Clamp01(stick.magnitude);
            if (reach < StickDeadZone) return Vector2.zero;
            Vector3 up = Vector3.forward, right = Vector3.right;
            if (camera != null)
            {
                up = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
                if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(camera.up, Vector3.up); // looking straight down
                up.Normalize();
                right = Vector3.Cross(Vector3.up, up);
            }
            var desired = (right * stick.x + up * stick.y).normalized;
            shipForward.y = 0f;
            if (shipForward.sqrMagnitude < 0.0001f) return new Vector2(0f, reach);
            var angle = Vector3.SignedAngle(shipForward.normalized, desired, Vector3.up); // + = to the right
            var steer = Mathf.Abs(angle) < SteerDeadZoneAngle ? 0f : Mathf.Clamp(angle / FullSteerAngle, -1f, 1f);
            var throttle = reach * Mathf.Lerp(1f, 0.55f, Mathf.InverseLerp(90f, 180f, Mathf.Abs(angle)));
            return new Vector2(steer, throttle);
        }

        public Vector2 ReadSteerThrottle()
        {
            var kb = _keyboard.ReadSteerThrottle();
            if (Mathf.Abs(kb.x) > 0.01f || Mathf.Abs(kb.y) > 0.01f)
                return kb;

            if (_joystick == null)
                return Vector2.zero;

            var stick = _joystick.ReadMove();
            if (_shipForward != null)
                return HeadingToSteerThrottle(stick, _shipForward(), Camera.main != null ? Camera.main.transform : null);
            return new Vector2(
                Mathf.Clamp(stick.x, -1f, 1f),
                Mathf.Clamp(stick.y, -1f, 1f));
        }
    }

    public sealed class ShipKeyboardInput : IShipSteerInput
    {
        public Vector2 ReadSteerThrottle()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var steer = 0f;
            var throttle = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
            return new Vector2(
                Mathf.Clamp(steer, -1f, 1f),
                Mathf.Clamp(throttle, -1f, 1f));
#else
            var steer = 0f;
            var throttle = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) steer -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) steer += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) throttle += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) throttle -= 1f;
            return new Vector2(
                Mathf.Clamp(steer, -1f, 1f),
                Mathf.Clamp(throttle, -1f, 1f));
#endif
        }
    }

    public sealed class CompositeMoveInput : IMoveInput
    {
        readonly KeyboardMoveInput _keyboard = new();
        readonly VirtualJoystick _joystick;

        public CompositeMoveInput(VirtualJoystick joystick)
        {
            _joystick = joystick;
        }

        public Vector2 ReadMove()
        {
            var kb = _keyboard.ReadMove();
            if (kb.sqrMagnitude > 0.01f) return kb;
            return _joystick != null ? _joystick.ReadMove() : Vector2.zero;
        }
    }

    public sealed class KeyboardMoveInput : IMoveInput
    {
        public Vector2 ReadMove()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null) return Vector2.zero;
            var x = 0f;
            var y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            var v = new Vector2(x, y);
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
#else
            var x = 0f;
            var y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;
            var v = new Vector2(x, y);
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
#endif
        }
    }
}
