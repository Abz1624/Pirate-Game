using RoguePirates.V4.Foundation.Audio;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RoguePirates.V4.Foundation.PlayerSystems
{
    public sealed class ShipBoostController : MonoBehaviour
    {
        // Playtest (3 Oct, owner): a boost lasts 5 s and recharges over 30 s (was 2.5 s and 6 s). It only speeds the ship
        // up (no camera zoom): the speed eases in over the first moment and eases back down over the last second.
        [SerializeField] float boostDuration = 5f;
        [SerializeField] float boostCooldown = 30f;
        [SerializeField] float boostMoveMultiplier = 1.5f;
        [SerializeField] float easeInSeconds = 0.25f;
        [SerializeField] float easeOutSeconds = 1f;

        PlayerShipController _ship;
        float _boostTimer;
        float _cooldownTimer;

        public float BoostNormalized => _boostTimer > 0f ? _boostTimer / boostDuration : 0f;
        public bool CanBoost => _cooldownTimer <= 0f && _boostTimer <= 0f;
        /// <summary>V5 M4: the ship is boosting now (the HUD and the 1v1 read it).</summary>
        public bool IsBoosting => _boostTimer > 0f;
        /// <summary>Playtest (3 Oct, owner): seconds of boost left (0 when not boosting), for the HUD meter.</summary>
        public float BoostRemaining => Mathf.Max(0f, _boostTimer);
        /// <summary>Playtest (3 Oct, owner): seconds until the boost is ready again (0 when ready or boosting).</summary>
        public float CooldownRemaining => _boostTimer > 0f ? 0f : Mathf.Max(0f, _cooldownTimer);
        /// <summary>Playtest (3 Oct, owner): cooldown refill, 0 just after a boost ends to 1 when ready.</summary>
        public float CooldownNormalized => boostCooldown > 0f ? 1f - Mathf.Clamp01(CooldownRemaining / boostCooldown) : 1f;

        public void Bind(PlayerShipController ship) => _ship = ship;

        void Update()
        {
            if (_ship == null || RunManager.Instance == null || RunManager.Instance.IsPaused)
            {
                RoguePirates.V4.Foundation.Platform.TouchActionButton.Consume(RoguePirates.V4.Foundation.Platform.TouchAction.Boost); // V5 M7: a tap while paused does not fire later
                return;
            }

            Advance(Time.deltaTime);

            var boostPressed = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
            if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
                boostPressed = true;
            if (RoguePirates.V4.Foundation.Platform.TouchActionButton.Consume(RoguePirates.V4.Foundation.Platform.TouchAction.Boost))
                boostPressed = true; // V5 M7: the touch boost button
            if (boostPressed && CanBoost)
                StartBoost();

        }

        public void RequestBoost()
        {
            if (!CanBoost) return;
            StartBoost();
        }

        void StartBoost(bool silent = false)
        {
            _boostTimer = boostDuration;
            if (!silent) SfxService.PlayPlaceholder("boost");
        }

        /// <summary>The timers and the ease for <paramref name="dt"/> seconds (the frame in the voyage, one net tick when stepped).</summary>
        void Advance(float dt)
        {
            _cooldownTimer = Mathf.Max(0f, _cooldownTimer - dt);
            if (_boostTimer > 0f)
            {
                var elapsed = boostDuration - _boostTimer;
                _boostTimer -= dt;
                var easeIn = easeInSeconds > 0f ? Mathf.Clamp01(elapsed / easeInSeconds) : 1f;
                var easeOut = easeOutSeconds > 0f ? Mathf.Clamp01(_boostTimer / easeOutSeconds) : 1f;
                var ease = Mathf.SmoothStep(0f, 1f, Mathf.Min(easeIn, easeOut));
                _ship.ApplyMoveEffect(Mathf.Lerp(1f, boostMoveMultiplier, ease), 0.05f);
                if (_boostTimer <= 0f)
                    _cooldownTimer = boostCooldown;
            }
        }

        /// <summary>
        /// V5 M9b part 2b: one boost step of <paramref name="dt"/> seconds on a net tick, in the order the frame runs it (the
        /// timers and the ease, then the button held: a boost starts when it is ready, as in the voyage). The match server
        /// and the client's prediction both call it before the ship's movement step (the component's own Update is off
        /// then), so a boost is predicted on the same code and clock as the server's. <paramref name="silent"/>: no sound
        /// (a replayed step).
        /// </summary>
        public void TickNet(float dt, bool pressed, bool silent)
        {
            if (_ship == null) return;
            Advance(dt);
            if (pressed && CanBoost) StartBoost(silent);
        }

        /// <summary>V5 M9b part 2b: the boost's state, part of what a net step reads and writes (rewound with the ship's motion).</summary>
        public struct NetState
        {
            public float boostTimer, cooldownTimer;
        }

        public NetState CaptureNet() => new NetState { boostTimer = _boostTimer, cooldownTimer = _cooldownTimer };

        public void RestoreNet(in NetState state)
        {
            _boostTimer = state.boostTimer;
            _cooldownTimer = state.cooldownTimer;
        }
    }
}
