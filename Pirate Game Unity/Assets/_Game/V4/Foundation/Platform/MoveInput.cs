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

        public CompositeShipSteerInput(VirtualJoystick joystick)
        {
            _joystick = joystick;
        }

        public Vector2 ReadSteerThrottle()
        {
            var kb = _keyboard.ReadSteerThrottle();
            if (Mathf.Abs(kb.x) > 0.01f || Mathf.Abs(kb.y) > 0.01f)
                return kb;

            if (_joystick == null)
                return Vector2.zero;

            var stick = _joystick.ReadMove();
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
