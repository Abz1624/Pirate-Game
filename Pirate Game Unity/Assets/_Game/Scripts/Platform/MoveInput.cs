using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HighSeas.Platform
{
    public interface IMoveInput
    {
        Vector2 ReadMove();
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
