using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HighSeas.Platform
{
    public enum ShipKeyboardBinding
    {
        Wasd,
        ArrowKeys,
        Numpad8456
    }

    /// <summary>Dedicated keyboard binding for couch / local multi-ship (one binding per ship).</summary>
    public sealed class BoundShipKeyboardInput : IShipSteerInput
    {
        readonly ShipKeyboardBinding _binding;

        public BoundShipKeyboardInput(ShipKeyboardBinding binding) => _binding = binding;

        public Vector2 ReadSteerThrottle()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb == null)
                return Vector2.zero;

            var steer = 0f;
            var throttle = 0f;
            switch (_binding)
            {
                case ShipKeyboardBinding.Wasd:
                    if (kb.aKey.isPressed) steer -= 1f;
                    if (kb.dKey.isPressed) steer += 1f;
                    if (kb.wKey.isPressed) throttle += 1f;
                    if (kb.sKey.isPressed) throttle -= 1f;
                    break;
                case ShipKeyboardBinding.ArrowKeys:
                    if (kb.leftArrowKey.isPressed) steer -= 1f;
                    if (kb.rightArrowKey.isPressed) steer += 1f;
                    if (kb.upArrowKey.isPressed) throttle += 1f;
                    if (kb.downArrowKey.isPressed) throttle -= 1f;
                    break;
                case ShipKeyboardBinding.Numpad8456:
                    if (kb.numpad4Key.isPressed) steer -= 1f;
                    if (kb.numpad6Key.isPressed) steer += 1f;
                    if (kb.numpad8Key.isPressed) throttle += 1f;
                    if (kb.numpad5Key.isPressed) throttle -= 1f;
                    break;
            }

            return new Vector2(
                Mathf.Clamp(steer, -1f, 1f),
                Mathf.Clamp(throttle, -1f, 1f));
#else
            return Vector2.zero;
#endif
        }
    }
}
